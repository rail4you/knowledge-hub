using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 课程 ↔ 教学资源库 关联（多对多）。
///
/// <para>与 <c>ResourceLibraryResource</c> 同构：一个课程可同时挂进多个资源库，
/// 也可一个都不挂。<c>Course.LibraryId</c> 保留为「主资源库」镜像。</para>
/// </summary>
public class ResourceLibraryCourse : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid LibraryId { get; set; }
    public Guid CourseId { get; set; }
    /// <summary>课程所属租户（学校）Id。</summary>
    public Guid? TenantId { get; set; }

    public ResourceLibraryCourse()
    {
    }

    public ResourceLibraryCourse(Guid id, Guid libraryId, Guid courseId, Guid? tenantId)
        : base(id)
    {
        LibraryId = libraryId;
        CourseId = courseId;
        TenantId = tenantId;
    }
}
