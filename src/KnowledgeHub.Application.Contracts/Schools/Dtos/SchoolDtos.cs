using System;
using System.Collections.Generic;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.ResourceLibraries.Dtos;

namespace KnowledgeHub.Schools.Dtos;

/// <summary>学校档案。</summary>
public class SchoolDto
{
    /// <summary>学校档案 Id。</summary>
    public Guid Id { get; set; }

    /// <summary>关联的租户 Id。</summary>
    public Guid TenantId { get; set; }

    /// <summary>学校名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>学校徽标 URL。</summary>
    public string? LogoUrl { get; set; }

    /// <summary>学校横幅图 URL。</summary>
    public string? BannerUrl { get; set; }

    /// <summary>学校简介（HTML 富文本）。</summary>
    public string? Introduction { get; set; }

    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    /// <summary>是否在门户公开展示。</summary>
    public bool IsPublic { get; set; }

    /// <summary>排序值。</summary>
    public int SortOrder { get; set; }

    /// <summary>是否为平台唯一「社会学校」租户。</summary>
    public bool IsPublicSchool { get; set; }

    /// <summary>学校自有域名（完整主机名，如 qidi-edu.cn）。</summary>
    public string? Domain { get; set; }

    /// <summary>学校子域名前缀（如 qidi，用于 {slug}.{基础域名}）。</summary>
    public string? Slug { get; set; }
}

/// <summary>更新本租户学校档案。</summary>
public class UpdateSchoolDto
{
    public string? Name { get; set; }
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? Introduction { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public bool? IsPublic { get; set; }
    public int? SortOrder { get; set; }

    /// <summary>学校自有域名（完整主机名）。变更后需同步清理解析缓存。</summary>
    public string? Domain { get; set; }

    /// <summary>学校子域名前缀。变更后需同步清理解析缓存。</summary>
    public string? Slug { get; set; }
}

/// <summary>学校专业（简介页专业维度展示）。</summary>
public class SchoolMajorDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public int CourseCount { get; set; }
    public int ResourceCount { get; set; }
}

/// <summary>学校资源库（简介页共建库展示）。</summary>
public class SchoolLibraryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public Guid? PrimaryMajorId { get; set; }
    public string? PrimaryMajorName { get; set; }
    public Guid OwnerTenantId { get; set; }
    public string? OwnerTenantName { get; set; }
    public int ResourceCount { get; set; }
    public int CourseCount { get; set; }
    public List<ResourceLibraryMemberDto> Members { get; set; } = new();
}

/// <summary>学校简介页聚合数据。</summary>
public class SchoolProfileDto
{
    public SchoolDto School { get; set; } = new();
    public int MajorCount { get; set; }
    public int CourseCount { get; set; }
    public int ResourceCount { get; set; }
    public int LibraryCount { get; set; }
    public List<SchoolMajorDto> Majors { get; set; } = new();
    public List<SchoolLibraryDto> Libraries { get; set; } = new();
}

/// <summary>平台唯一「社会学校」信息（注册页展示 / 平台设置页回显）。</summary>
public class PublicSchoolDto
{
    /// <summary>社会学校租户 Id；未配置时为 null。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>社会学校名称。</summary>
    public string? Name { get; set; }

    /// <summary>社会学校徽标。</summary>
    public string? LogoUrl { get; set; }

    /// <summary>是否已配置社会学校（决定开放注册入口是否展示）。</summary>
    public bool Configured { get; set; }
}

/// <summary>域名绑定平台设置（仅 host 全局管理员可见/可改）。</summary>
public class DomainSettingsDto
{
    /// <summary>
    /// 平台基础域名列表（小写、不含端口），如 <c>iketong.com</c>。
    /// 学校子域名形如 <c>{slug}.{基础域名}</c>；为空表示只支持学校配置独立域名。
    /// </summary>
    public List<string> BaseDomains { get; set; } = new();
}

/// <summary>保存平台基础域名。</summary>
public class SetBaseDomainsDto
{
    /// <summary>基础域名列表（逗号分隔或数组均可）。</summary>
    public List<string> BaseDomains { get; set; } = new();
}
