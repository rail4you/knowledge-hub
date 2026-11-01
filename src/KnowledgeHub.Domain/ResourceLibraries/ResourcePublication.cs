using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 库内内容「发布 / 准入」记录（V2.0）。
///
/// <para>
/// 为什么需要它：<c>Resource.LibraryId</c> 只能表达「这个资源现在挂在哪个库」，
/// 无法表达「它是否已经被批准进入这个库、由谁提交、审了多久、为什么被驳回」。
/// 内容加入资源库需要审核，这份审核的对象正是本记录。
/// </para>
///
/// <para>
/// <b>与内容审核的关系</b>（合并审核）：
/// </para>
/// <list type="bullet">
/// <item><description>
/// 内容还是草稿 → 一条审核单同时审「内容」与「准入」，通过后内容置
/// <c>LeagueApproved</c> 且本记录置 <c>Published</c>（不会出现两次排队）。
/// </description></item>
/// <item><description>
/// 内容已审核通过 → 只审「准入」，通过后本记录置 <c>Published</c>，内容状态不动。
/// </description></item>
/// </list>
///
/// <para>
/// <b>租户归属</b>：<see cref="TenantId"/> 是<b>内容归属学校</b>（贡献方），
/// 与库的归属学校可能不同（共建库里关联学校贡献内容）。
/// </para>
/// </summary>
public class ResourcePublication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxRejectReasonLength = 1000;
    public const int MaxPolicyJsonLength = 4000;

    /// <summary>目标教学资源库。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>内容类型（资源 / 课程 / 实训）。</summary>
    public LibraryContentType ContentType { get; set; }

    /// <summary>内容实体 Id（<c>Resource.Id</c> / <c>Course.Id</c> / <c>PracticumProject.Id</c>）。</summary>
    public Guid ContentId { get; set; }

    /// <summary>固定的内容版本（资源填 <c>ResourceVersion.Id</c>；课程/实训暂为空）。</summary>
    public Guid? ContentVersionId { get; set; }

    /// <summary>内容归属学校（贡献方）。</summary>
    public Guid? ContentTenantId { get; set; }

    /// <summary>当前准入状态。</summary>
    public ResourcePublicationStatus Status { get; set; } = ResourcePublicationStatus.Draft;

    /// <summary>贡献方类型（学校 / 企业 / 平台）。</summary>
    public PublicationSubmitterType SubmitterType { get; set; } = PublicationSubmitterType.School;

    /// <summary>关联的审核单（一条准入对应一条审核单；重提沿用同一单）。</summary>
    public Guid? ReviewTicketId { get; set; }

    /// <summary>
    /// 共享策略快照（JSON：scope / preview / download / reference / derivative）。
    /// 准入通过时冻结，后续扩权需另走授权变更。
    /// </summary>
    public string? PolicyJson { get; set; }

    /// <summary>通过时间（库内可见的起点）。</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>最近一次驳回原因（展示给提交人）。</summary>
    public string? RejectReason { get; set; }

    /// <summary>内容归属租户（ABP 多租户过滤器维度）。</summary>
    public Guid? TenantId { get; set; }

    public ResourcePublication()
    {
    }

    public ResourcePublication(
        Guid id,
        Guid libraryId,
        LibraryContentType contentType,
        Guid contentId,
        Guid? contentTenantId,
        PublicationSubmitterType submitterType = PublicationSubmitterType.School)
        : base(id)
    {
        LibraryId = libraryId;
        ContentType = contentType;
        ContentId = contentId;
        ContentTenantId = contentTenantId;
        TenantId = contentTenantId;
        SubmitterType = submitterType;
        Status = ResourcePublicationStatus.Draft;
    }

    /// <summary>提交审核：进入审核中，并绑定审核单。</summary>
    public void MarkPendingReview(Guid ticketId)
    {
        Status = ResourcePublicationStatus.PendingReview;
        ReviewTicketId = ticketId;
        RejectReason = null;
    }

    /// <summary>审核通过：库内可见。</summary>
    public void MarkPublished(string? policyJson, Guid? contentVersionId = null)
    {
        Status = ResourcePublicationStatus.Published;
        PublishedAt = DateTime.UtcNow;
        RejectReason = null;

        if (!string.IsNullOrWhiteSpace(policyJson))
        {
            PolicyJson = policyJson.Length > MaxPolicyJsonLength
                ? policyJson[..MaxPolicyJsonLength]
                : policyJson;
        }

        if (contentVersionId.HasValue)
        {
            ContentVersionId = contentVersionId.Value;
        }
    }

    /// <summary>驳回：保留记录，提交人可修改后重提。</summary>
    public void MarkRejected(string? reason)
    {
        Status = ResourcePublicationStatus.Rejected;
        RejectReason = Truncate(reason);
    }

    /// <summary>下架：从库内撤出（不删除记录，保留可追溯）。</summary>
    public void MarkWithdrawn(string? reason = null)
    {
        Status = ResourcePublicationStatus.Withdrawn;
        RejectReason = Truncate(reason);
    }

    private static string? Truncate(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var trimmed = reason.Trim();
        return trimmed.Length > MaxRejectReasonLength ? trimmed[..MaxRejectReasonLength] : trimmed;
    }
}
