using System;
using System.Collections.Generic;
using KnowledgeHub.ResourceLibraries;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 资源库 —— 主建学校拥有的租户级实体，通过成员表实现「多校共建」。
///
/// <para>
/// 数据模型要点（与需求「资源库可以多校共建，体现主建学校（专业），和合作学校」对应）：
/// </para>
/// <list type="number">
/// <item><description>
/// 库本身是<b>主建学校</b>的租户级实体（<see cref="TenantId"/> = 主建学校租户），
/// 因此库内资源/课程依然归属主建学校，ABP 租户过滤器不被打破；
/// </description></item>
/// <item><description>
/// <b>专业维度</b>：<see cref="PrimaryMajorId"/> 指定该库的主建专业，
/// 挂进库的课程/资源/习题必须落在该专业上（由 AppService 校验）；
/// </description></item>
/// <item><description>
/// <b>合作学校</b>：通过 <see cref="Members"/>（<c>ResourceLibraryMember</c>）记录，
/// 合作学校在跨租户查询时可获得库内内容的可见性；
/// </description></item>
/// </list>
/// </summary>
public class ResourceLibrary : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxNameLength = 256;
    public const int MaxDescriptionLength = 2000;
    public const int MaxCoverUrlLength = 1000;

    /// <summary>库归属租户（= 主建学校）。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>资源库名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>资源库简介。</summary>
    public string? Description { get; set; }

    /// <summary>封面图 URL。</summary>
    public string? CoverUrl { get; set; }

    /// <summary>主建专业 Id（库的专业维度）。</summary>
    public Guid? PrimaryMajorId { get; set; }

    /// <summary>主建学校租户 Id（冗余存储，便于跨租户按主建学校检索）。</summary>
    public Guid OwnerTenantId { get; set; }

    /// <summary>库状态。</summary>
    public ResourceLibraryStatus Status { get; set; } = ResourceLibraryStatus.Draft;

    /// <summary>是否在门户公开展示。</summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>排序值，越小越靠前。</summary>
    public int SortOrder { get; set; }

    /// <summary>共建成员（主建学校 + 合作学校）。</summary>
    public ICollection<ResourceLibraryMember> Members { get; set; } = new List<ResourceLibraryMember>();

    public ResourceLibrary()
    {
    }

    public ResourceLibrary(Guid id, Guid ownerTenantId, string name)
        : base(id)
    {
        OwnerTenantId = ownerTenantId;
        TenantId = ownerTenantId;
        Name = name;
        Status = ResourceLibraryStatus.Draft;
    }

    public void SetName(string name)
    {
        Name = Volo.Abp.Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: MaxNameLength);
    }

    public void SetDescription(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description)
            ? null
            : Volo.Abp.Check.Length(description.Trim(), nameof(description), maxLength: MaxDescriptionLength);
    }

    public void SetCoverUrl(string? coverUrl)
    {
        CoverUrl = string.IsNullOrWhiteSpace(coverUrl)
            ? null
            : Volo.Abp.Check.Length(coverUrl.Trim(), nameof(coverUrl), maxLength: MaxCoverUrlLength);
    }

    public void SetPrimaryMajorId(Guid? primaryMajorId)
    {
        PrimaryMajorId = primaryMajorId;
    }

    public void SetStatus(ResourceLibraryStatus status)
    {
        Status = status;
    }

    public void SetIsPublic(bool isPublic)
    {
        IsPublic = isPublic;
    }

    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }
}
