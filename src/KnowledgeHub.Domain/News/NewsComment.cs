using System;
using KnowledgeHub.News.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.News;

public class NewsComment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ArticleId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public NewsCommentStatus Status { get; set; } = NewsCommentStatus.Approved;

    public NewsComment()
    {
    }

    public NewsComment(Guid id, Guid articleId, Guid userId, string content)
        : base(id)
    {
        ArticleId = articleId;
        UserId = userId;
        Content = content;
    }
}
