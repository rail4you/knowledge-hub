using System;
using KnowledgeHub.Practicums.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Practicums.Dtos;

public class PracticumChatMessageDto : EntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public Guid? SenderId { get; set; }
    public PracticumChatSenderType SenderType { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public PracticumChatMessageType MessageType { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public long? AttachmentSize { get; set; }
    public bool IsAgentReply { get; set; }
    public DateTime CreationTime { get; set; }
}

public class SendPracticumChatMessageDto
{
    public Guid ProjectId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public long? AttachmentSize { get; set; }
    public PracticumChatMessageType MessageType { get; set; } = PracticumChatMessageType.Text;
}

public class GetPracticumChatMessagesDto : LimitedResultRequestDto
{
    public Guid ProjectId { get; set; }
    public Guid? BeforeId { get; set; }
}

public class PracticumAgentConfigDto
{
    public string? AgentName { get; set; }
    public string? AgentPrompt { get; set; }
}

public class UpdatePracticumAgentConfigDto
{
    public string? AgentName { get; set; }
    public string? AgentPrompt { get; set; }
}
