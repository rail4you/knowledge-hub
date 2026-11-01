using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>跨校引用状态。</summary>
public enum CourseSharedRefStatus : byte
{
    /// <summary>生效中：目标课程的学生可学习。</summary>
    Active = 0,

    /// <summary>已撤销：来源下架 / 授权撤回 / 教师手动撤销，课程页显示"暂不可用"。</summary>
    Revoked = 1
}

/// <summary>
/// 跨校引用（V2.0 主线闭环的最后一环）。
///
/// <para>
/// 场景：B 校教师在共建库里看到 A 校发布的资源 → 引用进本校课程 →
/// B 校学生在原课程中直接学习，而 A 校看不到 B 校学生明细。
/// </para>
///
/// <para>
/// <b>为什么不把文件地址写进课程表</b>：来源方可能下架、授权可能撤回、版本会升级。
/// 引用只记录"哪个库的哪个内容、哪个版本"，播放时再按实时权限鉴权，
/// 撤销后立即生效（比签发短效 URL 更可靠）。
/// </para>
///
/// <para>
/// <b>版本固定</b>：<see cref="ContentVersionId"/> 记录引用时的版本；升级是显式动作，
/// 旧版本信息存入 <c>ExtraProperties.PreviousVersionId</c> 以便追溯。
/// </para>
/// </summary>
public class CourseSharedRef : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxRevokedReasonLength = 500;

    /// <summary>接收学校（引用方）。</summary>
    public Guid TargetTenantId { get; set; }

    /// <summary>目标课程（引用方本校课程）。</summary>
    public Guid CourseId { get; set; }

    /// <summary>目标章节（可空：挂在课程下而非具体章节）。</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>来源资源库。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>内容类型（一期只支持文件资源）。</summary>
    public LibraryContentType ContentType { get; set; } = LibraryContentType.Resource;

    /// <summary>来源内容 Id。</summary>
    public Guid ContentId { get; set; }

    /// <summary>引用的内容版本（固定）。</summary>
    public Guid? ContentVersionId { get; set; }

    /// <summary>来源学校。</summary>
    public Guid SourceTenantId { get; set; }

    public CourseSharedRefStatus Status { get; set; } = CourseSharedRefStatus.Active;

    public Guid? RevokedBy { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    /// <summary>接收方租户（ABP 多租户过滤维度）。</summary>
    public Guid? TenantId { get; set; }

    public CourseSharedRef()
    {
    }

    public CourseSharedRef(
        Guid id,
        Guid targetTenantId,
        Guid courseId,
        Guid? chapterId,
        Guid libraryId,
        Guid contentId,
        Guid? contentVersionId,
        Guid sourceTenantId)
        : base(id)
    {
        TargetTenantId = targetTenantId;
        TenantId = targetTenantId;
        CourseId = courseId;
        ChapterId = chapterId;
        LibraryId = libraryId;
        ContentId = contentId;
        ContentVersionId = contentVersionId;
        SourceTenantId = sourceTenantId;
        Status = CourseSharedRefStatus.Active;
    }

    /// <summary>撤销引用（保留记录，课程页据此显示"暂不可用"）。</summary>
    public void Revoke(Guid? actorId, string? reason)
    {
        Status = CourseSharedRefStatus.Revoked;
        RevokedBy = actorId;
        RevokedAt = DateTime.UtcNow;
        RevokedReason = string.IsNullOrWhiteSpace(reason)
            ? null
            : (reason.Trim().Length > MaxRevokedReasonLength ? reason.Trim()[..MaxRevokedReasonLength] : reason.Trim());
    }

    /// <summary>升级到新版本（旧版本号记入 ExtraProperties 以便追溯）。</summary>
    public void Upgrade(Guid? newVersionId)
    {
        if (newVersionId.HasValue && newVersionId == ContentVersionId)
        {
            throw new UserFriendlyException("新版本与当前引用版本相同，无需升级。");
        }

        if (ContentVersionId.HasValue)
        {
            ExtraProperties["PreviousVersionId"] = ContentVersionId.ToString();
        }

        ContentVersionId = newVersionId;
    }
}

/// <summary>资源使用事件（去重统计用）。</summary>
public class ResourceUsageEvent : CreationAuditedEntity<Guid>, IMultiTenant
{
    public ResourceUsageEvent()
    {
    }

    public ResourceUsageEvent(Guid id)
        : base(id)
    {
    }

    public const int MaxTypeLength = 64;

    /// <summary>幂等键（同一事件重复上报只记一次）。</summary>
    public Guid EventId { get; set; }

    /// <summary>使用方学校。</summary>
    public Guid? SchoolId { get; set; }

    public Guid? CourseId { get; set; }

    /// <summary>关联的跨校引用（本校使用时为空）。</summary>
    public Guid? RefId { get; set; }

    public Guid? ResourceId { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>事件类型：study / preview / download / reference。</summary>
    public string Type { get; set; } = "study";

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Guid? TenantId { get; set; }
}
