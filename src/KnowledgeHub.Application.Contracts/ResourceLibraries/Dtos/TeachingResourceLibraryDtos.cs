using System;
using System.Collections.Generic;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.ResourceLibraries.Dtos;

/// <summary>
/// 教师端「教学资源库建设」——教学资源库概要。
///
/// <para>
/// 教学资源库 = 文件资源 + 课程 + 实训 的关联集合。
/// </para>
/// </summary>
public class TeachingLibraryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }

    /// <summary>库可见性：私有 / 公开 / 院校共建。</summary>
    public ResourceLibraryVisibility Visibility { get; set; }

    /// <summary>可见性中文说明（前端直接展示）。</summary>
    public string? VisibilityName { get; set; }

    /// <summary>主学校租户 Id。</summary>
    public Guid OwnerTenantId { get; set; }

    /// <summary>主学校名称。</summary>
    public string? OwnerTenantName { get; set; }

    /// <summary>建设负责人。</summary>
    public string? Principal { get; set; }

    /// <summary>主建专业 Id。</summary>
    public Guid? PrimaryMajorId { get; set; }

    /// <summary>主建专业名称。</summary>
    public string? PrimaryMajorName { get; set; }

    /// <summary>主学校是否为当前租户。</summary>
    public bool IsOwner { get; set; }

    /// <summary>当前租户是否为共建方（关联学校）。</summary>
    public bool IsPartner { get; set; }

    /// <summary>关联学校名称列表（共建库）。</summary>
    public List<string> PartnerSchoolNames { get; set; } = new();

    public int ResourceCount { get; set; }
    public int CourseCount { get; set; }
    public int PracticumCount { get; set; }

    /// <summary>我是否为该库的建设者之一（被分配的教师 / 共建方教师 / 管理员）。</summary>
    public bool CanContribute { get; set; }
}

/// <summary>教师端教学资源库详情。</summary>
public class TeachingLibraryDetailDto : TeachingLibraryDto
{
    /// <summary>共建学校明细。</summary>
    public List<ResourceLibraryMemberDto> Members { get; set; } = new();

    /// <summary>我参与该库的角色说明。</summary>
    public string? MyRoleName { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>最后修改时间。</summary>
    public DateTime? LastModificationTime { get; set; }
}

/// <summary>教学资源库内容项（文件资源 / 课程 / 实训 统一视图）。</summary>
public class TeachingLibraryContentDto
{
    public Guid Id { get; set; }

    /// <summary>内容类型：文件资源 / 课程 / 实训。</summary>
    public LibraryContentType ContentType { get; set; }

    /// <summary>内容类型中文说明。</summary>
    public string? ContentTypeName { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }

    /// <summary>内容归属学校租户 Id（共建库中可能不是当前学校）。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>内容归属学校名称。</summary>
    public string? TenantName { get; set; }

    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }

    /// <summary>创建人 Id。</summary>
    public Guid? CreatorId { get; set; }

    /// <summary>创建人姓名。</summary>
    public string? CreatorName { get; set; }

    /// <summary>文件扩展名（文件资源）。</summary>
    public string? FileExtension { get; set; }

    /// <summary>文件大小（字节，文件资源）。</summary>
    public long? FileSize { get; set; }

    /// <summary>内容状态（对应各内容类型的状态枚举值）。</summary>
    public int? Status { get; set; }

    public DateTime CreationTime { get; set; }

    /// <summary>当前用户是否可将该内容移出资源库。</summary>
    public bool CanDetach { get; set; }
}

/// <summary>教师端「我的教学资源库」列表查询条件。</summary>
public class TeachingLibraryListRequestDto
{
    /// <summary>按名称 / 简介关键字过滤。</summary>
    public string? Filter { get; set; }

    /// <summary>按可见性筛选。</summary>
    public ResourceLibraryVisibility? Visibility { get; set; }

    /// <summary>只看我参与共建的库。</summary>
    public bool? OnlyShared { get; set; }
}

/// <summary>教学资源库内容列表查询条件。</summary>
public class TeachingLibraryContentListRequestDto : PagedAndSortedResultRequestDto
{
    /// <summary>按内容类型筛选；不传则返回全部三类内容。</summary>
    public LibraryContentType? ContentType { get; set; }

    /// <summary>按名称关键字过滤。</summary>
    public string? Filter { get; set; }

    /// <summary>只看我创建的内容。</summary>
    public bool? OnlyMine { get; set; }
}

/// <summary>把已有内容挂进教学资源库。</summary>
public class AttachLibraryContentDto
{
    /// <summary>内容类型：文件资源 / 课程 / 实训。</summary>
    public LibraryContentType ContentType { get; set; }

    /// <summary>内容 Id。</summary>
    public Guid ContentId { get; set; }
}

/// <summary>在教学资源库内新建文件资源。</summary>
public class CreateTeachingLibraryResourceDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>资源类型：Document / Video / Audio / Image / PPT。</summary>
    public ResourceType ResourceType { get; set; } = ResourceType.Document;

    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? FileExtension { get; set; }
    public string? OriginalFileName { get; set; }
    public string? Keywords { get; set; }
    public string? CopyrightInfo { get; set; }
    public bool? IsDownloadable { get; set; }

    /// <summary>专业 Id（库限定专业时必须一致）。</summary>
    public Guid? MajorId { get; set; }
}

/// <summary>在教学资源库内新建课程。</summary>
public class CreateTeachingLibraryCourseDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }

    /// <summary>专业 Id（库限定专业时必须一致）。</summary>
    public Guid? MajorId { get; set; }

    public string? Semester { get; set; }
    public int? Credits { get; set; }
    public int? SemesterHours { get; set; }
}

/// <summary>在教学资源库内新建实训。</summary>
public class CreateTeachingLibraryPracticumDto
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Major { get; set; }
    public string? ClassName { get; set; }
    public decimal? MaxScore { get; set; }
}
