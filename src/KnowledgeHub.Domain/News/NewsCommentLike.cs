using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.News;

public class NewsCommentLike : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CommentId { get; set; }
    public Guid UserId { get; set; }

    public NewsCommentLike()
    {
    }

    public NewsCommentLike(Guid id, Guid commentId, Guid userId)
        : base(id)
    {
        CommentId = commentId;
        UserId = userId;
    }
}
