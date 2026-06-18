using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IRepository<MicroMajorResource, Guid> _microMajorResourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<Resources.Resource, Guid> _resourceRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;

    public MicroMajorAppService(
        IRepository<MicroMajor, Guid> microMajorRepository,
        IRepository<MicroMajorCourse, Guid> microMajorCourseRepository,
        IRepository<MicroMajorEnrollment, Guid> microMajorEnrollmentRepository,
        IRepository<MicroMajorCertificate, Guid> microMajorCertificateRepository,
        IRepository<MicroMajorResource, Guid> microMajorResourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<Resources.Resource, Guid> resourceRepository,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant)
    {
        _microMajorRepository = microMajorRepository;
        _microMajorCourseRepository = microMajorCourseRepository;
        _microMajorEnrollmentRepository = microMajorEnrollmentRepository;
        _microMajorCertificateRepository = microMajorCertificateRepository;
        _microMajorResourceRepository = microMajorResourceRepository;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _userRepository = userRepository;
        _resourceRepository = resourceRepository;
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

        await _microMajorRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.MicroMajors.Default)]
    public async Task EnrollAsync(Guid microMajorId)
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
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

    [Authorize(KnowledgeHubPermissions.MicroMajors.IssueCertificate)]
    public async Task<MicroMajorCertificateDto> IssueCertificateAsync(Guid enrollmentId)
    {
        var enrollment = await _microMajorEnrollmentRepository.GetAsync(enrollmentId);
        var microMajor = await _microMajorRepository.GetAsync(enrollment.MicroMajorId);

        if (!microMajor.IsCertificateEnabled)
        {
            throw new UserFriendlyException("当前微专业未启用证书。");
        }

        await RefreshEnrollmentProgressAsync(enrollment);
        if (enrollment.Status != MicroMajorEnrollmentStatus.Completed &&
            enrollment.Status != MicroMajorEnrollmentStatus.Certified)
        {
            throw new UserFriendlyException("该学生尚未满足发证条件。");
        }

        var existing = await _microMajorCertificateRepository.FirstOrDefaultAsync(x => x.EnrollmentId == enrollmentId);
        if (existing != null)
        {
            return (await MapCertificateDtosAsync(new List<MicroMajorCertificate> { existing }))[0];
        }

        var certificate = new MicroMajorCertificate(
            GuidGenerator.Create(),
            enrollment.MicroMajorId,
            enrollmentId,
            enrollment.StudentId,
            $"MM-{Clock.Now:yyyyMMdd}-{GuidGenerator.Create():N}"[..21],
            GuidGenerator.Create().ToString("N")[..10].ToUpperInvariant())
        {
            TenantId = enrollment.TenantId
        };

        await _microMajorCertificateRepository.InsertAsync(certificate, autoSave: true);
        enrollment.Status = MicroMajorEnrollmentStatus.Certified;
        enrollment.CertificateIssuedAt = certificate.IssuedAt;
        await _microMajorEnrollmentRepository.UpdateAsync(enrollment, autoSave: true);

        return (await MapCertificateDtosAsync(new List<MicroMajorCertificate> { certificate }))[0];
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
            Status = item.Status,
            IssuedAt = item.IssuedAt,
            CreationTime = item.CreationTime,
            CreatorId = item.CreatorId,
            LastModificationTime = item.LastModificationTime,
            LastModifierId = item.LastModifierId,
            IsDeleted = item.IsDeleted,
            DeleterId = item.DeleterId,
            DeletionTime = item.DeletionTime
        }).ToList();
    }

    private static void CopyDto(MicroMajorDto source, MicroMajorDetailDto target)
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
