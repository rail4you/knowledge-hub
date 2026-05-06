using System;
using KnowledgeHub.News.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.News;

public class NewsAudit : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ArticleId { get; set; }
    public NewsArticleStatus FromStatus { get; set; }
    public NewsArticleStatus ToStatus { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public Guid? OperatorId { get; set; }

    public NewsAudit()
    {
    }

    public NewsAudit(
        Guid id,
        Guid articleId,
        NewsArticleStatus fromStatus,
        NewsArticleStatus toStatus,
        string action)
        : base(id)
    {
        ArticleId = articleId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Action = action;
    }
}
