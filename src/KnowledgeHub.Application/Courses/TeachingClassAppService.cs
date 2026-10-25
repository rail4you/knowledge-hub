using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Majors;
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

/// <summary>
/// 教学班服务。
///
/// 用途：把「班级」从自由文本升级为可查询的教学组织单元，
/// 从而支持「按班级筛选学生 → 多选 → 批量选课」的闭环。
///
/// 隔离规则（与《资源库与学员数据归属设计》一致）：
///   · 班级与成员都在所属租户（学校）内，ABP 的 IMultiTenant 过滤自动生效。
///   · 添加成员时校验学生账号所属租户 = 班级所属租户，
///     不信任外部传入的「学校」信息。
///   · 移除成员 / 删除班级都**不删除**该学生已产生的学习与考核记录。
/// </summary>
[Authorize(KnowledgeHubPermissions.TeachingClasses.Default)]
public class TeachingClassAppService : KnowledgeHubAppService, ITeachingClassAppService
{
    private readonly IRepository<TeachingClass, Guid> _classRepository;
    private readonly IRepository<TeachingClassMember, Guid> _memberRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Major, Guid> _majorRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ICurrentTenant _currentTenant;

    public TeachingClassAppService(
        IRepository<TeachingClass, Guid> classRepository,
        IRepository<TeachingClassMember, Guid> memberRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Major, Guid> majorRepository,
        IRepository<IdentityUser, Guid> userRepository,
        ICurrentTenant currentTenant)
    {
        _classRepository = classRepository;
        _memberRepository = memberRepository;
        _courseRepository = courseRepository;
        _majorRepository = majorRepository;
        _userRepository = userRepository;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// 生效的租户过滤：租户管理员锁定本租户；宿主管理员按入参，空则不限租户。
    /// </summary>
    private Guid? ResolveTenantFilter(Guid? inputTenantId)
    {
        if (_currentTenant.Id.HasValue) return _currentTenant.Id;
        return inputTenantId;
    }

    public async Task<PagedResultDto<TeachingClassDto>> GetListAsync(GetTeachingClassesInput input)
    {
        var tenantFilter = ResolveTenantFilter(input.TenantId);

        var query = await _classRepository.GetQueryableAsync();
        query = query.WhereIf(tenantFilter.HasValue, c => c.TenantId == tenantFilter!.Value);

        if (input.MajorId.HasValue) query = query.Where(c => c.MajorId == input.MajorId.Value);
        if (input.CourseId.HasValue) query = query.Where(c => c.CourseId == input.CourseId.Value);
        if (input.TeacherId.HasValue) query = query.Where(c => c.TeacherId == input.TeacherId.Value);
        if (input.Status.HasValue) query = query.Where(c => c.Status == input.Status.Value);
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter!.Trim();
            query = query.Where(c => c.Name.Contains(f));
        }

        var totalCount = await query.CountAsync();

        var classes = await query
            .OrderBy(c => c.GradeYear ?? 0).ThenBy(c => c.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        var ids = classes.Select(c => c.Id).ToList();
        var memberCounts = await GetMemberCountsAsync(ids);

        var (majorNames, courseNames, teacherNames) = await GetRelatedNamesAsync(classes);

        var dtos = classes.Select(c => new TeachingClassDto
        {
            Id = c.Id,
            TenantId = c.TenantId,
            Name = c.Name,
            MajorId = c.MajorId,
            MajorName = c.MajorId.HasValue ? majorNames.GetValueOrDefault(c.MajorId.Value) : null,
            CourseId = c.CourseId,
            CourseName = c.CourseId.HasValue ? courseNames.GetValueOrDefault(c.CourseId.Value) : null,
            TeacherId = c.TeacherId,
            TeacherName = c.TeacherId.HasValue ? teacherNames.GetValueOrDefault(c.TeacherId.Value) : null,
            GradeYear = c.GradeYear,
            Description = c.Description,
            Status = c.Status,
            MemberCount = memberCounts.GetValueOrDefault(c.Id),
            CreationTime = c.CreationTime,
        }).ToList();

        return new PagedResultDto<TeachingClassDto>(totalCount, dtos);
    }

    public async Task<TeachingClassDto> GetAsync(Guid id)
    {
        var entity = await _classRepository.GetAsync(id);
        return await ToDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.TeachingClasses.Create)]
    public async Task<TeachingClassDto> CreateAsync(CreateTeachingClassDto input)
    {
        Check.NotNullOrWhiteSpace(input.Name, nameof(input.Name));

        var entity = new TeachingClass(GuidGenerator.Create(), input.Name.Trim())
        {
            TenantId = CurrentTenant.Id,
            MajorId = input.MajorId,
            CourseId = input.CourseId,
            TeacherId = input.TeacherId ?? CurrentUser.Id,
            GradeYear = input.GradeYear,
            Description = input.Description,
            Status = input.Status,
        };

        await _classRepository.InsertAsync(entity, autoSave: true);
        return await ToDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.TeachingClasses.Edit)]
    public async Task<TeachingClassDto> UpdateAsync(Guid id, UpdateTeachingClassDto input)
    {
        var entity = await GetOwnedClassAsync(id);

        Check.NotNullOrWhiteSpace(input.Name, nameof(input.Name));

        entity.Name = input.Name.Trim();
        entity.MajorId = input.MajorId;
        entity.CourseId = input.CourseId;
        entity.TeacherId = input.TeacherId ?? entity.TeacherId;
        entity.GradeYear = input.GradeYear;
        entity.Description = input.Description;
        entity.Status = input.Status;

        await _classRepository.UpdateAsync(entity, autoSave: true);
        return await ToDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.TeachingClasses.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetOwnedClassAsync(id);

        // 成员关系一并失效；学生的选课与成绩记录不受影响（不级联删除 StudentCourse）
        var members = await _memberRepository.GetListAsync(m => m.ClassId == id);
        await _memberRepository.DeleteManyAsync(members);
        await _classRepository.DeleteAsync(entity, autoSave: true);
    }

    [Authorize(KnowledgeHubPermissions.TeachingClasses.ManageMembers)]
    public async Task<List<Guid>> AddMembersAsync(Guid classId, AddTeachingClassMembersDto input)
    {
        var entity = await GetOwnedClassAsync(classId);

        if (entity.Status == TeachingClassStatus.Archived)
        {
            throw new AbpException("已结课的班级不能添加学生。");
        }

        if (input.StudentIds == null || input.StudentIds.Count == 0)
        {
            return new List<Guid>();
        }

        var studentIds = input.StudentIds.Distinct().ToList();

        // 校验学生账号确实属于本班所在学校（不信任外部传入的「学校」信息）
        var students = await _userRepository.GetListAsync(u => studentIds.Contains(u.Id));
        var studentMap = students.ToDictionary(u => u.Id);

        var invalid = studentIds.Where(id => !studentMap.ContainsKey(id)).ToList();
        if (invalid.Count > 0)
        {
            throw new AbpException($"有 {invalid.Count} 个学生账号不存在或不属于当前学校。");
        }

        if (entity.TenantId.HasValue)
        {
            var crossTenant = students.Where(u => u.TenantId != entity.TenantId).Select(u => u.UserName).ToList();
            if (crossTenant.Count > 0)
            {
                throw new AbpException(
                    $"以下学生不属于本班所在学校，无法加入：{string.Join("、", crossTenant.Take(10))}"
                    + (crossTenant.Count > 10 ? " 等" : ""));
            }
        }

        var existing = await _memberRepository.GetListAsync(m => m.ClassId == entity.Id);
        var existingIds = existing.Select(m => m.StudentId).ToHashSet();

        var toAdd = studentIds.Where(id => !existingIds.Contains(id)).ToList();
        if (toAdd.Count == 0)
        {
            // 重复导入按跳过处理，不覆盖既有关系
            return new List<Guid>();
        }

        var entities = toAdd.Select(id =>
        {
            var user = studentMap[id];
            var fullName = string.Join(string.Empty, new[] { user.Name, user.Surname }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return new TeachingClassMember(GuidGenerator.Create(), entity.Id, id)
            {
                TenantId = entity.TenantId,
                StudentName = fullName,
                StudentNumber = user.UserName,
                Source = input.Source,
                JoinedAt = DateTime.UtcNow,
            };
        }).ToList();

        await _memberRepository.InsertManyAsync(entities, autoSave: true);
        return toAdd;
    }

    [Authorize(KnowledgeHubPermissions.TeachingClasses.ManageMembers)]
    public async Task RemoveMemberAsync(Guid classId, Guid studentId)
    {
        var entity = await GetOwnedClassAsync(classId);

        var member = await _memberRepository.FirstOrDefaultAsync(m => m.ClassId == entity.Id && m.StudentId == studentId);
        if (member == null) return;

        // 只解除成员关系；该生的选课与成绩记录保留在原课程与原教师名下
        await _memberRepository.DeleteAsync(member, autoSave: true);
    }

    public async Task<PagedResultDto<TeachingClassMemberDto>> GetMembersAsync(GetTeachingClassMembersInput input)
    {
        var entity = await GetOwnedClassAsync(input.ClassId);

        var query = await _memberRepository.GetQueryableAsync();
        query = query.Where(m => m.ClassId == entity.Id);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter!.Trim();
            query = query.Where(m =>
                (m.StudentName != null && m.StudentName.Contains(f)) ||
                (m.StudentNumber != null && m.StudentNumber.Contains(f)));
        }

        var totalCount = await query.CountAsync();
        var members = await query
            .OrderBy(m => m.JoinedAt)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        var studentIds = members.Select(m => m.StudentId).Distinct().ToList();
        var users = studentIds.Count == 0
            ? new List<IdentityUser>()
            : await _userRepository.GetListAsync(u => studentIds.Contains(u.Id));
        var userMap = users.ToDictionary(u => u.Id);

        var majorNames = await GetMajorNamesAsync(users);

        var dtos = members.Select(m =>
        {
            userMap.TryGetValue(m.StudentId, out var user);
            var majorId = user?.GetProperty<Guid?>("MajorId");
            return new TeachingClassMemberDto
            {
                Id = m.Id,
                ClassId = m.ClassId,
                StudentId = m.StudentId,
                StudentName = m.StudentName,
                StudentNumber = m.StudentNumber,
                UserName = user?.UserName,
                MajorName = majorId.HasValue ? majorNames.GetValueOrDefault(majorId.Value) : null,
                Source = m.Source,
                JoinedAt = m.JoinedAt,
            };
        }).ToList();

        return new PagedResultDto<TeachingClassMemberDto>(totalCount, dtos);
    }

    // ── helpers ────────────────────────────────────────────────

    /// <summary>取班级并校验归属：租户管理员只能操作本租户的班级</summary>
    private async Task<TeachingClass> GetOwnedClassAsync(Guid id)
    {
        var entity = await _classRepository.GetAsync(id);

        if (_currentTenant.Id.HasValue && entity.TenantId != _currentTenant.Id)
        {
            throw new AbpException("无权访问该教学班。");
        }

        return entity;
    }

    private async Task<Dictionary<Guid, int>> GetMemberCountsAsync(List<Guid> classIds)
    {
        if (classIds.Count == 0) return new Dictionary<Guid, int>();

        using (DataFilter.Disable<IMultiTenant>())
        {
            var members = await _memberRepository.GetListAsync(m => classIds.Contains(m.ClassId));
            return members
                .GroupBy(m => m.ClassId)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }

    private async Task<(Dictionary<Guid, string> majors, Dictionary<Guid, string> courses, Dictionary<Guid, string> teachers)>
        GetRelatedNamesAsync(List<TeachingClass> classes)
    {
        var majorIds = classes.Where(c => c.MajorId.HasValue).Select(c => c.MajorId!.Value).Distinct().ToList();
        var courseIds = classes.Where(c => c.CourseId.HasValue).Select(c => c.CourseId!.Value).Distinct().ToList();
        var teacherIds = classes.Where(c => c.TeacherId.HasValue).Select(c => c.TeacherId!.Value).Distinct().ToList();

        using (DataFilter.Disable<IMultiTenant>())
        {
            var majors = majorIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _majorRepository.GetListAsync(m => majorIds.Contains(m.Id))).ToDictionary(m => m.Id, m => m.Name);

            var courses = courseIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _courseRepository.GetListAsync(c => courseIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Title);

            var teachers = teacherIds.Count == 0
                ? new Dictionary<Guid, string>()
                : (await _userRepository.GetListAsync(u => teacherIds.Contains(u.Id))).ToDictionary(
                    u => u.Id,
                    u => string.Join(string.Empty, new[] { u.Name, u.Surname }.Where(s => !string.IsNullOrWhiteSpace(s))));

            return (majors, courses, teachers);
        }
    }

    private async Task<Dictionary<Guid, string>> GetMajorNamesAsync(List<IdentityUser> users)
    {
        var majorIds = users
            .Select(u => u.GetProperty<Guid?>("MajorId"))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (majorIds.Count == 0) return new Dictionary<Guid, string>();

        using (DataFilter.Disable<IMultiTenant>())
        {
            return (await _majorRepository.GetListAsync(m => majorIds.Contains(m.Id))).ToDictionary(m => m.Id, m => m.Name);
        }
    }

    private async Task<TeachingClassDto> ToDtoAsync(TeachingClass entity)
    {
        var counts = await GetMemberCountsAsync(new List<Guid> { entity.Id });
        var (majors, courses, teachers) = await GetRelatedNamesAsync(new List<TeachingClass> { entity });

        return new TeachingClassDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Name = entity.Name,
            MajorId = entity.MajorId,
            MajorName = entity.MajorId.HasValue ? majors.GetValueOrDefault(entity.MajorId.Value) : null,
            CourseId = entity.CourseId,
            CourseName = entity.CourseId.HasValue ? courses.GetValueOrDefault(entity.CourseId.Value) : null,
            TeacherId = entity.TeacherId,
            TeacherName = entity.TeacherId.HasValue ? teachers.GetValueOrDefault(entity.TeacherId.Value) : null,
            GradeYear = entity.GradeYear,
            Description = entity.Description,
            Status = entity.Status,
            MemberCount = counts.GetValueOrDefault(entity.Id),
            CreationTime = entity.CreationTime,
        };
    }
}
