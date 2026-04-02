using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
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
        if (input.Rating < 1 || input.Rating > 5)
        {
            throw new BusinessException("KnowledgeHub:InvalidRating")
                .WithData("Message", "Rating must be between 1 and 5");
        }

        var userId = _currentUser.GetId();
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
        if (input.Rating < 1 || input.Rating > 5)
        {
            throw new BusinessException("KnowledgeHub:InvalidRating")
                .WithData("Message", "Rating must be between 1 and 5");
        }

        var review = await _reviewRepository.GetAsync(id);

        if (review.UserId != _currentUser.GetId() && !await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Search.ReviewResource))
        {
            throw new BusinessException("KnowledgeHub:NotAuthorized");
        }

        review.Rating = input.Rating;
        review.Content = input.Content;

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
        var reviews = await _reviewRepository.GetListAsync(r => r.ResourceId == resourceId);

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

    private async Task<ResourceReviewDto> MapToDtoAsync(ResourceReview review)
    {
        var user = await _userManager.FindByIdAsync(review.UserId.ToString());
        return new ResourceReviewDto
        {
            Id = review.Id,
            ResourceId = review.ResourceId,
            UserId = review.UserId,
            UserName = user?.Name ?? user?.UserName ?? "Unknown",
            Rating = review.Rating,
            Content = review.Content,
            CreationTime = review.CreationTime
        };
    }
}
