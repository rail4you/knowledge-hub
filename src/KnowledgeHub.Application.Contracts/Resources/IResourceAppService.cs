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

    /// <summary>
    /// 资源库维度侧边栏数据：当前可见资源在各资源库下的数量，末尾附「未分类」一项。
    /// </summary>
    Task<List<ResourceLibraryFacetDto>> GetLibraryFacetsAsync();

    /// <summary>
    /// 资源中心「教学资源库」侧边栏：当前用户可见资源在各教学资源库下的数量。
    ///
    /// <para>
    /// 与 <see cref="GetLibraryFacetsAsync"/>（资源表单 / 工作台选库用的「可关联库」列表）**不同**：
    /// 本接口只返回「实际有可见资源」的库，且计数口径与列表的 <c>libraryId</c> 筛选完全一致
    /// （本租户自有 + 共建库共享进来的他校已审资源），避免「侧边栏 0、点进去却有内容」。
    /// </para>
    /// </summary>
    Task<List<ResourceLibraryFacetDto>> GetLibraryContentFacetsAsync();

    /// <summary>
    /// 课程维度侧边栏数据：当前可见资源在各课程下的数量，末尾附「未关联课程」一项。
    /// </summary>
    Task<List<ResourceCourseFacetDto>> GetCourseFacetsAsync();
    Task<ResourceVersionDto> UploadVersionAsync(UploadVersionDto input);
    Task<List<ResourceVersionDto>> GetVersionsAsync(Guid resourceId);
    Task<ResourceVersionDto> RollbackVersionAsync(Guid versionId);
    Task<bool> IsCollectedAsync(Guid resourceId);

    /// <summary>批量查询当前用户已收藏的资源 Id，避免列表页 N+1 请求。</summary>
    Task<List<Guid>> CheckCollectedStatusAsync(List<Guid> resourceIds);
    Task CollectAsync(Guid resourceId);
    Task UncollectAsync(Guid resourceId);
    Task<List<ResourceCategoryDto>> GetCategoriesAsync(Guid? majorId = null);
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

    /// <summary>创建者删除自己尚未审核通过的资源（草稿 / 待审核 / 已驳回），无需再走审核。</summary>
    Task DeleteOwnUnapprovedAsync(Guid id);
    
    Task<PhysicalDeleteRequestDto> RequestPhysicalDeleteAsync(CreatePhysicalDeleteRequestDto input);
    Task<PagedResultDto<PhysicalDeleteRequestDto>> GetPendingPhysicalDeleteRequestsAsync(ResourceListQueryDto input);
    Task<PhysicalDeleteRequestDto> ApprovePhysicalDeleteAsync(Guid id);
    Task<PhysicalDeleteRequestDto> RejectPhysicalDeleteAsync(Guid id);
    
    Task<PagedResultDto<ResourceDto>> GetLeagueApprovedAsync(PagedResultRequestDto input);
    Task<PagedResultDto<ResourceDto>> GetCollectedListAsync(ResourceListQueryDto input);
    
    Task<PagedResultDto<ResourceDto>> SearchAsync(ResourceSearchQueryDto input);
    Task<MeiliSearchResultDto> SearchDocumentsAsync(MeiliSearchQueryDto input);
    Task SeedTestDocumentsAsync();
}

public class ResourceListQueryDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public ResourceStatus? Status { get; set; }
    /// <summary>
    /// 可选的多个状态（与 <see cref="Status"/> 二选一，同时给出时以本项为准）。
    /// 用于「资源中心」按 [SchoolApproved, LeagueApproved] 取已在本校层面审核通过的资源 ——
    /// 与其余接口（已发布列表、首页、搜索推荐均按 Status &gt;= SchoolApproved）保持同一口径，
    /// 否则会出现「相关推荐里能点开、列表里却搜不到」的死角。
    /// </summary>
    public List<ResourceStatus>? Statuses { get; set; }
    public ResourceType? ResourceType { get; set; }

    /// <summary>按模块（业务栏目）过滤；为空表示不过滤。</summary>
    public ResourceModule? Module { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? MajorId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// 资源库维度过滤：资源库 Id 字符串；传 <c>unclassified</c> 表示「未分类」（未关联任何资源库）。
    /// 留空表示不按资源库过滤。
    /// </summary>
    public string? LibraryId { get; set; }

    /// <summary>
    /// 课程维度过滤：课程 Id 字符串；传 <c>unclassified</c> 表示「未关联课程」。
    /// 留空表示不按课程过滤。
    /// </summary>
    public string? CourseId { get; set; }

    /// <summary>
    /// 只返回该用户创建的资源。
    ///
    /// <para>老师端「教学资源库工作台 · 共享资源」必须只能共享自己创建的资源，
    /// 前端传当前用户 Id 即只拿自己的资源，避免把同事的资源挂到库里。</para>
    /// </summary>
    public Guid? CreatorId { get; set; }
}

public class ResourceSearchQueryDto : PagedAndSortedResultRequestDto
{
    public string? Query { get; set; }
    public ResourceType? ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? MajorId { get; set; }
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
    public string IndexName { get; set; } = "documents";
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
    public float RelevanceScore { get; set; }

    /// <summary>"document" | "video"</summary>
    public string SourceType { get; set; } = "document";
    public string? VideoId { get; set; }
    public string? VideoName { get; set; }
    public string? VideoUrl { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? EventDescription { get; set; }
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

// =================== 资源共享（跨租户） ===================

