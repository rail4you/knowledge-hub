using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Permissions;
using KnowledgeHub.ResourceLibraries.Dtos;
using KnowledgeHub.Schools;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 「其他学员」AppService 实现（PRD 5.3）。
/// </summary>
[Authorize]
public class ResourceLibraryLearnerAppService : KnowledgeHubAppService, IResourceLibraryLearnerAppService
{
    private readonly IRepository<ResourceLibraryLearner, Guid> _learnerRepository;
    private readonly IRepository<KnowledgeHub.Certificates.Certificate, Guid> _certificateRepository;
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IResourceLibraryAccessService _accessService;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ISchoolRepository _schoolRepository;
    private readonly IDataFilter _dataFilter;

    public ResourceLibraryLearnerAppService(
        IRepository<ResourceLibraryLearner, Guid> learnerRepository,
        IRepository<KnowledgeHub.Certificates.Certificate, Guid> certificateRepository,
        IResourceLibraryRepository libraryRepository,
        IResourceLibraryAccessService accessService,
        IRepository<IdentityUser, Guid> userRepository,
        ISchoolRepository schoolRepository,
        IDataFilter dataFilter)
    {
        _learnerRepository = learnerRepository;
        _certificateRepository = certificateRepository;
        _libraryRepository = libraryRepository;
        _accessService = accessService;
        _userRepository = userRepository;
        _schoolRepository = schoolRepository;
        _dataFilter = dataFilter;
    }

    /// <summary>
    /// 登记 / 更新「其他学员」档案（手机号验证 + 实名认证）。
    ///
    /// <para>
    /// 手机号校验：需与账号绑定手机号一致（当前平台未接入短信网关，
    /// 账号手机号在注册 / 资料维护时已由用户填写，此处以「账号绑定手机号一致」作为验证口径；
    /// 接入短信网关后应改为验证码校验）。
    /// </para>
    /// </summary>
    public async Task<ResourceLibraryLearnerDto> RegisterAsync(RegisterOtherLearnerInput input)
    {
        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录。");

        if (input.LibraryId == Guid.Empty)
        {
            throw new UserFriendlyException("请选择要学习的教学资源库。");
        }

        if (string.IsNullOrWhiteSpace(input.Phone))
        {
            throw new UserFriendlyException("请填写手机号。");
        }

        if (string.IsNullOrWhiteSpace(input.RealName))
        {
            throw new UserFriendlyException("请填写真实姓名。");
        }

        var library = await _libraryRepository.FindWithMembersAsync(input.LibraryId);
        if (library == null)
        {
            throw new UserFriendlyException("教学资源库不存在。");
        }

        if (!library.OpenToExternalLearners)
        {
            throw new UserFriendlyException($"「{library.Name}」不对外开放学习，无法登记为其他学员。");
        }

        var user = await _userRepository.GetAsync(userId);
        var boundPhone = user.PhoneNumber;
        if (string.IsNullOrWhiteSpace(boundPhone))
        {
            throw new UserFriendlyException("账号尚未绑定手机号，请先在个人资料中完善手机号。");
        }

        if (!string.Equals(boundPhone.Trim(), input.Phone.Trim(), StringComparison.Ordinal))
        {
            throw new UserFriendlyException("手机号与账号绑定手机号不一致，请使用账号绑定手机号。");
        }

        var displayName = string.Join(" ", new[] { user.Name, user.Surname }
            .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = user.UserName ?? userId.ToString();
        }

        var (ownerTeacherId, ownerTeacherName) = await ResolveOwnerTeacherAsync(library);

        ResourceLibraryLearner learner;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            learner = await _learnerRepository.FirstOrDefaultAsync(x =>
                x.LibraryId == library.Id && x.UserId == userId);
        }

        if (learner == null)
        {
            learner = new ResourceLibraryLearner(
                GuidGenerator.Create(),
                library.Id,
                userId,
                displayName,
                library.OwnerTenantId,
                ownerTeacherId,
                ownerTeacherName,
                CurrentTenant.Id);
            learner.SetSource(ResourceLibraryLearnerSource.SelfLearner);
        }
        else if (string.IsNullOrWhiteSpace(learner.DisplayName))
        {
            learner.DisplayName = displayName;
        }

        learner.VerifyPhone(input.Phone.Trim(), DateTime.UtcNow);
        learner.VerifyRealName(input.RealName.Trim(), MaskIdCard(input.IdCardNo), DateTime.UtcNow);
        learner.SetOwnerTeacher(ownerTeacherId, ownerTeacherName);

        var isNew = learner.CreationTime == default;
        if (isNew)
        {
            await _learnerRepository.InsertAsync(learner, autoSave: true);
        }
        else
        {
            await _learnerRepository.UpdateAsync(learner, autoSave: true);
        }

        return await ToDtoAsync(learner);
    }

    public async Task<ResourceLibraryLearnerDto?> GetMyProfileAsync(Guid libraryId)
    {
        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录。");

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var learner = await _learnerRepository.FirstOrDefaultAsync(x =>
                x.LibraryId == libraryId && x.UserId == userId);
            return learner == null ? null : await ToDtoAsync(learner);
        }
    }

    /// <summary>
    /// 教师端「其他学员」目录：仅展示当前用户可见（主建 / 参与共建 / 被分配）的资源库学员。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageLearners)]
    public async Task<PagedResultDto<ResourceLibraryLearnerDto>> GetLearnersAsync(
        GetResourceLibraryLearnersInput input)
    {
        var currentTenantId = CurrentTenant.Id;
        if (!currentTenantId.HasValue)
        {
            throw new UserFriendlyException("当前不在租户上下文中。");
        }

        var libraries = await _accessService.GetVisibleLibrariesAsync(
            currentTenantId.Value,
            CurrentUser.Id,
            IsTenantAdmin());

        var libraryIds = libraries.Select(l => l.Id).ToList();
        if (input.LibraryId.HasValue)
        {
            libraryIds = libraryIds.Where(id => id == input.LibraryId.Value).ToList();
        }

        if (libraryIds.Count == 0)
        {
            return new PagedResultDto<ResourceLibraryLearnerDto>(0, new List<ResourceLibraryLearnerDto>());
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _learnerRepository.GetQueryableAsync();
            query = query.Where(x => libraryIds.Contains(x.LibraryId));

            if (input.Source.HasValue)
            {
                query = query.Where(x => x.Source == input.Source.Value);
            }

            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                var filter = input.Filter.Trim();
                query = query.Where(x => x.DisplayName.Contains(filter) ||
                                        (x.RealName != null && x.RealName.Contains(filter)) ||
                                        (x.Phone != null && x.Phone.Contains(filter)));
            }

            var total = await AsyncExecuter.CountAsync(query);
            var items = await AsyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.EnrolledAt)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount));

            var dtos = new List<ResourceLibraryLearnerDto>();
            foreach (var item in items)
            {
                dtos.Add(await ToDtoAsync(item));
            }

            return new PagedResultDto<ResourceLibraryLearnerDto>(total, dtos);
        }
    }

    /// <summary>导出「其他学员」数据（Excel）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageLearners)]
    public async Task<byte[]> ExportLearnersAsync(GetResourceLibraryLearnersInput input)
    {
        var paged = await GetLearnersAsync(new GetResourceLibraryLearnersInput
        {
            LibraryId = input.LibraryId,
            Source = input.Source,
            Filter = input.Filter,
            SkipCount = 0,
            MaxResultCount = 10000
        });

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("其他学员");

        var headers = new[] { "学员姓名", "实名姓名", "手机号", "所属资源库", "来源", "首次学习时间", "登记时间", "已发证书数" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var row = 2;
        foreach (var dto in paged.Items)
        {
            sheet.Cell(row, 1).Value = dto.DisplayName;
            sheet.Cell(row, 2).Value = dto.RealName ?? string.Empty;
            sheet.Cell(row, 3).Value = dto.Phone ?? string.Empty;
            sheet.Cell(row, 4).Value = dto.LibraryName ?? string.Empty;
            sheet.Cell(row, 5).Value = dto.Source == ResourceLibraryLearnerSource.SelfLearner ? "其他学员（自学）" : "班级导入";
            sheet.Cell(row, 6).Value = dto.FirstLearningAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
            sheet.Cell(row, 7).Value = dto.EnrolledAt.ToString("yyyy-MM-dd HH:mm");
            sheet.Cell(row, 8).Value = dto.CertificateCount;
            row++;
        }

        sheet.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ── 内部工具 ────────────────────────────────────────────

    private async Task<(Guid? TeacherId, string? TeacherName)> ResolveOwnerTeacherAsync(ResourceLibrary library)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var teacherIds = await _libraryRepository.GetTeacherIdsAsync(library.Id);
            if (teacherIds.Count == 0)
            {
                return (null, library.Principal);
            }

            var teacherId = teacherIds[0];
            var teacher = await _userRepository.FirstOrDefaultAsync(x => x.Id == teacherId);
            if (teacher == null)
            {
                return (teacherId, library.Principal);
            }

            var name = string.Join(" ", new[] { teacher.Name, teacher.Surname }
                .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

            return (teacherId, string.IsNullOrWhiteSpace(name) ? teacher.UserName : name);
        }
    }

    /// <summary>当前用户是否为本租户的院校管理员。</summary>
    private bool IsTenantAdmin()
    {
        var roles = CurrentUser.Roles;
        if (roles == null)
        {
            return false;
        }

        return roles.Contains("SchoolAdmin") || roles.Contains("admin");
    }

    private async Task<ResourceLibraryLearnerDto> ToDtoAsync(ResourceLibraryLearner learner)
    {
        string? libraryName = null;
        var library = await _libraryRepository.FindWithMembersAsync(learner.LibraryId);
        libraryName = library?.Name;

        var certificateCount = 0;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            certificateCount = await AsyncExecuter.CountAsync(
                (await _certificateRepository.GetQueryableAsync())
                    .Where(x => x.LearnerId == learner.Id));
        }

        return new ResourceLibraryLearnerDto
        {
            Id = learner.Id,
            LibraryId = learner.LibraryId,
            LibraryName = libraryName,
            UserId = learner.UserId,
            DisplayName = learner.DisplayName,
            Phone = learner.Phone,
            IsPhoneVerified = learner.IsPhoneVerified,
            RealName = learner.RealName,
            IdCardMasked = learner.IdCardMasked,
            IsRealNameVerified = learner.IsRealNameVerified,
            FirstLearningAt = learner.FirstLearningAt,
            EnrolledAt = learner.EnrolledAt,
            Status = learner.Status,
            Source = learner.Source,
            OwnerTeacherId = learner.OwnerTeacherId,
            OwnerTeacherName = learner.OwnerTeacherName,
            CertificateCount = certificateCount,
            CreationTime = learner.CreationTime
        };
    }

    /// <summary>身份证脱敏：保留前 4 位与后 2 位。</summary>
    private static string? MaskIdCard(string? idCard)
    {
        if (string.IsNullOrWhiteSpace(idCard))
        {
            return null;
        }

        var trimmed = idCard.Trim();
        if (trimmed.Length <= 6)
        {
            return new string('*', trimmed.Length);
        }

        return trimmed[..4] + new string('*', trimmed.Length - 6) + trimmed[^2..];
    }
}
