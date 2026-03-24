using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.AI;

public class ChatMessage : AuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ThreadId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ToolCalls { get; set; }
    
    public ChatThread? Thread { get; set; }
    
    private ChatMessage() { }
    
    public ChatMessage(Guid id, Guid threadId, string role, string content) : base(id)
    {
        ThreadId = threadId;
        Role = role;
        Content = content;
    }
}
