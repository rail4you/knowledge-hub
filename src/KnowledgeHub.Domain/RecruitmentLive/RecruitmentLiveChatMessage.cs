using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.RecruitmentLive;

/// <summary>招聘直播聊天消息持久化</summary>
public class RecruitmentLiveChatMessage : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>所属直播 ID</summary>
    public Guid LiveId { get; set; }

    /// <summary>发送者身份：teacher / student</summary>
    public string SenderRole { get; set; } = string.Empty;

    /// <summary>发送者用户 ID</summary>
    public Guid SenderId { get; set; }

    /// <summary>消息内容</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>发送时间</summary>
    public DateTime SentAt { get; set; }

    protected RecruitmentLiveChatMessage() { }

    public RecruitmentLiveChatMessage(Guid id, Guid liveId, string senderRole, Guid senderId, string content)
        : base(id)
    {
        LiveId = liveId;
        SenderRole = senderRole;
        SenderId = senderId;
        Content = content;
        SentAt = DateTime.UtcNow;
    }
}
