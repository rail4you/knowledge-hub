using System;
using KnowledgeHub.News.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.News.Dtos;

public class NewsCommentDto : FullAuditedEntityDto<Guid>
{
    public Guid ArticleId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public string Content { get; set; } = string.Empty;
    public NewsCommentStatus Status { get; set; }
}

public class CreateNewsCommentDto
{
    public Guid ArticleId { get; set; }
    public Guid? ParentId { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class ReviewNewsCommentDto
{
    public NewsCommentStatus Status { get; set; }
}

public class PagedNewsCommentRequestDto : PagedAndSortedResultRequestDto
{
    public Guid? ArticleId { get; set; }
    public NewsCommentStatus? Status { get; set; }
    public string? Filter { get; set; }
}
