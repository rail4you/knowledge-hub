using System;
using KnowledgeHub.News.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.News;

public class NewsArticle : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? Tags { get; set; }
    public bool IsTop { get; set; }
    public bool IsHot { get; set; }
    public bool AllowComments { get; set; } = true;
    public NewsArticleStatus Status { get; set; } = NewsArticleStatus.Draft;
    public Guid? AuthorId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }

    public NewsArticle()
    {
    }

    public NewsArticle(Guid id, Guid categoryId, string title, string content)
        : base(id)
    {
        CategoryId = categoryId;
        Title = title;
        Content = content;
    }
}
