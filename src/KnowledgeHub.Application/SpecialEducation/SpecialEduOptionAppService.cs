using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Edition;
using KnowledgeHub.Learning;
using KnowledgeHub.Permissions;
using KnowledgeHub.SpecialEducation;
using KnowledgeHub.SpecialEducation.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace KnowledgeHub.Application.SpecialEducation;

/// <summary>特教下拉选项：课程 + 学生（学生角色过滤，支持按课程标注选课状态）。</summary>
[Authorize(KnowledgeHubPermissions.SpecialEducation.Default)]
public class SpecialEduOptionAppService : KnowledgeHubAppService, ISpecialEduOptionAppService
{
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _enrollmentRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IEditionConfigService _editionConfig;

    public SpecialEduOptionAppService(
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> enrollmentRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        IEditionConfigService editionConfig)
    {
        _courseRepository = courseRepository;
        _enrollmentRepository = enrollmentRepository;
        _userRepository = userRepository;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _editionConfig = editionConfig;
    }

    public async Task<List<SpecialEduCourseOptionDto>> GetCourseOptionsAsync()
    {
        if (!await _editionConfig.IsSpecialEducationEnabledAsync())
        {
            return new List<SpecialEduCourseOptionDto>();
        }
        var query = await _courseRepository.GetQueryableAsync();
        var courses = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime).Take(100));
        return courses.Select(x => new SpecialEduCourseOptionDto { Id = x.Id, Title = x.Title }).ToList();
    }

    public async Task<List<SpecialEduStudentOptionDto>> GetStudentOptionsAsync(GetSpecialEduStudentOptionsInput input)
    {
        if (!await _editionConfig.IsSpecialEducationEnabledAsync())
        {
            return new List<SpecialEduStudentOptionDto>();
        }
        var courseId = input.CourseId;
        var query = await _userRepository.GetQueryableAsync();
        query = query.Where(x => x.TenantId == _currentTenant.Id);
        var users = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.UserName));

        HashSet<Guid> enrolled = new();
        if (courseId.HasValue)
        {
            var enrollments = await _enrollmentRepository.GetListAsync(x => x.CourseId == courseId.Value);
            enrolled = enrollments.Select(x => x.StudentId).ToHashSet();
        }

        var result = new List<SpecialEduStudentOptionDto>();
        foreach (var user in users)
        {
            using (_currentTenant.Change(user.TenantId))
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains("Student"))
                {
                    continue;
                }
                result.Add(new SpecialEduStudentOptionDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Name = !user.Name.IsNullOrEmpty() ? user.Name! : user.UserName ?? string.Empty,
                    EnrolledInSelectedCourse = courseId.HasValue && enrolled.Contains(user.Id)
                });
            }
        }
        return result;
    }

    /// <summary>租户内可指派的审核教师（Teacher / SchoolAdmin 角色）。</summary>
    public async Task<List<SpecialEduTeacherOptionDto>> GetTeacherOptionsAsync()
    {
        if (!await _editionConfig.IsSpecialEducationEnabledAsync())
        {
            return new List<SpecialEduTeacherOptionDto>();
        }
        var query = await _userRepository.GetQueryableAsync();
        query = query.Where(x => x.TenantId == _currentTenant.Id);
        var users = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.UserName));

        var result = new List<SpecialEduTeacherOptionDto>();
        foreach (var user in users)
        {
            using (_currentTenant.Change(user.TenantId))
            {
                var roles = await _userManager.GetRolesAsync(user);
                var matched = roles.FirstOrDefault(r => r == "SchoolAdmin" || r == "Teacher");
                if (matched == null)
                {
                    continue;
                }
                result.Add(new SpecialEduTeacherOptionDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Name = !user.Name.IsNullOrEmpty() ? user.Name! : user.UserName ?? string.Empty,
                    RoleName = matched
                });
            }
        }
        return result;
    }
}
