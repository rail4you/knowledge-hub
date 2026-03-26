using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Common;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
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
        IRepository<DocumentIndexingJob, Guid> indexingJobRepository)
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
    }

    public virtual async Task<ResourceDto> GetAsync(Guid id)
    {
        var resource = await ResourceRepository.GetWithDetailsAsync(id);
        return ObjectMapper.Map<Resource, ResourceDto>(resource);
    }

    public virtual async Task<PagedResultDto<ResourceDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var totalCount = await ResourceRepository.GetCountAsync("");
        var resources = await ResourceRepository.GetListAsync(
            input.SkipCount,
            input.MaxResultCount,
            input.Sorting ?? "CreationTime DESC",
            ""
        );

        return new PagedResultDto<ResourceDto>(
            totalCount,
            ObjectMapper.Map<List<Resource>, List<ResourceDto>>(resources)
        );
    }

    public virtual async Task<ResourceDto> GetWithVersionsAsync(Guid id)
    {
        var resource = await ResourceRepository.GetWithDetailsAsync(id);
        return ObjectMapper.Map<Resource, ResourceDto>(resource);
    }

    public virtual async Task<PagedResultDto<ResourceDto>> GetFilteredListAsync(ResourceListQueryDto input)
    {
        var totalCount = await ResourceRepository.GetCountAsync(input.Filter ?? "");
        var resources = await ResourceRepository.GetListAsync(
            input.SkipCount,
            input.MaxResultCount,
            input.Sorting ?? "CreationTime DESC",
            input.Filter ?? ""
        );

        var dtos = ObjectMapper.Map<List<Resource>, List<ResourceDto>>(resources);

        if (input.Status.HasValue)
        {
            dtos = dtos.Where(x => x.Status == input.Status).ToList();
        }
        if (input.ResourceType.HasValue)
        {
            dtos = dtos.Where(x => x.ResourceType == input.ResourceType).ToList();
        }
        if (input.CategoryId.HasValue)
        {
            dtos = dtos.Where(x => x.CategoryId == input.CategoryId).ToList();
        }

        return new PagedResultDto<ResourceDto>(totalCount, dtos);
    }

    [AllowAnonymous]
    public async Task<PagedResultDto<ResourceDto>> GetLeagueApprovedAsync(PagedResultRequestDto input)
    {
        var query = await ResourceRepository.GetQueryableAsync();
        
        var resources = query
            .Where(r => r.Status == ResourceStatus.SchoolApproved || r.Status == ResourceStatus.LeagueApproved)
            .OrderByDescending(r => r.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();
        
        var count = query.Count(r => r.Status == ResourceStatus.SchoolApproved || r.Status == ResourceStatus.LeagueApproved);
        
        return new PagedResultDto<ResourceDto>(
            count,
            ObjectMapper.Map<List<Resource>, List<ResourceDto>>(resources)
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
            UpdateContent = "Initial version",
            IsCurrentVersion = true
        };
        await VersionRepository.InsertAsync(initialVersion);

        // Trigger document indexing job
        if (!string.IsNullOrEmpty(resource.FilePath))
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
                    TenantId = CurrentTenant.Id
                });
                Logger.LogInformation("Successfully enqueued background job for DocumentIndexingJob {JobId}", indexingJob.Id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to enqueue background job for DocumentIndexingJob {JobId}", indexingJob.Id);
                throw;
            }
        }

        return ObjectMapper.Map<Resource, ResourceDto>(resource);
    }

    [Authorize(KnowledgeHubPermissions.Resources.Edit)]
    public virtual async Task<ResourceDto> UpdateAsync(Guid id, CreateUpdateResourceDto input)
    {
        var resource = await Repository.GetAsync(id);
        
        var oldVersions = await VersionRepository.GetVersionsAsync(id);
        var currentVersions = oldVersions.Where(x => x.IsCurrentVersion).ToList();
        foreach (var oldVersion in currentVersions)
        {
            oldVersion.IsCurrentVersion = false;
            await VersionRepository.UpdateAsync(oldVersion);
        }

        var newVersion = new ResourceVersion
        {
            ResourceId = id,
            Version = resource.CurrentVersion + 1,
            FilePath = resource.FilePath,
            FileSize = resource.FileSize,
            UpdateContent = input.Description ?? "Updated",
            IsCurrentVersion = true
        };
        await VersionRepository.InsertAsync(newVersion);

        ObjectMapper.Map(input, resource);
        resource.CurrentVersion = newVersion.Version;
        resource.Status = ResourceStatus.PendingReview;
        
        await Repository.UpdateAsync(resource);
        return ObjectMapper.Map<Resource, ResourceDto>(resource);
    }

    [Authorize(KnowledgeHubPermissions.Resources.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
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

    public virtual async Task<List<ResourceCategoryDto>> GetCategoriesAsync()
    {
        var categories = await CategoryRepository.GetListAsync();
        return ObjectMapper.Map<List<ResourceCategory>, List<ResourceCategoryDto>>(categories);
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
        var resource = await Repository.GetAsync(resourceId);

        resource.DownloadCount++;
        await Repository.UpdateAsync(resource);

        var stream = await FileStorageService.GetAsync(resource.FilePath);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    protected virtual Resource MapToEntity(CreateUpdateResourceDto input)
    {
        var resource = ObjectMapper.Map<CreateUpdateResourceDto, Resource>(input);
        resource.Status = ResourceStatus.Draft;
        resource.CurrentVersion = 1;
        resource.CollectionCount = 0;
        resource.DownloadCount = 0;
        resource.ViewCount = 0;
        resource.TenantId = CurrentTenant.Id;
        return resource;
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

    [Authorize(KnowledgeHubPermissions.Resources.SchoolAudit)]
    public virtual async Task<PagedResultDto<ResourceDto>> GetPendingAuditListAsync(ResourceListQueryDto input)
    {
        var resources = await ResourceRepository.GetListAsync(
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount,
            sorting: input.Sorting ?? "CreationTime",
            filter: input.Filter
        );

        var filteredResources = resources.Where(r => 
            r.Status == ResourceStatus.PendingReview || 
            r.Status == ResourceStatus.SchoolApproved).ToList();
        
        var dtos = ObjectMapper.Map<List<Resource>, List<ResourceDto>>(filteredResources);
        return new PagedResultDto<ResourceDto>(dtos.Count, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Resources.SchoolAudit)]
    public virtual async Task<ResourceAuditDto> AuditAsync(AuditResourceDto input)
    {
        var resource = await Repository.GetAsync(input.ResourceId);
        
        var audit = new ResourceAudit
        {
            ResourceId = input.ResourceId,
            AuditType = resource.Status == ResourceStatus.PendingReview ? AuditType.Initial : AuditType.Final,
            Status = input.Status,
            Comment = input.Comment
        };

        await AuditRepository.InsertAsync(audit);

        if (input.Status == AuditStatus.Approved)
        {
            if (resource.Status == ResourceStatus.PendingReview)
            {
                resource.Status = ResourceStatus.SchoolApproved;
            }
            else if (resource.Status == ResourceStatus.SchoolApproved)
            {
                resource.Status = ResourceStatus.LeagueApproved;
            }
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

    [Authorize(KnowledgeHubPermissions.Resources.PhysicalDelete)]
    public virtual async Task<PhysicalDeleteRequestDto> RequestPhysicalDeleteAsync(CreatePhysicalDeleteRequestDto input)
    {
        var resource = await Repository.GetAsync(input.ResourceId);
        
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
        var requests = await PhysicalDeleteRequestRepository.GetPendingRequestsAsync();
        
        var dtos = ObjectMapper.Map<List<PhysicalDeleteRequest>, List<PhysicalDeleteRequestDto>>(requests);
        return new PagedResultDto<PhysicalDeleteRequestDto>(dtos.Count, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Resources.PhysicalDelete)]
    public virtual async Task<PhysicalDeleteRequestDto> ApprovePhysicalDeleteAsync(Guid id)
    {
        var request = await PhysicalDeleteRequestRepository.GetAsync(id);
        
        var resource = await Repository.GetAsync(request.ResourceId);
        
        await FileStorageService.DeleteAsync(resource.FilePath);
        
        await Repository.DeleteAsync(request.ResourceId);
        
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
        
        query = query.Where(x => x.Status == ResourceStatus.Draft || 
                                  x.Status == ResourceStatus.SchoolApproved || 
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
        
        return new PagedResultDto<ResourceDto>(totalCount, dtos);
    }

    [AllowAnonymous]
    public virtual async Task<MeiliSearchResultDto> SearchDocumentsAsync(MeiliSearchQueryDto input)
    {
        var result = await SearchService.SearchAsync(input.Query, input.Limit, input.Offset, input.IndexName);
        
        var dtos = result.Hits.Select(h => {
            var highlightedContent = h._formatted?.pageContent ?? h.pageContent;
            var highlightedTitle = h._formatted?.pageTitle ?? h.pageTitle;
            
            if (string.IsNullOrEmpty(highlightedContent) && !string.IsNullOrEmpty(h.pageContent))
            {
                highlightedContent = GenerateSnippet(h.pageContent, input.Query, 200);
            }
            
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
                UploadDate = h.UploadDate
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
