using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Permissions;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.ResourceLibraries.Dtos;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.FileStorage;
using KnowledgeHub.Schools;
using Volo.Abp.TenantManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// 课程资源 ⇄ 教学资源库资源 互转（复制式转换）。
///
/// <para>
/// <b>为什么是复制而不是引用</b>：产品口径里这两类是分开处理的两套资源 ——
/// 库资源独立于课程，课程资源不直接共享。转换因此会复制一份新记录（含文件与当前版本），
/// 新记录带 `SourceType / SourceResourceId / SourceCourseId / SourceLibraryId` 便于溯源，
/// 之后两侧各自维护、互不同步。
/// </para>
///
/// <para>
/// 复制出的资源一律回到 <see cref="ResourceStatus.PendingReview"/>，走既有审核：
/// 课程侧由院校审核后学生可见；入库侧由资源库准入审核（`ResourcePublication`）把关。
/// </para>
/// </summary>
[IgnoreAntiforgeryToken]
public class ResourceConversionAppService : KnowledgeHubAppService, IResourceConversionAppService
{
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceVersion, Guid> _versionRepository;
    private readonly IRepository<CourseResource, Guid> _courseResourceRepository;
    private readonly IRepository<ResourceLibraryResource, Guid> _libraryLinkRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IResourcePublicationService _publicationService;
    private readonly IResourceLibraryAttachmentValidator _attachmentValidator;
    private readonly IContentCollaborationPolicy _collaborationPolicy;
    private readonly IResourceLibraryAccessService _libraryAccessService;
    private readonly ISchoolRepository _schoolRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public ResourceConversionAppService(
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceVersion, Guid> versionRepository,
        IRepository<CourseResource, Guid> courseResourceRepository,
        IRepository<ResourceLibraryResource, Guid> libraryLinkRepository,
        IRepository<Course, Guid> courseRepository,
        IResourceLibraryRepository libraryRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IFileStorageService fileStorage,
        IResourcePublicationService publicationService,
        IResourceLibraryAttachmentValidator attachmentValidator,
        IContentCollaborationPolicy collaborationPolicy,
        IResourceLibraryAccessService libraryAccessService,
        ISchoolRepository schoolRepository,
        ITenantRepository tenantRepository,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _resourceRepository = resourceRepository;
        _versionRepository = versionRepository;
        _courseResourceRepository = courseResourceRepository;
        _libraryLinkRepository = libraryLinkRepository;
        _courseRepository = courseRepository;
        _libraryRepository = libraryRepository;
        _userRepository = userRepository;
        _fileStorage = fileStorage;
        _publicationService = publicationService;
        _attachmentValidator = attachmentValidator;
        _collaborationPolicy = collaborationPolicy;
        _libraryAccessService = libraryAccessService;
        _schoolRepository = schoolRepository;
        _tenantRepository = tenantRepository;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
    }

    // ══════════════════════════════════════════════════════════
    //  列表（左右两栏）
    // ══════════════════════════════════════════════════════════

    /// <inheritdoc />
    [Authorize(KnowledgeHubPermissions.Courses.Default)]
    public async Task<PagedResultDto<CourseConvertibleResourceDto>> GetCourseResourcesAsync(CourseResourceQueryDto input)
    {
        if (input.CourseId == Guid.Empty)
        {
            return new PagedResultDto<CourseConvertibleResourceDto>(0, new List<CourseConvertibleResourceDto>());
        }

        var course = await _courseRepository.FindAsync(input.CourseId)
                     ?? throw new UserFriendlyException("课程不存在");

        // 教师只能看自己创建的课程（管理员不限）：避免通过接口列出别人课程的资源
        EnsureOwnCourse(course);

        var userId = CurrentUser.Id;

        // 课程资源 = 通过 CourseResource 关联到该课程的 Resource
        var linkQuery = await _courseResourceRepository.GetQueryableAsync();
        var resourceIds = await _asyncExecuter.ToListAsync(
            linkQuery.Where(x => x.CourseId == input.CourseId).Select(x => x.ResourceId));

        if (resourceIds.Count == 0)
        {
            return new PagedResultDto<CourseConvertibleResourceDto>(0, new List<CourseConvertibleResourceDto>());
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            // 复制式互转：源资源自身不会挂库，「已共享」按**目标库**逐库判断（见 ConvertToLibraryAsync），
            // 这里只把库副本信息回填给前端做提示。
            var convertedSourceIds = await GetConvertedSourceResourceIdsAsync();

            var query = await _resourceRepository.GetQueryableAsync();
            query = query
                .Where(x => resourceIds.Contains(x.Id))
                // 来源：自己新建（可共享去库）或 从资源库转入（只做展示跟踪，不可回共享）
                .Where(x => x.SourceType == ResourceSourceType.Created
                            || x.SourceType == ResourceSourceType.FromLibrary);

            // 状态与归属：
            // ① 审核通过的资源所有人可见；
            // ② 未通过的（待审核 / 已驳回 / 草稿）只对本人可见，方便跟踪「转入课程」的审核进度；
            // ③ 管理员看本校全部（仅审核通过，避免列表被待审内容淹没）。
            if (IsCourseAdmin() || !userId.HasValue)
            {
                query = query.Where(x => x.Status == ResourceStatus.SchoolApproved
                                         || x.Status == ResourceStatus.LeagueApproved);
            }
            else
            {
                var me = userId.Value;
                query = query.Where(x =>
                    x.Status == ResourceStatus.SchoolApproved
                    || x.Status == ResourceStatus.LeagueApproved
                    || ((x.CreatorId == me || x.CreatorId == null)
                        && (x.Status == ResourceStatus.PendingReview
                            || x.Status == ResourceStatus.Rejected
                            || x.Status == ResourceStatus.Draft)));
            }

            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                var f = input.Filter.Trim();
                query = query.Where(x => x.Name.Contains(f));
            }

            var total = await _asyncExecuter.CountAsync(query);
            var rows = await _asyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.CreationTime)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount <= 0 ? 20 : input.MaxResultCount));

            var dtos = rows.Select(x => new CourseConvertibleResourceDto
            {
                ResourceId = x.Id,
                Name = x.Name,
                FileExtension = x.FileExtension,
                FileSize = x.FileSize,
                ResourceType = x.ResourceType,
                Status = x.Status,
                SourceType = x.SourceType,
                CreationTime = x.CreationTime
            }).ToList();

            var convertedIds = await GetConvertedSourceResourceIdsAsync();
            await FillCourseResourceMetaAsync(dtos, course.Id, convertedIds);
            return new PagedResultDto<CourseConvertibleResourceDto>(total, dtos);
        }
    }

    /// <inheritdoc />
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public async Task<PagedResultDto<LibraryConvertibleResourceDto>> GetLibraryResourcesAsync(LibraryResourceQueryDto input)
    {
        if (input.LibraryId == Guid.Empty)
        {
            return new PagedResultDto<LibraryConvertibleResourceDto>(0, new List<LibraryConvertibleResourceDto>());
        }

        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;
        var isTenantAdmin = IsTenantAdmin();

        // 复用工作台的可见性判定：无权访问直接抛业务异常
        var library = await _libraryRepository.FindWithMembersAsync(input.LibraryId)
                      ?? throw new UserFriendlyException("教学资源库不存在。");
        if (library.OwnerTenantId != tenantId && !library.Members.Any(m => m.MemberTenantId == tenantId) && !isTenantAdmin)
        {
            throw new UserFriendlyException("无权访问该教学资源库。");
        }

        // 库内资源 Id：主库字段 ∪ 关联表 ∪ 已发布（与其他库内容列表口径一致）
        var publishedIds = await _publicationService.GetPublicationContentIdsAsync(
            library.Id, LibraryContentType.Resource);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var linkQuery = await _libraryLinkRepository.GetQueryableAsync();
            var linkedIds = await _asyncExecuter.ToListAsync(
                linkQuery.Where(x => x.LibraryId == library.Id).Select(x => x.ResourceId));

            var query = await _resourceRepository.GetQueryableAsync();
            query = query.Where(x => x.LibraryId == library.Id
                                     || linkedIds.Contains(x.Id)
                                     || publishedIds.Contains(x.Id));

            // ① 审核通过的资源：所有人可见、可转换
            // ② 未通过的（待审核 / 已驳回 / 草稿）：只对「本人提交的」可见，方便老师跟踪自己的共享进度；
            //    院校/平台管理员只看已通过的（避免列表被待审内容淹没，审核有专门页面）。
            if (isTenantAdmin || !userId.HasValue)
            {
                query = query.Where(x => x.Status == ResourceStatus.SchoolApproved
                                         || x.Status == ResourceStatus.LeagueApproved);
            }
            else
            {
                var me = userId.Value;
                query = query.Where(x =>
                    x.Status == ResourceStatus.SchoolApproved
                    || x.Status == ResourceStatus.LeagueApproved
                    || (x.CreatorId == me && (x.Status == ResourceStatus.PendingReview
                                              || x.Status == ResourceStatus.Rejected
                                              || x.Status == ResourceStatus.Draft)));
            }
            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                var f = input.Filter.Trim();
                query = query.Where(x => x.Name.Contains(f));
            }

            var total = await _asyncExecuter.CountAsync(query);
            var rows = await _asyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.CreationTime)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount <= 0 ? 20 : input.MaxResultCount));

            var dtos = rows.Select(x => new LibraryConvertibleResourceDto
            {
                ResourceId = x.Id,
                Name = x.Name,
                FileExtension = x.FileExtension,
                FileSize = x.FileSize,
                ResourceType = x.ResourceType,
                Status = x.Status,
                SourceType = x.SourceType,
                CreationTime = x.CreationTime
            }).ToList();

            await FillLibraryResourceMetaAsync(dtos);
            return new PagedResultDto<LibraryConvertibleResourceDto>(total, dtos);
        }
    }

    /// <inheritdoc />
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public async Task<List<ResourceLibraryOptionDto>> GetMyLibrariesAsync()
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;
        var isAdmin = IsCourseAdmin();

        // 与「教学资源库工作台」同一口径：
        // 自己参与的库 ∪ 含自己课程的库 ∪ 自己资源已共享进去的库
        var myLibraryIds = await _libraryAccessService.GetMyLibraryIdsAsync(tenantId, userId, isAdmin);
        if (myLibraryIds.Count == 0)
        {
            return new List<ResourceLibraryOptionDto>();
        }

        List<ResourceLibrary> libraries;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _libraryRepository.GetQueryableAsync();
            libraries = await _asyncExecuter.ToListAsync(query.Where(x => myLibraryIds.Contains(x.Id)));
        }

        return libraries
            .OrderBy(l => l.Name)
            .Select(l => new ResourceLibraryOptionDto
            {
                Id = l.Id,
                Name = l.Name,
                Visibility = (int)l.Visibility,
                IsMine = true
            })
            .ToList();
    }

    // ══════════════════════════════════════════════════════════
    //  转换
    // ══════════════════════════════════════════════════════════

    /// <inheritdoc />
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Build)]
    public async Task<ResourceConversionResultDto> ConvertToLibraryAsync(ConvertToLibraryInput input)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        if (input.ResourceId == Guid.Empty || input.LibraryId == Guid.Empty)
        {
            throw new UserFriendlyException("请选择要转换的资源与目标资源库。");
        }

        // 目标库必须可挂载（本校主建 / 参与共建）
        var validatedLibraryId = await _attachmentValidator.ValidateAsync(input.LibraryId, tenantId)
                                 ?? throw new UserFriendlyException("目标教学资源库不可用。");

        // 来源课程（若给出）必须是自己创建的课程
        if (input.SourceCourseId.HasValue)
        {
            var sourceCourse = await _courseRepository.FindAsync(input.SourceCourseId.Value);
            if (sourceCourse != null)
            {
                EnsureOwnCourse(sourceCourse);
            }
        }

        // 源资源必须属于本校（课程资源只在校内流转）
        var source = await _resourceRepository.FindAsync(input.ResourceId)
                     ?? throw new UserFriendlyException("资源不存在。");
        if (source.TenantId.HasValue && source.TenantId != tenantId)
        {
            throw new UserFriendlyException("只能转换本校的课程资源。");
        }

        // 防重复共享：**按目标库**判断——同一资源不能在同一库重复共享，但可以共享到不同库
        if (source.SourceType == ResourceSourceType.FromLibrary)
        {
            throw new UserFriendlyException("该资源来自教学资源库，已属库资源，无需再次共享。");
        }

        var targetLibraryName = (await GetLibraryNameMapAsync(new List<Guid> { validatedLibraryId }))
            .GetValueOrDefault(validatedLibraryId, "该资源库");

        var alreadyInTarget = source.LibraryId == validatedLibraryId;

        if (!alreadyInTarget)
        {
            var existingLinkQuery = await _libraryLinkRepository.GetQueryableAsync();
            alreadyInTarget = await _asyncExecuter.AnyAsync(
                existingLinkQuery.Where(x => x.LibraryId == validatedLibraryId && x.ResourceId == source.Id));
        }

        if (!alreadyInTarget)
        {
            // 之前互转生成过副本，且副本已挂在这个库
            var convertedMap = await GetConvertedSourceResourceIdsAsync();
            alreadyInTarget = convertedMap.TryGetValue(source.Id, out var copyLibIds)
                              && copyLibIds.Contains(validatedLibraryId);
        }

        if (alreadyInTarget)
        {
            throw new UserFriendlyException($"该资源已在「{targetLibraryName}」共享过，无需重复共享。");
        }

        var copy = await CloneResourceAsync(source, ResourceSourceType.FromCourse, input.Name);
        copy.SourceCourseId = input.SourceCourseId;
        copy.SourceLibraryId = validatedLibraryId;
        copy.LibraryId = validatedLibraryId;
        await _resourceRepository.UpdateAsync(copy);

        // 库关联（多对多）
        var linkQuery = await _libraryLinkRepository.GetQueryableAsync();
        var exists = await _asyncExecuter.AnyAsync(
            linkQuery.Where(x => x.LibraryId == validatedLibraryId && x.ResourceId == copy.Id));
        if (!exists)
        {
            await _libraryLinkRepository.InsertAsync(
                new ResourceLibraryResource(GuidGenerator.Create(), validatedLibraryId, copy.Id, tenantId));
        }

        // 共享进库即成为该库的关联老师（「我的库」口径 = 库的关联老师）
        if (CurrentUser.Id.HasValue)
        {
            await _libraryAccessService.EnsureLibraryTeacherAsync(
                validatedLibraryId, CurrentUser.Id.Value, CurrentUser.UserName, tenantId);
        }

        // 入库走准入审核（开启审核时审核通过才真正对外可见）
        var message = "已转换为教学资源库资源";
        if (await _publicationService.IsReviewEnabledAsync())
        {
            var versionId = await GetCurrentVersionIdAsync(copy.Id);
            await _publicationService.BeginAdmissionAsync(
                validatedLibraryId, LibraryContentType.Resource, copy.Id, versionId);
            message += "，已提交资源库准入审核";
        }

        return new ResourceConversionResultDto
        {
            ResourceId = copy.Id,
            Name = copy.Name,
            Message = message
        };
    }

    /// <inheritdoc />
    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ResourceConversionResultDto> ConvertToCourseAsync(ConvertToCourseInput input)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        if (input.ResourceId == Guid.Empty || input.CourseId == Guid.Empty)
        {
            throw new UserFriendlyException("请选择要转换的资源与目标课程。");
        }

        // 目标课程必须可编辑（课程负责人 / 本校管理员 / 共建库主建单位管理员）
        var course = await _courseRepository.FindAsync(input.CourseId)
                     ?? throw new UserFriendlyException("课程不存在。");
        var canEdit = await _collaborationPolicy.CanEditContentAsync(
            course.TeacherId ?? course.CreatorId, course.TenantId, course.LibraryId);
        if (!canEdit && !IsTenantAdmin())
        {
            throw new UserFriendlyException("只能把资源转换到自己负责的课程。");
        }

        // 源资源可能是他校共建库内容：跨租户读，并要求「已在该库发布」
        Resource? source;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            source = await _resourceRepository.FindAsync(input.ResourceId);
        }
        if (source == null)
        {
            throw new UserFriendlyException("资源不存在。");
        }

        if (source.TenantId != tenantId)
        {
            var publication = await _publicationService.FindAsync(
                input.LibraryId, LibraryContentType.Resource, source.Id);
            if (publication == null || publication.Status != ResourcePublicationStatus.Published)
            {
                throw new UserFriendlyException("该资源尚未在资源库发布，不能转换到课程。");
            }
        }

        var copy = await CloneResourceAsync(source, ResourceSourceType.FromLibrary, input.Name);
        copy.SourceCourseId = course.Id;
        copy.SourceLibraryId = input.LibraryId;
        // 课程资源不归属资源库：课程资源与库资源分开处理
        copy.LibraryId = null;
        await _resourceRepository.UpdateAsync(copy);

        // 挂到课程
        var existingQuery = await _courseResourceRepository.GetQueryableAsync();
        var already = await _asyncExecuter.AnyAsync(
            existingQuery.Where(x => x.CourseId == course.Id && x.ResourceId == copy.Id));
        if (!already)
        {
            await _courseResourceRepository.InsertAsync(
                new CourseResource(GuidGenerator.Create(), course.Id, copy.Id));
        }

        return new ResourceConversionResultDto
        {
            ResourceId = copy.Id,
            Name = copy.Name,
            Message = "已转换为课程资源（待审核通过后学生可见）"
        };
    }

    // ══════════════════════════════════════════════════════════
    //  内部
    // ══════════════════════════════════════════════════════════

    /// <summary>复制资源主体 + 当前版本文件（副本之间互不同步）。</summary>
    private async Task<Resource> CloneResourceAsync(Resource source, ResourceSourceType sourceType, string? newName)
    {
        var copyName = string.IsNullOrWhiteSpace(newName) ? source.Name : newName.Trim();
        string? newFilePath = null;

        if (!string.IsNullOrWhiteSpace(source.FilePath))
        {
            try
            {
                await using var stream = await _fileStorage.GetAsync(source.FilePath!);
                var fileName = $"{Guid.NewGuid():N}{source.FileExtension ?? Path.GetExtension(source.OriginalFileName) ?? string.Empty}";
                newFilePath = await _fileStorage.SaveAsync(stream, fileName, DateTime.Now.ToString("yyyyMMdd"));
            }
            catch (Exception ex)
            {
                throw new UserFriendlyException($"复制资源文件失败：{ex.Message}");
            }
        }

        var copy = new Resource
        {
            Name = copyName.Length > 256 ? copyName[..256] : copyName,
            Description = source.Description,
            ResourceType = source.ResourceType,
            CategoryId = source.CategoryId,
            MajorId = source.MajorId,
            FilePath = newFilePath,
            FileSize = source.FileSize,
            FileExtension = source.FileExtension,
            OriginalFileName = source.OriginalFileName,
            CopyrightInfo = source.CopyrightInfo,
            IsDownloadable = source.IsDownloadable,
            Status = ResourceStatus.PendingReview,
            CurrentVersion = 1,
            MediaStatus = ResourceMediaStatus.None,
            SourceType = sourceType,
            SourceResourceId = source.Id
        };

        await _resourceRepository.InsertAsync(copy);

        var version = new ResourceVersion
        {
            ResourceId = copy.Id,
            Version = 1,
            FilePath = copy.FilePath,
            FileSize = copy.FileSize,
            UpdateContent = "由资源互转生成",
            IsCurrentVersion = true
        };
        await _versionRepository.InsertAsync(version);

        return copy;
    }

    /// <summary>
    /// 源资源 → 它的「库副本」所在的库集合（复制式互转：副本挂了哪些库）。
    /// 用于「是否已在目标库共享过」的逐库判断。
    /// </summary>
    private async Task<Dictionary<Guid, List<Guid>>> GetConvertedSourceResourceIdsAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _resourceRepository.GetQueryableAsync();
            var copies = await _asyncExecuter.ToListAsync(
                query.Where(x => x.SourceType == ResourceSourceType.FromCourse && x.SourceResourceId.HasValue)
                    .Select(x => new { SourceId = x.SourceResourceId!.Value, x.Id, x.LibraryId }));

            if (copies.Count == 0)
            {
                return new Dictionary<Guid, List<Guid>>();
            }

            var copyIds = copies.Select(x => x.Id).ToList();
            var linkQuery = await _libraryLinkRepository.GetQueryableAsync();
            var links = await _asyncExecuter.ToListAsync(
                linkQuery.Where(x => copyIds.Contains(x.ResourceId))
                    .Select(x => new { x.ResourceId, x.LibraryId }));

            var result = new Dictionary<Guid, List<Guid>>();
            foreach (var copy in copies)
            {
                var libs = links.Where(l => l.ResourceId == copy.Id).Select(l => l.LibraryId).ToList();
                if (copy.LibraryId.HasValue)
                {
                    libs.Add(copy.LibraryId.Value);
                }

                if (libs.Count == 0) continue;

                if (!result.TryGetValue(copy.SourceId, out var list))
                {
                    list = new List<Guid>();
                    result[copy.SourceId] = list;
                }
                list.AddRange(libs);
            }

            foreach (var key in result.Keys.ToList())
            {
                result[key] = result[key].Distinct().ToList();
            }

            return result;
        }
    }

    private async Task<Guid?> GetCurrentVersionIdAsync(Guid resourceId)
    {
        var query = await _versionRepository.GetQueryableAsync();
        return await _asyncExecuter.FirstOrDefaultAsync(
            query.Where(x => x.ResourceId == resourceId && x.IsCurrentVersion).Select(x => (Guid?)x.Id));
    }

    /// <summary>课程资源补齐：来源文案 + 创建人 + 已关联的库名。</summary>
    private async Task FillCourseResourceMetaAsync(
        List<CourseConvertibleResourceDto> dtos, Guid courseId, Dictionary<Guid, List<Guid>> convertedSourceIds)
    {
        if (dtos.Count == 0) return;

        var ids = dtos.Select(d => d.ResourceId).ToList();

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _resourceRepository.GetQueryableAsync();
            var rows = await _asyncExecuter.ToListAsync(
                query.Where(x => ids.Contains(x.Id))
                    .Select(x => new
                    {
                        x.Id,
                        x.SourceType,
                        x.SourceCourseId,
                        x.SourceLibraryId,
                        x.CreatorId,
                        x.LibraryId
                    }));

            var courseNames = await GetCourseNameMapAsync(
                rows.Where(r => r.SourceCourseId.HasValue).Select(r => r.SourceCourseId!.Value).Distinct().ToList());
            var libraryNames = await GetLibraryNameMapAsync(
                rows.Where(r => r.SourceLibraryId.HasValue).Select(r => r.SourceLibraryId!.Value).Distinct().ToList());
            var userNames = await GetUserNameMapAsync(
                rows.Where(r => r.CreatorId.HasValue).Select(r => r.CreatorId!.Value).Distinct().ToList());

            // 已关联的库（避免重复转换）
            var linkQuery = await _libraryLinkRepository.GetQueryableAsync();
            var links = await _asyncExecuter.ToListAsync(
                linkQuery.Where(x => ids.Contains(x.ResourceId)));

            // 已关联的库名要单独查（上面的 libraryNames 是「来源库」的名字，不是挂载库）
            // 包含：关联表 ∪ 主库字段 ∪ 互转副本所在库
            var linkedLibraryIds = links.Select(l => l.LibraryId)
                .Concat(rows.Where(r => r.LibraryId.HasValue).Select(r => r.LibraryId!.Value))
                .Concat(convertedSourceIds.Values.SelectMany(x => x))
                .Distinct()
                .ToList();
            var linkedLibraryNames = await GetLibraryNameMapAsync(linkedLibraryIds);

            foreach (var dto in dtos)
            {
                var row = rows.FirstOrDefault(r => r.Id == dto.ResourceId);
                if (row != null)
                {
                    dto.SourceType = row.SourceType;
                    dto.SourceName = BuildSourceName(row.SourceType, row.SourceCourseId, row.SourceLibraryId,
                        courseNames, libraryNames);
                    if (row.CreatorId.HasValue)
                    {
                        dto.CreatorName = userNames.GetValueOrDefault(row.CreatorId.Value);
                    }
                }

                var mainLibId = rows.FirstOrDefault(r => r.Id == dto.ResourceId)?.LibraryId;
                var libIds = links.Where(l => l.ResourceId == dto.ResourceId).Select(l => l.LibraryId)
                    .Concat(mainLibId.HasValue ? new[] { mainLibId.Value } : Array.Empty<Guid>())
                    .Distinct()
                    .ToList();

                // 「已生成库副本」也算进了那个库（副本是通过互转生成的，源资源本身不挂库）
                if (convertedSourceIds.TryGetValue(dto.ResourceId, out var copyLibIds))
                {
                    libIds = libIds.Concat(copyLibIds).Distinct().ToList();
                }

                dto.SharedLibraryIds = libIds;
                dto.IsSharedToLibrary = libIds.Count > 0;
                dto.LibraryNames = libIds
                    .Select(id => linkedLibraryNames.GetValueOrDefault(id))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!)
                    .ToList();
            }
        }

        _ = courseId;
    }

    /// <summary>库资源补齐：来源文案 + 创建人 + 归属学校。</summary>
    private async Task FillLibraryResourceMetaAsync(List<LibraryConvertibleResourceDto> dtos)
    {
        if (dtos.Count == 0) return;
        var ids = dtos.Select(d => d.ResourceId).ToList();

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _resourceRepository.GetQueryableAsync();
            var rows = await _asyncExecuter.ToListAsync(
                query.Where(x => ids.Contains(x.Id))
                    .Select(x => new { x.Id, x.SourceType, x.SourceCourseId, x.SourceLibraryId, x.CreatorId, x.TenantId }));

            var courseNames = await GetCourseNameMapAsync(
                rows.Where(r => r.SourceCourseId.HasValue).Select(r => r.SourceCourseId!.Value).Distinct().ToList());
            var libraryNames = await GetLibraryNameMapAsync(
                rows.Where(r => r.SourceLibraryId.HasValue).Select(r => r.SourceLibraryId!.Value).Distinct().ToList());
            var userNames = await GetUserNameMapAsync(
                rows.Where(r => r.CreatorId.HasValue).Select(r => r.CreatorId!.Value).Distinct().ToList());
            var tenantNames = await GetTenantNameMapAsync(
                rows.Where(r => r.TenantId.HasValue).Select(r => r.TenantId!.Value).Distinct().ToList());

            foreach (var dto in dtos)
            {
                var row = rows.FirstOrDefault(r => r.Id == dto.ResourceId);
                if (row == null) continue;

                dto.SourceType = row.SourceType;
                dto.SourceName = BuildSourceName(row.SourceType, row.SourceCourseId, row.SourceLibraryId,
                    courseNames, libraryNames);
                if (row.CreatorId.HasValue)
                {
                    dto.CreatorName = userNames.GetValueOrDefault(row.CreatorId.Value);
                }
                if (row.TenantId.HasValue)
                {
                    dto.OwnerTenantName = tenantNames.GetValueOrDefault(row.TenantId.Value);
                }
            }
        }
    }

    internal static string? BuildSourceName(
        ResourceSourceType sourceType,
        Guid? sourceCourseId,
        Guid? sourceLibraryId,
        Dictionary<Guid, string> courseNames,
        Dictionary<Guid, string> libraryNames)
    {
        return sourceType switch
        {
            ResourceSourceType.FromCourse => sourceCourseId.HasValue
                ? $"来自课程：{courseNames.GetValueOrDefault(sourceCourseId.Value, "未知课程")}"
                : "来自课程",
            ResourceSourceType.FromLibrary => sourceLibraryId.HasValue
                ? $"来自资源库：{libraryNames.GetValueOrDefault(sourceLibraryId.Value, "未知资源库")}"
                : "来自资源库",
            _ => "新建"
        };
    }

    private async Task<Dictionary<Guid, string>> GetCourseNameMapAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        using var _ = _dataFilter.Disable<IMultiTenant>();
        var query = await _courseRepository.GetQueryableAsync();
        var rows = await _asyncExecuter.ToListAsync(
            query.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Title }));
        return rows.ToDictionary(x => x.Id, x => x.Title ?? string.Empty);
    }

    private async Task<Dictionary<Guid, string>> GetLibraryNameMapAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        using var _ = _dataFilter.Disable<IMultiTenant>();
        var query = await _libraryRepository.GetQueryableAsync();
        var rows = await _asyncExecuter.ToListAsync(
            query.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
        return rows.ToDictionary(x => x.Id, x => x.Name ?? string.Empty);
    }

    private async Task<Dictionary<Guid, string>> GetUserNameMapAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        using var _ = _dataFilter.Disable<IMultiTenant>();
        var query = await _userRepository.GetQueryableAsync();
        var rows = await _asyncExecuter.ToListAsync(
            query.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.UserName }));
        return rows.ToDictionary(x => x.Id, x => x.Name ?? x.UserName ?? string.Empty);
    }

    /// <summary>租户（学校）显示名：优先学校档案中文名，回退租户名。</summary>
    private async Task<Dictionary<Guid, string>> GetTenantNameMapAsync(List<Guid> ids)
    {
        var map = new Dictionary<Guid, string>();
        foreach (var tenantId in ids)
        {
            var school = await _schoolRepository.FindByTenantIdAsync(tenantId);
            if (school != null && !string.IsNullOrWhiteSpace(school.Name))
            {
                map[tenantId] = school.Name!;
                continue;
            }

            using (_dataFilter.Disable<IMultiTenant>())
            {
                var tenant = await _tenantRepository.FindAsync(tenantId);
                if (tenant != null)
                {
                    map[tenantId] = tenant.Name ?? string.Empty;
                }
            }
        }

        return map;
    }

    private bool IsTenantAdmin()
    {
        var roles = CurrentUser.Roles;
        return roles != null && (roles.Contains("SchoolAdmin") || roles.Contains("admin"));
    }

    /// <summary>是否院校/平台管理员（可以跨教师管理课程）。</summary>
    private bool IsCourseAdmin()
    {
        var roles = CurrentUser.Roles;
        return roles != null && (roles.Contains("SchoolAdmin") || roles.Contains("admin") || roles.Contains("LeagueAdmin"));
    }

    /// <summary>教师只能操作自己创建的课程（负责人或创建者）；管理员不限。</summary>
    private void EnsureOwnCourse(Course course)
    {
        if (IsCourseAdmin())
        {
            return;
        }

        var me = CurrentUser.Id;
        if (me.HasValue && (course.TeacherId == me || course.CreatorId == me))
        {
            return;
        }

        throw new UserFriendlyException("只能查看/转换自己创建的课程资源。");
    }
}
