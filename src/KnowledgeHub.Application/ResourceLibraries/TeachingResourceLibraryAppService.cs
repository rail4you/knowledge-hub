using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Majors;
using KnowledgeHub.Practicums;
using KnowledgeHub.Practicums.Enums;
using KnowledgeHub.Permissions;
using KnowledgeHub.ResourceLibraries.Dtos;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Schools;
using KnowledgeHub.TenantInfos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教师端「教学资源库建设」AppService。
///
/// <para>
/// 需求「教师用户的教学资源库建设」：教师看到与自己相关的教学资源库，
/// 并把<b>文件资源、课程、实训</b>共享进库里。
/// </para>
///
/// <para>
/// 教学资源库 = 租户（学校）下多个库；库是一系列文件资源 / 课程 / 实训的关联集合，
/// 不包含微专业、就业、咨询、双高等数据。
/// </para>
///
/// <para>
/// <b>跨租户</b>：院校共建库里，各校教师共享的内容仍归属各自租户，
/// 只通过 <c>LibraryId</c> 跨租户指向该库 —— 因此读取库内容必须禁用 ABP 租户过滤器，
/// 再按 LibraryId 精确匹配。
/// </para>
/// </summary>
[IgnoreAntiforgeryToken]
public class TeachingResourceLibraryAppService : KnowledgeHubAppService, ITeachingResourceLibraryAppService
{
    /// <summary>院校管理员角色名（租户级）。</summary>
    public const string SchoolAdminRole = "SchoolAdmin";

    /// <summary>平台全局管理员角色名（host 级）。</summary>
    public const string AdminRole = "admin";

    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IResourceLibraryAccessService _accessService;
    private readonly IResourceLibraryAttachmentValidator _attachmentValidator;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceVersion, Guid> _resourceVersionRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<PracticumProject, Guid> _practicumRepository;
    private readonly IMajorRepository _majorRepository;
    private readonly ISchoolRepository _schoolRepository;
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    /// <summary>资源—资源库关联表（多对多；库内资源以「主库字段 + 关联表」的并集为准）。</summary>
    private readonly IRepository<ResourceLibraryResource, Guid> _libraryResourceRepository;
    private readonly IDataFilter _dataFilter;
    private readonly IResourcePublicationService _publicationService;
    private readonly IRepository<KnowledgeHub.Enterprises.EnterpriseProfile, Guid> _enterpriseRepository;
    private readonly IRepository<KnowledgeHub.Enterprises.EnterpriseMember, Guid> _enterpriseMemberRepository;

    public TeachingResourceLibraryAppService(
        IResourceLibraryRepository libraryRepository,
        IResourceLibraryAccessService accessService,
        IResourceLibraryAttachmentValidator attachmentValidator,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceVersion, Guid> resourceVersionRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<PracticumProject, Guid> practicumRepository,
        IMajorRepository majorRepository,
        ISchoolRepository schoolRepository,
        ITenantInfoRepository tenantInfoRepository,
        ITenantRepository tenantRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<ResourceLibraryResource, Guid> libraryResourceRepository,
        IDataFilter dataFilter,
        IResourcePublicationService publicationService,
        IRepository<KnowledgeHub.Enterprises.EnterpriseProfile, Guid> enterpriseRepository,
        IRepository<KnowledgeHub.Enterprises.EnterpriseMember, Guid> enterpriseMemberRepository)
    {
        _libraryRepository = libraryRepository;
        _accessService = accessService;
        _attachmentValidator = attachmentValidator;
        _resourceRepository = resourceRepository;
        _resourceVersionRepository = resourceVersionRepository;
        _courseRepository = courseRepository;
        _practicumRepository = practicumRepository;
        _majorRepository = majorRepository;
        _schoolRepository = schoolRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _tenantRepository = tenantRepository;
        _identityUserRepository = identityUserRepository;
        _libraryResourceRepository = libraryResourceRepository;
        _dataFilter = dataFilter;
        _publicationService = publicationService;
        _enterpriseRepository = enterpriseRepository;
        _enterpriseMemberRepository = enterpriseMemberRepository;
    }

    /// <summary>获取「我的教学资源库」列表。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public virtual async Task<List<TeachingLibraryDto>> GetMyLibrariesAsync(TeachingLibraryListRequestDto input)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;
        var isTenantAdmin = IsTenantAdmin();

        var libraries = await _accessService.GetVisibleLibrariesAsync(tenantId, userId, isTenantAdmin);

        if (input?.Visibility.HasValue == true)
        {
            libraries = libraries.Where(x => x.Visibility == input.Visibility.Value).ToList();
        }

        if (input?.OnlyShared == true)
        {
            libraries = libraries.Where(x => x.Visibility == ResourceLibraryVisibility.CoBuilt).ToList();
        }

        if (!string.IsNullOrWhiteSpace(input?.Filter))
        {
            var filter = input.Filter.Trim();
            libraries = libraries
                .Where(x => (x.Name != null && x.Name.Contains(filter)) ||
                            (x.Description != null && x.Description.Contains(filter)))
                .ToList();
        }

        var result = new List<TeachingLibraryDto>();
        foreach (var library in libraries
                     .OrderBy(x => x.SortOrder)
                     .ThenByDescending(x => x.CreationTime))
        {
            result.Add(await ToLibraryDtoAsync(library, tenantId, userId, isTenantAdmin));
        }

        return result;
    }

    /// <summary>获取教学资源库详情。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public virtual async Task<TeachingLibraryDetailDto> GetLibraryAsync(Guid id)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;
        var isTenantAdmin = IsTenantAdmin();

        var library = await _accessService.GetLibraryAsync(id, tenantId, userId, isTenantAdmin);

        var dto = await ToLibraryDtoAsync(library, tenantId, userId, isTenantAdmin);

        var members = await _libraryRepository.GetMembersAsync(library.Id);
        var memberDtos = new List<ResourceLibraryMemberDto>();
        foreach (var m in members)
        {
            memberDtos.Add(new ResourceLibraryMemberDto
            {
                Id = m.Id,
                MemberTenantId = m.MemberTenantId,
                MemberTenantName = await ResolveTenantDisplayNameAsync(m.MemberTenantId, m.MemberTenantName),
                Role = m.Role,
                JoinedTime = m.JoinedTime
            });
        }

        var detail = new TeachingLibraryDetailDto
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            CoverUrl = dto.CoverUrl,
            Visibility = dto.Visibility,
            VisibilityName = dto.VisibilityName,
            OwnerTenantId = dto.OwnerTenantId,
            OwnerTenantName = dto.OwnerTenantName,
            Principal = dto.Principal,
            PrimaryMajorId = dto.PrimaryMajorId,
            PrimaryMajorName = dto.PrimaryMajorName,
            IsOwner = dto.IsOwner,
            IsPartner = dto.IsPartner,
            PartnerSchoolNames = dto.PartnerSchoolNames,
            ResourceCount = dto.ResourceCount,
            CourseCount = dto.CourseCount,
            PracticumCount = dto.PracticumCount,
            CanContribute = dto.CanContribute,
            Members = memberDtos,
            MyRoleName = await ResolveMyRoleNameAsync(library, tenantId, userId, isTenantAdmin),
            CreationTime = library.CreationTime,
            LastModificationTime = library.LastModificationTime
        };

        return detail;
    }

    /// <summary>获取教学资源库内的内容（文件资源 / 课程 / 实训）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public virtual async Task<PagedResultDto<TeachingLibraryContentDto>> GetContentsAsync(
        Guid id,
        TeachingLibraryContentListRequestDto input)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;
        var isTenantAdmin = IsTenantAdmin();

        var library = await _accessService.GetLibraryAsync(id, tenantId, userId, isTenantAdmin);

        input ??= new TeachingLibraryContentListRequestDto();
        var skip = input.SkipCount;
        var take = input.MaxResultCount <= 0 ? 20 : input.MaxResultCount;
        // 三类内容合并后统一分页，因此每类先多取 (skip + take) 条
        var fetch = skip + take;

        var items = new List<TeachingLibraryContentDto>();

        // V2.0：库内只展示「已发布」的内容。历史上直接挂库（无准入记录）的数据继续可见，
        // 避免升级后存量库内容凭空消失。
        var filterByPublication = await _publicationService.IsReviewEnabledAsync();
        var publishedResourceIds = filterByPublication
            ? await _publicationService.GetPublishedContentIdsAsync(library.Id, LibraryContentType.Resource)
            : null;
        var publishedCourseIds = filterByPublication
            ? await _publicationService.GetPublishedContentIdsAsync(library.Id, LibraryContentType.Course)
            : null;
        var publishedPracticumIds = filterByPublication
            ? await _publicationService.GetPublishedContentIdsAsync(library.Id, LibraryContentType.Practicum)
            : null;
        var allPublishedResourceIds = filterByPublication
            ? await _publicationService.GetPublicationContentIdsAsync(library.Id, LibraryContentType.Resource)
            : null;
        var allPublishedCourseIds = filterByPublication
            ? await _publicationService.GetPublicationContentIdsAsync(library.Id, LibraryContentType.Course)
            : null;
        var allPublishedPracticumIds = filterByPublication
            ? await _publicationService.GetPublicationContentIdsAsync(library.Id, LibraryContentType.Practicum)
            : null;

        // 未发布内容的可见性按「准入提交人」判定（而不是内容创建人），
        // 否则教师共享他人创建的资源入库后看不到自己提交的待审项（V2 前端验收 P2-8）。
        var resourceSubmitters = filterByPublication
            ? await _publicationService.GetPublicationSubmitterMapAsync(library.Id, LibraryContentType.Resource)
            : null;
        var courseSubmitters = filterByPublication
            ? await _publicationService.GetPublicationSubmitterMapAsync(library.Id, LibraryContentType.Course)
            : null;
        var practicumSubmitters = filterByPublication
            ? await _publicationService.GetPublicationSubmitterMapAsync(library.Id, LibraryContentType.Practicum)
            : null;

        if (input.ContentType is null or LibraryContentType.Resource)
        {
            items.AddRange(await LoadResourcesAsync(
                library.Id, input.Filter, input.OnlyMine, userId, fetch,
                publishedResourceIds, allPublishedResourceIds, isTenantAdmin, resourceSubmitters));
        }

        if (input.ContentType is null or LibraryContentType.Course)
        {
            items.AddRange(await LoadCoursesAsync(
                library.Id, input.Filter, input.OnlyMine, userId, fetch,
                publishedCourseIds, allPublishedCourseIds, isTenantAdmin, courseSubmitters));
        }

        if (input.ContentType is null or LibraryContentType.Practicum)
        {
            items.AddRange(await LoadPracticumsAsync(
                library.Id, input.Filter, input.OnlyMine, userId, fetch,
                publishedPracticumIds, allPublishedPracticumIds, isTenantAdmin, practicumSubmitters));
        }

        var sorted = ApplySorting(items, input.Sorting);
        var total = sorted.Count;
        var page = sorted.Skip(skip).Take(take).ToList();

        return new PagedResultDto<TeachingLibraryContentDto>(total, page);
    }

    /// <summary>把已有内容共享进教学资源库。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Build)]
    public virtual async Task<TeachingLibraryContentDto> AttachAsync(Guid id, AttachLibraryContentDto input)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;
        var isTenantAdmin = IsTenantAdmin();

        var library = await ResolveContributableLibraryAsync(id, tenantId, userId, isTenantAdmin);

        if (input.ContentId == Guid.Empty)
        {
            throw new UserFriendlyException("请选择要共享的内容。");
        }

        switch (input.ContentType)
        {
            case LibraryContentType.Resource:
            {
                var resource = await _resourceRepository.FindAsync(input.ContentId)
                               ?? throw new UserFriendlyException("资源不存在。");
                EnsureOwnContent(resource.TenantId, tenantId);
                await _attachmentValidator.ValidateAsync(library.Id, tenantId);

                if (await _publicationService.IsReviewEnabledAsync())
                {
                    // 企业人员提交 → 标记 Enterprise，审核链路仍是「入驻校初审 → 联盟终审」
                    var submitterType = await ResolveSubmitterTypeAsync();
                    // 审核开启：先建准入记录 + 审核单，通过后才真正挂库（挂库由审核处理器完成）
                    await _publicationService.BeginAdmissionAsync(
                        library.Id, LibraryContentType.Resource, resource.Id, input.ContentVersionId,
                        policyJson: null, submitterType: submitterType);
                    return await ToContentDtoAsync(resource);
                }

                resource.LibraryId = library.Id;
                await _resourceRepository.UpdateAsync(resource, autoSave: true);
                await AddLibraryLinkAsync(library.Id, resource.Id, tenantId);
                return await ToContentDtoAsync(resource);
            }

            case LibraryContentType.Course:
            {
                var course = await _courseRepository.FindAsync(input.ContentId)
                             ?? throw new UserFriendlyException("课程不存在。");
                EnsureOwnContent(course.TenantId, tenantId);
                await _attachmentValidator.ValidateAsync(library.Id, tenantId);

                if (await _publicationService.IsReviewEnabledAsync())
                {
                    await _publicationService.BeginAdmissionAsync(
                        library.Id, LibraryContentType.Course, course.Id, null);
                    return await ToContentDtoAsync(course);
                }

                course.LibraryId = library.Id;
                await _courseRepository.UpdateAsync(course, autoSave: true);
                return await ToContentDtoAsync(course);
            }

            case LibraryContentType.Practicum:
            {
                var practicum = await _practicumRepository.FindAsync(input.ContentId)
                                ?? throw new UserFriendlyException("实训不存在。");
                EnsureOwnContent(practicum.TenantId, tenantId);
                await _attachmentValidator.ValidateAsync(library.Id, tenantId);

                if (await _publicationService.IsReviewEnabledAsync())
                {
                    await _publicationService.BeginAdmissionAsync(
                        library.Id, LibraryContentType.Practicum, practicum.Id, null);
                    return await ToContentDtoAsync(practicum);
                }

                practicum.LibraryId = library.Id;
                await _practicumRepository.UpdateAsync(practicum, autoSave: true);
                return await ToContentDtoAsync(practicum);
            }

            default:
                throw new UserFriendlyException("不支持的内容类型。");
        }
    }

    /// <summary>把内容移出教学资源库。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Build)]
    public virtual async Task DetachAsync(Guid id, LibraryContentType contentType, Guid contentId)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;
        var isTenantAdmin = IsTenantAdmin();

        var library = await _accessService.GetContributableLibraryAsync(id, tenantId, userId, isTenantAdmin);

        switch (contentType)
        {
            case LibraryContentType.Resource:
            {
                var resource = await _resourceRepository.FindAsync(contentId)
                               ?? throw new UserFriendlyException("资源不存在。");
                EnsureCanDetach(resource.CreatorId, resource.TenantId, userId, tenantId, isTenantAdmin);
                // 下架留痕：有准入记录时置 Withdrawn（不删除，便于追溯与审计）
                await _publicationService.WithdrawAsync(library.Id, LibraryContentType.Resource, resource.Id, "教师移出资源库");
                resource.LibraryId = null;
                await _resourceRepository.UpdateAsync(resource, autoSave: true);
                await RemoveLibraryLinkAsync(library.Id, resource.Id);
                return;
            }

            case LibraryContentType.Course:
            {
                var course = await _courseRepository.FindAsync(contentId)
                             ?? throw new UserFriendlyException("课程不存在。");
                EnsureCanDetach(course.CreatorId, course.TenantId, userId, tenantId, isTenantAdmin);
                await _publicationService.WithdrawAsync(library.Id, LibraryContentType.Course, course.Id, "教师移出资源库");
                course.LibraryId = null;
                await _courseRepository.UpdateAsync(course, autoSave: true);
                return;
            }

            case LibraryContentType.Practicum:
            {
                var practicum = await _practicumRepository.FindAsync(contentId)
                                ?? throw new UserFriendlyException("实训不存在。");
                EnsureCanDetach(practicum.CreatorId, practicum.TenantId, userId, tenantId, isTenantAdmin);
                await _publicationService.WithdrawAsync(library.Id, LibraryContentType.Practicum, practicum.Id, "教师移出资源库");
                practicum.LibraryId = null;
                await _practicumRepository.UpdateAsync(practicum, autoSave: true);
                return;
            }

            default:
                throw new UserFriendlyException("不支持的内容类型。");
        }
    }

    /// <summary>在教学资源库内新建文件资源。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Build)]
    public virtual async Task<TeachingLibraryContentDto> CreateResourceAsync(Guid id, CreateTeachingLibraryResourceDto input)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;
        var isTenantAdmin = IsTenantAdmin();

        var library = await ResolveContributableLibraryAsync(id, tenantId, userId, isTenantAdmin);

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new UserFriendlyException("请填写资源名称。");
        }

        await _attachmentValidator.ValidateAsync(library.Id, tenantId);

        var resource = new Resource
        {
            TenantId = tenantId,
            Name = input.Name.Trim(),
            Description = input.Description,
            ResourceType = input.ResourceType,
            FilePath = input.FilePath,
            FileSize = input.FileSize,
            FileExtension = input.FileExtension,
            OriginalFileName = input.OriginalFileName,
            Keywords = input.Keywords,
            CopyrightInfo = input.CopyrightInfo,
            IsDownloadable = input.IsDownloadable ?? true,
            MajorId = input.MajorId,
            Status = ResourceStatus.Draft,
            CurrentVersion = 1
        };

        await _resourceRepository.InsertAsync(resource, autoSave: true);

        await _resourceVersionRepository.InsertAsync(new ResourceVersion
        {
            ResourceId = resource.Id,
            Version = 1,
            FilePath = resource.FilePath,
            FileSize = resource.FileSize,
            UpdateContent = "初始版本",
            IsCurrentVersion = true
        }, autoSave: true);

        if (await _publicationService.IsReviewEnabledAsync())
        {
            // 库内新建：内容（草稿）与准入合并审一条，通过后内容置已发布并挂库
            var version = await _resourceVersionRepository.FirstOrDefaultAsync(
                x => x.ResourceId == resource.Id && x.IsCurrentVersion);
            await _publicationService.BeginAdmissionAsync(
                library.Id, LibraryContentType.Resource, resource.Id, version?.Id);
            return await ToContentDtoAsync(resource);
        }

        resource.LibraryId = library.Id;
        await _resourceRepository.UpdateAsync(resource, autoSave: true);

        return await ToContentDtoAsync(resource);
    }

    /// <summary>在教学资源库内新建课程。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Build)]
    public virtual async Task<TeachingLibraryContentDto> CreateCourseAsync(Guid id, CreateTeachingLibraryCourseDto input)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;
        var isTenantAdmin = IsTenantAdmin();

        var library = await _accessService.GetContributableLibraryAsync(id, tenantId, userId, isTenantAdmin);

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写课程名称。");
        }

        await _attachmentValidator.ValidateAsync(library.Id, tenantId);

        var course = new Course(GuidGenerator.Create(), input.Title.Trim())
        {
            TenantId = tenantId,
            Description = input.Description,
            CoverImageUrl = input.CoverImageUrl,
            MajorId = input.MajorId,
            Semester = input.Semester,
            Credits = input.Credits,
            SemesterHours = input.SemesterHours,
            TeacherId = userId,
            Status = CourseStatus.Draft,
            LibraryId = library.Id
        };

        await _courseRepository.InsertAsync(course, autoSave: true);

        return await ToContentDtoAsync(course);
    }

    /// <summary>在教学资源库内新建实训。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Build)]
    public virtual async Task<TeachingLibraryContentDto> CreatePracticumAsync(Guid id, CreateTeachingLibraryPracticumDto input)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;
        var isTenantAdmin = IsTenantAdmin();

        var library = await _accessService.GetContributableLibraryAsync(id, tenantId, userId, isTenantAdmin);

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写实训名称。");
        }

        await _attachmentValidator.ValidateAsync(library.Id, tenantId);

        var practicum = new PracticumProject(GuidGenerator.Create(), input.Title.Trim())
        {
            TenantId = tenantId,
            Summary = input.Summary,
            Description = input.Description,
            CoverImageUrl = input.CoverImageUrl,
            Major = input.Major,
            ClassName = input.ClassName,
            MaxScore = input.MaxScore ?? 100,
            Status = PracticumProjectStatus.Draft,
            LibraryId = library.Id
        };

        await _practicumRepository.InsertAsync(practicum, autoSave: true);

        return await ToContentDtoAsync(practicum);
    }

    #region 私有辅助

    /// <summary>当前用户是否为本租户的院校管理员。</summary>
    private bool IsTenantAdmin()
    {
        var roles = CurrentUser.Roles;
        if (roles == null)
        {
            return false;
        }

        return roles.Contains(SchoolAdminRole) || roles.Contains(AdminRole);
    }

    /// <summary>只能共享本校的内容（防止把别校内容据为己有）。</summary>
    private static void EnsureOwnContent(Guid? contentTenantId, Guid currentTenantId)
    {
        if (contentTenantId != currentTenantId)
        {
            throw new UserFriendlyException("只能共享本校的内容到教学资源库。");
        }
    }

    /// <summary>移出内容：创建人本人或本校院校管理员可以操作。</summary>
    private static void EnsureCanDetach(
        Guid? creatorId,
        Guid? contentTenantId,
        Guid? currentUserId,
        Guid currentTenantId,
        bool isTenantAdmin)
    {
        if (isTenantAdmin && contentTenantId == currentTenantId)
        {
            return;
        }

        if (currentUserId.HasValue && creatorId.HasValue && creatorId.Value == currentUserId.Value)
        {
            return;
        }

        throw new UserFriendlyException("只有内容创建人或本校院校管理员可以将其移出教学资源库。");
    }

    /// <summary>取某个库在关联表中登记的全部资源 Id（多对多）。</summary>
    private async Task<List<Guid>> GetLinkedResourceIdsAsync(Guid libraryId)
    {
        var query = await _libraryResourceRepository.GetQueryableAsync();
        return await AsyncExecuter.ToListAsync(
            query.Where(x => x.LibraryId == libraryId).Select(x => x.ResourceId));
    }

    /// <summary>把资源登记进库的关联表（幂等）。</summary>
    private async Task AddLibraryLinkAsync(Guid libraryId, Guid resourceId, Guid? tenantId)
    {
        var query = await _libraryResourceRepository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(
            query.Where(x => x.LibraryId == libraryId && x.ResourceId == resourceId));
        if (exists)
        {
            return;
        }

        await _libraryResourceRepository.InsertAsync(
            new ResourceLibraryResource(GuidGenerator.Create(), libraryId, resourceId, tenantId),
            autoSave: true);
    }

    /// <summary>解除资源与库的关联（移除关联表记录）。</summary>
    private async Task RemoveLibraryLinkAsync(Guid libraryId, Guid resourceId)
    {
        var query = await _libraryResourceRepository.GetQueryableAsync();
        var link = await AsyncExecuter.FirstOrDefaultAsync(
            query.Where(x => x.LibraryId == libraryId && x.ResourceId == resourceId));
        if (link != null)
        {
            await _libraryResourceRepository.DeleteAsync(link, autoSave: true);
        }
    }

    /// <summary>库内内容列表：文件资源（跨租户读取以支持共建库）。</summary>
    /// <summary>
    /// 库内内容可见性：已发布 → 所有人可见；有准入记录但未发布 → 仅提交人 / 院校管理员可见；
    /// 无准入记录（历史直接挂库的数据）→ 保持可见，避免升级后存量内容消失。
    /// </summary>
    private static List<T> FilterByPublication<T>(
        List<T> items,
        Func<T, Guid> idSelector,
        Func<T, Guid?> creatorSelector,
        HashSet<Guid>? publishedIds,
        HashSet<Guid>? publicationIds,
        Guid? currentUserId,
        bool isTenantAdmin,
        IReadOnlyDictionary<Guid, Guid>? submitterMap = null)
    {
        if (publishedIds == null || publicationIds == null)
        {
            return items;
        }

        return items.Where(item =>
        {
            var id = idSelector(item);
            if (publishedIds.Contains(id))
            {
                return true;
            }

            if (!publicationIds.Contains(id))
            {
                // 没有准入记录：历史数据，保持旧行为
                return true;
            }

            // 有准入记录但未发布：只有提交人本人或院校管理员能看到
            if (isTenantAdmin)
            {
                return true;
            }

            if (!currentUserId.HasValue)
            {
                return false;
            }

            // 优先按「准入提交人」判定；没有映射时回退到内容创建人
            if (submitterMap != null && submitterMap.TryGetValue(id, out var submitter))
            {
                return submitter == currentUserId.Value;
            }

            return creatorSelector(item) == currentUserId.Value;
        }).ToList();
    }

    private async Task<List<TeachingLibraryContentDto>> LoadResourcesAsync(
        Guid libraryId,
        string? filter,
        bool? onlyMine,
        Guid? userId,
        int take,
        HashSet<Guid>? publishedIds = null,
        HashSet<Guid>? publicationIds = null,
        bool isTenantAdmin = false,
        IReadOnlyDictionary<Guid, Guid>? submitters = null)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _resourceRepository.GetQueryableAsync();
            // 多对多：资源可能只关联到本库（主库字段指向别的库），以关联表并集为准
            var linkedResourceIds = await GetLinkedResourceIdsAsync(libraryId);
            var publicationSet = publicationIds ?? new HashSet<Guid>();
            query = query.Where(x => x.LibraryId == libraryId
                                     || linkedResourceIds.Contains(x.Id)
                                     || publicationSet.Contains(x.Id));

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var f = filter.Trim();
                query = query.Where(x => x.Name.Contains(f));
            }

            if (onlyMine == true && userId.HasValue)
            {
                query = query.Where(x => x.CreatorId == userId.Value);
            }

            var resources = await AsyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.CreationTime).Take(take));

            resources = FilterByPublication(resources, x => x.Id, x => x.CreatorId, publishedIds, publicationIds, userId, isTenantAdmin, submitters);

            var result = new List<TeachingLibraryContentDto>(resources.Count);
            foreach (var r in resources)
            {
                result.Add(await WithPublicationAsync(await ToContentDtoAsync(r), libraryId));
            }

            return result;
        }
    }

    /// <summary>库内内容列表：课程。</summary>
    private async Task<List<TeachingLibraryContentDto>> LoadCoursesAsync(
        Guid libraryId,
        string? filter,
        bool? onlyMine,
        Guid? userId,
        int take,
        HashSet<Guid>? publishedIds = null,
        HashSet<Guid>? publicationIds = null,
        bool isTenantAdmin = false,
        IReadOnlyDictionary<Guid, Guid>? submitters = null)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _courseRepository.GetQueryableAsync();
            var publicationSet = publicationIds ?? new HashSet<Guid>();
            query = query.Where(x => x.LibraryId == libraryId || publicationSet.Contains(x.Id));

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var f = filter.Trim();
                query = query.Where(x => x.Title.Contains(f));
            }

            if (onlyMine == true && userId.HasValue)
            {
                query = query.Where(x => x.CreatorId == userId.Value);
            }

            var courses = await AsyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.CreationTime).Take(take));

            courses = FilterByPublication(courses, x => x.Id, x => x.CreatorId, publishedIds, publicationIds, userId, isTenantAdmin, submitters);

            var result = new List<TeachingLibraryContentDto>(courses.Count);
            foreach (var c in courses)
            {
                result.Add(await WithPublicationAsync(await ToContentDtoAsync(c), libraryId));
            }

            return result;
        }
    }

    /// <summary>库内内容列表：实训。</summary>
    private async Task<List<TeachingLibraryContentDto>> LoadPracticumsAsync(
        Guid libraryId,
        string? filter,
        bool? onlyMine,
        Guid? userId,
        int take,
        HashSet<Guid>? publishedIds = null,
        HashSet<Guid>? publicationIds = null,
        bool isTenantAdmin = false,
        IReadOnlyDictionary<Guid, Guid>? submitters = null)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _practicumRepository.GetQueryableAsync();
            var publicationSet = publicationIds ?? new HashSet<Guid>();
            query = query.Where(x => x.LibraryId == libraryId || publicationSet.Contains(x.Id));

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var f = filter.Trim();
                query = query.Where(x => x.Title.Contains(f));
            }

            if (onlyMine == true && userId.HasValue)
            {
                query = query.Where(x => x.CreatorId == userId.Value);
            }

            var practicums = await AsyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.CreationTime).Take(take));

            practicums = FilterByPublication(practicums, x => x.Id, x => x.CreatorId, publishedIds, publicationIds, userId, isTenantAdmin, submitters);

            var result = new List<TeachingLibraryContentDto>(practicums.Count);
            foreach (var p in practicums)
            {
                result.Add(await WithPublicationAsync(await ToContentDtoAsync(p), libraryId));
            }

            return result;
        }
    }

    /// <summary>排序：默认按创建时间倒序。</summary>
    private static List<TeachingLibraryContentDto> ApplySorting(
        List<TeachingLibraryContentDto> items,
        string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting) ||
            sorting.Contains("creationTime", StringComparison.OrdinalIgnoreCase))
        {
            return items.OrderByDescending(x => x.CreationTime).ToList();
        }

        if (sorting.Contains("name", StringComparison.OrdinalIgnoreCase))
        {
            return items.OrderBy(x => x.Name).ToList();
        }

        return items.OrderByDescending(x => x.CreationTime).ToList();
    }

    private async Task<TeachingLibraryDto> ToLibraryDtoAsync(
        ResourceLibrary library,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        var members = await _libraryRepository.GetMembersAsync(library.Id);
        var ownerTenant = await _tenantRepository.FindAsync(library.OwnerTenantId);

        string? majorName = null;
        if (library.PrimaryMajorId.HasValue)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var major = await _majorRepository.FindAsync(library.PrimaryMajorId.Value);
                majorName = major?.Name;
            }
        }

        int resourceCount;
        int courseCount;
        int practicumCount;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            // 多对多：资源可能只登记在关联表里（主库字段指向别的库），
            // 统计口径必须与 GetContentsAsync 一致，取「主库字段 + 关联表 + 准入发布」的并集
            // （否则库里明明有已发布内容，卡片却显示 0 份资源 —— V2 前端验收 P2-9）。
            var linkedResourceIds = await GetLinkedResourceIdsAsync(library.Id);
            var countedResourceIds = await _publicationService.GetCountedContentIdsAsync(library.Id, LibraryContentType.Resource);
            var countedCourseIds = await _publicationService.GetCountedContentIdsAsync(library.Id, LibraryContentType.Course);
            var countedPracticumIds = await _publicationService.GetCountedContentIdsAsync(library.Id, LibraryContentType.Practicum);

            resourceCount = await AsyncExecuter.CountAsync(
                (await _resourceRepository.GetQueryableAsync())
                    .Where(x => x.LibraryId == library.Id
                                || linkedResourceIds.Contains(x.Id)
                                || countedResourceIds.Contains(x.Id)));
            courseCount = await AsyncExecuter.CountAsync(
                (await _courseRepository.GetQueryableAsync())
                    .Where(x => x.LibraryId == library.Id || countedCourseIds.Contains(x.Id)));
            practicumCount = await AsyncExecuter.CountAsync(
                (await _practicumRepository.GetQueryableAsync())
                    .Where(x => x.LibraryId == library.Id || countedPracticumIds.Contains(x.Id)));
        }

        // 同步内存中的共建成员（可见性判定依赖它，见 ResourceLibraryAccessService）
        library.Members.Clear();
        foreach (var member in members)
        {
            library.Members.Add(member);
        }

        var partnerNames = new List<string>();
        foreach (var m in members.Where(x => x.Role == ResourceLibraryMemberRole.Partner))
        {
            partnerNames.Add(await ResolveTenantDisplayNameAsync(m.MemberTenantId, m.MemberTenantName));
        }

        return new TeachingLibraryDto
        {
            Id = library.Id,
            Name = library.Name,
            Description = library.Description,
            CoverUrl = library.CoverUrl,
            Visibility = library.Visibility,
            VisibilityName = _accessService.GetVisibilityName(library.Visibility),
            OwnerTenantId = library.OwnerTenantId,
            OwnerTenantName = await ResolveTenantDisplayNameAsync(library.OwnerTenantId, ownerTenant?.Name),
            Principal = library.Principal,
            PrimaryMajorId = library.PrimaryMajorId,
            PrimaryMajorName = majorName,
            IsOwner = library.OwnerTenantId == tenantId,
            IsPartner = !library.OwnerTenantId.Equals(tenantId) &&
                        members.Any(x => x.MemberTenantId == tenantId),
            PartnerSchoolNames = partnerNames,
            ResourceCount = resourceCount,
            CourseCount = courseCount,
            PracticumCount = practicumCount,
            CanContribute = await _accessService.CanContributeAsync(library, tenantId, userId, isTenantAdmin)
        };
    }

    /// <summary>我在该库中的角色说明。</summary>
    private async Task<string?> ResolveMyRoleNameAsync(
        ResourceLibrary library,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        if (isTenantAdmin)
        {
            return "院校管理员";
        }

        if (library.OwnerTenantId == tenantId)
        {
            if (userId.HasValue && await _accessService.IsAssignedTeacherAsync(library.Id, userId.Value))
            {
                return "建设教师";
            }

            return library.Visibility switch
            {
                ResourceLibraryVisibility.Private => "主建教师（未分配）",
                ResourceLibraryVisibility.Public => "本校教师",
                ResourceLibraryVisibility.CoBuilt => "主建方教师",
                _ => null
            };
        }

        if (library.Members.Any(x => x.MemberTenantId == tenantId))
        {
            return library.Visibility == ResourceLibraryVisibility.CoBuilt ? "共建方教师" : "参与方教师";
        }

        return null;
    }


    /// <summary>
    /// 解析「可贡献的库」。
    ///
    /// <para>
    /// 默认走 <see cref="IResourceLibraryAccessService.GetContributableLibraryAsync"/>（教师分配 / 主建专业 / 共建成员）。
    /// 额外放行一种角色：<b>已认证企业的人员</b>，允许向「本校主建的库」贡献内容
    /// （产教合作里企业案例入库的真实场景；企业不是教师，不会出现在教师分配名单里）。
    /// </para>
    /// </summary>
    private async Task<ResourceLibrary> ResolveContributableLibraryAsync(
        Guid libraryId,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        // 企业人员：必须「已认证且在有效期内」才允许贡献内容。
        // 这道门禁要放在最前面：企业账号现在能看到本校的库（可见性放开），
        // 若只在「取库失败」的兜底分支里校验，未认证企业会绕过认证直接入库（回归）。
        if (await IsEnterpriseMemberAsync())
        {
            var enterpriseSubmitterType = await TryResolveEnterpriseSubmitterAsync();
            if (enterpriseSubmitterType == null)
            {
                throw new UserFriendlyException(
                    "企业尚未通过认证或已过有效期，暂不能向资源库贡献内容。请先完成企业认证后重试。");
            }
        }

        try
        {
            return await _accessService.GetContributableLibraryAsync(libraryId, tenantId, userId, isTenantAdmin);
        }
        catch (UserFriendlyException)
        {
            // 企业贡献者兜底：必须是本校主建的库 + 已认证企业
            var submitterType = await TryResolveEnterpriseSubmitterAsync();
            if (submitterType == null)
            {
                // 是企业人员但企业未认证 / 已过期 → 给明确原因，而不是笼统的"无权"
                if (await IsEnterpriseMemberAsync())
                {
                    throw new UserFriendlyException(
                        "企业尚未通过认证或已过有效期，暂不能向资源库贡献内容。请先完成企业认证后重试。");
                }

                throw;
            }

            ResourceLibrary library;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                library = await _libraryRepository.FindAsync(libraryId)
                          ?? throw new UserFriendlyException("教学资源库不存在。");
            }

            if (library.OwnerTenantId != tenantId || library.Status != ResourceLibraryStatus.Active)
            {
                throw new UserFriendlyException("只能向本校主建且已发布的资源库贡献内容。");
            }

            return library;
        }
    }

    /// <summary>
    /// 解析提交方类型：企业用户（绑定了企业主体）→ Enterprise，否则 School。
    ///
    /// <para>
    /// 企业贡献有额外门禁：企业必须<b>已认证且在有效期内</b>，否则不允许把内容推进共享库
    /// （避免未认证主体以企业名义对外发布内容）。
    /// </para>
    /// </summary>
    private async Task<PublicationSubmitterType> ResolveSubmitterTypeAsync()
    {
        var soft = await TryResolveEnterpriseSubmitterAsync();
        return soft ?? PublicationSubmitterType.School;
    }

    /// <summary>当前用户是否为企业成员（不判断认证状态）。</summary>
    private async Task<bool> IsEnterpriseMemberAsync()
    {
        var userId = CurrentUser.Id;
        if (!userId.HasValue)
        {
            return false;
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await AsyncExecuter.AnyAsync(
                (await _enterpriseMemberRepository.GetQueryableAsync()).Where(x => x.UserId == userId.Value));
        }
    }

    /// <summary>企业提交方软判定：是企业成员且企业已认证可用 → Enterprise；否则 null。</summary>
    private async Task<PublicationSubmitterType?> TryResolveEnterpriseSubmitterAsync()
    {
        var userId = CurrentUser.Id;
        if (!userId.HasValue)
        {
            return null;
        }

        KnowledgeHub.Enterprises.EnterpriseMember? member;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            member = await AsyncExecuter.FirstOrDefaultAsync(
                (await _enterpriseMemberRepository.GetQueryableAsync()).Where(x => x.UserId == userId.Value));
        }

        if (member == null)
        {
            return null;
        }

        KnowledgeHub.Enterprises.EnterpriseProfile? profile;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            profile = await _enterpriseRepository.FindAsync(member.EnterpriseProfileId);
        }

        if (profile == null)
        {
            return null;
        }

        return profile.IsUsable(DateTime.UtcNow) ? PublicationSubmitterType.Enterprise : null;
    }

    /// <summary>把库内发布（准入）状态补进 DTO，供界面显示"审核中 / 已驳回 / 已下架"。</summary>
    private async Task<TeachingLibraryContentDto> WithPublicationAsync(
        TeachingLibraryContentDto dto,
        Guid libraryId)
    {
        var publication = await _publicationService.FindAsync(libraryId, dto.ContentType, dto.Id);
        if (publication == null)
        {
            // 历史数据：无准入记录 → 视为已发布
            dto.PublicationStatus = ResourcePublicationStatus.Published;
            dto.PublicationStatusName = "已发布";
            return dto;
        }

        dto.PublicationStatus = publication.Status;
        dto.PublicationStatusName = publication.Status switch
        {
            ResourcePublicationStatus.Draft => "草稿",
            ResourcePublicationStatus.PendingReview => "审核中",
            ResourcePublicationStatus.Published => "已发布",
            ResourcePublicationStatus.Rejected => "已驳回",
            ResourcePublicationStatus.Withdrawn => "已下架",
            _ => publication.Status.ToString()
        };
        dto.PublicationRejectReason = publication.RejectReason;
        dto.ReviewTicketId = publication.ReviewTicketId;
        return dto;
    }

    private async Task<TeachingLibraryContentDto> ToContentDtoAsync(Resource resource)
    {
        return new TeachingLibraryContentDto
        {
            Id = resource.Id,
            ContentType = LibraryContentType.Resource,
            ContentTypeName = "文件资源",
            Name = resource.Name,
            Description = resource.Description,
            CoverUrl = null,
            TenantId = resource.TenantId,
            TenantName = await ResolveTenantDisplayNameAsync(resource.TenantId ?? Guid.Empty, null),
            MajorId = resource.MajorId,
            MajorName = await ResolveMajorNameAsync(resource.MajorId),
            CreatorId = resource.CreatorId,
            CreatorName = await ResolveUserNameAsync(resource.CreatorId),
            FileExtension = resource.FileExtension,
            FileSize = resource.FileSize,
            Status = (int)resource.Status,
            CreationTime = resource.CreationTime,
            CanDetach = await CanDetachAsync(resource.CreatorId, resource.TenantId)
        };
    }

    private async Task<TeachingLibraryContentDto> ToContentDtoAsync(Course course)
    {
        return new TeachingLibraryContentDto
        {
            Id = course.Id,
            ContentType = LibraryContentType.Course,
            ContentTypeName = "课程",
            Name = course.Title,
            Description = course.Description,
            CoverUrl = course.CoverImageUrl,
            TenantId = course.TenantId,
            TenantName = await ResolveTenantDisplayNameAsync(course.TenantId ?? Guid.Empty, null),
            MajorId = course.MajorId,
            MajorName = await ResolveMajorNameAsync(course.MajorId),
            CreatorId = course.CreatorId,
            CreatorName = await ResolveUserNameAsync(course.CreatorId),
            Status = (int)course.Status,
            CreationTime = course.CreationTime,
            CanDetach = await CanDetachAsync(course.CreatorId, course.TenantId)
        };
    }

    private async Task<TeachingLibraryContentDto> ToContentDtoAsync(PracticumProject practicum)
    {
        return new TeachingLibraryContentDto
        {
            Id = practicum.Id,
            ContentType = LibraryContentType.Practicum,
            ContentTypeName = "实训",
            Name = practicum.Title,
            Description = practicum.Summary ?? practicum.Description,
            CoverUrl = practicum.CoverImageUrl,
            TenantId = practicum.TenantId,
            TenantName = await ResolveTenantDisplayNameAsync(practicum.TenantId ?? Guid.Empty, null),
            CreatorId = practicum.CreatorId,
            CreatorName = await ResolveUserNameAsync(practicum.CreatorId),
            Status = (int)practicum.Status,
            CreationTime = practicum.CreationTime,
            CanDetach = await CanDetachAsync(practicum.CreatorId, practicum.TenantId)
        };
    }

    private async Task<bool> CanDetachAsync(Guid? creatorId, Guid? contentTenantId)
    {
        var userId = CurrentUser.Id;
        if (!userId.HasValue)
        {
            return false;
        }

        if (creatorId.HasValue && creatorId.Value == userId.Value)
        {
            return true;
        }

        return IsTenantAdmin() && contentTenantId == CurrentTenant.Id;
    }

    private async Task<string?> ResolveMajorNameAsync(Guid? majorId)
    {
        if (!majorId.HasValue)
        {
            return null;
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var major = await _majorRepository.FindAsync(majorId.Value);
            return major?.Name;
        }
    }

    private async Task<string?> ResolveUserNameAsync(Guid? userId)
    {
        if (!userId.HasValue)
        {
            return null;
        }

        var users = await _identityUserRepository.GetListAsync(x => x.Id == userId.Value);
        var user = users.FirstOrDefault();
        return user?.Name ?? user?.UserName;
    }

    /// <summary>租户 → 中文机构名（学校档案名 → 专业库展示名 → 租户名）。</summary>
    private async Task<string> ResolveTenantDisplayNameAsync(Guid tenantId, string? fallback)
    {
        // 学校档案名优先：那是「这所学校」的权威中文机构名。
        // 注意不能拿 fallback 做等值排除 —— 历史成员记录里存的往往就是学校档案名，
        // 一旦等值就漏掉学校名、错误地回退到专业库展示名。
        var school = await _schoolRepository.FindByTenantIdAsync(tenantId);
        if (school != null && !string.IsNullOrWhiteSpace(school.Name))
        {
            return school.Name;
        }

        var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenantId);
        if (!string.IsNullOrWhiteSpace(tenantInfo?.Name))
        {
            return tenantInfo.Name;
        }

        var tenant = await _tenantRepository.FindAsync(tenantId);
        if (!string.IsNullOrWhiteSpace(tenant?.Name))
        {
            return tenant.Name;
        }

        return fallback ?? tenantId.ToString();
    }

    #endregion
}
