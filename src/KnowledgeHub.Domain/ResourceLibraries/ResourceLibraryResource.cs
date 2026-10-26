using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 资源 ↔ 教学资源库 关联（多对多）。
///
/// <para>
/// 需求「一个文件资源可以关联多个资源库；新建 / 编辑资源时可以勾选关联的资源库」：
/// 原模型里 <c>Resource.LibraryId</c> 只能表达「一个资源属于一个库」，
/// 本表补上多对多维度 —— 一个资源可同时挂进多个库，也可一个都不挂（= 未分类）。
/// </para>
///
/// <para>
/// <b>与 <c>Resource.LibraryId</c> 的关系</b>：该字段保留为「主资源库」镜像
/// （= 本表中排序最靠前的库），用于兼容既有按库归属查询的页面与统计；
/// 以本表为准的读写会同步维护它。
/// </para>
///
/// <para>
/// <see cref="TenantId"/> 是<b>资源所属学校</b>（内容归属方），与库的归属租户可能不同：
/// 共建库中关联学校可以把本校内容挂进主建学校的库。
/// </para>
/// </summary>
public class ResourceLibraryResource : FullAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>教学资源库 Id。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>文件资源 Id。</summary>
    public Guid ResourceId { get; set; }

    /// <summary>资源所属租户（学校）Id。</summary>
    public Guid? TenantId { get; set; }

    public ResourceLibraryResource()
    {
    }

    public ResourceLibraryResource(Guid id, Guid libraryId, Guid resourceId, Guid? tenantId)
        : base(id)
    {
        LibraryId = libraryId;
        ResourceId = resourceId;
        TenantId = tenantId;
    }
}
