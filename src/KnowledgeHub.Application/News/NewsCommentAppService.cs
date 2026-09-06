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

public class NewsCommentAppService : KnowledgeHubAppService, INewsCommentAppService
{
    private readonly IRepository<NewsComment, Guid> _commentRepository;
    private readonly IRepository<NewsCommentLike, Guid> _commentLikeRepository;
    private readonly IRepository<NewsArticle, Guid> _articleRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ICurrentUser _currentUser;

    public NewsCommentAppService(
        IRepository<NewsComment, Guid> commentRepository,
        IRepository<NewsCommentLike, Guid> commentLikeRepository,
        IRepository<NewsArticle, Guid> articleRepository,
        IRepository<IdentityUser, Guid> userRepository,
        ICurrentUser currentUser)
    {
        _commentRepository = commentRepository;
        _commentLikeRepository = commentLikeRepository;
        _articleRepository = articleRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    [Authorize(KnowledgeHubPermissions.News.ManageComment)]
    public async Task<PagedResultDto<NewsCommentDto>> GetListAsync(PagedNewsCommentRequestDto input)
    {
        var query = await _commentRepository.GetQueryableAsync();
        query = query
            .WhereIf(input.ArticleId.HasValue, x => x.ArticleId == input.ArticleId.Value)
            .WhereIf(input.Status.HasValue, x => x.Status == input.Status.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Content.Contains(input.Filter!));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<NewsCommentDto>(totalCount, await MapToDtosAsync(items));
    }

    public async Task<List<NewsCommentDto>> GetApprovedListByArticleAsync(Guid articleId)
    {
        var items = await _commentRepository.GetListAsync(
            x => x.ArticleId == articleId && x.Status == NewsCommentStatus.Approved);

        return await MapToDtosAsync(items
            .OrderByDescending(x => x.CreationTime)
            .ToList());
    }

    [Authorize(KnowledgeHubPermissions.News.Default)]
    public async Task<NewsCommentDto> CreateAsync(CreateNewsCommentDto input)
    {
        var userId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var article = await _articleRepository.GetAsync(input.ArticleId);
        if (!article.AllowComments)
        {
            throw new UserFriendlyException("该资讯未开启评论。");
        }

        var comment = new NewsComment(GuidGenerator.Create(), input.ArticleId, userId, input.Content.Trim())
        {
            ParentId = input.ParentId,
            TenantId = CurrentTenant.Id,
            Status = NewsCommentStatus.Approved
        };

        await _commentRepository.InsertAsync(comment, autoSave: true);

        article.CommentCount += 1;
        await _articleRepository.UpdateAsync(article, autoSave: true);

        return (await MapToDtosAsync(new List<NewsComment> { comment }))[0];
    }

    [Authorize(KnowledgeHubPermissions.News.Default)]
    public async Task<NewsCommentDto> LikeAsync(Guid id)
    {
        var userId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var comment = await _commentRepository.GetAsync(id);

        var existing = await _commentLikeRepository.FirstOrDefaultAsync(x => x.CommentId == id && x.UserId == userId);
        if (existing != null)
        {
            // 已点赞则取消点赞（切换）。
            // 必须硬删除：NewsCommentLike 是软删除实体，而 (CommentId, UserId) 唯一索引
            // 对已软删除的行依然生效；若只软删除，下次点赞会因撞唯一键而 500。
            await _commentLikeRepository.HardDeleteAsync(existing, autoSave: true);
            comment.LikeCount = Math.Max(0, comment.LikeCount - 1);
        }
        else
        {
            var like = new NewsCommentLike(GuidGenerator.Create(), id, userId)
            {
                TenantId = CurrentTenant.Id
            };
            await _commentLikeRepository.InsertAsync(like, autoSave: true);
            comment.LikeCount += 1;
        }

        await _commentRepository.UpdateAsync(comment, autoSave: true);
        return (await MapToDtosAsync(new List<NewsComment> { comment }))[0];
    }

    [Authorize(KnowledgeHubPermissions.News.ManageComment)]
    public async Task<NewsCommentDto> ReviewAsync(Guid id, ReviewNewsCommentDto input)
    {
        var comment = await _commentRepository.GetAsync(id);
        var article = await _articleRepository.GetAsync(comment.ArticleId);

        if (comment.Status == NewsCommentStatus.Approved && input.Status != NewsCommentStatus.Approved)
        {
            article.CommentCount = Math.Max(0, article.CommentCount - 1);
        }

        if (comment.Status != NewsCommentStatus.Approved && input.Status == NewsCommentStatus.Approved)
        {
            article.CommentCount += 1;
        }

        comment.Status = input.Status;
        await _commentRepository.UpdateAsync(comment, autoSave: true);
        await _articleRepository.UpdateAsync(article, autoSave: true);

        return (await MapToDtosAsync(new List<NewsComment> { comment }))[0];
    }

    [Authorize(KnowledgeHubPermissions.News.ManageComment)]
    public async Task DeleteAsync(Guid id)
    {
        var comment = await _commentRepository.GetAsync(id);
        var article = await _articleRepository.GetAsync(comment.ArticleId);
        if (comment.Status == NewsCommentStatus.Approved)
        {
            article.CommentCount = Math.Max(0, article.CommentCount - 1);
            await _articleRepository.UpdateAsync(article, autoSave: true);
        }

        var likes = await _commentLikeRepository.GetListAsync(x => x.CommentId == id);
        foreach (var like in likes)
        {
            await _commentLikeRepository.HardDeleteAsync(like);
        }

        await _commentRepository.DeleteAsync(id);
    }

    private async Task<List<NewsCommentDto>> MapToDtosAsync(List<NewsComment> items)
    {
        if (items.Count == 0)
        {
            return new List<NewsCommentDto>();
        }

        var userIds = items.Select(x => x.UserId).Distinct().ToList();
        var users = await _userRepository.GetListAsync(x => userIds.Contains(x.Id));
        var userMap = users.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);

        var currentUserId = _currentUser.Id;
        // 先物化为内存集合，避免 EF 无法翻译表达式树内的 Select
        var commentIds = items.Select(i => i.Id).ToList();
        var likedCommentIds = currentUserId.HasValue
            ? (await _commentLikeRepository.GetListAsync(x =>
                x.UserId == currentUserId.Value && commentIds.Contains(x.CommentId)))
                .Select(x => x.CommentId)
                .ToHashSet()
            : new HashSet<Guid>();

        return items.Select(item => new NewsCommentDto
        {
            Id = item.Id,
            ArticleId = item.ArticleId,
            ParentId = item.ParentId,
            UserId = item.UserId,
            UserName = userMap.GetValueOrDefault(item.UserId),
            Content = item.Content,
            Status = item.Status,
            LikeCount = item.LikeCount,
            UserHasLiked = likedCommentIds.Contains(item.Id),
            CreationTime = item.CreationTime,
            CreatorId = item.CreatorId,
            LastModificationTime = item.LastModificationTime,
            LastModifierId = item.LastModifierId,
            IsDeleted = item.IsDeleted,
            DeleterId = item.DeleterId,
            DeletionTime = item.DeletionTime
        }).ToList();
    }
}
