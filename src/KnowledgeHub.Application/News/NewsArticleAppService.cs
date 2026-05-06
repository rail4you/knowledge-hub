using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.News.Dtos;
using KnowledgeHub.News.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace KnowledgeHub.News;

public class NewsArticleAppService : KnowledgeHubAppService, INewsArticleAppService
{
    private readonly IRepository<NewsArticle, Guid> _articleRepository;
    private readonly IRepository<NewsCategory, Guid> _categoryRepository;
    private readonly IRepository<NewsAudit, Guid> _auditRepository;
    private readonly IRepository<NewsComment, Guid> _commentRepository;
    private readonly IRepository<NewsReaction, Guid> _reactionRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ICurrentUser _currentUser;

    public NewsArticleAppService(
        IRepository<NewsArticle, Guid> articleRepository,
        IRepository<NewsCategory, Guid> categoryRepository,
        IRepository<NewsAudit, Guid> auditRepository,
        IRepository<NewsComment, Guid> commentRepository,
        IRepository<NewsReaction, Guid> reactionRepository,
        IRepository<IdentityUser, Guid> userRepository,
        ICurrentUser currentUser)
    {
        _articleRepository = articleRepository;
        _categoryRepository = categoryRepository;
        _auditRepository = auditRepository;
        _commentRepository = commentRepository;
        _reactionRepository = reactionRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    public async Task<NewsArticleDto> GetAsync(Guid id)
    {
        var article = await _articleRepository.GetAsync(id);
        article.ViewCount += 1;
        await _articleRepository.UpdateAsync(article, autoSave: true);

        return await MapToDtoAsync(article);
    }

    [Authorize(KnowledgeHubPermissions.News.Default)]
    public async Task<PagedResultDto<NewsArticleDto>> GetListAsync(PagedNewsArticleRequestDto input)
    {
        var query = await _articleRepository.GetQueryableAsync();
        query = ApplyArticleFilter(query, input, onlyPublished: false);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.IsTop)
            .ThenByDescending(x => x.PublishedAt ?? x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<NewsArticleDto>(totalCount, await MapToDtosAsync(items));
    }

    public async Task<PagedResultDto<NewsArticleDto>> GetPublishedListAsync(PagedNewsArticleRequestDto input)
    {
        var query = await _articleRepository.GetQueryableAsync();
        query = ApplyArticleFilter(query, input, onlyPublished: true);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.IsTop)
            .ThenByDescending(x => x.PublishedAt ?? x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<NewsArticleDto>(totalCount, await MapToDtosAsync(items));
    }

    public async Task<List<NewsArticleDto>> GetHotListAsync(int maxCount = 10)
    {
        var query = await _articleRepository.GetQueryableAsync();
        var items = await query
            .Where(x => x.Status == NewsArticleStatus.Published)
            .OrderByDescending(x => x.IsHot)
            .ThenByDescending(x => x.ViewCount)
            .ThenByDescending(x => x.LikeCount)
            .Take(maxCount)
            .ToListAsync();

        return await MapToDtosAsync(items);
    }

    [Authorize(KnowledgeHubPermissions.News.Create)]
    public async Task<NewsArticleDto> CreateAsync(CreateUpdateNewsArticleDto input)
    {
        await EnsureCategoryExistsAsync(input.CategoryId);

        var article = new NewsArticle(GuidGenerator.Create(), input.CategoryId, input.Title.Trim(), input.Content.Trim())
        {
            Summary = input.Summary?.Trim(),
            CoverImageUrl = input.CoverImageUrl?.Trim(),
            Tags = input.Tags?.Trim(),
            IsTop = input.IsTop,
            IsHot = input.IsHot,
            AllowComments = input.AllowComments,
            Status = NewsArticleStatus.Draft,
            AuthorId = _currentUser.Id,
            TenantId = CurrentTenant.Id
        };

        await _articleRepository.InsertAsync(article, autoSave: true);
        await AddAuditAsync(article, NewsArticleStatus.Draft, NewsArticleStatus.Draft, "Create", "创建资讯");

        return await MapToDtoAsync(article);
    }

    [Authorize(KnowledgeHubPermissions.News.Edit)]
    public async Task<NewsArticleDto> UpdateAsync(Guid id, CreateUpdateNewsArticleDto input)
    {
        await EnsureCategoryExistsAsync(input.CategoryId);

        var article = await _articleRepository.GetAsync(id);
        article.CategoryId = input.CategoryId;
        article.Title = input.Title.Trim();
        article.Summary = input.Summary?.Trim();
        article.Content = input.Content.Trim();
        article.CoverImageUrl = input.CoverImageUrl?.Trim();
        article.Tags = input.Tags?.Trim();
        article.IsTop = input.IsTop;
        article.IsHot = input.IsHot;
        article.AllowComments = input.AllowComments;

        await _articleRepository.UpdateAsync(article, autoSave: true);
        return await MapToDtoAsync(article);
    }

    [Authorize(KnowledgeHubPermissions.News.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var comments = await _commentRepository.GetListAsync(x => x.ArticleId == id);
        foreach (var comment in comments)
        {
            await _commentRepository.DeleteAsync(comment);
        }

        var audits = await _auditRepository.GetListAsync(x => x.ArticleId == id);
        foreach (var audit in audits)
        {
            await _auditRepository.DeleteAsync(audit);
        }

        var reactions = await _reactionRepository.GetListAsync(x => x.ArticleId == id);
        foreach (var reaction in reactions)
        {
            await _reactionRepository.DeleteAsync(reaction);
        }

        await _articleRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.News.Review)]
    public async Task SubmitForReviewAsync(Guid id)
    {
        var article = await _articleRepository.GetAsync(id);
        var fromStatus = article.Status;
        article.Status = NewsArticleStatus.PendingReview;
        await _articleRepository.UpdateAsync(article, autoSave: true);
        await AddAuditAsync(article, fromStatus, NewsArticleStatus.PendingReview, "SubmitForReview", "提交审核");
    }

    [Authorize(KnowledgeHubPermissions.News.Publish)]
    public async Task PublishAsync(Guid id)
    {
        var article = await _articleRepository.GetAsync(id);
        var fromStatus = article.Status;
        article.Status = NewsArticleStatus.Published;
        article.PublishedAt = Clock.Now;
        await _articleRepository.UpdateAsync(article, autoSave: true);
        await AddAuditAsync(article, fromStatus, NewsArticleStatus.Published, "Publish", "发布资讯");
    }

    [Authorize(KnowledgeHubPermissions.News.Review)]
    public async Task RejectAsync(Guid id, ReviewNewsArticleDto input)
    {
        var article = await _articleRepository.GetAsync(id);
        var fromStatus = article.Status;
        article.Status = NewsArticleStatus.Rejected;
        await _articleRepository.UpdateAsync(article, autoSave: true);
        await AddAuditAsync(article, fromStatus, NewsArticleStatus.Rejected, "Reject", input.Comment);
    }

    [Authorize(KnowledgeHubPermissions.News.Publish)]
    public async Task ArchiveAsync(Guid id, ReviewNewsArticleDto input)
    {
        var article = await _articleRepository.GetAsync(id);
        var fromStatus = article.Status;
        article.Status = NewsArticleStatus.Archived;
        await _articleRepository.UpdateAsync(article, autoSave: true);
        await AddAuditAsync(article, fromStatus, NewsArticleStatus.Archived, "Archive", input.Comment);
    }

    [Authorize(KnowledgeHubPermissions.News.Default)]
    public async Task LikeAsync(Guid id)
    {
        var userId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var article = await _articleRepository.GetAsync(id);

        var exists = await _reactionRepository.AnyAsync(x => x.ArticleId == id && x.UserId == userId);
        if (exists)
        {
            return;
        }

        var reaction = new NewsReaction(GuidGenerator.Create(), id, userId)
        {
            TenantId = CurrentTenant.Id
        };
        await _reactionRepository.InsertAsync(reaction);

        article.LikeCount += 1;
        await _articleRepository.UpdateAsync(article, autoSave: true);
    }

    private IQueryable<NewsArticle> ApplyArticleFilter(
        IQueryable<NewsArticle> query,
        PagedNewsArticleRequestDto input,
        bool onlyPublished)
    {
        if (onlyPublished)
        {
            query = query.Where(x => x.Status == NewsArticleStatus.Published);
        }
        else if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }

        query = query
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x =>
                x.Title.Contains(input.Filter!) ||
                (x.Summary != null && x.Summary.Contains(input.Filter!)) ||
                (x.Tags != null && x.Tags.Contains(input.Filter!)))
            .WhereIf(input.CategoryId.HasValue, x => x.CategoryId == input.CategoryId.Value);

        return query;
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId)
    {
        var exists = await _categoryRepository.AnyAsync(x => x.Id == categoryId && x.IsActive);
        if (!exists)
        {
            throw new UserFriendlyException("资讯分类不存在或已停用。");
        }
    }

    private async Task AddAuditAsync(
        NewsArticle article,
        NewsArticleStatus fromStatus,
        NewsArticleStatus toStatus,
        string action,
        string? comment)
    {
        var audit = new NewsAudit(GuidGenerator.Create(), article.Id, fromStatus, toStatus, action)
        {
            TenantId = article.TenantId,
            Comment = comment,
            OperatorId = _currentUser.Id
        };

        await _auditRepository.InsertAsync(audit, autoSave: true);
    }

    private async Task<List<NewsArticleDto>> MapToDtosAsync(List<NewsArticle> items)
    {
        if (items.Count == 0)
        {
            return new List<NewsArticleDto>();
        }

        var categoryIds = items.Select(x => x.CategoryId).Distinct().ToList();
        var authorIds = items.Where(x => x.AuthorId.HasValue).Select(x => x.AuthorId!.Value).Distinct().ToList();
        var currentUserId = _currentUser.Id;

        var categories = await _categoryRepository.GetListAsync(x => categoryIds.Contains(x.Id));
        var users = authorIds.Count > 0
            ? await _userRepository.GetListAsync(x => authorIds.Contains(x.Id))
            : new List<IdentityUser>();
        var likedArticleIds = currentUserId.HasValue
            ? (await _reactionRepository.GetListAsync(x => x.UserId == currentUserId.Value && categoryIds.Count >= 0))
                .Select(x => x.ArticleId)
                .ToHashSet()
            : new HashSet<Guid>();

        var categoryMap = categories.ToDictionary(x => x.Id, x => x.Name);
        var userMap = users.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);

        return items.Select(item => new NewsArticleDto
        {
            Id = item.Id,
            CategoryId = item.CategoryId,
            CategoryName = categoryMap.GetValueOrDefault(item.CategoryId),
            Title = item.Title,
            Summary = item.Summary,
            Content = item.Content,
            CoverImageUrl = item.CoverImageUrl,
            Tags = item.Tags,
            IsTop = item.IsTop,
            IsHot = item.IsHot,
            AllowComments = item.AllowComments,
            Status = item.Status,
            AuthorId = item.AuthorId,
            AuthorName = item.AuthorId.HasValue ? userMap.GetValueOrDefault(item.AuthorId.Value) : null,
            PublishedAt = item.PublishedAt,
            ViewCount = item.ViewCount,
            LikeCount = item.LikeCount,
            CommentCount = item.CommentCount,
            UserHasLiked = likedArticleIds.Contains(item.Id),
            CreationTime = item.CreationTime,
            CreatorId = item.CreatorId,
            LastModificationTime = item.LastModificationTime,
            LastModifierId = item.LastModifierId,
            IsDeleted = item.IsDeleted,
            DeleterId = item.DeleterId,
            DeletionTime = item.DeletionTime
        }).ToList();
    }

    private async Task<NewsArticleDto> MapToDtoAsync(NewsArticle item)
    {
        var list = await MapToDtosAsync(new List<NewsArticle> { item });
        return list[0];
    }
}
