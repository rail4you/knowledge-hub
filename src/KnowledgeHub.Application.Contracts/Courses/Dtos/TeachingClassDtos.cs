using System;
using System.Collections.Generic;
using KnowledgeHub.Courses.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Courses.Dtos;

/// <summary>教学班列表 / 详情 DTO</summary>
public class TeachingClassDto : EntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; }
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    public Guid? CourseId { get; set; }
    public string? CourseName { get; set; }
    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public int? GradeYear { get; set; }
    public string? Description { get; set; }
    public TeachingClassStatus Status { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreationTime { get; set; }
}

/// <summary>教学班成员 DTO</summary>
public class TeachingClassMemberDto : EntityDto<Guid>
{
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentNumber { get; set; }
    public string? UserName { get; set; }
    public string? MajorName { get; set; }
    public TeachingClassMemberSource Source { get; set; }
    public DateTime JoinedAt { get; set; }
}

/// <summary>查询教学班</summary>
public class GetTeachingClassesInput : PagedAndSortedResultRequestDto
{
    /// <summary>关键词：匹配班级名 / 教师名 / 学号</summary>
    public string? Filter { get; set; }
    public Guid? MajorId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? TeacherId { get; set; }
    public TeachingClassStatus? Status { get; set; }
    /// <summary>指定租户（仅宿主管理员可用；租户管理员固定为本租户）</summary>
    public Guid? TenantId { get; set; }
}

/// <summary>新建教学班</summary>
public class CreateTeachingClassDto
{
    public string Name { get; set; }
    public Guid? MajorId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? TeacherId { get; set; }
    public int? GradeYear { get; set; }
    public string? Description { get; set; }
    public TeachingClassStatus Status { get; set; } = TeachingClassStatus.Active;
}

/// <summary>编辑教学班</summary>
public class UpdateTeachingClassDto : CreateTeachingClassDto
{
}

/// <summary>批量添加班级成员</summary>
public class AddTeachingClassMembersDto
{
    /// <summary>要加入的学生 Id</summary>
    public List<Guid> StudentIds { get; set; } = new();
    /// <summary>成员来源</summary>
    public TeachingClassMemberSource Source { get; set; } = TeachingClassMemberSource.Manual;
}

/// <summary>班级成员查询</summary>
public class GetTeachingClassMembersInput : PagedAndSortedResultRequestDto
{
    public Guid ClassId { get; set; }
    public string? Filter { get; set; }
}
