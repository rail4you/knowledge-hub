using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 实训项目 ↔ 教学资源库 关联（多对多）。
///
/// <para>实训与课程相关，因此可选库限定为「已含课程」的库；
/// <c>PracticumProject.LibraryId</c> 保留为「主资源库」镜像。</para>
/// </summary>
public class ResourceLibraryPracticum : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid LibraryId { get; set; }
    public Guid PracticumProjectId { get; set; }
    /// <summary>实训所属租户（学校）Id。</summary>
    public Guid? TenantId { get; set; }

    public ResourceLibraryPracticum()
    {
    }

    public ResourceLibraryPracticum(Guid id, Guid libraryId, Guid practicumProjectId, Guid? tenantId)
        : base(id)
    {
        LibraryId = libraryId;
        PracticumProjectId = practicumProjectId;
        TenantId = tenantId;
    }
}
