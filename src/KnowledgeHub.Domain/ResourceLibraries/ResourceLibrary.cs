using System;
using System.Collections.Generic;
using KnowledgeHub.ResourceLibraries;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库 —— 租户（学校）级实体：一系列<b>文件资源、课程、实训</b>的关联集合。
///
/// <para>
/// 需求「目前的租户其实是对应学校 … 租户（学校）下可以有多个资源库，资源库可以是私有、公开、共建」：
/// 库归属某所学校（<see cref="OwnerTenantId"/>，即<b>主学校</b>），由该校的院校管理员维护。
/// </para>
///
/// <para>
/// 数据模型要点：
/// </para>
/// <list type="number">
/// <item><description>
/// 可见性由 <see cref="Visibility"/> 决定：<b>私有</b>（仅被分配教师）、
/// <b>公开</b>（本校全体教师）、<b>院校共建</b>（主学校 + 关联学校共享）。
/// </description></item>
/// <item><description>
/// <b>教师分配</b>：通过 <c>ResourceLibraryTeacher</c> 记录，一个教师可在多个库中；
/// </description></item>
/// <item><description>
/// <b>关联学校</b>：通过 <see cref="Members"/>（<c>ResourceLibraryMember</c>）记录，
/// 仅在 <see cref="ResourceLibraryVisibility.CoBuilt"/> 下有意义；
/// </description></item>
/// <item><description>
/// <b>专业维度</b>：<see cref="PrimaryMajorId"/> 可选。留空表示不限专业；
/// 设置后库内课程/资源的专业必须与之一致（由 <c>ResourceLibraryAttachmentValidator</c> 校验）。
/// </description></item>
/// </list>
///
/// <para>
/// 库内内容归属各内容自身的租户：共建库中关联学校教师共享的内容仍留在关联学校租户，
/// 只通过 <c>LibraryId</c> 跨租户指向本库 —— 因此跨租户读取库内容时必须禁用 ABP 租户过滤器。
/// </para>
/// </summary>
public class ResourceLibrary : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxNameLength = 256;
    public const int MaxDescriptionLength = 2000;
    public const int MaxCoverUrlLength = 1000;
    public const int MaxPrincipalLength = 128;

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

    /// <summary>
    /// 建设负责人（自然人姓名，文本字段）。
    ///
    /// <para>
    /// 与 <see cref="OwnerTenantId"/>（主建学校）区分：后者是组织维度（库归属哪所学校），
    /// 这里是库的建设责任人，用于门户展示「主建学校 · 建设负责人 · N 校共建」。
    /// </para>
    /// </summary>
    public string? Principal { get; set; }

    /// <summary>库状态。</summary>
    public ResourceLibraryStatus Status { get; set; } = ResourceLibraryStatus.Draft;

    /// <summary>
    /// 库可见性（私有 / 公开 / 院校共建）。
    ///
    /// <para>
    /// 默认 <see cref="ResourceLibraryVisibility.Private"/>：新建库默认私有，
    /// 需要由院校管理员显式分配教师；改成公开 / 共建后参与方全体教师可见。
    /// </para>
    /// </summary>
    public ResourceLibraryVisibility Visibility { get; set; } = ResourceLibraryVisibility.Private;

    /// <summary>
    /// 是否在门户公开展示。
    ///
    /// <para>
    /// 与 <see cref="Visibility"/> 正交：<see cref="Visibility"/> 管「校内 / 校际谁能看」，
    /// 本字段管「访客 / 社会学校在门户上能不能看到」。
    /// </para>
    /// </summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>排序值，越小越靠前。</summary>
    public int SortOrder { get; set; }

    /// <summary>共建成员（主学校 + 关联学校）。仅共建库有意义。</summary>
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

    /// <summary>设置建设负责人（自然人姓名）。</summary>
    public void SetPrincipal(string? principal)
    {
        Principal = string.IsNullOrWhiteSpace(principal)
            ? null
            : Volo.Abp.Check.Length(principal.Trim(), nameof(Principal), maxLength: MaxPrincipalLength);
    }

    public void SetStatus(ResourceLibraryStatus status)
    {
        Status = status;
    }

    /// <summary>
    /// 设置库可见性（私有 / 公开 / 院校共建）。
    /// </summary>
    public void SetVisibility(ResourceLibraryVisibility visibility)
    {
        Visibility = visibility;
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
