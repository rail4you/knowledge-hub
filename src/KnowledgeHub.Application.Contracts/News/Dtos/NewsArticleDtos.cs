using System;
using KnowledgeHub.News.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.News.Dtos;

public class NewsArticleDto : FullAuditedEntityDto<Guid>
{
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? Tags { get; set; }
    public bool IsTop { get; set; }
    public bool IsHot { get; set; }
    public bool AllowComments { get; set; }
    public NewsArticleStatus Status { get; set; }
    public Guid? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public bool UserHasLiked { get; set; }
}

public class CreateUpdateNewsArticleDto
{
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? Tags { get; set; }
    public bool IsTop { get; set; }
    public bool IsHot { get; set; }
    public bool AllowComments { get; set; } = true;
}

public class PagedNewsArticleRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CategoryId { get; set; }
    public NewsArticleStatus? Status { get; set; }
}

public class ReviewNewsArticleDto
{
    public string? Comment { get; set; }
}
