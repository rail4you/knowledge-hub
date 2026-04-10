using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

public class StudentCourseAppService : KnowledgeHubAppService, IStudentCourseAppService
{
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;

    public StudentCourseAppService(
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant)
    {
        _studentCourseRepository = studentCourseRepository;
        _courseRepository = courseRepository;
        _userRepository = userRepository;
        _userManager = userManager;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Resolve the effective tenant filter.
    /// Host admin: use input.TenantId if specified, otherwise null (see all).
    /// Tenant admin: always use their own tenant.
    /// </summary>
    private Guid? ResolveTenantFilter(Guid? inputTenantId)
    {
        if (_currentTenant.Id.HasValue)
            return _currentTenant.Id; // Tenant admin: locked to own tenant
        return inputTenantId; // Host admin: use specified or null
    }

    [Authorize(KnowledgeHubPermissions.Courses.ManageEnrollment)]
    public async Task<PagedResultDto<StudentCourseDto>> GetPagedAsync(GetStudentCoursesInput input)
    {
        var tenantFilter = ResolveTenantFilter(input.TenantId);

        List<StudentCourse> items;
        long totalCount;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _studentCourseRepository.GetQueryableAsync();

            query = query
                .WhereIf(tenantFilter.HasValue, x => x.TenantId == tenantFilter!.Value)
                .WhereIf(input.CourseId.HasValue, x => x.CourseId == input.CourseId!.Value)
                .WhereIf(input.StudentId.HasValue, x => x.StudentId == input.StudentId!.Value)
                .WhereIf(input.Status.HasValue, x => x.Status == input.Status!.Value)
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x =>
                    x.StudentId.ToString().Contains(input.Filter!)); // fallback filter

            totalCount = await query.LongCountAsync();
            items = await query
                .OrderByDescending(x => x.EnrolledAt)
                .PageBy(input.SkipCount, input.MaxResultCount)
                .ToListAsync();
        }

        // Load student names (cross-tenant)
        var studentIds = items.Select(x => x.StudentId).Distinct().ToList();
        var courseIds = items.Select(x => x.CourseId).Distinct().ToList();

        Dictionary<Guid, string> studentMap;
        Dictionary<Guid, string> courseMap;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var userQuery = await _userRepository.GetQueryableAsync();
            studentMap = (await userQuery.Where(u => studentIds.Contains(u.Id)).ToListAsync())
                .ToDictionary(s => s.Id, s => s.Name ?? s.UserName);

            var courseQuery = await _courseRepository.GetQueryableAsync();
            courseMap = (await courseQuery.Where(c => courseIds.Contains(c.Id)).ToListAsync())
                .ToDictionary(c => c.Id, c => c.Title);
        }

        var dtos = items.Select(sc => new StudentCourseDto
        {
            Id = sc.Id,
            TenantId = sc.TenantId,
            StudentId = sc.StudentId,
            StudentName = studentMap.GetValueOrDefault(sc.StudentId, ""),
            CourseId = sc.CourseId,
            CourseName = courseMap.GetValueOrDefault(sc.CourseId, ""),
            Status = sc.Status,
            EnrolledAt = sc.EnrolledAt,
            Progress = sc.Progress,
            CreationTime = sc.CreationTime,
            CreatorId = sc.CreatorId,
            LastModificationTime = sc.LastModificationTime,
            LastModifierId = sc.LastModifierId,
        }).ToList();

        return new PagedResultDto<StudentCourseDto>(totalCount, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Courses.ManageEnrollment)]
    public async Task<PagedResultDto<IdentityUserDto>> GetAvailableStudentsAsync(GetAvailableStudentsInput input)
    {
        var tenantFilter = ResolveTenantFilter(input.TenantId);

        // Get already enrolled student IDs (cross-tenant query)
        HashSet<Guid> enrolledStudentIds;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var enrolledQuery = await _studentCourseRepository.GetQueryableAsync();
            enrolledStudentIds = (await enrolledQuery
                    .Where(sc => sc.CourseId == input.CourseId && sc.Status != StudentCourseStatus.Dropped)
                    .WhereIf(tenantFilter.HasValue, sc => sc.TenantId == tenantFilter!.Value)
                    .Select(sc => sc.StudentId)
                    .ToListAsync())
                .ToHashSet();
        }

        // Query users (cross-tenant, then filter by tenant manually)
        List<IdentityUser> allStudents;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var userQuery = await _userRepository.GetQueryableAsync();

            if (tenantFilter.HasValue)
            {
                userQuery = userQuery.Where(u => u.TenantId == tenantFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                userQuery = userQuery.Where(u =>
                    u.UserName.Contains(input.Filter!) ||
                    (u.Name != null && u.Name.Contains(input.Filter!)) ||
                    u.Email.Contains(input.Filter!));
            }

            allStudents = await userQuery.OrderBy(u => u.UserName).ToListAsync();
        }

        // Filter by "Student" role
        var studentsWithRole = new List<IdentityUser>();
        foreach (var user in allStudents)
        {
            using (_currentTenant.Change(user.TenantId))
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Student"))
                {
                    studentsWithRole.Add(user);
                }
            }
        }

        // Exclude already enrolled
        var available = studentsWithRole.Where(s => !enrolledStudentIds.Contains(s.Id)).ToList();

        var totalCount = available.Count;
        var paged = available
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos = paged.Select(u => new IdentityUserDto
        {
            Id = u.Id,
            UserName = u.UserName,
            Name = u.Name,
            Surname = u.Surname,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            IsActive = u.IsActive,
            CreationTime = u.CreationTime,
        }).ToList();

        return new PagedResultDto<IdentityUserDto>(totalCount, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Courses.ManageEnrollment)]
    public async Task EnrollStudentAsync(CreateStudentCourseDto input)
    {
        // Cross-tenant lookup for existing record
        StudentCourse? existing;
        using (DataFilter.Disable<IMultiTenant>())
        {
            existing = await _studentCourseRepository.FirstOrDefaultAsync(
                x => x.StudentId == input.StudentId && x.CourseId == input.CourseId);
        }

        if (existing != null)
        {
            if (existing.Status != StudentCourseStatus.Dropped)
            {
                throw new UserFriendlyException("该学生已选修此课程");
            }

            // Reactivate
            existing.Status = StudentCourseStatus.Enrolled;
            existing.EnrolledAt = DateTime.UtcNow;
            existing.Progress = 0;
            await _studentCourseRepository.UpdateAsync(existing);
            return;
        }

        // Get student's tenant to set correct TenantId
        var studentTenantId = await GetStudentTenantIdAsync(input.StudentId);
        using (_currentTenant.Change(studentTenantId))
        {
            var studentCourse = new StudentCourse(GuidGenerator.Create(), input.StudentId, input.CourseId);
            await _studentCourseRepository.InsertAsync(studentCourse);
        }
    }

    [Authorize(KnowledgeHubPermissions.Courses.ManageEnrollment)]
    public async Task BatchEnrollAsync(BatchEnrollDto input)
    {
        // Cross-tenant lookup for all existing records
        List<StudentCourse> existingRecords;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _studentCourseRepository.GetQueryableAsync();
            existingRecords = await query
                .Where(sc => sc.CourseId == input.CourseId)
                .ToListAsync();
        }

        var activeStudentIds = existingRecords
            .Where(sc => sc.Status != StudentCourseStatus.Dropped)
            .Select(sc => sc.StudentId)
            .ToHashSet();

        var droppedRecords = existingRecords
            .Where(sc => sc.Status == StudentCourseStatus.Dropped)
            .ToDictionary(sc => sc.StudentId);

        var newStudentIds = input.StudentIds.Except(activeStudentIds).ToList();

        // Batch get student tenant IDs
        var studentTenantMap = await GetStudentTenantIdsAsync(newStudentIds);

        foreach (var studentId in newStudentIds)
        {
            if (droppedRecords.TryGetValue(studentId, out var dropped))
            {
                dropped.Status = StudentCourseStatus.Enrolled;
                dropped.EnrolledAt = DateTime.UtcNow;
                dropped.Progress = 0;
                await _studentCourseRepository.UpdateAsync(dropped);
            }
            else
            {
                var tenantId = studentTenantMap.GetValueOrDefault(studentId);
                using (_currentTenant.Change(tenantId))
                {
                    var studentCourse = new StudentCourse(GuidGenerator.Create(), studentId, input.CourseId);
                    await _studentCourseRepository.InsertAsync(studentCourse);
                }
            }
        }
    }

    [Authorize(KnowledgeHubPermissions.Courses.ManageEnrollment)]
    public async Task DeleteAsync(Guid id)
    {
        StudentCourse studentCourse;
        using (DataFilter.Disable<IMultiTenant>())
        {
            studentCourse = await _studentCourseRepository.GetAsync(id);
        }

        studentCourse.Drop();
        await _studentCourseRepository.UpdateAsync(studentCourse);
    }

    private async Task<Guid?> GetStudentTenantIdAsync(Guid studentId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FirstOrDefaultAsync(u => u.Id == studentId);
            return user?.TenantId;
        }
    }

    private async Task<Dictionary<Guid, Guid?>> GetStudentTenantIdsAsync(List<Guid> studentIds)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _userRepository.GetQueryableAsync();
            return await query
                .Where(u => studentIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.TenantId);
        }
    }
}
