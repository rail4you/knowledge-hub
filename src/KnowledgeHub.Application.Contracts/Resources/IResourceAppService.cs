using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Resources;

public interface IResourceAppService : ICrudAppService<ResourceDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateResourceDto>
{
    Task<ResourceDto> GetWithVersionsAsync(Guid id);
    Task<PagedResultDto<ResourceDto>> GetFilteredListAsync(ResourceListQueryDto input);
    Task<ResourceVersionDto> UploadVersionAsync(UploadVersionDto input);
    Task<List<ResourceVersionDto>> GetVersionsAsync(Guid resourceId);
    Task<ResourceVersionDto> RollbackVersionAsync(Guid versionId);
    Task<bool> IsCollectedAsync(Guid resourceId);
    Task CollectAsync(Guid resourceId);
    Task UncollectAsync(Guid resourceId);
    Task<List<ResourceCategoryDto>> GetCategoriesAsync();
    Task<ResourceCategoryDto> CreateCategoryAsync(CreateUpdateResourceCategoryDto input);
    Task<ResourceCategoryDto> UpdateCategoryAsync(Guid id, CreateUpdateResourceCategoryDto input);
    Task DeleteCategoryAsync(Guid id);
    Task<string> GetFileUrlAsync(Guid resourceId);
    Task<byte[]> DownloadAsync(Guid resourceId);
    
    Task<InitiateUploadResultDto> InitiateUploadAsync(InitiateUploadDto input);
    Task<bool> UploadChunkAsync(UploadChunkDto input);
    Task<CompleteUploadResultDto> CompleteUploadAsync(CompleteUploadDto input);
    Task<bool> CancelUploadAsync(string uploadId);
    
    Task<PagedResultDto<ResourceDto>> GetPendingAuditListAsync(ResourceListQueryDto input);
    Task<ResourceAuditDto> AuditAsync(AuditResourceDto input);
    Task<List<ResourceAuditDto>> GetAuditsAsync(Guid resourceId);
    Task SubmitForReviewAsync(Guid resourceId);
    
    Task<PhysicalDeleteRequestDto> RequestPhysicalDeleteAsync(CreatePhysicalDeleteRequestDto input);
    Task<PagedResultDto<PhysicalDeleteRequestDto>> GetPendingPhysicalDeleteRequestsAsync(ResourceListQueryDto input);
    Task<PhysicalDeleteRequestDto> ApprovePhysicalDeleteAsync(Guid id);
    Task<PhysicalDeleteRequestDto> RejectPhysicalDeleteAsync(Guid id);
    
    Task<PagedResultDto<ResourceDto>> SearchAsync(ResourceSearchQueryDto input);
    Task<MeiliSearchResultDto> SearchDocumentsAsync(MeiliSearchQueryDto input);
    Task SeedTestDocumentsAsync();
}

public class ResourceListQueryDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public ResourceStatus? Status { get; set; }
    public ResourceType? ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
}

public class ResourceSearchQueryDto : PagedAndSortedResultRequestDto
{
    public string? Query { get; set; }
    public ResourceType? ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class MeiliSearchQueryDto
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 20;
    public int Offset { get; set; } = 0;
    public int? ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
}

public class MeiliSearchResultDto
{
    public List<DocumentPageSearchResultDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public double ProcessingTimeMs { get; set; }
}

public class DocumentPageSearchResultDto
{
    public string Id { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? HighlightedContent { get; set; }
    public string? Title { get; set; }
    public string FileExtension { get; set; } = string.Empty;
    public int ResourceType { get; set; }
    public string? CategoryName { get; set; }
    public DateTime UploadDate { get; set; }
}

public interface IResourceAuditAppService
{
    Task<PagedResultDto<ResourceDto>> GetPendingListAsync(ResourceListQueryDto input);
    Task<ResourceAuditDto> AuditAsync(AuditResourceDto input);
    Task<List<ResourceAuditDto>> GetAuditsAsync(Guid resourceId);
}

public interface IResourceCategoryAppService : ICrudAppService<ResourceCategoryDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateResourceCategoryDto>
{
    Task<List<ResourceCategoryDto>> GetTreeAsync();
}
