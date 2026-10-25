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
    Task<PublicBrowseDto> GetPublicBrowseAsync(Guid? tenantId, Guid? majorId, string? search, int skipCount, int maxResultCount, bool? onlyPublicCourses = null);

    /// <summary>
    /// 获取所有租户的资源库摘要列表（公开访问）
    /// </summary>
    Task<List<TenantResourceSummaryDto>> GetPublicTenantListAsync();

    /// <summary>
    /// 获取首页公共统计数据（公开访问）
    /// </summary>
    Task<PublicHomeStatsDto> GetPublicHomeStatsAsync();

    /// <summary>
    /// 获取下载量最高的资源（跨所有租户），用于首页"资源排行"展示
    /// </summary>
    Task<List<MaterialBriefDto>> GetTopResourcesByDownloadAsync(int count = 10);

    /// <summary>
    /// 获取公开的资源库（多校共建专业库）列表，跨租户。
    /// 仅返回 <c>IsPublic=true</c> 且状态为「已发布」的库。
    /// </summary>
    Task<List<PublicResourceLibraryDto>> GetPublicLibraryListAsync();

    /// <summary>
    /// 获取单个公开资源库的详情（含参建单位、课程、资源、微专业、师资与统计），跨租户。
    /// </summary>
    Task<PublicResourceLibraryDetailDto> GetPublicLibraryAsync(Guid id);
}

/// <summary>
/// 公开的资源库摘要（门户「共建共享资源库」列表卡片 / 首页重点共建卡片）。
/// </summary>
public class PublicResourceLibraryDto
{
    /// <summary>资源库 Id。</summary>
    public Guid Id { get; set; }

    /// <summary>资源库名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>资源库简介。</summary>
    public string? Description { get; set; }

    /// <summary>封面图 URL。</summary>
    public string? CoverUrl { get; set; }

    /// <summary>主建专业 Id。</summary>
    public Guid? PrimaryMajorId { get; set; }

    /// <summary>主建专业名称（库的专业维度）。</summary>
    public string? PrimaryMajorName { get; set; }

    /// <summary>主建学校租户 Id。</summary>
    public Guid OwnerTenantId { get; set; }

    /// <summary>主建学校名称（展示用中文校名）。</summary>
    public string? OwnerTenantName { get; set; }

    /// <summary>主建学校徽标 URL。</summary>
    public string? OwnerSchoolLogoUrl { get; set; }

    /// <summary>建设负责人（自然人姓名）。</summary>
    public string? Principal { get; set; }

    /// <summary>建设学校数（含主建学校）。</summary>
    public int MemberCount { get; set; }

    /// <summary>参与建设的学校名称（不含主建学校）。</summary>
    public List<string> PartnerNames { get; set; } = new();

    /// <summary>库内课程数。</summary>
    public int CourseCount { get; set; }

    /// <summary>库内资源素材数。</summary>
    public int ResourceCount { get; set; }

    /// <summary>主建学校的微专业数。</summary>
    public int MicroMajorCount { get; set; }

    /// <summary>库状态（0=草稿 1=已发布 2=已归档）。</summary>
    public int Status { get; set; }
}

/// <summary>资源库参建单位（公开视图）。</summary>
public class PublicLibraryMemberDto
{
    /// <summary>成员学校租户 Id。</summary>
    public Guid MemberTenantId { get; set; }

    /// <summary>成员学校名称。</summary>
    public string? MemberTenantName { get; set; }

    /// <summary>共建角色：0=主建学校，1=参与建设学校。</summary>
    public int Role { get; set; }

    /// <summary>加入时间。</summary>
    public DateTime JoinedTime { get; set; }
}

/// <summary>资源库内课程（公开视图）。</summary>
public class PublicLibraryCourseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? TeacherName { get; set; }
    public string? MajorName { get; set; }
    public string? Description { get; set; }
    public int StudentCount { get; set; }
    public int SemesterHours { get; set; }
    public int Credits { get; set; }
    public int Difficulty { get; set; }
}

/// <summary>资源库内资源素材（公开视图）。</summary>
public class PublicLibraryResourceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public string? OriginalFileName { get; set; }
    public long FileSize { get; set; }
    public int DownloadCount { get; set; }
    public string? CoverUrl { get; set; }
    /// <summary>资源类型名称（用于「企业案例 / 标准规范」等栏目归类展示）。</summary>
    public string? ResourceTypeName { get; set; }
    public DateTime? CreationTime { get; set; }
}

/// <summary>资源库关联微专业（公开视图）。</summary>
public class PublicLibraryMicroMajorDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? CollaborationUnit { get; set; }
    public int CourseCount { get; set; }
    public int ResourceCount { get; set; }
}

/// <summary>资源库师资（公开视图）。</summary>
public class PublicLibraryTeacherDto
{
    /// <summary>教师姓名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>在库内承担的课程数。</summary>
    public int CourseCount { get; set; }
}

/// <summary>公开的资源库详情（门户「资源库详情」页）。</summary>
public class PublicResourceLibraryDetailDto : PublicResourceLibraryDto
{
    /// <summary>参建单位（含主建学校）。</summary>
    public List<PublicLibraryMemberDto> Members { get; set; } = new();

    /// <summary>库内课程。</summary>
    public List<PublicLibraryCourseDto> Courses { get; set; } = new();

    /// <summary>库内资源素材。</summary>
    public List<PublicLibraryResourceDto> Resources { get; set; } = new();

    /// <summary>关联微专业。</summary>
    public List<PublicLibraryMicroMajorDto> MicroMajors { get; set; } = new();

    /// <summary>库内师资。</summary>
    public List<PublicLibraryTeacherDto> Teachers { get; set; } = new();

    /// <summary>库内课程的学习人数合计。</summary>
    public int StudentCount { get; set; }
}

public class PortalHomeDataDto
{
    public TenantBriefDto TenantInfo { get; set; } = new();
    public PortalStatsDto Stats { get; set; } = new();
    public List<MicroMajorBriefDto> MicroMajors { get; set; } = new();
    /// <summary>
    /// 首页"精选课程"：仅展示已发布且标记为推荐的课程（IsRecommended=true）
    /// </summary>
    public List<CourseBriefDto> FeaturedCourses { get; set; } = new();
    /// <summary>
    /// 租户首页"学历课程体系"：该租户下所有已发布课程（含未标记推荐的），
    /// 与 FeaturedCourses 分开，避免新租户/未勾推荐时该区域空白。
    /// </summary>
    public List<CourseBriefDto> PublishedCourses { get; set; } = new();
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
    /// <summary>资源库名称（专业库配置名），为空时退回租户名</summary>
    public string? LibraryName { get; set; }
    /// <summary>建设单位（学校档案名称），用于资源库详情与卡片展示</summary>
    public string? SchoolName { get; set; }
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
    /// <summary>归属的全部专业名（主专业排第一），为空表示公共课</summary>
    public List<string> MajorNames { get; set; } = new();
    /// <summary>归属的全部专业 Id（主专业排第一），为空表示公共课</summary>
    public List<Guid> MajorIds { get; set; } = new();
    public int StudentCount { get; set; }
    public int Difficulty { get; set; }
    /// <summary>是否共享课：共享给「社会学校」后，所有注册用户均可学习</summary>
    public bool IsShared { get; set; }
    /// <summary>学时</summary>
    public int SemesterHours { get; set; }
    /// <summary>学分</summary>
    public int Credits { get; set; }
}

public class MaterialBriefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public int DownloadCount { get; set; }
    public string? CoverUrl { get; set; }
    public long FileSize { get; set; }
    public string? OriginalFileName { get; set; }
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
    /// <summary>归属的全部专业名（主专业排第一），为空表示公共课</summary>
    public List<string> MajorNames { get; set; } = new();
    /// <summary>归属的全部专业 Id（主专业排第一），为空表示公共课</summary>
    public List<Guid> MajorIds { get; set; } = new();
    public string? TenantName { get; set; }
    public Guid TenantId { get; set; }
    public int StudentCount { get; set; }
    public string? Description { get; set; }
    /// <summary>是否共享课：共享给「社会学校」后，所有注册用户均可学习</summary>
    public bool IsShared { get; set; }
    /// <summary>学时</summary>
    public int SemesterHours { get; set; }
    /// <summary>学分</summary>
    public int Credits { get; set; }
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
    public long FileSize { get; set; }
    public string? OriginalFileName { get; set; }
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
