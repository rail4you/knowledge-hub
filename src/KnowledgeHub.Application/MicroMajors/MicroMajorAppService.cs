using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.MicroMajors.Dtos;
using KnowledgeHub.MicroMajors.Enums;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.MicroMajors;

[IgnoreAntiforgeryToken]
public class MicroMajorAppService : KnowledgeHubAppService, IMicroMajorAppService
{
    private readonly IRepository<MicroMajor, Guid> _microMajorRepository;
    private readonly IRepository<MicroMajorCourse, Guid> _microMajorCourseRepository;
    private readonly IRepository<MicroMajorEnrollment, Guid> _microMajorEnrollmentRepository;
    private readonly IRepository<MicroMajorCertificate, Guid> _microMajorCertificateRepository;
    private readonly IRepository<MicroMajorCertificateTemplate, Guid> _microMajorCertificateTemplateRepository;
    private readonly IRepository<MicroMajorResource, Guid> _microMajorResourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<Resources.Resource, Guid> _resourceRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;

    public MicroMajorAppService(
        IRepository<MicroMajor, Guid> microMajorRepository,
        IRepository<MicroMajorCourse, Guid> microMajorCourseRepository,
        IRepository<MicroMajorEnrollment, Guid> microMajorEnrollmentRepository,
        IRepository<MicroMajorCertificate, Guid> microMajorCertificateRepository,
        IRepository<MicroMajorCertificateTemplate, Guid> microMajorCertificateTemplateRepository,
        IRepository<MicroMajorResource, Guid> microMajorResourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<Resources.Resource, Guid> resourceRepository,
        IdentityUserManager userManager,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant)
    {
        _microMajorRepository = microMajorRepository;
        _microMajorCourseRepository = microMajorCourseRepository;
        _microMajorEnrollmentRepository = microMajorEnrollmentRepository;
        _microMajorCertificateRepository = microMajorCertificateRepository;
        _microMajorCertificateTemplateRepository = microMajorCertificateTemplateRepository;
        _microMajorResourceRepository = microMajorResourceRepository;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _userRepository = userRepository;
        _resourceRepository = resourceRepository;
        _userManager = userManager;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
    }

    public async Task<MicroMajorDto> GetAsync(Guid id)
    {
        var entity = await _microMajorRepository.GetAsync(id);
        return await MapToDtoAsync(entity);
    }

    public async Task<MicroMajorDetailDto> GetDetailAsync(Guid id)
    {
        var entity = await _microMajorRepository.GetAsync(id);
        var dto = new MicroMajorDetailDto();
        CopyDto(await MapToDtoAsync(entity), dto);
        dto.Courses = await GetCourseDtosAsync(id);
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.Default)]
    public async Task<PagedResultDto<MicroMajorDto>> GetListAsync(PagedMicroMajorRequestDto input)
    {
        var query = await _microMajorRepository.GetQueryableAsync();
        query = query
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x =>
                x.Title.Contains(input.Filter!) ||
                (x.Summary != null && x.Summary.Contains(input.Filter!)) ||
                (x.IndustryField != null && x.IndustryField.Contains(input.Filter!)))
            .WhereIf(input.Status.HasValue, x => x.Status == input.Status.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<MicroMajorDto>(totalCount, await MapToDtosAsync(items));
    }

    public async Task<PagedResultDto<MicroMajorDto>> GetPublishedAsync(PagedMicroMajorRequestDto input)
    {
        var query = await _microMajorRepository.GetQueryableAsync();
        query = query
            .Where(x => x.Status == MicroMajorStatus.Published)
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x =>
                x.Title.Contains(input.Filter!) ||
                (x.Summary != null && x.Summary.Contains(input.Filter!)) ||
                (x.IndustryField != null && x.IndustryField.Contains(input.Filter!)));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<MicroMajorDto>(totalCount, await MapToDtosAsync(items));
    }

    public async Task<List<MicroMajorEnrollmentDto>> GetMyEnrollmentsAsync()
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var items = await _microMajorEnrollmentRepository.GetListAsync(x => x.StudentId == studentId);
        foreach (var item in items)
        {
            await RefreshEnrollmentProgressAsync(item);
        }

        return await MapEnrollmentDtosAsync(items.OrderByDescending(x => x.EnrolledAt).ToList());
    }

    public async Task<List<MyMicroMajorDto>> GetMyMicroMajorsAsync()
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var enrollments = await _microMajorEnrollmentRepository.GetListAsync(x => x.StudentId == studentId);

        foreach (var item in enrollments)
        {
            await RefreshEnrollmentProgressAsync(item);
        }

        var ordered = enrollments.OrderByDescending(x => x.EnrolledAt).ToList();
        if (ordered.Count == 0)
        {
            return new List<MyMicroMajorDto>();
        }

        var microMajorIds = ordered.Select(x => x.MicroMajorId).Distinct().ToList();
        var microMajors = await _microMajorRepository.GetListAsync(x => microMajorIds.Contains(x.Id));
        var baseDtoMap = (await MapToDtosAsync(microMajors)).ToDictionary(x => x.Id);

        var certificates = await _microMajorCertificateRepository.GetListAsync(x => x.StudentId == studentId);
        var certMap = certificates.ToDictionary(x => x.EnrollmentId);

        var result = new List<MyMicroMajorDto>();
        foreach (var enrollment in ordered)
        {
            var dto = new MyMicroMajorDto
            {
                EnrollmentId = enrollment.Id,
                EnrollmentStatus = enrollment.Status,
                Progress = enrollment.Progress,
                EnrolledAt = enrollment.EnrolledAt,
                CompletedAt = enrollment.CompletedAt,
                CertificateIssuedAt = enrollment.CertificateIssuedAt,
                CertificateImageUrl = certMap.TryGetValue(enrollment.Id, out var c) ? c.CertificateImageUrl : null,
                Courses = await GetCourseDtosAsync(enrollment.MicroMajorId)
            };

            if (baseDtoMap.TryGetValue(enrollment.MicroMajorId, out var baseDto))
            {
                CopyDto(baseDto, dto);
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task<List<MicroMajorCertificateDto>> GetMyCertificatesAsync()
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var items = await _microMajorCertificateRepository.GetListAsync(x => x.StudentId == studentId);
        return await MapCertificateDtosAsync(items.OrderByDescending(x => x.IssuedAt).ToList());
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.ManageEnrollment)]
    public async Task<PagedResultDto<MicroMajorEnrollmentDto>> GetEnrollmentListAsync(GetMicroMajorEnrollmentsInput input)
    {
        var query = await _microMajorEnrollmentRepository.GetQueryableAsync();
        query = query
            .WhereIf(input.MicroMajorId.HasValue, x => x.MicroMajorId == input.MicroMajorId.Value)
            .WhereIf(input.StudentId.HasValue, x => x.StudentId == input.StudentId.Value)
            .WhereIf(input.Status.HasValue, x => x.Status == input.Status.Value);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim();

            var mmQueryable = await _microMajorRepository.GetQueryableAsync();
            var matchingMmIds = await mmQueryable
                .Where(x => x.Title.Contains(filter))
                .Select(x => x.Id)
                .ToListAsync();

            List<Guid> matchingUserIds;
            using (DataFilter.Disable<IMultiTenant>())
            {
                var userQueryable = await _userRepository.GetQueryableAsync();
                matchingUserIds = await userQueryable
                    .Where(x => x.UserName.Contains(filter) ||
                                (x.Name != null && x.Name.Contains(filter)) ||
                                (x.Surname != null && x.Surname.Contains(filter)))
                    .Select(x => x.Id)
                    .ToListAsync();
            }

            query = query.Where(x =>
                matchingMmIds.Contains(x.MicroMajorId) ||
                matchingUserIds.Contains(x.StudentId));
        }

        var items = await query
            .OrderByDescending(x => x.EnrolledAt)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        foreach (var item in items)
        {
            await RefreshEnrollmentProgressAsync(item);
        }

        var totalCount = await query.CountAsync();
        return new PagedResultDto<MicroMajorEnrollmentDto>(totalCount, await MapEnrollmentDtosAsync(items));
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.Create)]
    public async Task<MicroMajorDto> CreateAsync(CreateUpdateMicroMajorDto input)
    {
        await EnsureCoursesValidAsync(input.Courses);

        var entity = new MicroMajor(GuidGenerator.Create(), input.Title.Trim())
        {
            TenantId = CurrentTenant.Id,
            Summary = input.Summary?.Trim(),
            Description = input.Description?.Trim(),
            CoverImageUrl = input.CoverImageUrl?.Trim(),
            IndustryField = input.IndustryField?.Trim(),
            CollaborationUnit = input.CollaborationUnit?.Trim(),
            Status = input.Status,
            RequiredCompletionRate = Math.Clamp(input.RequiredCompletionRate, 1, 100),
            IsCertificateEnabled = input.IsCertificateEnabled
        };

        await _microMajorRepository.InsertAsync(entity, autoSave: true);
        await ReplaceCoursesAsync(entity.Id, input.Courses);
        return await MapToDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.Edit)]
    public async Task<MicroMajorDto> UpdateAsync(Guid id, CreateUpdateMicroMajorDto input)
    {
        await EnsureCoursesValidAsync(input.Courses);

        var entity = await _microMajorRepository.GetAsync(id);
        entity.Title = input.Title.Trim();
        entity.Summary = input.Summary?.Trim();
        entity.Description = input.Description?.Trim();
        entity.CoverImageUrl = input.CoverImageUrl?.Trim();
        entity.IndustryField = input.IndustryField?.Trim();
        entity.CollaborationUnit = input.CollaborationUnit?.Trim();
        entity.Status = input.Status;
        entity.RequiredCompletionRate = Math.Clamp(input.RequiredCompletionRate, 1, 100);
        entity.IsCertificateEnabled = input.IsCertificateEnabled;

        await _microMajorRepository.UpdateAsync(entity, autoSave: true);
        await ReplaceCoursesAsync(entity.Id, input.Courses);
        return await MapToDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var hasEnrollment = await _microMajorEnrollmentRepository.AnyAsync(x => x.MicroMajorId == id);
        if (hasEnrollment)
        {
            throw new UserFriendlyException("已有学生报名，暂不能删除。");
        }

        var links = await _microMajorCourseRepository.GetListAsync(x => x.MicroMajorId == id);
        foreach (var link in links)
        {
            await _microMajorCourseRepository.DeleteAsync(link);
        }

        // 删除该微专业的证书模板
        var templates = await _microMajorCertificateTemplateRepository.GetListAsync(x => x.MicroMajorId == id);
        foreach (var template in templates)
        {
            await _microMajorCertificateTemplateRepository.DeleteAsync(template);
        }

        await _microMajorRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.Default)]
    public async Task EnrollAsync(Guid microMajorId)
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");

        // 仅学生角色可以报名微专业（教师/管理员等不可报名）
        var user = await _userRepository.GetAsync(studentId);
        if (!await _userManager.IsInRoleAsync(user, "Student"))
        {
            throw new UserFriendlyException("仅学生用户可报名微专业。");
        }

        var entity = await _microMajorRepository.GetAsync(microMajorId);
        if (entity.Status != MicroMajorStatus.Published)
        {
            throw new UserFriendlyException("当前微专业未发布。");
        }

        var existing = await _microMajorEnrollmentRepository.FirstOrDefaultAsync(
            x => x.MicroMajorId == microMajorId && x.StudentId == studentId);
        if (existing != null && existing.Status != MicroMajorEnrollmentStatus.Cancelled)
        {
            throw new UserFriendlyException("您已报名该微专业，请等待审核。");
        }

        if (existing != null)
        {
            existing.Status = MicroMajorEnrollmentStatus.Pending;
            existing.Progress = 0;
            existing.EnrolledAt = DateTime.UtcNow;
            existing.CompletedAt = null;
            existing.CertificateIssuedAt = null;
            await _microMajorEnrollmentRepository.UpdateAsync(existing, autoSave: true);
        }
        else
        {
            var enrollment = new MicroMajorEnrollment(GuidGenerator.Create(), microMajorId, studentId)
            {
                TenantId = CurrentTenant.Id
            };
            await _microMajorEnrollmentRepository.InsertAsync(enrollment, autoSave: true);
        }
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.ManageEnrollment)]
    public async Task ApproveEnrollmentAsync(Guid enrollmentId)
    {
        var enrollment = await _microMajorEnrollmentRepository.GetAsync(enrollmentId);
        if (enrollment.Status != MicroMajorEnrollmentStatus.Pending)
        {
            throw new UserFriendlyException("该报名记录不在待审批状态。");
        }

        enrollment.Status = MicroMajorEnrollmentStatus.Enrolled;
        enrollment.EnrolledAt = DateTime.UtcNow;
        await _microMajorEnrollmentRepository.UpdateAsync(enrollment, autoSave: true);
        await EnsureStudentCoursesAsync(enrollment.StudentId, enrollment.MicroMajorId);
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.ManageEnrollment)]
    public async Task RejectEnrollmentAsync(Guid enrollmentId)
    {
        var enrollment = await _microMajorEnrollmentRepository.GetAsync(enrollmentId);
        if (enrollment.Status != MicroMajorEnrollmentStatus.Pending)
        {
            throw new UserFriendlyException("该报名记录不在待审批状态。");
        }

        enrollment.Status = MicroMajorEnrollmentStatus.Cancelled;
        await _microMajorEnrollmentRepository.UpdateAsync(enrollment, autoSave: true);
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.ManageEnrollment)]
    public async Task MarkAsCompletedAsync(Guid enrollmentId)
    {
        var enrollment = await _microMajorEnrollmentRepository.GetAsync(enrollmentId);
        if (enrollment.Status != MicroMajorEnrollmentStatus.Enrolled &&
            enrollment.Status != MicroMajorEnrollmentStatus.InProgress)
        {
            throw new UserFriendlyException("仅学习中的报名可以结业。");
        }

        enrollment.Status = MicroMajorEnrollmentStatus.Completed;
        enrollment.CompletedAt = Clock.Now;
        await _microMajorEnrollmentRepository.UpdateAsync(enrollment, autoSave: true);
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.IssueCertificate)]
    public async Task<IssueCertificateDefaultsDto> GetIssueCertificateDefaultsAsync(Guid enrollmentId)
    {
        var enrollment = await _microMajorEnrollmentRepository.GetAsync(enrollmentId);

        string? studentName = null;
        string? studentNo = null;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(enrollment.StudentId);
            if (user != null)
            {
                studentName = string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name;
                studentNo = user.GetProperty<string>("StudentNumber");
            }
        }

        var microMajor = await _microMajorRepository.GetAsync(enrollment.MicroMajorId);

        return new IssueCertificateDefaultsDto
        {
            EnrollmentId = enrollment.Id,
            MicroMajorId = enrollment.MicroMajorId,
            MicroMajorTitle = microMajor.Title,
            StudentName = studentName,
            StudentNo = string.IsNullOrWhiteSpace(studentNo) ? null : studentNo.Trim(),
            SuggestedCertificateNo = await GetNextCertificateNoAsync(enrollment.MicroMajorId),
            IssueDate = Clock.Now.Date
        };
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.IssueCertificate)]
    [HttpPost]
    public async Task<MicroMajorCertificateDto> IssueCertificateAsync(IssueCertificateInputDto input)
    {
        var enrollment = await _microMajorEnrollmentRepository.GetAsync(input.EnrollmentId);
        var microMajor = await _microMajorRepository.GetAsync(enrollment.MicroMajorId);

        if (!microMajor.IsCertificateEnabled)
        {
            throw new UserFriendlyException("当前微专业未启用证书。");
        }

        // 先检查是否已有证书，防止 EF 追踪的实体修改被意外保存
        var existing = await _microMajorCertificateRepository.FirstOrDefaultAsync(x => x.EnrollmentId == input.EnrollmentId);
        if (existing != null)
        {
            // 如果已有证书但 enrollment 状态不是 Certified，修复它
            if (enrollment.Status != MicroMajorEnrollmentStatus.Certified)
            {
                enrollment.Progress = 100;
                enrollment.Status = MicroMajorEnrollmentStatus.Certified;
                enrollment.CompletedAt ??= Clock.Now;
                enrollment.CertificateIssuedAt = existing.IssuedAt;
                await _microMajorEnrollmentRepository.UpdateAsync(enrollment, autoSave: true);
            }
            return (await MapCertificateDtosAsync(new List<MicroMajorCertificate> { existing }))[0];
        }

        // 发证必须附带证书图片：从所选证书模板解析；无模板则拒绝
        if (!input.CertificateTemplateId.HasValue)
        {
            throw new UserFriendlyException("发证必须附带证书图片，请先在微专业的「证书模板」中上传证书。");
        }

        var template = await _microMajorCertificateTemplateRepository.GetAsync(input.CertificateTemplateId.Value);
        if (template.MicroMajorId != enrollment.MicroMajorId)
        {
            throw new UserFriendlyException("所选证书模板不属于该微专业。");
        }
        // 优先使用前端画布合成的最终证书图片，否则回退到模板原图
        var certificateImageUrl = string.IsNullOrWhiteSpace(input.CompositeImageUrl)
            ? template.ImageUrl
            : input.CompositeImageUrl.Trim();

        // 证书编号：手动填写覆盖自动编号
        var certificateNo = string.IsNullOrWhiteSpace(input.CertificateNo)
            ? await GetNextCertificateNoAsync(enrollment.MicroMajorId)
            : input.CertificateNo.Trim();

        // 发证即代表认定完成，强制设为 100% 进度和已发证状态，无需校验实际学习进度
        enrollment.Progress = 100;
        enrollment.Status = MicroMajorEnrollmentStatus.Completed;
        enrollment.CompletedAt ??= Clock.Now;

        var certificate = new MicroMajorCertificate(
            GuidGenerator.Create(),
            enrollment.MicroMajorId,
            input.EnrollmentId,
            enrollment.StudentId,
            certificateNo,
            GuidGenerator.Create().ToString("N")[..10].ToUpperInvariant())
        {
            TenantId = enrollment.TenantId,
            CertificateImageUrl = certificateImageUrl,
            StudentNo = string.IsNullOrWhiteSpace(input.StudentNo) ? null : input.StudentNo.Trim(),
            Advisor = string.IsNullOrWhiteSpace(input.Advisor) ? null : input.Advisor.Trim(),
            IssueDate = input.IssueDate ?? Clock.Now,
            ValidUntil = input.ValidUntil
        };

        await _microMajorCertificateRepository.InsertAsync(certificate, autoSave: true);
        enrollment.Status = MicroMajorEnrollmentStatus.Certified;
        enrollment.CertificateIssuedAt = certificate.IssuedAt;
        await _microMajorEnrollmentRepository.UpdateAsync(enrollment, autoSave: true);

        return (await MapCertificateDtosAsync(new List<MicroMajorCertificate> { certificate }))[0];
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.Edit)]
    public async Task<List<MicroMajorCertificateTemplateDto>> GetCertificateTemplatesAsync(Guid microMajorId)
    {
        var items = await _microMajorCertificateTemplateRepository.GetListAsync(x => x.MicroMajorId == microMajorId);
        return items
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreationTime)
            .Select(x => new MicroMajorCertificateTemplateDto
            {
                Id = x.Id,
                MicroMajorId = x.MicroMajorId,
                Name = x.Name,
                ImageUrl = x.ImageUrl,
                SortOrder = x.SortOrder,
                Layers = DeserializeLayers(x.LayersJson),
                CreationTime = x.CreationTime,
                CreatorId = x.CreatorId,
                LastModificationTime = x.LastModificationTime,
                LastModifierId = x.LastModifierId,
                IsDeleted = x.IsDeleted,
                DeleterId = x.DeleterId,
                DeletionTime = x.DeletionTime
            })
            .ToList();
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.Edit)]
    public async Task<MicroMajorCertificateTemplateDto> CreateCertificateTemplateAsync(CreateUpdateMicroMajorCertificateTemplateDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new UserFriendlyException("证书模板名称不能为空。");
        }
        if (string.IsNullOrWhiteSpace(input.ImageUrl))
        {
            throw new UserFriendlyException("请先上传证书图片。");
        }

        var microMajor = await _microMajorRepository.GetAsync(input.MicroMajorId);
        var count = await _microMajorCertificateTemplateRepository.CountAsync(x => x.MicroMajorId == input.MicroMajorId);
        var template = new MicroMajorCertificateTemplate(
            GuidGenerator.Create(),
            microMajor.Id,
            input.Name.Trim(),
            input.ImageUrl.Trim())
        {
            TenantId = CurrentTenant.Id,
            SortOrder = input.SortOrder == 0 ? count + 1 : input.SortOrder,
            LayersJson = SerializeLayers(input.Layers)
        };

        // 上传证书模板即代表要启用证书，避免发证时提示「未启用证书」
        if (!microMajor.IsCertificateEnabled)
        {
            microMajor.IsCertificateEnabled = true;
        }

        await _microMajorCertificateTemplateRepository.InsertAsync(template, autoSave: true);
        return (await GetCertificateTemplatesAsync(template.MicroMajorId)).First(x => x.Id == template.Id);
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.Edit)]
    public async Task<MicroMajorCertificateTemplateDto> UpdateCertificateTemplateAsync(Guid id, CreateUpdateMicroMajorCertificateTemplateDto input)
    {
        var template = await _microMajorCertificateTemplateRepository.GetAsync(id);
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new UserFriendlyException("证书模板名称不能为空。");
        }
        if (string.IsNullOrWhiteSpace(input.ImageUrl))
        {
            throw new UserFriendlyException("请先上传证书图片。");
        }

        template.Name = input.Name.Trim();
        template.ImageUrl = input.ImageUrl.Trim();
        template.LayersJson = SerializeLayers(input.Layers);
        if (input.SortOrder > 0)
        {
            template.SortOrder = input.SortOrder;
        }

        await _microMajorCertificateTemplateRepository.UpdateAsync(template, autoSave: true);
        return (await GetCertificateTemplatesAsync(template.MicroMajorId)).First(x => x.Id == template.Id);
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.Edit)]
    public async Task DeleteCertificateTemplateAsync(Guid id)
    {
        await _microMajorCertificateTemplateRepository.DeleteAsync(id);
    }

    private async Task EnsureCoursesValidAsync(List<CreateUpdateMicroMajorCourseDto> courses)
    {
        if (courses.Count == 0)
        {
            return;
        }

        var ids = courses.Select(x => x.CourseId).Distinct().ToList();
        if (ids.Count != courses.Count)
        {
            throw new UserFriendlyException("课程不能重复绑定。");
        }

        var count = await _courseRepository.CountAsync(x => ids.Contains(x.Id));
        if (count != ids.Count)
        {
            throw new UserFriendlyException("存在无效课程。");
        }
    }

    private async Task ReplaceCoursesAsync(Guid microMajorId, List<CreateUpdateMicroMajorCourseDto> courses)
    {
        // Hard-delete existing links directly in SQL to avoid unique constraint violations
        // when re-inserting with the same (MicroMajorId, CourseId) pairs
        var dbContext = await _microMajorCourseRepository.GetDbContextAsync();
        await dbContext.Set<MicroMajorCourse>()
            .Where(x => x.MicroMajorId == microMajorId)
            .ExecuteDeleteAsync();

        foreach (var item in courses.OrderBy(x => x.SortOrder))
        {
            var link = new MicroMajorCourse(GuidGenerator.Create(), microMajorId, item.CourseId, item.SortOrder)
            {
                TenantId = CurrentTenant.Id,
                IsCore = item.IsCore
            };
            await _microMajorCourseRepository.InsertAsync(link);
        }
    }

    private async Task EnsureStudentCoursesAsync(Guid studentId, Guid microMajorId)
    {
        var links = await _microMajorCourseRepository.GetListAsync(x => x.MicroMajorId == microMajorId);
        var courseIds = links.Select(x => x.CourseId).Distinct().ToList();
        if (courseIds.Count == 0)
        {
            return;
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var existing = await _studentCourseRepository.GetListAsync(
                x => x.StudentId == studentId && courseIds.Contains(x.CourseId));
            var existingMap = existing.ToDictionary(x => x.CourseId);

            foreach (var courseId in courseIds)
            {
                if (existingMap.TryGetValue(courseId, out var studentCourse))
                {
                    if (studentCourse.Status == StudentCourseStatus.Dropped)
                    {
                        studentCourse.Status = StudentCourseStatus.Enrolled;
                        studentCourse.EnrolledAt = DateTime.UtcNow;
                        studentCourse.Progress = 0;
                        await _studentCourseRepository.UpdateAsync(studentCourse);
                    }

                    continue;
                }

                using (_currentTenant.Change(CurrentTenant.Id))
                {
                    var created = new StudentCourse(GuidGenerator.Create(), studentId, courseId);
                    await _studentCourseRepository.InsertAsync(created);
                }
            }
        }
    }

    private async Task RefreshEnrollmentProgressAsync(MicroMajorEnrollment enrollment)
    {
        // Pending 状态的报名尚未审批，不计算进度
        if (enrollment.Status == MicroMajorEnrollmentStatus.Pending)
            return;

        // 已手动结业或已发证的报名不再重新计算进度，保持管理员手动设置的状态
        if (enrollment.Status == MicroMajorEnrollmentStatus.Completed ||
            enrollment.Status == MicroMajorEnrollmentStatus.Certified)
            return;

        var microMajor = await _microMajorRepository.GetAsync(enrollment.MicroMajorId);
        var courseIds = (await _microMajorCourseRepository.GetListAsync(x => x.MicroMajorId == enrollment.MicroMajorId))
            .Select(x => x.CourseId)
            .Distinct()
            .ToList();

        if (courseIds.Count == 0)
        {
            enrollment.Progress = 0;
            if (enrollment.Status != MicroMajorEnrollmentStatus.Pending)
                enrollment.Status = MicroMajorEnrollmentStatus.Enrolled;
            await _microMajorEnrollmentRepository.UpdateAsync(enrollment, autoSave: true);
            return;
        }

        List<StudentCourse> studentCourses;
        using (DataFilter.Disable<IMultiTenant>())
        {
            studentCourses = await _studentCourseRepository.GetListAsync(
                x => x.StudentId == enrollment.StudentId && courseIds.Contains(x.CourseId));
        }

        var progress = studentCourses.Count == 0 ? 0 : studentCourses.Average(x => x.Progress);
        var allCompleted = courseIds.All(courseId =>
            studentCourses.Any(x => x.CourseId == courseId && x.Status == StudentCourseStatus.Completed));

        enrollment.Progress = progress;
        if (allCompleted && progress >= microMajor.RequiredCompletionRate)
        {
            enrollment.Status = enrollment.CertificateIssuedAt.HasValue
                ? MicroMajorEnrollmentStatus.Certified
                : MicroMajorEnrollmentStatus.Completed;
            enrollment.CompletedAt ??= Clock.Now;
        }
        else if (progress > 0)
        {
            enrollment.Status = MicroMajorEnrollmentStatus.InProgress;
            enrollment.CompletedAt = null;
        }
        else
        {
            if (enrollment.Status != MicroMajorEnrollmentStatus.Pending)
                enrollment.Status = MicroMajorEnrollmentStatus.Enrolled;
            enrollment.CompletedAt = null;
        }

        await _microMajorEnrollmentRepository.UpdateAsync(enrollment, autoSave: true);
    }

    private async Task<List<MicroMajorDto>> MapToDtosAsync(List<MicroMajor> items)
    {
        if (items.Count == 0)
        {
            return new List<MicroMajorDto>();
        }

        var ids = items.Select(x => x.Id).ToList();
        var links = await _microMajorCourseRepository.GetListAsync(x => ids.Contains(x.MicroMajorId));
        var enrollments = await _microMajorEnrollmentRepository.GetListAsync(x => ids.Contains(x.MicroMajorId));
        var currentUserId = _currentUser.Id;

        Dictionary<Guid, MicroMajorEnrollment> currentUserEnrollments = new();
        if (currentUserId.HasValue)
        {
            currentUserEnrollments = enrollments
                .Where(x => x.StudentId == currentUserId.Value)
                .GroupBy(x => x.MicroMajorId)
                .ToDictionary(x => x.Key, x => x.OrderByDescending(i => i.CreationTime).First());
        }

        var courseCountMap = links.GroupBy(x => x.MicroMajorId).ToDictionary(x => x.Key, x => x.Count());
        var enrollmentCountMap = enrollments
            .Where(x => x.Status != MicroMajorEnrollmentStatus.Cancelled)
            .GroupBy(x => x.MicroMajorId)
            .ToDictionary(x => x.Key, x => x.Count());

        return items.Select(item =>
        {
            currentUserEnrollments.TryGetValue(item.Id, out var currentEnrollment);
            return new MicroMajorDto
            {
                Id = item.Id,
                Title = item.Title,
                Summary = item.Summary,
                Description = item.Description,
                CoverImageUrl = item.CoverImageUrl,
                IndustryField = item.IndustryField,
                CollaborationUnit = item.CollaborationUnit,
                Status = item.Status,
                RequiredCompletionRate = item.RequiredCompletionRate,
                IsCertificateEnabled = item.IsCertificateEnabled,
                CourseCount = courseCountMap.GetValueOrDefault(item.Id, 0),
                EnrollmentCount = enrollmentCountMap.GetValueOrDefault(item.Id, 0),
                CurrentUserProgress = currentEnrollment?.Progress,
                IsCurrentUserEnrolled = currentEnrollment != null &&
                    currentEnrollment.Status != MicroMajorEnrollmentStatus.Cancelled,
                CreationTime = item.CreationTime,
                CreatorId = item.CreatorId,
                LastModificationTime = item.LastModificationTime,
                LastModifierId = item.LastModifierId,
                IsDeleted = item.IsDeleted,
                DeleterId = item.DeleterId,
                DeletionTime = item.DeletionTime
            };
        }).ToList();
    }

    private async Task<MicroMajorDto> MapToDtoAsync(MicroMajor item)
    {
        return (await MapToDtosAsync(new List<MicroMajor> { item }))[0];
    }

    private async Task<List<MicroMajorCourseDto>> GetCourseDtosAsync(Guid microMajorId)
    {
        var links = await _microMajorCourseRepository.GetListAsync(x => x.MicroMajorId == microMajorId);
        var courseIds = links.Select(x => x.CourseId).Distinct().ToList();
        var courses = await _courseRepository.GetListAsync(x => courseIds.Contains(x.Id));
        var courseMap = courses.ToDictionary(x => x.Id);

        var majorIds = courses
            .Where(x => x.MajorId.HasValue)
            .Select(x => x.MajorId!.Value)
            .Distinct()
            .ToList();
        var majorMap = new Dictionary<Guid, string>();
        if (majorIds.Count > 0)
        {
            var majorRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<KnowledgeHub.Majors.Major, Guid>>();
            var queryable = await majorRepo.GetQueryableAsync();
            var majorList = await AsyncExecuter.ToListAsync(queryable.Where(m => majorIds.Contains(m.Id)));
            foreach (var m in majorList)
            {
                majorMap[m.Id] = m.Name;
            }
        }

        return links
            .OrderBy(x => x.SortOrder)
            .Select(link =>
            {
                courseMap.TryGetValue(link.CourseId, out var course);
                string? majorName = null;
                if (course?.MajorId.HasValue == true && majorMap.TryGetValue(course.MajorId.Value, out var name))
                {
                    majorName = name;
                }
                return new MicroMajorCourseDto
                {
                    Id = link.Id,
                    MicroMajorId = link.MicroMajorId,
                    CourseId = link.CourseId,
                    CourseTitle = course?.Title,
                    CourseCoverImageUrl = course?.CoverImageUrl,
                    MajorId = course?.MajorId,
                    MajorName = majorName,
                    Semester = course?.Semester,
                    SortOrder = link.SortOrder,
                    IsCore = link.IsCore
                };
            })
            .ToList();
    }

    private async Task<List<MicroMajorEnrollmentDto>> MapEnrollmentDtosAsync(List<MicroMajorEnrollment> items)
    {
        if (items.Count == 0)
        {
            return new List<MicroMajorEnrollmentDto>();
        }

        var microMajorIds = items.Select(x => x.MicroMajorId).Distinct().ToList();
        var studentIds = items.Select(x => x.StudentId).Distinct().ToList();
        var microMajors = await _microMajorRepository.GetListAsync(x => microMajorIds.Contains(x.Id));

        List<IdentityUser> users;
        using (DataFilter.Disable<IMultiTenant>())
        {
            users = await _userRepository.GetListAsync(x => studentIds.Contains(x.Id));
        }

        var microMajorMap = microMajors.ToDictionary(x => x.Id, x => x.Title);
        var userMap = users.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);

        // 批量查询已发证的 certificates，便于填充 CertificateImageUrl
        var enrollmentIds = items.Select(x => x.Id).ToList();
        var certificates = await _microMajorCertificateRepository.GetListAsync(x => enrollmentIds.Contains(x.EnrollmentId));
        var certMap = certificates.ToDictionary(x => x.EnrollmentId, x => x.CertificateImageUrl);

        return items.Select(item => new MicroMajorEnrollmentDto
        {
            Id = item.Id,
            MicroMajorId = item.MicroMajorId,
            MicroMajorTitle = microMajorMap.GetValueOrDefault(item.MicroMajorId),
            StudentId = item.StudentId,
            StudentName = userMap.GetValueOrDefault(item.StudentId),
            Status = item.Status,
            Progress = item.Progress,
            EnrolledAt = item.EnrolledAt,
            CompletedAt = item.CompletedAt,
            CertificateIssuedAt = item.CertificateIssuedAt,
            CertificateImageUrl = certMap.GetValueOrDefault(item.Id),
            CreationTime = item.CreationTime,
            CreatorId = item.CreatorId,
            LastModificationTime = item.LastModificationTime,
            LastModifierId = item.LastModifierId,
            IsDeleted = item.IsDeleted,
            DeleterId = item.DeleterId,
            DeletionTime = item.DeletionTime
        }).ToList();
    }

    private async Task<List<MicroMajorCertificateDto>> MapCertificateDtosAsync(List<MicroMajorCertificate> items)
    {
        if (items.Count == 0)
        {
            return new List<MicroMajorCertificateDto>();
        }

        var microMajorIds = items.Select(x => x.MicroMajorId).Distinct().ToList();
        var studentIds = items.Select(x => x.StudentId).Distinct().ToList();
        var microMajors = await _microMajorRepository.GetListAsync(x => microMajorIds.Contains(x.Id));

        List<IdentityUser> users;
        using (DataFilter.Disable<IMultiTenant>())
        {
            users = await _userRepository.GetListAsync(x => studentIds.Contains(x.Id));
        }

        var microMajorMap = microMajors.ToDictionary(x => x.Id, x => x.Title);
        var userMap = users.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);

        return items.Select(item => new MicroMajorCertificateDto
        {
            Id = item.Id,
            MicroMajorId = item.MicroMajorId,
            MicroMajorTitle = microMajorMap.GetValueOrDefault(item.MicroMajorId),
            EnrollmentId = item.EnrollmentId,
            StudentId = item.StudentId,
            StudentName = userMap.GetValueOrDefault(item.StudentId),
            CertificateNo = item.CertificateNo,
            VerifyCode = item.VerifyCode,
            CertificateImageUrl = item.CertificateImageUrl,
            Status = item.Status,
            IssuedAt = item.IssuedAt,
            StudentNo = item.StudentNo,
            Advisor = item.Advisor,
            IssueDate = item.IssueDate,
            ValidUntil = item.ValidUntil,
            CreationTime = item.CreationTime,
            CreatorId = item.CreatorId,
            LastModificationTime = item.LastModificationTime,
            LastModifierId = item.LastModifierId,
            IsDeleted = item.IsDeleted,
            DeleterId = item.DeleterId,
            DeletionTime = item.DeletionTime
        }).ToList();
    }

    private async Task<string> GetNextCertificateNoAsync(Guid microMajorId)
    {
        var count = await _microMajorCertificateRepository.CountAsync(x => x.MicroMajorId == microMajorId);
        return $"KG-MM-{Clock.Now:yyyyMM}-{count + 1:D4}";
    }

    private static List<CertificateTemplateLayerDto> DeserializeLayers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<CertificateTemplateLayerDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<CertificateTemplateLayerDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<CertificateTemplateLayerDto>();
        }
        catch (JsonException)
        {
            return new List<CertificateTemplateLayerDto>();
        }
    }

    private static string? SerializeLayers(List<CertificateTemplateLayerDto>? layers)
    {
        if (layers == null || layers.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(layers, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static void CopyDto(MicroMajorDto source, MicroMajorDto target)
    {
        target.Id = source.Id;
        target.Title = source.Title;
        target.Summary = source.Summary;
        target.Description = source.Description;
        target.CoverImageUrl = source.CoverImageUrl;
        target.IndustryField = source.IndustryField;
        target.CollaborationUnit = source.CollaborationUnit;
        target.Status = source.Status;
        target.RequiredCompletionRate = source.RequiredCompletionRate;
        target.IsCertificateEnabled = source.IsCertificateEnabled;
        target.CourseCount = source.CourseCount;
        target.EnrollmentCount = source.EnrollmentCount;
        target.CurrentUserProgress = source.CurrentUserProgress;
        target.IsCurrentUserEnrolled = source.IsCurrentUserEnrolled;
        target.CreationTime = source.CreationTime;
        target.CreatorId = source.CreatorId;
        target.LastModificationTime = source.LastModificationTime;
        target.LastModifierId = source.LastModifierId;
        target.IsDeleted = source.IsDeleted;
        target.DeleterId = source.DeleterId;
        target.DeletionTime = source.DeletionTime;
    }

    [AllowAnonymous]
    public async Task<List<MicroMajorResourceDto>> GetResourcesAsync(Guid microMajorId)
    {
        var query = await _microMajorResourceRepository.GetQueryableAsync();
        var bridges = query.Where(x => x.MicroMajorId == microMajorId)
            .OrderBy(x => x.SortOrder)
            .ToList();

        var result = new List<MicroMajorResourceDto>();
        foreach (var b in bridges)
        {
            var resource = await _resourceRepository.FindAsync(b.ResourceId);
            result.Add(new MicroMajorResourceDto
            {
                Id = b.Id,
                MicroMajorId = b.MicroMajorId,
                ResourceId = b.ResourceId,
                ResourceName = resource?.Name ?? "未知素材",
                FileExtension = resource?.FileExtension,
                DownloadCount = resource?.DownloadCount ?? 0,
                SortOrder = b.SortOrder,
                Description = b.Description
            });
        }

        return result;
    }
}
