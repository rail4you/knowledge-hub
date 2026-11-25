using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 课程级「课程思政」设置 —— 本模块与课程体系的**绑定开关**。
///
/// <para>一门课一行：是否启用（启用后课程工作台 / 课程详情出现「课程思政」入口）、
/// 以及课程思政总目标（写教案、导出材料时用到）。</para>
///
/// <para>注意：这里只持有 <c>CourseId</c>，不改 <c>Course</c> 实体 ——
/// 删掉本表即解除绑定，课程功能不受影响。</para>
/// </summary>
public class CourseIdeologySetting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>课程 Id（租户内唯一）。</summary>
    public Guid CourseId { get; set; }

    /// <summary>是否对本课程启用课程思政（关闭后入口隐藏，数据保留）。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>课程思政总目标（如「树立法治意识与安全底线思维」）。</summary>
    public string? IdeologyGoal { get; set; }

    /// <summary>备注 / 建设思路。</summary>
    public string? Note { get; set; }

    protected CourseIdeologySetting()
    {
    }

    public CourseIdeologySetting(Guid id, Guid courseId, Guid? tenantId) : base(id)
    {
        CourseId = courseId;
        TenantId = tenantId;
    }
}
