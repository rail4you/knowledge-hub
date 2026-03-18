using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Common;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
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
    protected ICurrentTenant CurrentTenant { get; }
    protected IHttpContextAccessor HttpContextAccessor { get; }

    public ResourceAppService(
        IRepository<Resource, Guid> repository,
        IResourceRepository resourceRepository,
        IResourceVersionRepository versionRepository,
        IResourceCategoryRepository categoryRepository,
        IResourceCollectionRepository collectionRepository,
        IResourceAuditRepository auditRepository,
        IPhysicalDeleteRequestRepository physicalDeleteRequestRepository,
        IFileStorageService fileStorageService,
        ICurrentTenant currentTenant,
        IHttpContextAccessor httpContextAccessor)
    {
        Repository = repository;
        ResourceRepository = resourceRepository;
        VersionRepository = versionRepository;
        CategoryRepository = categoryRepository;
        CollectionRepository = collectionRepository;
        AuditRepository = auditRepository;
        PhysicalDeleteRequestRepository = physicalDeleteRequestRepository;
        FileStorageService = fileStorageService;
        CurrentTenant = currentTenant;
        HttpContextAccessor = httpContextAccessor;
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

    [Authorize(KnowledgeHubPermissions.Resources.Create)]
    public virtual async Task<ResourceDto> CreateAsync(CreateUpdateResourceDto input)
    {
        var resource = MapToEntity(input);
        await Repository.InsertAsync(resource);
        return ObjectMapper.Map<Resource, ResourceDto>(resource);
    }

    [Authorize(KnowledgeHubPermissions.Resources.Edit)]
    public virtual async Task<ResourceDto> UpdateAsync(Guid id, CreateUpdateResourceDto input)
    {
        var resource = await Repository.GetAsync(id);
        ObjectMapper.Map(input, resource);
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
            FilePath = resource.FilePath,
            FileSize = resource.FileSize,
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
        var userId = CurrentUser.Id ?? Guid.Empty;
        return await CollectionRepository.IsCollectedAsync(resourceId, userId);
    }

    public virtual async Task CollectAsync(Guid resourceId)
    {
        var userId = CurrentUser.Id ?? Guid.Empty;

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
        var userId = CurrentUser.Id;

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

        var filteredResources = resources.Where(r => r.Status == ResourceStatus.PendingReview).ToList();
        
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
            AuditType = input.AuditType,
            Status = input.Status,
            Comment = input.Comment
        };

        await AuditRepository.InsertAsync(audit);

        if (input.Status == AuditStatus.Approved)
        {
            if (input.AuditType == AuditType.Initial)
            {
                resource.Status = ResourceStatus.SchoolApproved;
            }
            else
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
}
