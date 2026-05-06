using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.News;

public class NewsReaction : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ArticleId { get; set; }
    public Guid UserId { get; set; }

    public NewsReaction()
    {
    }

    public NewsReaction(Guid id, Guid articleId, Guid userId)
        : base(id)
    {
        ArticleId = articleId;
        UserId = userId;
    }
}
