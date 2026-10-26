using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.ResourceLibraries.Dtos;

/// <summary>
/// 教学资源库目录卡片（公开门户「教学资源库」页使用）。
///
/// <para>
/// 对应原型 <c>#courses</c>：把原来的课程列表改成**资源库列表**，
/// 并按「校内资源库 / 公共共享资源库」两个分区展示（三种可见性归到两个分区里）。
/// </para>
/// </summary>
public class TeachingLibraryCardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }

    /// <summary>主建专业 Id。</summary>
    public Guid? PrimaryMajorId { get; set; }

    /// <summary>主建专业名称。</summary>
    public string? PrimaryMajorName { get; set; }

    /// <summary>主建学校租户 Id。</summary>
    public Guid OwnerTenantId { get; set; }

    /// <summary>主建学校名称（学校档案中文名优先）。</summary>
    public string? OwnerTenantName { get; set; }

    /// <summary>主建学校徽标。</summary>
    public string? OwnerSchoolLogoUrl { get; set; }

    /// <summary>建设负责人（自然人姓名）。</summary>
    public string? Principal { get; set; }

    /// <summary>可见性：0 私有 / 1 公开 / 2 院校共建。</summary>
    public int Visibility { get; set; }

    /// <summary>可见性中文名。</summary>
    public string? VisibilityName { get; set; }

    /// <summary>建设学校数（含主建学校）。</summary>
    public int MemberCount { get; set; }

    /// <summary>参建学校名称（不含主建学校）。</summary>
    public List<string> PartnerNames { get; set; } = new();

    /// <summary>库内课程数。</summary>
    public int CourseCount { get; set; }

    /// <summary>库内资源数。</summary>
    public int ResourceCount { get; set; }

    /// <summary>所属分区：0 校内资源库（本校主建）/ 1 公共共享资源库（他校公开 / 共建）。</summary>
    public int Scope { get; set; }
}

/// <summary>教学资源库目录查询条件。</summary>
public class TeachingLibraryCatalogRequestDto : PagedAndSortedResultRequestDto
{
    /// <summary>分区：school 校内（本校主建，含私有/公开/共建）；public 公共共享（他校公开或共建）。</summary>
    public string? Scope { get; set; }

    /// <summary>按名称 / 简介 / 主建专业 / 主建学校关键字过滤。</summary>
    public string? Filter { get; set; }

    /// <summary>按主建专业过滤。</summary>
    public Guid? PrimaryMajorId { get; set; }
}
