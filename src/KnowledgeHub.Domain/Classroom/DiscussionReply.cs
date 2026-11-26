using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>讨论主题下的回复（支持一层「回复某条回复」，用于追问）。</summary>
public class DiscussionReply : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid DiscussionId { get; set; }

    /// <summary>父回复 Id（二级回复；为空表示直接回复主题）。</summary>
    public Guid? ParentReplyId { get; set; }

    public string Content { get; set; } = string.Empty;

    public Guid AuthorId { get; set; }

    public string? AuthorName { get; set; }

    /// <summary>被学生采纳为最佳答案。</summary>
    public bool IsAccepted { get; set; }

    private DiscussionReply() { }

    public DiscussionReply(Guid id, Guid discussionId, string content, Guid authorId) : base(id)
    {
        DiscussionId = discussionId;
        Content = content;
        AuthorId = authorId;
    }
}
