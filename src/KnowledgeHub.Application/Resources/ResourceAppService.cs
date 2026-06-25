using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Search;
using KnowledgeHub.Common;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Edition;
using KnowledgeHub.Majors;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectMapping;
using KnowledgeHub.Permissions;

namespace KnowledgeHub.Resources;

[Authorize(KnowledgeHubPermissions.Resources.Default)]
public class ResourceAppService : KnowledgeHubAppService, IResourceAppService
{
    protected IRepository<Resource, Guid> Repository { get; }
    protected IResourceRepository ResourceRepository { get; }
    protected IResourceVersionRepository VersionRepository { get; }
    protected IResourceCategoryRepository CategoryRepository { get; }
    protected IResourceCollectionRepository CollectionRepository { get; }
    protected IResourceAuditRepository AuditRepository { get; }
    protected IPhysicalDeleteRequestRepository PhysicalDeleteRequestRepository { get; }
    protected IFileStorageService FileStorageService { get; }
    protected ISearchService SearchService { get; }
    protected ICurrentTenant CurrentTenant { get; }
    protected IHttpContextAccessor HttpContextAccessor { get; }
    protected IBackgroundJobManager BackgroundJobManager { get; }
    protected IRepository<DocumentIndexingJob, Guid> IndexingJobRepository { get; }
    protected IRepository<VideoIndexingJob, Guid> VideoIndexingJobRepository { get; }
    protected IEditionConfigService EditionConfigService { get; }
    protected IMeiliSearchService MeiliSearchService { get; }
    protected IDocumentIndexRepository DocumentIndexRepository { get; }
    protected IRepository<ResourcePageIndex, Guid> PageIndexRepository { get; }
    protected IRepository<Major, Guid> MajorRepository { get; }
    protected IRepository<IdentityUser, Guid> UserRepository { get; }

    public ResourceAppService(
        IRepository<Resource, Guid> repository,
        IResourceRepository resourceRepository,
        IResourceVersionRepository versionRepository,
        IResourceCategoryRepository categoryRepository,
        IResourceCollectionRepository collectionRepository,
        IResourceAuditRepository auditRepository,
        IPhysicalDeleteRequestRepository physicalDeleteRequestRepository,
        IFileStorageService fileStorageService,
        ISearchService searchService,
        ICurrentTenant currentTenant,
        IHttpContextAccessor httpContextAccessor,
        IBackgroundJobManager backgroundJobManager,
        IRepository<DocumentIndexingJob, Guid> indexingJobRepository,
        IRepository<VideoIndexingJob, Guid> videoIndexingJobRepository,
        IEditionConfigService editionConfigService,
        IMeiliSearchService meiliSearchService,
        IDocumentIndexRepository documentIndexRepository,
        IRepository<ResourcePageIndex, Guid> pageIndexRepository,
        IRepository<Major, Guid> majorRepository,
        IRepository<IdentityUser, Guid> userRepository)
    {
        Repository = repository;
        ResourceRepository = resourceRepository;
        VersionRepository = versionRepository;
        CategoryRepository = categoryRepository;
        CollectionRepository = collectionRepository;
        AuditRepository = auditRepository;
        PhysicalDeleteRequestRepository = physicalDeleteRequestRepository;
        FileStorageService = fileStorageService;
        SearchService = searchService;
        CurrentTenant = currentTenant;
        HttpContextAccessor = httpContextAccessor;
        BackgroundJobManager = backgroundJobManager;
        IndexingJobRepository = indexingJobRepository;
        VideoIndexingJobRepository = videoIndexingJobRepository;
        EditionConfigService = editionConfigService;
        MeiliSearchService = meiliSearchService;
        DocumentIndexRepository = documentIndexRepository;
        PageIndexRepository = pageIndexRepository;
        MajorRepository = majorRepository;
        UserRepository = userRepository;
    }

    [AllowAnonymous]
    public virtual async Task<ResourceDto> GetAsync(Guid id)
    {
        var resource = await ResourceRepository.GetWithDetailsAsync(id);
        var dto = ObjectMapper.Map<Resource, ResourceDto>(resource);
        EnsureFileMetadata(dto);
        dto.MajorName = await ResolveMajorNameAsync(resource.MajorId);
        return dto;
    }

    /// <summary>
    /// 确保单个 DTO 的 FileExtension 正确：去前导点，为空则从 FilePath 推导。
    /// </summary>
    private void EnsureFileMetadata(ResourceDto dto)
    {
        // 去掉可能的前导点（如 ".docx" → "docx"）
        if (!string.IsNullOrEmpty(dto.FileExtension))
        {
            dto.FileExtension = dto.FileExtension.TrimStart('.');
        }
        
        // 若仍然为空，从 FilePath 推导
        if (string.IsNullOrEmpty(dto.FileExtension) && !string.IsNullOrEmpty(dto.FilePath))
        {
            var ext = System.IO.Path.GetExtension(dto.FilePath);
            if (!string.IsNullOrEmpty(ext))
            {
                dto.FileExtension = ext.TrimStart('.');
            }
        }
    }

    [AllowAnonymous]
    public virtual async Task<PagedResultDto<ResourceDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await ResourceRepository.GetCountAsync("");
        var resources = await ResourceRepository.GetListAsync(
            input.SkipCount,
            input.MaxResultCount,
            input.Sorting ?? "CreationTime DESC",
            ""
        );

        var dtos = ObjectMapper.Map<List<Resource>, List<ResourceDto>>(resources);
        EnsureFileMetadata(dtos);
        await FillCreatorNamesAsync(dtos);
        return new PagedResultDto<ResourceDto>(
            totalCount,
            dtos
        );
    }

    [AllowAnonymous]
    public virtual async Task<ResourceDto> GetWithVersionsAsync(Guid id)
    {
        var resource = await ResourceRepository.GetWithDetailsAsync(id);
        var dto = ObjectMapper.Map<Resource, ResourceDto>(resource);
        EnsureFileMetadata(dto);
        return dto;
    }

    [AllowAnonymous]
    public virtual async Task<PagedResultDto<ResourceDto>> GetFilteredListAsync(ResourceListQueryDto input)
    {
        var query = await ResourceRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.ToLower();
            query = query.Where(x =>
                (x.Name != null && x.Name.ToLower().Contains(filter)) ||
                (x.Description != null && x.Description.ToLower().Contains(filter)));
        }

        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }

        if (input.ResourceType.HasValue)
        {
            query = query.Where(x => x.ResourceType == input.ResourceType.Value);
        }

        if (input.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == input.CategoryId.Value);
        }

        if (input.MajorId.HasValue)
        {
            query = query.Where(x => x.MajorId == input.MajorId.Value);
        }

        if (input.StartDate.HasValue)
        {
            query = query.Where(x => x.CreationTime >= input.StartDate.Value);
        }
        if (input.EndDate.HasValue)
        {
            query = query.Where(x => x.CreationTime <= input.EndDate.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(query);

        query = query.OrderByDescending(x => x.CreationTime);
        query = query.Skip(input.SkipCount).Take(input.MaxResultCount);

        var resources = await AsyncExecuter.ToListAsync(query);
        var dtos = ObjectMapper.Map<List<Resource>, List<ResourceDto>>(resources);
        EnsureFileMetadata(dtos);
        await FillMajorNamesAsync(dtos);
        await FillCreatorNamesAsync(dtos);

        return new PagedResultDto<ResourceDto>(totalCount, dtos);
    }

    [AllowAnonymous]
    public async Task<PagedResultDto<ResourceDto>> GetLeagueApprovedAsync(PagedResultRequestDto input)
    {
        var isTwoLevelApproval = await EditionConfigService.IsTwoLevelApprovalEnabledAsync();
        
        var query = await ResourceRepository.GetQueryableAsync();
        
        List<Resource> resources;
        int count;
        
        if (isTwoLevelApproval)
        {
            resources = query
                .Where(r => r.Status == ResourceStatus.LeagueApproved)
                .OrderByDescending(r => r.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToList();
            
            count = query.Count(r => r.Status == ResourceStatus.LeagueApproved);
        }
        else
        {
            resources = query
                .Where(r => r.Status == ResourceStatus.SchoolApproved || r.Status == ResourceStatus.LeagueApproved)
                .OrderByDescending(r => r.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToList();
            
            count = query.Count(r => r.Status == ResourceStatus.SchoolApproved || r.Status == ResourceStatus.LeagueApproved);
        }
        
        var dtos = ObjectMapper.Map<List<Resource>, List<ResourceDto>>(resources);
        EnsureFileMetadata(dtos);
        await FillCreatorNamesAsync(dtos);
        return new PagedResultDto<ResourceDto>(
            count,
            dtos
        );
    }

    public virtual async Task<PagedResultDto<ResourceDto>> GetCollectedListAsync(PagedResultRequestDto input)
    {
        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录");
        var collections = await CollectionRepository.GetByUserIdAsync(userId);

        var collectionIds = collections.Select(x => x.ResourceId).ToList();

        var resourceQuery = await Repository.GetQueryableAsync();
        // Only include approved resources in collection list
        var approvedResources = await AsyncExecuter.ToListAsync(
            resourceQuery.Where(x => collectionIds.Contains(x.Id) &&
                                     (x.Status == ResourceStatus.SchoolApproved ||
                                      x.Status == ResourceStatus.LeagueApproved))
        );

        var totalCount = approvedResources.Count;

        var orderedResources = approvedResources
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        var dtos2 = ObjectMapper.Map<List<Resource>, List<ResourceDto>>(orderedResources);
        EnsureFileMetadata(dtos2);
        return new PagedResultDto<ResourceDto>(
            totalCount,
            dtos2
        );
    }

    [Authorize(KnowledgeHubPermissions.Resources.Create)]
    public virtual async Task<ResourceDto> CreateAsync(CreateUpdateResourceDto input)
    {
        var resource = MapToEntity(input);

        if (!string.IsNullOrEmpty(input.FilePath))
        {
            resource.FilePath = input.FilePath;
            resource.FileSize = input.FileSize ?? 0;
            resource.FileExtension = input.FileExtension;
            resource.OriginalFileName = input.OriginalFileName;
        }

        await Repository.InsertAsync(resource);

        var initialVersion = new ResourceVersion
        {
            ResourceId = resource.Id,
            Version = 1,
            FilePath = resource.FilePath,
            FileSize = resource.FileSize,
            UpdateContent = L["InitialVersion"],
            IsCurrentVersion = true
        };
        await VersionRepository.InsertAsync(initialVersion);

        var isVideo = IsVideoResource(input);
        var hasFile = !string.IsNullOrEmpty(resource.FilePath);

        if (hasFile)
        {
            if (isVideo)
            {
                await EnqueueVideoIndexingJobAsync(resource);
            }
            else
            {
                await EnqueueDocumentIndexingJobAsync(resource, initialVersion.Id);
            }
        }

        var dto = ObjectMapper.Map<Resource, ResourceDto>(resource);
        dto.MajorName = await ResolveMajorNameAsync(resource.MajorId);
        return dto;
    }

    /// <summary>
    /// 确保 DTO 的 FileExtension 正确：去前导点，为空则从 FilePath 推导。
    /// 修复旧数据中 fileExtension 为 null 或带点导致前端"不支持预览"的问题。
    /// </summary>
    private void EnsureFileMetadata(List<ResourceDto> dtos)
    {
        foreach (var dto in dtos)
        {
            EnsureFileMetadata(dto);
        }
    }

    private bool IsVideoResource(CreateUpdateResourceDto input)
    {
        if (input.ResourceType == ResourceType.Video)
        {
            return true;
        }

        if (VideoIndexingBackgroundJob.IsVideoFile(input.FileExtension))
        {
            return true;
        }

        return false;
    }

    private async Task EnqueueVideoIndexingJobAsync(Resource resource)
    {
        var indexingJob = new VideoIndexingJob
        {
            ResourceId = resource.Id,
            Status = VideoIndexingJobStatus.Pending,
            TenantId = CurrentTenant.Id
        };
        await VideoIndexingJobRepository.InsertAsync(indexingJob);
        Logger.LogInformation("Created VideoIndexingJob {JobId} for resource {ResourceId}", indexingJob.Id, resource.Id);

        try
        {
            await BackgroundJobManager.EnqueueAsync(new VideoIndexingJobArgs
            {
                JobId = indexingJob.Id,
                ResourceId = resource.Id,
                FilePath = resource.FilePath,
                TenantId = CurrentTenant.Id
            });
            Logger.LogInformation("Successfully enqueued background job for VideoIndexingJob {JobId}", indexingJob.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to enqueue background job for VideoIndexingJob {JobId}", indexingJob.Id);
            throw;
        }
    }

    private async Task EnqueueDocumentIndexingJobAsync(Resource resource, Guid? resourceVersionId = null)
    {
        var indexingJob = new DocumentIndexingJob
        {
            ResourceId = resource.Id,
            Status = IndexingJobStatus.Pending,
            TenantId = CurrentTenant.Id
        };
        await IndexingJobRepository.InsertAsync(indexingJob);
        Logger.LogInformation("Created DocumentIndexingJob {JobId} for resource {ResourceId}", indexingJob.Id, resource.Id);

        try
        {
            await BackgroundJobManager.EnqueueAsync(new DocumentIndexingJobArgs
            {
                JobId = indexingJob.Id,
                ResourceId = resource.Id,
                FilePath = resource.FilePath,
                TenantId = CurrentTenant.Id,
                ResourceVersionId = resourceVersionId
            });
            Logger.LogInformation("Successfully enqueued background job for DocumentIndexingJob {JobId}, ResourceVersionId: {VersionId}", indexingJob.Id, resourceVersionId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to enqueue background job for DocumentIndexingJob {JobId}", indexingJob.Id);
            throw;
        }
    }

    [Authorize(KnowledgeHubPermissions.Resources.Edit)]
    public virtual async Task<ResourceDto> UpdateAsync(Guid id, CreateUpdateResourceDto input)
    {
        var resource = await Repository.GetAsync(id);

        ObjectMapper.Map(input, resource);
        resource.MajorId = input.MajorId;

        await Repository.UpdateAsync(resource);
        var dto = ObjectMapper.Map<Resource, ResourceDto>(resource);
        dto.MajorName = await ResolveMajorNameAsync(resource.MajorId);
        return dto;
    }

    /// <summary>
    /// P1-15：切换"作为简历"标记。
    /// 权限：Resources.Default（学生即可调用）+ 仅资源创建者可修改自己资源的 IsResume。
    /// 目的：让学生能在资料库列表的"设为简历/取消简历"按钮维护自己的简历资源，
    ///       而不必走 Resources.Edit 完整编辑流程（也不需要带走 Name/CategoryId 等其他字段）。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.Resources.Default)]
    public virtual async Task SetResumeAsync(Guid id, SetResumeInput input)
    {
        var resource = await Repository.GetAsync(id);

        // 仅资源创建者本人可切换 IsResume；管理员/教师通过原 Update 走完整编辑流程。
        var currentUserId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录");
        if (resource.CreatorId != currentUserId)
        {
            throw new UserFriendlyException("只有资源创建者可以切换简历标记");
        }

        resource.IsResume = input.IsResume;
        await Repository.UpdateAsync(resource);
    }

    [Authorize(KnowledgeHubPermissions.Resources.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await CleanupResourceIndexDataAsync(id);
        await Repository.DeleteAsync(id);
    }

    public virtual async Task<ResourceVersionDto> UploadVersionAsync(UploadVersionDto input)
    {
        var resource = await ResourceRepository.GetWithDetailsAsync(input.ResourceId);

        var newVersion = new ResourceVersion
        {
            ResourceId = resource.Id,
            Version = resource.CurrentVersion + 1,
            FilePath = input.FilePath ?? resource.FilePath,
            FileSize = input.FileSize ?? resource.FileSize,
            UpdateContent = input.UpdateContent,
            IsCurrentVersion = true
        };

        var oldVersions = resource.Versions.Where(x => x.IsCurrentVersion).ToList();
        foreach (var oldVersion in oldVersions)
        {
            oldVersion.IsCurrentVersion = false;
        }

        resource.CurrentVersion = newVersion.Version;
        resource.Status = ResourceStatus.PendingReview;

        if (!string.IsNullOrEmpty(input.FilePath))
        {
            resource.FilePath = input.FilePath;
            resource.FileSize = input.FileSize;
            resource.FileExtension = input.FileExtension;
            resource.OriginalFileName = input.OriginalFileName;
        }

        await VersionRepository.InsertAsync(newVersion);
        await Repository.UpdateAsync(resource);

        // 清理旧版本的索引数据并等待索引任务完成
        if (oldVersions.Any())
        {
            foreach (var oldVersion in oldVersions)
            {
                await CleanupVersionIndexDataAsync(oldVersion.Id);
            }
        }

        if (VideoIndexingBackgroundJob.IsVideoFile(resource.FileExtension))
        {
            await EnqueueVideoIndexingJobAsync(resource);
        }
        else
        {
            // 非视频文件，触发文档索引任务
            await EnqueueDocumentIndexingJobAsync(resource, newVersion.Id);
        }

        return ObjectMapper.Map<ResourceVersion, ResourceVersionDto>(newVersion);
    }

    public virtual async Task<List<ResourceVersionDto>> GetVersionsAsync(Guid resourceId)
    {
        var versions = await VersionRepository.GetVersionsAsync(resourceId);
        return ObjectMapper.Map<List<ResourceVersion>, List<ResourceVersionDto>>(versions);
    }

    public virtual async Task<ResourceVersionDto> RollbackVersionAsync(Guid versionId)
    {
        var version = await VersionRepository.GetAsync(versionId);
        var resource = await ResourceRepository.GetWithDetailsAsync(version.ResourceId);

        var currentVersions = resource.Versions.Where(x => x.IsCurrentVersion).ToList();
        foreach (var cv in currentVersions)
        {
            cv.IsCurrentVersion = false;
        }

        version.IsCurrentVersion = true;
        resource.CurrentVersion = version.Version;
        resource.Status = ResourceStatus.PendingReview;

        await VersionRepository.UpdateAsync(version);
        await Repository.UpdateAsync(resource);

        // 清理当前版本的索引数据并等待索引任务完成，然后触发重新索引
        if (currentVersions.Any())
        {
            foreach (var currentVersion in currentVersions)
            {
                await CleanupVersionIndexDataAsync(currentVersion.Id);
            }
        }

        if (VideoIndexingBackgroundJob.IsVideoFile(resource.FileExtension))
        {
            await EnqueueVideoIndexingJobAsync(resource);
        }
        else
        {
            // 非视频文件，触发文档索引任务
            await EnqueueDocumentIndexingJobAsync(resource, version.Id);
        }

        return ObjectMapper.Map<ResourceVersion, ResourceVersionDto>(version);
    }

    public virtual async Task<bool> IsCollectedAsync(Guid resourceId)
    {
        if (CurrentUser.Id == null)
        {
            return false;
        }
        return await CollectionRepository.IsCollectedAsync(resourceId, CurrentUser.Id.Value);
    }

    public virtual async Task CollectAsync(Guid resourceId)
    {
        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录");

        if (await CollectionRepository.IsCollectedAsync(resourceId, userId))
        {
            return;
        }

        var collection = new ResourceCollection
        {
            ResourceId = resourceId,
            UserId = userId
        };

        await CollectionRepository.InsertAsync(collection);

        var resource = await Repository.GetAsync(resourceId);
        resource.CollectionCount++;
        await Repository.UpdateAsync(resource);
    }

    public virtual async Task UncollectAsync(Guid resourceId)
    {
        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录");

        var collections = await CollectionRepository.GetListAsync();
        var collection = collections.FirstOrDefault(x => x.ResourceId == resourceId && x.UserId == userId);

        if (collection != null)
        {
            await CollectionRepository.DeleteAsync(collection);

            var resource = await Repository.GetAsync(resourceId);
            resource.CollectionCount = Math.Max(0, resource.CollectionCount - 1);
            await Repository.UpdateAsync(resource);
        }
    }

    [AllowAnonymous]
    public virtual async Task<List<ResourceCategoryDto>> GetCategoriesAsync()
    {
        var categories = await CategoryRepository.GetListAsync();
        var dtos = ObjectMapper.Map<List<ResourceCategory>, List<ResourceCategoryDto>>(categories);

        // 统计每个分类下的资源数量（仅 LeagueApproved 状态的资源）
        var resourceQuery = await ResourceRepository.GetQueryableAsync();
        var categoryCounts = await resourceQuery
            .Where(r => r.CategoryId != null && r.Status == ResourceStatus.LeagueApproved)
            .GroupBy(r => r.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

        var lookup = dtos.ToDictionary(c => c.Id);
        var roots = new List<ResourceCategoryDto>();

        foreach (var dto in dtos)
        {
            if (dto.ParentId.HasValue && lookup.TryGetValue(dto.ParentId.Value, out var parent))
            {
                dto.ParentName = parent.Name;
                parent.Children.Add(dto);
            }
            else
            {
                roots.Add(dto);
            }

            // 分配直接资源数量
            dto.ResourceCount = categoryCounts.GetValueOrDefault(dto.Id, 0);
        }

        // 累计子分类的资源数量到父分类
        void AccumulateCounts(List<ResourceCategoryDto> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Children.Count > 0)
                {
                    AccumulateCounts(node.Children);
                    foreach (var child in node.Children)
                    {
                        node.ResourceCount += child.ResourceCount;
                    }
                }
            }
        }
        AccumulateCounts(roots);

        return roots;
    }

    [Authorize(KnowledgeHubPermissions.Resources.ManageCategory)]
    public virtual async Task<ResourceCategoryDto> CreateCategoryAsync(CreateUpdateResourceCategoryDto input)
    {
        var category = new ResourceCategory
        {
            Name = input.Name,
            ParentId = input.ParentId,
            Code = input.Code,
            SortOrder = input.SortOrder,
            IsActive = input.IsActive,
            TenantId = CurrentTenant.Id
        };

        await CategoryRepository.InsertAsync(category);
        return ObjectMapper.Map<ResourceCategory, ResourceCategoryDto>(category);
    }

    [Authorize(KnowledgeHubPermissions.Resources.ManageCategory)]
    public virtual async Task<ResourceCategoryDto> UpdateCategoryAsync(Guid id, CreateUpdateResourceCategoryDto input)
    {
        var category = await CategoryRepository.GetAsync(id);

        category.Name = input.Name;
        category.ParentId = input.ParentId;
        category.Code = input.Code;
        category.SortOrder = input.SortOrder;
        category.IsActive = input.IsActive;

        await CategoryRepository.UpdateAsync(category);
        return ObjectMapper.Map<ResourceCategory, ResourceCategoryDto>(category);
    }

    [Authorize(KnowledgeHubPermissions.Resources.ManageCategory)]
    public virtual async Task DeleteCategoryAsync(Guid id)
    {
        await CategoryRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.Resources.Download)]
    public virtual async Task<string> GetFileUrlAsync(Guid resourceId)
    {
        var resource = await Repository.GetAsync(resourceId);
        return FileStorageService.GetFileUrl(resource.FilePath);
    }

    [Authorize(KnowledgeHubPermissions.Resources.Download)]
    public virtual async Task<byte[]> DownloadAsync(Guid resourceId)
    {
        var resource = await ResourceRepository.GetWithDetailsAsync(resourceId);

        resource.DownloadCount++;
        await Repository.UpdateAsync(resource);

        var filePath = resource.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            var currentVersion = resource.Versions.FirstOrDefault(x => x.IsCurrentVersion);
            filePath = currentVersion?.FilePath;
        }

        if (string.IsNullOrEmpty(filePath))
        {
            throw new BusinessException("KnowledgeHub:ResourceFileNotFound")
                .WithData("ResourceName", resource.Name);
        }

        var stream = await FileStorageService.GetAsync(filePath);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    protected virtual async Task CleanupResourceIndexDataAsync(Guid resourceId)
    {
        try
        {
            await MeiliSearchService.DeleteDocumentAsync(resourceId);
            Logger.LogInformation("Deleted MeiliSearch index data for resource {ResourceId}", resourceId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete MeiliSearch index data for resource {ResourceId}", resourceId);
        }

        try
        {
            var pageIndexList = await PageIndexRepository.GetListAsync(x => x.ResourceId == resourceId);
            if (pageIndexList.Any())
            {
                await PageIndexRepository.DeleteManyAsync(pageIndexList);
                Logger.LogInformation("Deleted {Count} ResourcePageIndex records for resource {ResourceId}", pageIndexList.Count, resourceId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete ResourcePageIndex for resource {ResourceId}", resourceId);
        }

        try
        {
            var indexingJobs = await IndexingJobRepository.GetListAsync(x => x.ResourceId == resourceId);
            if (indexingJobs.Any())
            {
                await IndexingJobRepository.DeleteManyAsync(indexingJobs);
                Logger.LogInformation("Deleted {Count} DocumentIndexingJob records for resource {ResourceId}", indexingJobs.Count, resourceId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete DocumentIndexingJob for resource {ResourceId}", resourceId);
        }

        try
        {
            var videoIndexingJobs = await VideoIndexingJobRepository.GetListAsync(x => x.ResourceId == resourceId);
            if (videoIndexingJobs.Any())
            {
                await VideoIndexingJobRepository.DeleteManyAsync(videoIndexingJobs);
                Logger.LogInformation("Deleted {Count} VideoIndexingJob records for resource {ResourceId}", videoIndexingJobs.Count, resourceId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete VideoIndexingJob for resource {ResourceId}", resourceId);
        }
    }

    /// <summary>
    /// 清理指定版本的索引数据（用于版本切换时清理旧版本索引）
    /// </summary>
    /// <param name="resourceVersionId">资源版本ID</param>
    protected virtual async Task CleanupVersionIndexDataAsync(Guid resourceVersionId)
    {
        // 1. 等待该版本的索引任务完成
        var activeJobs = await IndexingJobRepository.GetListAsync(x =>
            x.ResourceVersionId == resourceVersionId &&
            (x.Status == IndexingJobStatus.Pending ||
             x.Status == IndexingJobStatus.Parsing ||
             x.Status == IndexingJobStatus.Indexing));

        // 等待索引任务完成（最多等待 5 分钟）
        var maxWaitTime = TimeSpan.FromMinutes(5);
        var startTime = DateTime.UtcNow;
        while (activeJobs.Any() && DateTime.UtcNow - startTime < maxWaitTime)
        {
            Logger.LogInformation("Waiting for indexing jobs to complete for version {ResourceVersionId}, remaining jobs: {Count}", resourceVersionId, activeJobs.Count);
            await Task.Delay(2000);
            activeJobs = await IndexingJobRepository.GetListAsync(x =>
                x.ResourceVersionId == resourceVersionId &&
                (x.Status == IndexingJobStatus.Pending ||
                 x.Status == IndexingJobStatus.Parsing ||
                 x.Status == IndexingJobStatus.Indexing));
        }

        // 如果还有未完成的索引任务，记录警告但继续执行清理
        if (activeJobs.Any())
        {
            Logger.LogWarning("Timeout waiting for indexing jobs to complete for version {ResourceVersionId}, proceeding with cleanup", resourceVersionId);
        }
        else
        {
            Logger.LogInformation("All indexing jobs completed for version {ResourceVersionId}", resourceVersionId);
        }

        // 获取该版本对应的资源ID
        var resourceVersion = await VersionRepository.GetAsync(resourceVersionId);
        var resourceId = resourceVersion.ResourceId;

        // 2. 删除 MeiliSearch 索引（该资源的所有索引数据，因为是基于 resourceId 删除的）
        try
        {
            await MeiliSearchService.DeleteDocumentAsync(resourceId);
            Logger.LogInformation("Deleted MeiliSearch index data for resource {ResourceId} (version cleanup)", resourceId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete MeiliSearch index data for resource {ResourceId}", resourceId);
        }

        // 3. 删除该版本的 PageIndex 记录
        try
        {
            var pageIndexList = await PageIndexRepository.GetListAsync(x =>
                x.ResourceId == resourceId && x.ResourceVersionId == resourceVersionId);
            if (pageIndexList.Any())
            {
                await PageIndexRepository.DeleteManyAsync(pageIndexList);
                Logger.LogInformation("Deleted {Count} ResourcePageIndex records for version {ResourceVersionId}", pageIndexList.Count, resourceVersionId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete ResourcePageIndex for version {ResourceVersionId}", resourceVersionId);
        }

        // 4. 删除该版本的索引任务记录
        try
        {
            var indexingJobs = await IndexingJobRepository.GetListAsync(x =>
                x.ResourceId == resourceId && x.ResourceVersionId == resourceVersionId);
            if (indexingJobs.Any())
            {
                await IndexingJobRepository.DeleteManyAsync(indexingJobs);
                Logger.LogInformation("Deleted {Count} DocumentIndexingJob records for version {ResourceVersionId}", indexingJobs.Count, resourceVersionId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete DocumentIndexingJob for version {ResourceVersionId}", resourceVersionId);
        }

        // 5. 删除该版本的 DocumentIndex 记录（页面内容）
        try
        {
            var documentIndices = await DocumentIndexRepository.GetByResourceIdAsync(resourceId);
            if (documentIndices.Any())
            {
                foreach (var docIndex in documentIndices)
                {
                    await DocumentIndexRepository.DeleteAsync(docIndex);
                }
                Logger.LogInformation("Deleted {Count} DocumentIndex records for resource {ResourceId}", documentIndices.Count, resourceId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete DocumentIndex for resource {ResourceId}", resourceId);
        }
    }

    protected virtual Resource MapToEntity(CreateUpdateResourceDto input)
    {
        var resource = ObjectMapper.Map<CreateUpdateResourceDto, Resource>(input);
        resource.MajorId = input.MajorId;
        resource.Status = ResourceStatus.Draft;
        resource.CurrentVersion = 1;
        resource.CollectionCount = 0;
        resource.DownloadCount = 0;
        resource.ViewCount = 0;
        resource.TenantId = CurrentTenant.Id;
        return resource;
    }

    private async Task<string?> ResolveMajorNameAsync(Guid? majorId)
    {
        if (!majorId.HasValue)
        {
            return null;
        }

        var major = await MajorRepository.FindAsync(majorId.Value);
        return major?.Name;
    }

    /// <summary>
    /// P1-1 修复：根据 CreatorId 批量补 CreatorName（列表展示创建人）
    /// </summary>
    private async Task FillCreatorNamesAsync(List<ResourceDto> dtos)
    {
        var ids = dtos
            .Where(x => x.CreatorId != Guid.Empty)
            .Select(x => x.CreatorId)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var query = await UserRepository.GetQueryableAsync();
        var users = await AsyncExecuter.ToListAsync(
            query.Where(u => ids.Contains(u.Id)));

        var userMap = users.ToDictionary(u => u.Id, u => ResolveUserDisplayName(u));
        foreach (var dto in dtos)
        {
            if (dto.CreatorId != Guid.Empty && userMap.TryGetValue(dto.CreatorId, out var name))
            {
                dto.CreatorName = name;
            }
        }
    }

    private static string ResolveUserDisplayName(IdentityUser user)
    {
        // 与 EmploymentAppService.GetUserDisplayName 同款策略：
        // Name + Surname → ExtraProperties["FullName"] → UserName/Email/Id
        var displayName = string.Join(" ", new[] { user.Name, user.Surname }
            .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (!string.IsNullOrWhiteSpace(displayName)) return displayName;

        var extraName = user.GetProperty<string>("FullName")
            ?? user.GetProperty<string>("RealName");
        if (!string.IsNullOrWhiteSpace(extraName)) return extraName!;

        return user.UserName ?? user.Email ?? user.Id.ToString("N");
    }

    private async Task FillMajorNamesAsync(List<ResourceDto> dtos)
    {
        var ids = dtos
            .Where(x => x.MajorId.HasValue)
            .Select(x => x.MajorId!.Value)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var query = await MajorRepository.GetQueryableAsync();
        var map = await query
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        foreach (var dto in dtos)
        {
            if (dto.MajorId.HasValue && map.TryGetValue(dto.MajorId.Value, out var name))
            {
                dto.MajorName = name;
            }
        }
    }

    public virtual async Task<InitiateUploadResultDto> InitiateUploadAsync(InitiateUploadDto input)
    {
        var uploadId = Guid.NewGuid().ToString("N");
        var totalChunks = (int)Math.Ceiling((double)input.TotalSize / input.ChunkSize);
        
        return new InitiateUploadResultDto
        {
            UploadId = uploadId,
            ChunkSize = input.ChunkSize,
            TotalChunks = totalChunks
        };
    }

    public virtual async Task<bool> UploadChunkAsync(UploadChunkDto input)
    {
        try
        {
            var form = await HttpContextAccessor.HttpContext.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            
            if (file == null || file.Length == 0)
            {
                return false;
            }
            
            using var stream = file.OpenReadStream();
            await FileStorageService.SaveChunkAsync(
                stream, 
                input.FileName, 
                input.ChunkNumber, 
                input.UploadId
            );
            
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public virtual async Task<CompleteUploadResultDto> CompleteUploadAsync(CompleteUploadDto input)
    {
        var uploadId = input.UploadId;
        var fileName = input.FileName;
        
        var extension = Path.GetExtension(fileName);
        var directory = DateTime.Now.ToString("yyyyMMdd");
        
        var filePath = await FileStorageService.MergeChunksAsync(uploadId, fileName, directory);
        
        var fullFilePath = Path.Combine(((LocalFileStorageService)FileStorageService).RootPath, filePath);
        var fileInfo = new FileInfo(fullFilePath);
        
        return new CompleteUploadResultDto
        {
            FilePath = filePath,
            FileSize = fileInfo.Exists ? fileInfo.Length : 0,
            FileExtension = extension,
            OriginalFileName = fileName
        };
    }

    public virtual Task<bool> CancelUploadAsync(string uploadId)
    {
        return Task.FromResult(true);
    }

    [Authorize(KnowledgeHubPermissions.Resources.Default)]
    public virtual async Task<PagedResultDto<ResourceDto>> GetPendingAuditListAsync(ResourceListQueryDto input)
    {
        var query = await ResourceRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            query = query.Where(x => x.Name.Contains(input.Filter));
        }

        // Filter by audit permissions - users only see resources they have permission to audit
        var hasSchoolAudit = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Resources.SchoolAudit);
        var hasLeagueAudit = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Resources.LeagueAudit);

        if (hasSchoolAudit && !hasLeagueAudit)
        {
            // School admin: only see pending review resources
            query = query.Where(r => r.Status == ResourceStatus.PendingReview);
        }
        else if (hasLeagueAudit && !hasSchoolAudit)
        {
            // League auditor: only see school-approved resources
            query = query.Where(r => r.Status == ResourceStatus.SchoolApproved);
        }
        else
        {
            // Both permissions: see both statuses
            query = query.Where(r =>
                r.Status == ResourceStatus.PendingReview ||
                r.Status == ResourceStatus.SchoolApproved);
        }

        var totalCount = await AsyncExecuter.CountAsync(query);

        query = query.OrderByDescending(r => r.CreationTime)
                     .Skip(input.SkipCount)
                     .Take(input.MaxResultCount);

        var resources = await AsyncExecuter.ToListAsync(query);
        var dtos = ObjectMapper.Map<List<Resource>, List<ResourceDto>>(resources);
        EnsureFileMetadata(dtos);
        return new PagedResultDto<ResourceDto>(totalCount, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Resources.SchoolAudit)]
    public virtual async Task<ResourceAuditDto> AuditAsync(AuditResourceDto input)
    {
        var resource = await Repository.GetAsync(input.ResourceId);

        if (resource.Status != ResourceStatus.PendingReview)
        {
            throw new UserFriendlyException("只有待审核状态的资源才能进行校审核");
        }

        var audit = new ResourceAudit
        {
            ResourceId = input.ResourceId,
            AuditType = AuditType.Initial,
            Status = input.Status,
            Comment = input.Comment
        };

        await AuditRepository.InsertAsync(audit);

        if (input.Status == AuditStatus.Approved)
        {
            resource.Status = ResourceStatus.SchoolApproved;
        }
        else
        {
            resource.Status = ResourceStatus.Rejected;
        }

        await Repository.UpdateAsync(resource);

        return ObjectMapper.Map<ResourceAudit, ResourceAuditDto>(audit);
    }

    public virtual async Task<List<ResourceAuditDto>> GetAuditsAsync(Guid resourceId)
    {
        var audits = await AuditRepository.GetAuditsAsync(resourceId);
        return ObjectMapper.Map<List<ResourceAudit>, List<ResourceAuditDto>>(audits);
    }

    public virtual async Task SubmitForReviewAsync(Guid resourceId)
    {
        var resource = await Repository.GetAsync(resourceId);
        
        if (resource.Status != ResourceStatus.Draft && resource.Status != ResourceStatus.Rejected)
        {
            throw new UserFriendlyException("只有草稿或已拒绝状态的资源可以提交审核");
        }

        resource.Status = ResourceStatus.PendingReview;
        await Repository.UpdateAsync(resource);
    }

    [Authorize(KnowledgeHubPermissions.Resources.RequestDelete)]
    [IgnoreAntiforgeryToken]
    public virtual async Task<PhysicalDeleteRequestDto> RequestPhysicalDeleteAsync(CreatePhysicalDeleteRequestDto input)
    {
        Resource resource;
        using (DataFilter.Disable<IMultiTenant>())
        {
            resource = await Repository.GetAsync(input.ResourceId);
        }
        
        var existingRequest = await PhysicalDeleteRequestRepository.GetByResourceIdAsync(input.ResourceId);
        if (existingRequest != null && existingRequest.Status == PhysicalDeleteStatus.Pending)
        {
            throw new UserFriendlyException("该资源已有待处理的删除申请");
        }

        var request = new PhysicalDeleteRequest
        {
            ResourceId = input.ResourceId,
            ResourceName = resource.Name,
            Reason = input.Reason,
            Status = PhysicalDeleteStatus.Pending,
            RequesterId = CurrentUser.Id ?? Guid.Empty,
            RequesterName = CurrentUser.Name ?? "Unknown"
        };

        await PhysicalDeleteRequestRepository.InsertAsync(request);
        return ObjectMapper.Map<PhysicalDeleteRequest, PhysicalDeleteRequestDto>(request);
    }

    [Authorize(KnowledgeHubPermissions.Resources.PhysicalDelete)]
    public virtual async Task<PagedResultDto<PhysicalDeleteRequestDto>> GetPendingPhysicalDeleteRequestsAsync(ResourceListQueryDto input)
    {
        var query = await PhysicalDeleteRequestRepository.GetQueryableAsync();
        query = query.Where(x => x.Status == PhysicalDeleteStatus.Pending);
        
        var totalCount = await AsyncExecuter.CountAsync(query);
        
        query = query.OrderByDescending(x => x.CreationTime);
        query = query.Skip(input.SkipCount).Take(input.MaxResultCount);
        
        var requests = await AsyncExecuter.ToListAsync(query);
        var dtos = ObjectMapper.Map<List<PhysicalDeleteRequest>, List<PhysicalDeleteRequestDto>>(requests);
        return new PagedResultDto<PhysicalDeleteRequestDto>(totalCount, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Resources.PhysicalDelete)]
    public virtual async Task<PhysicalDeleteRequestDto> ApprovePhysicalDeleteAsync(Guid id)
    {
        var request = await PhysicalDeleteRequestRepository.GetAsync(id);

        Resource resource;
        using (DataFilter.Disable<IMultiTenant>())
        {
            resource = await Repository.GetAsync(request.ResourceId);
        }

        await CleanupResourceIndexDataAsync(request.ResourceId);

        await FileStorageService.DeleteAsync(resource.FilePath);

        using (DataFilter.Disable<IMultiTenant>())
        {
            await Repository.DeleteAsync(request.ResourceId);
        }

        request.Status = PhysicalDeleteStatus.Approved;
        request.ApproverId = CurrentUser.Id ?? Guid.Empty;
        request.ApproverName = CurrentUser.Name;
        request.ApprovalTime = DateTime.UtcNow;

        await PhysicalDeleteRequestRepository.UpdateAsync(request);

        return ObjectMapper.Map<PhysicalDeleteRequest, PhysicalDeleteRequestDto>(request);
    }

    [Authorize(KnowledgeHubPermissions.Resources.PhysicalDelete)]
    public virtual async Task<PhysicalDeleteRequestDto> RejectPhysicalDeleteAsync(Guid id)
    {
        var request = await PhysicalDeleteRequestRepository.GetAsync(id);
        
        request.Status = PhysicalDeleteStatus.Rejected;
        request.ApproverId = CurrentUser.Id ?? Guid.Empty;
        request.ApproverName = CurrentUser.Name;
        request.ApprovalTime = DateTime.UtcNow;
        
        await PhysicalDeleteRequestRepository.UpdateAsync(request);
        
        return ObjectMapper.Map<PhysicalDeleteRequest, PhysicalDeleteRequestDto>(request);
    }

    [AllowAnonymous]
    public virtual async Task SeedTestDocumentsAsync()
    {
        var testDocs = new List<DocumentPage>
        {
            new DocumentPage { Id = "1", ResourceId = "res-1", ResourceName = "红楼梦", PageNumber = 1, pageContent = "第一回 甄士隐梦幻识通灵 贾雨村风尘怀闺秀", pageTitle = "第一章 梦开始的地方", FileExtension = "pdf", ResourceType = 0, CategoryName = "古典文学", UploadDate = DateTime.UtcNow },
            new DocumentPage { Id = "2", ResourceId = "res-1", ResourceName = "红楼梦", PageNumber = 2, pageContent = "此开卷第一回也。作者自云：因曾历过一番梦幻之后，故将真事隐去，而借『通灵』之说，撰此《石头记》一书也。", pageTitle = "第二章 作者自述", FileExtension = "pdf", ResourceType = 0, CategoryName = "古典文学", UploadDate = DateTime.UtcNow },
            new DocumentPage { Id = "3", ResourceId = "res-2", ResourceName = "西游记", PageNumber = 1, pageContent = "第一回 灵根孕育源流出 心性修持大道生", pageTitle = "第一章 孙悟空诞生", FileExtension = "pdf", ResourceType = 0, CategoryName = "古典文学", UploadDate = DateTime.UtcNow },
            new DocumentPage { Id = "4", ResourceId = "res-2", ResourceName = "西游记", PageNumber = 2, pageContent = "诗曰：混沌未分天地乱，茫茫渺渺无人见。自从盘古破鸿蒙，辟从开来清浊辨。覆载群生仰至仁，发明万物皆成善。", pageTitle = "第二章 天地初开", FileExtension = "pdf", ResourceType = 0, CategoryName = "古典文学", UploadDate = DateTime.UtcNow },
            new DocumentPage { Id = "5", ResourceId = "res-3", ResourceName = "三国演义", PageNumber = 1, pageContent = "第一回 宴桃园豪杰三结义 斩黄巾英雄首立功", pageTitle = "第一章 刘关张结义", FileExtension = "pdf", ResourceType = 0, CategoryName = "古典文学", UploadDate = DateTime.UtcNow },
            new DocumentPage { Id = "6", ResourceId = "res-4", ResourceName = "水浒传", PageNumber = 1, pageContent = "第一回 张天师祈禳瘟疫 洪太尉误走妖魔", pageTitle = "第一章 洪太尉上山", FileExtension = "pdf", ResourceType = 0, CategoryName = "古典文学", UploadDate = DateTime.UtcNow },
            new DocumentPage { Id = "7", ResourceId = "res-5", ResourceName = "Python教程", PageNumber = 1, pageContent = "第一章 Python基础语法\nPython是一种广泛使用的解释型、高级和通用的编程语言。", pageTitle = "第一章 Python入门", FileExtension = "docx", ResourceType = 0, CategoryName = "编程教程", UploadDate = DateTime.UtcNow },
            new DocumentPage { Id = "8", ResourceId = "res-5", ResourceName = "Python教程", PageNumber = 2, pageContent = "第二章 变量和数据类型\nPython中的变量不需要声明类型，直接赋值即可。", pageTitle = "第二章 变量类型", FileExtension = "docx", ResourceType = 0, CategoryName = "编程教程", UploadDate = DateTime.UtcNow },
            new DocumentPage { Id = "9", ResourceId = "res-6", ResourceName = "JavaScript教程", PageNumber = 1, pageContent = "第一章 JavaScript简介\nJavaScript是一种脚本语言，用于网页交互。", pageTitle = "第一章 JS入门", FileExtension = "docx", ResourceType = 0, CategoryName = "编程教程", UploadDate = DateTime.UtcNow },
            new DocumentPage { Id = "10", ResourceId = "res-7", ResourceName = "数据结构", PageNumber = 1, pageContent = "第一章 数组和链表\n数组是最基本的数据结构，链表可以动态添加删除元素。", pageTitle = "第一章 基础数据结构", FileExtension = "pdf", ResourceType = 0, CategoryName = "计算机基础", UploadDate = DateTime.UtcNow },
        };

        await SearchService.AddDocumentsAsync(testDocs);
    }

    public virtual async Task<PagedResultDto<ResourceDto>> SearchAsync(ResourceSearchQueryDto input)
    {
        var query = await Repository.GetQueryableAsync();
        
        query = query.Where(x => x.Status == ResourceStatus.SchoolApproved ||
                                  x.Status == ResourceStatus.LeagueApproved);

        if (!string.IsNullOrWhiteSpace(input.Query))
        {
            var q = input.Query.ToLower();
            query = query.Where(x => 
                (x.Name != null && x.Name.ToLower().Contains(q)) ||
                (x.Description != null && x.Description.ToLower().Contains(q)) ||
                (x.Keywords != null && x.Keywords.ToLower().Contains(q)));
        }

        if (input.ResourceType.HasValue)
        {
            query = query.Where(x => x.ResourceType == input.ResourceType.Value);
        }

        if (input.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == input.CategoryId.Value);
        }

        if (input.MajorId.HasValue)
        {
            query = query.Where(x => x.MajorId == input.MajorId.Value);
        }

        if (input.StartDate.HasValue)
        {
            query = query.Where(x => x.CreationTime >= input.StartDate.Value);
        }

        if (input.EndDate.HasValue)
        {
            query = query.Where(x => x.CreationTime <= input.EndDate.Value);
        }

        var totalCount = query.Count();

        query = query.OrderByDescending(x => x.CreationTime);
        query = query.Skip(input.SkipCount).Take(input.MaxResultCount);

        var items = await AsyncExecuter.ToListAsync(query);
        var dtos = ObjectMapper.Map<List<Resource>, List<ResourceDto>>(items);
        EnsureFileMetadata(dtos);
        await FillMajorNamesAsync(dtos);

        return new PagedResultDto<ResourceDto>(totalCount, dtos);
    }

    [AllowAnonymous]
    public virtual async Task<MeiliSearchResultDto> SearchDocumentsAsync(MeiliSearchQueryDto input)
    {
        var result = await SearchService.SearchAsync(input.Query, input.Limit, input.Offset, input.IndexName);

        // Log search query for analytics
        if (CurrentUser.Id.HasValue && !string.IsNullOrWhiteSpace(input.Query))
        {
            try
            {
                var analyticsService = LazyServiceProvider.LazyGetRequiredService<ISearchAnalyticsService>();
                var sourceType = result.Hits.Any(h => !string.IsNullOrEmpty(h.VideoId) || !string.IsNullOrEmpty(h.VideoName)) ? "video" : "document";
                await analyticsService.LogSearchAsync(
                    CurrentUser.Id.Value,
                    input.Query,
                    0,
                    result.TotalHits,
                    null,
                    sourceType);
            }
            catch { /* don't fail search on analytics error */ }
        }
        
        var dtos = result.Hits.Select(h => {
            var highlightedContent = h._formatted?.pageContent ?? h.pageContent;
            var highlightedTitle = h._formatted?.pageTitle ?? h.pageTitle;
            
            if (string.IsNullOrEmpty(highlightedContent) && !string.IsNullOrEmpty(h.pageContent))
            {
                highlightedContent = GenerateSnippet(h.pageContent, input.Query, 200);
            }
            
            var isVideo = !string.IsNullOrEmpty(h.VideoId) || !string.IsNullOrEmpty(h.VideoName);

            return new DocumentPageSearchResultDto
            {
                Id = h.Id,
                ResourceId = h.ResourceId,
                ResourceName = h.ResourceName,
                PageNumber = h.PageNumber,
                Content = h.pageContent,
                HighlightedContent = highlightedContent,
                Title = highlightedTitle,
                FileExtension = h.FileExtension,
                ResourceType = h.ResourceType,
                CategoryName = h.CategoryName,
                UploadDate = h.UploadDate,
                RelevanceScore = (float)h.RankingScore,
                SourceType = isVideo ? "video" : "document",
                VideoId = h.VideoId,
                VideoName = h.VideoName,
                VideoUrl = h.VideoUrl,
                StartTime = h.StartTime,
                EndTime = h.EndTime,
                EventDescription = h.EventDescription
            };
        }).ToList();

        return new MeiliSearchResultDto
        {
            Items = dtos,
            TotalCount = result.TotalHits,
            ProcessingTimeMs = result.ProcessingTimeMs
        };
    }

    private static string GenerateSnippet(string content, string query, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(query))
            return content ?? string.Empty;

        var lowerContent = content.ToLower();
        var lowerQuery = query.ToLower();
        var index = lowerContent.IndexOf(lowerQuery, StringComparison.Ordinal);

        if (index < 0)
            return content.Length > maxLength ? content.Substring(0, maxLength) + "..." : content;

        var start = Math.Max(0, index - 50);
        var end = Math.Min(content.Length, index + query.Length + maxLength - 50);

        var snippet = content.Substring(start, end - start);
        
        if (start > 0) snippet = "..." + snippet;
        if (end < content.Length) snippet += "...";

        snippet = System.Text.RegularExpressions.Regex.Replace(
            snippet, 
            System.Text.RegularExpressions.Regex.Escape(query), 
            "<mark>$&</mark>", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return snippet;
    }
}
