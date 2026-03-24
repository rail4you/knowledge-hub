using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.AI;

public class ChatThread : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public string? SessionData { get; set; }
    
    public ICollection<ChatMessage> Messages { get; set; }
    
    public ChatThread()
    {
        Messages = new List<ChatMessage>();
    }
    
    public ChatThread(Guid id, Guid userId) : base(id)
    {
        UserId = userId;
        Messages = new List<ChatMessage>();
    }
    
    public void UpdateSessionData(string sessionData)
    {
        SessionData = sessionData;
    }
    
    public void SetTitle(string title)
    {
        Title = title;
    }
}
