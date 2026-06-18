using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Portal;

public interface IPortalAppService : IApplicationService
{
    Task<PortalHomeDataDto> GetHomeDataAsync(Guid tenantId);

    /// <summary>
    /// 获取所有租户的公开浏览数据（游客可见）：课程、资源、微专业 + 租户/专业筛选列表
    /// </summary>
    Task<PublicBrowseDto> GetPublicBrowseAsync(Guid? tenantId, Guid? majorId, string? search, int skipCount, int maxResultCount);

    /// <summary>
    /// 获取所有租户的资源库摘要列表（公开访问）
    /// </summary>
    Task<List<TenantResourceSummaryDto>> GetPublicTenantListAsync();

    /// <summary>
    /// 获取首页公共统计数据（公开访问）
    /// </summary>
    Task<PublicHomeStatsDto> GetPublicHomeStatsAsync();
}

public class PortalHomeDataDto
{
    public TenantBriefDto TenantInfo { get; set; } = new();
    public PortalStatsDto Stats { get; set; } = new();
    public List<MicroMajorBriefDto> MicroMajors { get; set; } = new();
    public List<CourseBriefDto> FeaturedCourses { get; set; } = new();
    public List<MaterialBriefDto> LatestMaterials { get; set; } = new();
    public List<NewsBriefDto> LatestNews { get; set; } = new();
    public List<PartnerBriefDto> Partners { get; set; } = new();
}

/// <summary>
/// 租户资源库摘要（公开访问）
/// </summary>
public class TenantResourceSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? IndustryField { get; set; }
    public int TenantType { get; set; } = 0; // 0=专业, 1=项目
    public string? TenantName { get; set; } // 取自 TenantInfo 的显示名称
    public string? TenantDescription { get; set; } // 取自 TenantInfo
    public string? CoverImage { get; set; } // 第一张封面图
    public int CourseCount { get; set; }
    public int ResourceCount { get; set; }
    public int MicroMajorCount { get; set; }
}

/// <summary>
/// 首页公共统计数据（公开访问）
/// </summary>
public class PublicHomeStatsDto
{
    public int TenantCount { get; set; }
    public int TotalCourseCount { get; set; }
    public int TotalResourceCount { get; set; }
    public int TotalMicroMajorCount { get; set; }
}

public class PartnerBriefDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class TenantBriefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? IndustryField { get; set; }
}

public class PortalStatsDto
{
    public int CourseCount { get; set; }
    public int ResourceCount { get; set; }
    public int StudentCount { get; set; }
    public int MicroMajorCount { get; set; }
}

public class MicroMajorBriefDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public int CourseCount { get; set; }
}

public class CourseBriefDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? TeacherName { get; set; }
    public string? MajorName { get; set; }
    public int StudentCount { get; set; }
}

public class MaterialBriefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public int DownloadCount { get; set; }
    public string? CoverUrl { get; set; }
}

public class NewsBriefDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
}

// ── Public Browse DTOs ──

/// <summary>
/// 公开浏览数据：所有租户的课程/资源/微专业 + 筛选器选项
/// </summary>
public class PublicBrowseDto
{
    public List<PublicCourseDto> Courses { get; set; } = new();
    public List<PublicResourceDto> Resources { get; set; } = new();
    public List<PublicMicroMajorDto> MicroMajors { get; set; } = new();
    public List<PublicBrowseFilterOption> Tenants { get; set; } = new();
    public List<PublicBrowseFilterOption> Majors { get; set; } = new();
    public long TotalCourseCount { get; set; }
    public long TotalResourceCount { get; set; }
    public long TotalMicroMajorCount { get; set; }
}

public class PublicCourseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? TeacherName { get; set; }
    public string? MajorName { get; set; }
    public Guid? MajorId { get; set; }
    public string? TenantName { get; set; }
    public Guid TenantId { get; set; }
    public int StudentCount { get; set; }
    public string? Description { get; set; }
}

public class PublicResourceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public int DownloadCount { get; set; }
    public string? CoverUrl { get; set; }
    public string? TenantName { get; set; }
    public Guid TenantId { get; set; }
}

public class PublicMicroMajorDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public int CourseCount { get; set; }
    public string? TenantName { get; set; }
    public Guid TenantId { get; set; }
}

public class PublicBrowseFilterOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
