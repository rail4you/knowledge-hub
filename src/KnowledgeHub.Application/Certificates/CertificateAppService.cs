using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Certificates.Dtos;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Permissions;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.Schools;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Certificates;

/// <summary>
/// 结业证书 AppService 实现（PRD 第 9 节）。
/// </summary>
[Authorize(KnowledgeHubPermissions.Certificates.Default)]
public class CertificateAppService : KnowledgeHubAppService, ICertificateAppService
{
    private readonly IRepository<CertificateTemplate, Guid> _templateRepository;
    private readonly IRepository<CertificateApplication, Guid> _applicationRepository;
    private readonly IRepository<Certificate, Guid> _certificateRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<Exam, Guid> _examRepository;
    private readonly IRepository<StudentExam, Guid> _studentExamRepository;
    private readonly IRepository<ResourceLibraryLearner, Guid> _learnerRepository;
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IResourceLibraryLearningPolicy _learningPolicy;
    private readonly ISchoolRepository _schoolRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IDataFilter _dataFilter;

    public CertificateAppService(
        IRepository<CertificateTemplate, Guid> templateRepository,
        IRepository<CertificateApplication, Guid> applicationRepository,
        IRepository<Certificate, Guid> certificateRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<Exam, Guid> examRepository,
        IRepository<StudentExam, Guid> studentExamRepository,
        IRepository<ResourceLibraryLearner, Guid> learnerRepository,
        IResourceLibraryRepository libraryRepository,
        IResourceLibraryLearningPolicy learningPolicy,
        ISchoolRepository schoolRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IDataFilter dataFilter)
    {
        _templateRepository = templateRepository;
        _applicationRepository = applicationRepository;
        _certificateRepository = certificateRepository;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _examRepository = examRepository;
        _studentExamRepository = studentExamRepository;
        _learnerRepository = learnerRepository;
        _libraryRepository = libraryRepository;
        _learningPolicy = learningPolicy;
        _schoolRepository = schoolRepository;
        _userRepository = userRepository;
        _dataFilter = dataFilter;
    }

    // ── 模板管理 ────────────────────────────────────────────

    /// <summary>
    /// 模板列表。
    ///
    /// <para>
    /// 读取放宽到「有证书权限即可」：库所有者（租户管理员）发证时也要选模板；
    /// 模板的新增 / 修改 / 删除仍需 <c>Certificates.ManageTemplate</c>（平台侧统一配置）。
    /// </para>
    /// </summary>
    [Authorize(KnowledgeHubPermissions.Certificates.Default)]
    public async Task<List<CertificateTemplateDto>> GetTemplatesAsync()
    {
        var templates = await _templateRepository.GetListAsync();
        return templates
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.CreationTime)
            .Select(MapTemplate)
            .ToList();
    }

    [Authorize(KnowledgeHubPermissions.Certificates.ManageTemplate)]
    public async Task<CertificateTemplateDto> CreateTemplateAsync(CreateUpdateCertificateTemplateDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new UserFriendlyException("请填写模板名称。");
        }

        if (string.IsNullOrWhiteSpace(input.ImageUrl))
        {
            throw new UserFriendlyException("请先上传证书底图。");
        }

        var template = new CertificateTemplate(GuidGenerator.Create(), input.Name.Trim(), input.ImageUrl.Trim())
        {
            LayersJson = input.LayersJson,
            IsDefault = input.IsDefault ?? false,
            IsEnabled = input.IsEnabled ?? true,
            SortOrder = input.SortOrder ?? 0,
            Description = input.Description
        };

        await _templateRepository.InsertAsync(template, autoSave: true);

        if (template.IsDefault)
        {
            await ClearOtherDefaultsAsync(template.Id);
        }

        return MapTemplate(template);
    }

    [Authorize(KnowledgeHubPermissions.Certificates.ManageTemplate)]
    public async Task<CertificateTemplateDto> UpdateTemplateAsync(Guid id, CreateUpdateCertificateTemplateDto input)
    {
        var template = await _templateRepository.GetAsync(id);

        if (!string.IsNullOrWhiteSpace(input.Name))
        {
            template.SetName(input.Name.Trim());
        }

        if (!string.IsNullOrWhiteSpace(input.ImageUrl))
        {
            template.SetImageUrl(input.ImageUrl.Trim());
        }

        template.SetLayersJson(input.LayersJson);
        template.SetIsDefault(input.IsDefault ?? template.IsDefault);
        template.SetIsEnabled(input.IsEnabled ?? template.IsEnabled);
        template.SetSortOrder(input.SortOrder ?? template.SortOrder);
        template.SetDescription(input.Description);

        await _templateRepository.UpdateAsync(template, autoSave: true);

        if (template.IsDefault)
        {
            await ClearOtherDefaultsAsync(template.Id);
        }

        return MapTemplate(template);
    }

    [Authorize(KnowledgeHubPermissions.Certificates.ManageTemplate)]
    public async Task DeleteTemplateAsync(Guid id)
    {
        var template = await _templateRepository.GetAsync(id);
        await _templateRepository.DeleteAsync(template, autoSave: true);
    }

    // ── 审核发放 ────────────────────────────────────────────

    [Authorize(KnowledgeHubPermissions.Certificates.Review)]
    public async Task<PagedResultDto<CertificateApplicationDto>> GetApplicationsAsync(
        GetCertificateApplicationsInput input)
    {
        // 审核范围：
        //   · 平台管理员（host）→ 全部院校的申请；
        //   · 库所有者（资源库归属租户的租户管理员）→ 只看本租户名下资源库的申请。
        // 申请数据归属主建学校租户，host 上下文默认只看 TenantId IS NULL，
        // 因此必须显式关闭租户过滤器。
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _applicationRepository.GetQueryableAsync();

            var ownedLibraryIds = await GetOwnedLibraryIdsAsync();
            if (ownedLibraryIds != null)
            {
                query = query.Where(x => ownedLibraryIds.Contains(x.LibraryId));
            }

            query = query
                .WhereIf(input.Status.HasValue, x => x.Status == input.Status.Value)
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter),
                    x => x.CourseTitle.Contains(input.Filter!) || x.ApplicantName.Contains(input.Filter!));

            var total = await AsyncExecuter.CountAsync(query);
            var items = await AsyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.AppliedAt)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount));

            var dtos = items.Select(MapApplication).ToList();
            await FillCertificateAsync(dtos);

            return new PagedResultDto<CertificateApplicationDto>(total, dtos);
        }
    }

    [Authorize(KnowledgeHubPermissions.Certificates.Review)]
    public async Task<CertificateDto> ApproveAsync(ApproveCertificateInput input)
    {
        // 平台管理员跨院校发证：申请归属主建学校租户，需关闭租户过滤器。
        using (_dataFilter.Disable<IMultiTenant>())
        {
        var application = await _applicationRepository.GetAsync(input.ApplicationId);
        await EnsureCanReviewAsync(application);

        if (application.Status != CertificateApplicationStatus.Pending)
        {
            throw new UserFriendlyException("该申请已处理，请勿重复审核。");
        }

        var template = await ResolveTemplateAsync(input.TemplateId);
        var library = await FindLibraryAsync(application.LibraryId);

        var certificateNo = string.IsNullOrWhiteSpace(input.CertificateNo)
            ? await GenerateCertificateNoAsync()
            : input.CertificateNo!.Trim();

        var verifyCode = string.IsNullOrWhiteSpace(input.VerifyCode)
            ? await GenerateVerifyCodeAsync()
            : input.VerifyCode!.Trim();

        var certificate = new Certificate(
            GuidGenerator.Create(),
            template.Id,
            application.LibraryId,
            application.CourseId,
            application.StudentId,
            application.ApplicantName,
            application.CourseTitle,
            certificateNo,
            verifyCode,
            application.TenantId)
        {
            IssueDate = input.IssueDate ?? DateTime.Now,
            IssuedAt = DateTime.UtcNow,
            ValidUntil = input.ValidUntil
        };

        certificate.SetImageUrl(input.CertificateImageUrl);
        certificate.SetApplicationId(application.Id);
        certificate.SetSnapshots(application.SchoolName, application.LibraryName);

        // 归档：主建老师「其他学员」目录中的对应学员档案
        var (ownerTeacherId, ownerTeacherName) = await ResolveOwnerTeacherAsync(library);
        var learner = await FindLearnerAsync(application.LibraryId, application.StudentId);
        certificate.SetArchive(ownerTeacherId, learner?.Id);

        await _certificateRepository.InsertAsync(certificate, autoSave: true);

        application.Approve(
            CurrentUser.Id ?? Guid.Empty,
            await ResolveCurrentUserNameAsync(),
            input.ReviewRemark,
            certificate.Id,
            DateTime.UtcNow);
        await _applicationRepository.UpdateAsync(application, autoSave: true);

        return MapCertificate(certificate);
        }
    }

    /// <summary>
    /// 发证前准备：生成证书编号与防伪码。
    /// 前端用这两个值合成证书图片（含防伪二维码），再调用 <see cref="ApproveAsync"/> 发放。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.Certificates.Review)]
    public async Task<CertificatePrepareDto> PrepareAsync(Guid applicationId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var application = await _applicationRepository.GetAsync(applicationId);
            await EnsureCanReviewAsync(application);

            if (application.Status != CertificateApplicationStatus.Pending)
            {
                throw new UserFriendlyException("该申请已处理，请勿重复审核。");
            }

            return new CertificatePrepareDto
            {
                ApplicationId = application.Id,
                CertificateNo = await GenerateCertificateNoAsync(),
                VerifyCode = await GenerateVerifyCodeAsync()
            };
        }
    }

    [Authorize(KnowledgeHubPermissions.Certificates.Review)]
    public async Task RejectAsync(RejectCertificateInput input)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var application = await _applicationRepository.GetAsync(input.ApplicationId);
            await EnsureCanReviewAsync(application);

            if (application.Status != CertificateApplicationStatus.Pending)
            {
                throw new UserFriendlyException("该申请已处理，请勿重复审核。");
            }

            application.Reject(
                CurrentUser.Id ?? Guid.Empty,
                await ResolveCurrentUserNameAsync(),
                input.ReviewRemark,
                DateTime.UtcNow);

            await _applicationRepository.UpdateAsync(application, autoSave: true);
        }
    }

    // ── 学员 ────────────────────────────────────────────────

    [Authorize]
    public async Task<CertificateApplyEligibilityDto> GetApplyEligibilityAsync(Guid courseId)
    {
        var studentId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var course = await FindCourseAsync(courseId);
        var library = await FindCourseLibraryAsync(course);

        var dto = new CertificateApplyEligibilityDto
        {
            CourseId = course.Id,
            CourseTitle = course.Title,
            LibraryId = library?.Id,
            LibraryName = library?.Name
        };

        if (library == null)
        {
            dto.Reason = "该课程未挂载到教学资源库，暂不支持申请结业证书。";
            return dto;
        }

        if (!library.OpenToExternalLearners)
        {
            dto.Reason = $"「{library.Name}」不对外开放结业证书。";
            return dto;
        }

        var enrollment = await FindEnrollmentAsync(studentId, courseId);
        dto.IsLearningCompleted = enrollment != null && enrollment.Status == StudentCourseStatus.Completed;
        dto.IsAssessmentPassed = await IsAssessmentPassedAsync(studentId, courseId);

        // 实名认证口径与学习闸门一致：个人账户完成实名认证即可（账号级）
        dto.IsRealNameVerified = await _learningPolicy.IsLearnerVerifiedAsync(studentId);

        // 申请归属主建学校租户，学员可能属于其它租户 → 需关闭租户过滤器
        List<CertificateApplication> applications;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            applications = await _applicationRepository.GetListAsync(x =>
                x.CourseId == courseId && x.StudentId == studentId);
        }

        var pending = applications.FirstOrDefault(x =>
            x.Status == CertificateApplicationStatus.Pending || x.Status == CertificateApplicationStatus.Approved);
        dto.HasApplied = pending != null;

        if (dto.HasApplied)
        {
            dto.Reason = pending!.Status == CertificateApplicationStatus.Pending
                ? "申请已提交，等待平台管理员审核。"
                : "该课程已发放结业证书。";
            return dto;
        }

        if (!dto.IsLearningCompleted)
        {
            dto.Reason = "尚未完成课程学习，无法申请结业证书。";
            return dto;
        }

        if (!dto.IsAssessmentPassed)
        {
            dto.Reason = "尚未通过课程考核，无法申请结业证书。";
            return dto;
        }

        if (!dto.IsRealNameVerified)
        {
            dto.Reason = "请先完成手机号验证与实名认证后再申请结业证书。";
            return dto;
        }

        dto.CanApply = true;
        return dto;
    }

    [Authorize]
    public async Task<CertificateApplicationDto> ApplyAsync(ApplyCertificateInput input)
    {
        var studentId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var course = await FindCourseAsync(input.CourseId);
        var library = await FindCourseLibraryAsync(course);

        if (library == null)
        {
            throw new UserFriendlyException("该课程未挂载到教学资源库，暂不支持申请结业证书。");
        }

        if (!library.OpenToExternalLearners)
        {
            throw new UserFriendlyException($"「{library.Name}」不对外开放结业证书。");
        }

        var enrollment = await FindEnrollmentAsync(studentId, course.Id);
        if (enrollment == null || enrollment.Status != StudentCourseStatus.Completed)
        {
            throw new UserFriendlyException("尚未完成课程学习，无法申请结业证书。");
        }

        if (!await IsAssessmentPassedAsync(studentId, course.Id))
        {
            throw new UserFriendlyException("尚未通过课程考核，无法申请结业证书。");
        }

        // 实名认证（账号级，与学习闸门同一口径）
        if (!await _learningPolicy.IsLearnerVerifiedAsync(studentId))
        {
            throw new UserFriendlyException("请先在个人账户完成手机号验证与实名认证后再申请结业证书。");
        }

        // 「其他学员」档案（PRD 5.3 / 9）：归档到主建老师的「其他学员」目录
        var learner = await FindLearnerAsync(library.Id, studentId)
                      ?? await CreateLearnerRecordAsync(library, studentId);

        List<CertificateApplication> existing;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            existing = await _applicationRepository.GetListAsync(x =>
                x.CourseId == course.Id && x.StudentId == studentId);
        }

        if (existing.Any(x => x.Status == CertificateApplicationStatus.Pending))
        {
            throw new UserFriendlyException("申请已提交，请等待平台管理员审核。");
        }

        if (existing.Any(x => x.Status == CertificateApplicationStatus.Approved))
        {
            throw new UserFriendlyException("该课程已发放结业证书。");
        }

        var application = new CertificateApplication(
            GuidGenerator.Create(),
            library.Id,
            course.Id,
            studentId,
            learner.RealName ?? learner.DisplayName,
            course.Title,
            library.OwnerTenantId)
        {
            LibraryName = library.Name,
            SchoolName = await ResolveSchoolNameAsync(library.OwnerTenantId),
            PhoneMasked = MaskPhone(learner.Phone),
            IsRealNameVerified = learner.IsRealNameVerified,
            IsLearningCompleted = true,
            IsAssessmentPassed = true,
            LearnerId = learner.Id
        };

        var (ownerTeacherId, ownerTeacherName) = await ResolveOwnerTeacherAsync(library);
        application.OwnerTeacherId = ownerTeacherId;
        learner.SetOwnerTeacher(ownerTeacherId, ownerTeacherName);
        await _learnerRepository.UpdateAsync(learner);

        await _applicationRepository.InsertAsync(application, autoSave: true);

        return MapApplication(application);
    }

    [Authorize]
    public async Task<List<CertificateApplicationDto>> GetMyApplicationsAsync()
    {
        var studentId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录。");

        // 申请归属主建学校租户，学员可能属于其它租户 → 需关闭租户过滤器
        List<CertificateApplication> items;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            items = await _applicationRepository.GetListAsync(x => x.StudentId == studentId);
        }

        var dtos = items
            .OrderByDescending(x => x.AppliedAt)
            .Select(MapApplication)
            .ToList();

        await FillCertificateAsync(dtos);
        return dtos;
    }

    [Authorize]
    public async Task<List<CertificateDto>> GetMyCertificatesAsync()
    {
        var studentId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录。");

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var items = await _certificateRepository.GetListAsync(x => x.StudentId == studentId);
            return items
                .OrderByDescending(x => x.IssuedAt)
                .Select(MapCertificate)
                .ToList();
        }
    }

    // ── 公开防伪核验 ─────────────────────────────────────────

    [AllowAnonymous]
    public async Task<CertificateVerifyDto> VerifyAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new CertificateVerifyDto { IsValid = false, Message = "请输入证书编号或防伪码。" };
        }

        var trimmed = code.Trim();

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var certificate = await _certificateRepository.FirstOrDefaultAsync(
                x => x.CertificateNo == trimmed || x.VerifyCode == trimmed);

            if (certificate == null)
            {
                return new CertificateVerifyDto { IsValid = false, Message = "未查询到对应的证书。" };
            }

            if (certificate.Status == CertificateStatus.Revoked)
            {
                return new CertificateVerifyDto
                {
                    IsValid = false,
                    Message = "该证书已被撤销。",
                    Certificate = MapCertificate(certificate)
                };
            }

            return new CertificateVerifyDto
            {
                IsValid = true,
                Message = "证书真实有效。",
                Certificate = MapCertificate(certificate)
            };
        }
    }

    // ── 内部工具 ────────────────────────────────────────────

    /// <summary>
    /// 取「当前上下文可审核的资源库 Id 集合」：
    /// host 平台管理员 → null（不限）；租户上下文 → 本租户名下（主建）的资源库。
    /// </summary>
    private async Task<List<Guid>?> GetOwnedLibraryIdsAsync()
    {
        var tenantId = CurrentTenant.Id;
        if (!tenantId.HasValue)
        {
            return null; // 平台管理员：全校
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var libraryQuery = await _libraryRepository.GetQueryableAsync();
            return await AsyncExecuter.ToListAsync(
                libraryQuery.Where(l => l.OwnerTenantId == tenantId.Value).Select(l => l.Id));
        }
    }

    /// <summary>
    /// 审核权限校验：平台管理员可审核全部；库所有者（主建租户管理员）只能审核本租户名下资源库的申请。
    /// </summary>
    private async Task EnsureCanReviewAsync(CertificateApplication application)
    {
        var tenantId = CurrentTenant.Id;
        if (!tenantId.HasValue)
        {
            return; // 平台管理员
        }

        var library = await FindLibraryAsync(application.LibraryId);
        if (library == null || library.OwnerTenantId != tenantId.Value)
        {
            throw new UserFriendlyException("只能审核本机构主建的资源库所发起的证书申请。");
        }
    }

    private async Task<CertificateTemplate> ResolveTemplateAsync(Guid? templateId)
    {
        if (templateId.HasValue && templateId.Value != Guid.Empty)
        {
            var template = await _templateRepository.GetAsync(templateId.Value);
            if (!template.IsEnabled)
            {
                throw new UserFriendlyException("所选证书模板已停用。");
            }

            return template;
        }

        var templates = await _templateRepository.GetListAsync(x => x.IsEnabled);
        var target = templates.FirstOrDefault(x => x.IsDefault) ?? templates.OrderBy(x => x.SortOrder).FirstOrDefault();
        if (target == null)
        {
            throw new UserFriendlyException("平台尚未配置结业证书模板，请先在后台配置。");
        }

        return target;
    }

    private async Task ClearOtherDefaultsAsync(Guid keepTemplateId)
    {
        var templates = await _templateRepository.GetListAsync(x => x.IsDefault && x.Id != keepTemplateId);
        foreach (var other in templates)
        {
            other.SetIsDefault(false);
            await _templateRepository.UpdateAsync(other);
        }
    }

    private async Task<string> GenerateCertificateNoAsync()
    {
        var prefix = $"KH{DateTime.Now:yyyyMMdd}";
        for (var i = 0; i < 10; i++)
        {
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var candidate = $"{prefix}-{suffix}";

            using (_dataFilter.Disable<IMultiTenant>())
            {
                var exists = await _certificateRepository.AnyAsync(x => x.CertificateNo == candidate);
                if (!exists)
                {
                    return candidate;
                }
            }
        }

        return $"KH{DateTime.Now:yyyyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
    }

    private async Task<string> GenerateVerifyCodeAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            for (var i = 0; i < 10; i++)
            {
                var candidate = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
                var exists = await _certificateRepository.AnyAsync(x => x.VerifyCode == candidate);
                if (!exists)
                {
                    return candidate;
                }
            }
        }

        return Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
    }

    private async Task<Course> FindCourseAsync(Guid courseId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var course = await _courseRepository.FirstOrDefaultAsync(x => x.Id == courseId);
            if (course == null)
            {
                throw new UserFriendlyException("课程不存在。");
            }

            return course;
        }
    }

    private async Task<ResourceLibrary?> FindCourseLibraryAsync(Course course)
    {
        if (!course.LibraryId.HasValue || course.LibraryId.Value == Guid.Empty)
        {
            return null;
        }

        return await _libraryRepository.FindWithMembersAsync(course.LibraryId.Value);
    }

    private async Task<ResourceLibrary?> FindLibraryAsync(Guid libraryId)
    {
        if (libraryId == Guid.Empty)
        {
            return null;
        }

        return await _libraryRepository.FindWithMembersAsync(libraryId);
    }

    private async Task<StudentCourse?> FindEnrollmentAsync(Guid studentId, Guid courseId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _studentCourseRepository.FirstOrDefaultAsync(x =>
                x.StudentId == studentId && x.CourseId == courseId);
        }
    }

    /// <summary>
    /// 是否通过考核：课程配置了考试时要求存在通过的考试记录；未配置考试视为通过。
    /// </summary>
    private async Task<bool> IsAssessmentPassedAsync(Guid studentId, Guid courseId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var examIds = await AsyncExecuter.ToListAsync(
                (await _examRepository.GetQueryableAsync())
                    .Where(x => x.CourseId == courseId)
                    .Select(x => x.Id));

            if (examIds.Count == 0)
            {
                return true;
            }

            return await AsyncExecuter.AnyAsync(
                (await _studentExamRepository.GetQueryableAsync())
                    .Where(x => x.StudentId == studentId &&
                                examIds.Contains(x.ExamId) &&
                                x.IsPassed == true));
        }
    }

    /// <summary>
    /// 为「其他学员」建档（PRD 5.3）：校外学员首次申请证书时，若尚无档案则自动创建，
    /// 证书发放记录随后归档到主建老师该学员的档案下。
    /// </summary>
    private async Task<ResourceLibraryLearner> CreateLearnerRecordAsync(ResourceLibrary library, Guid studentId)
    {
        var user = await _userRepository.FirstOrDefaultAsync(x => x.Id == studentId);
        var realName = user?.GetProperty<string>("RealName");
        var displayName = !string.IsNullOrWhiteSpace(realName)
            ? realName!
            : (string.Join(" ", new[] { user?.Name, user?.Surname }
                .Where(x => !string.IsNullOrWhiteSpace(x))).Trim() is { Length: > 0 } d ? d : studentId.ToString());

        var record = new ResourceLibraryLearner(
            GuidGenerator.Create(),
            library.Id,
            studentId,
            displayName,
            library.OwnerTenantId,
            null,
            library.Principal,
            CurrentTenant.Id);

        record.SetSource(ResourceLibraryLearnerSource.SelfLearner);
        record.MarkFirstLearning(DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(user?.PhoneNumber))
        {
            record.VerifyPhone(user!.PhoneNumber!, DateTime.UtcNow);
        }

        if (!string.IsNullOrWhiteSpace(realName))
        {
            record.VerifyRealName(realName!, null, DateTime.UtcNow);
        }

        await _learnerRepository.InsertAsync(record, autoSave: true);
        return record;
    }

    private async Task<ResourceLibraryLearner?> FindLearnerAsync(Guid libraryId, Guid userId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _learnerRepository.FirstOrDefaultAsync(x => x.LibraryId == libraryId && x.UserId == userId);
        }
    }

    /// <summary>解析主建老师（库的创建者 → 首个被分配教师）。</summary>
    private async Task<(Guid? TeacherId, string? TeacherName)> ResolveOwnerTeacherAsync(ResourceLibrary? library)
    {
        if (library == null)
        {
            return (null, null);
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var teacherIds = await _libraryRepository.GetTeacherIdsAsync(library.Id);
            if (teacherIds.Count == 0)
            {
                return (null, library.Principal);
            }

            var teacherId = teacherIds[0];
            var name = await ResolveUserNameAsync(teacherId);
            return (teacherId, name ?? library.Principal);
        }
    }

    private async Task<string?> ResolveSchoolNameAsync(Guid tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var school = await _schoolRepository.FindByTenantIdAsync(tenantId);
            return school?.Name;
        }
    }

    private async Task<string?> ResolveCurrentUserNameAsync()
    {
        var userId = CurrentUser.Id;
        if (!userId.HasValue)
        {
            return null;
        }

        return await ResolveUserNameAsync(userId.Value);
    }

    private async Task<string?> ResolveUserNameAsync(Guid userId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null)
            {
                return null;
            }

            var display = string.Join(" ", new[] { user.Name, user.Surname }
                .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();

            return string.IsNullOrWhiteSpace(display) ? user.UserName : display;
        }
    }

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7)
        {
            return phone;
        }

        return phone[..3] + "****" + phone[^4..];
    }

    private static CertificateTemplateDto MapTemplate(CertificateTemplate template)
    {
        return new CertificateTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            ImageUrl = template.ImageUrl,
            LayersJson = template.LayersJson,
            IsDefault = template.IsDefault,
            IsEnabled = template.IsEnabled,
            SortOrder = template.SortOrder,
            Description = template.Description,
            CreationTime = template.CreationTime
        };
    }

    private static CertificateApplicationDto MapApplication(CertificateApplication application)
    {
        return new CertificateApplicationDto
        {
            Id = application.Id,
            LibraryId = application.LibraryId,
            LibraryName = application.LibraryName,
            CourseId = application.CourseId,
            CourseTitle = application.CourseTitle,
            StudentId = application.StudentId,
            ApplicantName = application.ApplicantName,
            PhoneMasked = application.PhoneMasked,
            SchoolName = application.SchoolName,
            IsLearningCompleted = application.IsLearningCompleted,
            IsAssessmentPassed = application.IsAssessmentPassed,
            IsRealNameVerified = application.IsRealNameVerified,
            Status = application.Status,
            AppliedAt = application.AppliedAt,
            ReviewedAt = application.ReviewedAt,
            ReviewerName = application.ReviewerName,
            ReviewRemark = application.ReviewRemark,
            CertificateId = application.CertificateId,
            CreationTime = application.CreationTime
        };
    }

    private static CertificateDto MapCertificate(Certificate certificate)
    {
        return new CertificateDto
        {
            Id = certificate.Id,
            TemplateId = certificate.TemplateId,
            ApplicationId = certificate.ApplicationId,
            LibraryId = certificate.LibraryId,
            LibraryName = certificate.LibraryName,
            CourseId = certificate.CourseId,
            CourseTitle = certificate.CourseTitle,
            StudentId = certificate.StudentId,
            StudentName = certificate.StudentName,
            SchoolName = certificate.SchoolName,
            CertificateNo = certificate.CertificateNo,
            VerifyCode = certificate.VerifyCode,
            ImageUrl = certificate.ImageUrl,
            IssueDate = certificate.IssueDate,
            IssuedAt = certificate.IssuedAt,
            ValidUntil = certificate.ValidUntil,
            Status = certificate.Status,
            IsArchived = certificate.LearnerId.HasValue,
            CreationTime = certificate.CreationTime
        };
    }

    private async Task FillCertificateAsync(List<CertificateApplicationDto> dtos)
    {
        var certificateIds = dtos.Where(d => d.CertificateId.HasValue).Select(d => d.CertificateId!.Value).ToList();
        if (certificateIds.Count == 0)
        {
            return;
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var certificates = await AsyncExecuter.ToListAsync(
                (await _certificateRepository.GetQueryableAsync()).Where(x => certificateIds.Contains(x.Id)));

            foreach (var dto in dtos)
            {
                if (dto.CertificateId.HasValue)
                {
                    var certificate = certificates.FirstOrDefault(c => c.Id == dto.CertificateId.Value);
                    if (certificate != null)
                    {
                        dto.Certificate = MapCertificate(certificate);
                    }
                }
            }
        }
    }
}
