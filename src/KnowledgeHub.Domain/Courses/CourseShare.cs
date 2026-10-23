using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程共享记录：把源租户（学校）的课程共享给目标租户。
///
/// <para>
/// 与 <see cref="KnowledgeHub.Resources.ResourceShare"/> 同构：
/// 一门课程可共享给多个目标租户（多条记录），目标租户通过「共享给我的课程」列表消费。
/// 记录的 <see cref="TenantId"/> = 源租户，ABP 租户过滤器天然按源租户隔离；
/// 目标租户查询时禁用租户过滤器并按 <see cref="TargetTenantId"/> 精确匹配。
/// </para>
///
/// <para>
/// 典型用法：把课程共享到平台唯一的社会学校（<c>KnowledgeHub.PublicSchool.TenantId</c>），
/// 让公开注册的社会学习者也能学习本校课程。
/// </para>
/// </summary>
public class CourseShare : Entity<Guid>, IMultiTenant
{
    /// <summary>被共享的课程 Id。</summary>
    public Guid CourseId { get; set; }

    /// <summary>源租户（课程所有者）Id。</summary>
    public Guid SourceTenantId { get; set; }

    /// <summary>目标租户 Id。</summary>
    public Guid TargetTenantId { get; set; }

    /// <summary>执行共享的用户 Id。</summary>
    public Guid SharedByUserId { get; set; }

    /// <summary>共享时间。</summary>
    public DateTime SharedAt { get; set; }

    /// <summary>共享备注。</summary>
    public string? Note { get; set; }

    /// <summary>记录归属租户（= 源租户）。</summary>
    public Guid? TenantId { get; set; }

    public CourseShare()
    {
    }

    public CourseShare(
        Guid id,
        Guid courseId,
        Guid sourceTenantId,
        Guid targetTenantId,
        Guid sharedByUserId,
        string? note = null)
        : base(id)
    {
        CourseId = courseId;
        SourceTenantId = sourceTenantId;
        TargetTenantId = targetTenantId;
        SharedByUserId = sharedByUserId;
        Note = note;
        SharedAt = DateTime.UtcNow;
        TenantId = sourceTenantId;
    }
}
