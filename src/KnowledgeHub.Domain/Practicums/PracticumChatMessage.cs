using System;
using KnowledgeHub.Practicums.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Practicums;

public class PracticumChatMessage : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? SenderId { get; set; }
    public PracticumChatSenderType SenderType { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public PracticumChatMessageType MessageType { get; set; } = PracticumChatMessageType.Text;
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public long? AttachmentSize { get; set; }
    public bool IsAgentReply { get; set; }

    public PracticumChatMessage()
    {
    }

    public PracticumChatMessage(
        Guid id,
        Guid projectId,
        Guid? senderId,
        PracticumChatSenderType senderType,
        string senderName,
        string content,
        PracticumChatMessageType messageType = PracticumChatMessageType.Text)
        : base(id)
    {
        ProjectId = projectId;
        SenderId = senderId;
        SenderType = senderType;
        SenderName = senderName;
        Content = content;
        MessageType = messageType;
    }
}
