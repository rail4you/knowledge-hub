using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.Search;

public class ResourceReviewAppService : KnowledgeHubAppService, IResourceReviewAppService
{
    private readonly IRepository<ResourceReview, Guid> _reviewRepository;
    private readonly IResourceReviewRepository _customReviewRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentUser _currentUser;

    public ResourceReviewAppService(
        IRepository<ResourceReview, Guid> reviewRepository,
        IResourceReviewRepository customReviewRepository,
        IdentityUserManager userManager,
        ICurrentUser currentUser)
    {
        _reviewRepository = reviewRepository;
        _customReviewRepository = customReviewRepository;
        _userManager = userManager;
        _currentUser = currentUser;
    }

    [Authorize]
    public async Task<ResourceReviewDto> CreateAsync(CreateResourceReviewDto input)
    {
        var userId = _currentUser.GetId();

        // 回复分支：ParentId 非空时为回复（参考资讯评论回复）
        if (input.ParentId.HasValue)
        {
            var parent = await _reviewRepository.FindAsync(input.ParentId.Value);
            if (parent == null || parent.ResourceId != input.ResourceId)
            {
                throw new UserFriendlyException("回复的评价不存在。");
            }
            if (string.IsNullOrWhiteSpace(input.Content))
            {
                throw new UserFriendlyException("回复内容不能为空。");
            }

            var reply = new ResourceReview
            {
                ResourceId = input.ResourceId,
                ParentId = parent.Id,
                UserId = userId,
                Rating = 0, // 回复不参与评分统计
                Content = input.Content!.Trim(),
                TenantId = CurrentTenant.Id
            };

            await _reviewRepository.InsertAsync(reply);

            return await MapToDtoAsync(reply);
        }

        if (input.Rating < 1 || input.Rating > 5)
        {
            throw new BusinessException("KnowledgeHub:InvalidRating")
                .WithData("Message", "Rating must be between 1 and 5");
        }

        var existing = await _customReviewRepository.GetByUserAndResourceAsync(userId, input.ResourceId);
        if (existing != null)
        {
            throw new BusinessException("KnowledgeHub:AlreadyReviewed");
        }

        var review = new ResourceReview
        {
            ResourceId = input.ResourceId,
            UserId = userId,
            Rating = input.Rating,
            Content = input.Content,
            TenantId = CurrentTenant.Id
        };

        await _reviewRepository.InsertAsync(review);

        return await MapToDtoAsync(review);
    }

    [Authorize]
    public async Task<ResourceReviewDto> UpdateAsync(Guid id, UpdateResourceReviewDto input)
    {
        var review = await _reviewRepository.GetAsync(id);

        if (review.UserId != _currentUser.GetId() && !await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Search.ReviewResource))
        {
            throw new BusinessException("KnowledgeHub:NotAuthorized");
        }

        // 回复仅更新内容，不改评分
        if (review.ParentId.HasValue)
        {
            review.Content = input.Content;
        }
        else
        {
            if (input.Rating < 1 || input.Rating > 5)
            {
                throw new BusinessException("KnowledgeHub:InvalidRating")
                    .WithData("Message", "Rating must be between 1 and 5");
            }

            review.Rating = input.Rating;
            review.Content = input.Content;
        }

        await _reviewRepository.UpdateAsync(review);

        return await MapToDtoAsync(review);
    }

    [Authorize]
    public async Task DeleteAsync(Guid id)
    {
        var review = await _reviewRepository.GetAsync(id);

        if (review.UserId != _currentUser.GetId() && !await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Search.ReviewResource))
        {
            throw new BusinessException("KnowledgeHub:NotAuthorized");
        }

        // 删除一级评价时级联删除其下回复，避免孤儿回复残留
        if (!review.ParentId.HasValue)
        {
            await _reviewRepository.DeleteAsync(r => r.ParentId == id);
        }

        await _reviewRepository.DeleteAsync(id);
    }

    [Authorize]
    public async Task<ResourceReviewDto?> GetMyReviewAsync(Guid resourceId)
    {
        var userId = _currentUser.GetId();
        var review = await _customReviewRepository.GetByUserAndResourceAsync(userId, resourceId);
        return review != null ? await MapToDtoAsync(review) : null;
    }

    public async Task<List<ResourceReviewDto>> GetResourceReviewsAsync(Guid resourceId, int skipCount = 0, int maxResultCount = 20)
    {
        var reviews = await _customReviewRepository.GetByResourceIdAsync(resourceId, skipCount, maxResultCount);
        var dtos = new List<ResourceReviewDto>();
        foreach (var review in reviews)
        {
            dtos.Add(await MapToDtoAsync(review));
        }
        return dtos;
    }

    public async Task<ResourceRatingSummaryDto> GetRatingSummaryAsync(Guid resourceId)
    {
        // 评分统计仅计入一级评价，回复（ParentId 非空）不参与平均分/分布/总数
        var reviews = await _reviewRepository.GetListAsync(r => r.ResourceId == resourceId && r.ParentId == null);

        var distribution = new int[5];
        foreach (var review in reviews)
        {
            if (review.Rating >= 1 && review.Rating <= 5)
            {
                distribution[review.Rating - 1]++;
            }
        }

        var myReview = _currentUser.Id.HasValue
            ? await _customReviewRepository.GetByUserAndResourceAsync(_currentUser.Id.Value, resourceId)
            : null;

        return new ResourceRatingSummaryDto
        {
            ResourceId = resourceId,
            AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0,
            TotalReviews = reviews.Count,
            RatingDistribution = distribution,
            MyReview = myReview != null ? await MapToDtoAsync(myReview) : null
        };
    }

    /// <summary>
    /// 批量获取多个资源的评分统计：一次性查询，避免列表页对每个资源单独请求。
    /// </summary>
    [HttpPost]
    public async Task<List<ResourceRatingSummaryDto>> GetRatingSummariesAsync(List<Guid> resourceIds)
    {
        var result = new List<ResourceRatingSummaryDto>();
        if (resourceIds == null || resourceIds.Count == 0)
        {
            return result;
        }

        var ids = resourceIds.Distinct().ToList();

        // 评分统计仅计入一级评价（ParentId 为空），回复不参与
        var reviews = await _reviewRepository.GetListAsync(
            r => ids.Contains(r.ResourceId) && r.ParentId == null);

        var myReviews = new List<ResourceReview>();
        if (_currentUser.Id.HasValue)
        {
            myReviews = await _reviewRepository.GetListAsync(
                r => ids.Contains(r.ResourceId) && r.UserId == _currentUser.Id.Value);
        }

        foreach (var id in ids)
        {
            var resourceReviews = reviews.Where(r => r.ResourceId == id).ToList();

            var distribution = new int[5];
            foreach (var review in resourceReviews)
            {
                if (review.Rating >= 1 && review.Rating <= 5)
                {
                    distribution[review.Rating - 1]++;
                }
            }

            var myReview = myReviews.FirstOrDefault(r => r.ResourceId == id);

            result.Add(new ResourceRatingSummaryDto
            {
                ResourceId = id,
                AverageRating = resourceReviews.Count > 0 ? Math.Round(resourceReviews.Average(r => r.Rating), 1) : 0,
                TotalReviews = resourceReviews.Count,
                RatingDistribution = distribution,
                MyReview = myReview != null ? await MapToDtoAsync(myReview) : null
            });
        }

        return result;
    }

    private async Task<ResourceReviewDto> MapToDtoAsync(ResourceReview review)
    {
        var user = await _userManager.FindByIdAsync(review.UserId.ToString());
        return new ResourceReviewDto
        {
            Id = review.Id,
            ResourceId = review.ResourceId,
            ParentId = review.ParentId,
            UserId = review.UserId,
            UserName = user?.Name ?? user?.UserName ?? "Unknown",
            Rating = review.Rating,
            Content = review.Content,
            CreationTime = review.CreationTime
        };
    }
}
