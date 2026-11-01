using System;
using System.Collections.Generic;
using KnowledgeHub.ResourceLibraries;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.ResourceLibraries.Dtos;

/// <summary>跨校引用（列表 / 详情）。</summary>
public class CourseSharedRefDto
{
    public Guid Id { get; set; }
    public Guid TargetTenantId { get; set; }
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid LibraryId { get; set; }
    public string? LibraryName { get; set; }
    public LibraryContentType ContentType { get; set; }
    public Guid ContentId { get; set; }
    public string? ContentName { get; set; }
    public Guid? ContentVersionId { get; set; }
    public Guid SourceTenantId { get; set; }
    public string? SourceTenantName { get; set; }
    public CourseSharedRefStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? RevokedReason { get; set; }
    public DateTime CreationTime { get; set; }

    /// <summary>当前用户是否可撤销/升级（引用方本校教师或管理员）。</summary>
    public bool CanManage { get; set; }
}

public class CreateCourseSharedRefDto
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }

    /// <summary>来源库（用于校验授权范围）。</summary>
    public Guid LibraryId { get; set; }

    public Guid ContentId { get; set; }

    /// <summary>固定版本（可空 → 取当前版本）。</summary>
    public Guid? ContentVersionId { get; set; }
}

public class UpgradeCourseSharedRefDto
{
    public Guid? ContentVersionId { get; set; }
}

/// <summary>学生/课程播放器访问判定结果。</summary>
public class CourseSharedRefAccessDto
{
    public bool CanAccess { get; set; }
    public string? Reason { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid? LibraryId { get; set; }
    public Guid? SourceTenantId { get; set; }
}

/// <summary>引用使用统计（跨校应用口径）。</summary>
public class SharedRefStatisticsDto
{
    /// <summary>我的资源被多少所学校引用（去重）。</summary>
    public int ReferencedSchoolCount { get; set; }

    /// <summary>我的资源被引用的课程数（去重）。</summary>
    public int ReferencedCourseCount { get; set; }

    /// <summary>当前生效的引用总数。</summary>
    public int ActiveRefCount { get; set; }

    /// <summary>已撤销的引用总数。</summary>
    public int RevokedRefCount { get; set; }

    /// <summary>授权学习人数（去重）。</summary>
    public int LearnerCount { get; set; }
}

/// <summary>引用列表查询。</summary>
public class CourseSharedRefListRequestDto : PagedResultRequestDto
{
    public Guid? CourseId { get; set; }

    public CourseSharedRefStatus? Status { get; set; }

    /// <summary>incoming = 别人引用了我的；outgoing = 我引用了别人的；all = 两者（默认）。</summary>
    public string? Direction { get; set; }
}

/// <summary>上报使用事件（幂等）。</summary>
public class ReportUsageEventDto
{
    public Guid EventId { get; set; }
    public Guid? CourseId { get; set; }
    public Guid? RefId { get; set; }
    public Guid? ResourceId { get; set; }
    public string Type { get; set; } = "study";
}
