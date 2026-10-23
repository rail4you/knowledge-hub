using System;
using System.Collections.Generic;
using KnowledgeHub.ResourceLibraries;

namespace KnowledgeHub.ResourceLibraries.Dtos;

/// <summary>资源库成员（合作学校）。</summary>
public class ResourceLibraryMemberDto
{
    public Guid Id { get; set; }
    public Guid MemberTenantId { get; set; }
    public string? MemberTenantName { get; set; }
    public ResourceLibraryMemberRole Role { get; set; }
    public DateTime JoinedTime { get; set; }
}

/// <summary>资源库（多校共建）。</summary>
public class ResourceLibraryDto
{
    public Guid Id { get; set; }

    /// <summary>库归属租户（主建学校）。</summary>
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }

    /// <summary>主建专业 Id（库的专业维度）。</summary>
    public Guid? PrimaryMajorId { get; set; }

    /// <summary>主建专业名称。</summary>
    public string? PrimaryMajorName { get; set; }

    /// <summary>主建学校租户 Id。</summary>
    public Guid OwnerTenantId { get; set; }

    /// <summary>主建学校名称。</summary>
    public string? OwnerTenantName { get; set; }

    public ResourceLibraryStatus Status { get; set; }
    public bool IsPublic { get; set; }
    public int SortOrder { get; set; }

    public int ResourceCount { get; set; }
    public int CourseCount { get; set; }
    public int MemberCount { get; set; }

    /// <summary>当前租户在该库中的角色：0=主建，1=合作，-1=非成员。</summary>
    public int CurrentRole { get; set; } = -1;

    /// <summary>当前租户是否为主建学校。</summary>
    public bool IsOwner { get; set; }

    public List<ResourceLibraryMemberDto> Members { get; set; } = new();
}

/// <summary>创建资源库。</summary>
public class CreateResourceLibraryDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }

    /// <summary>主建专业 Id（库的专业维度）。</summary>
    public Guid? PrimaryMajorId { get; set; }

    public bool? IsPublic { get; set; }
    public int? SortOrder { get; set; }
}

/// <summary>更新资源库。</summary>
public class UpdateResourceLibraryDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public Guid? PrimaryMajorId { get; set; }
    public bool? IsPublic { get; set; }
    public int? SortOrder { get; set; }
    public ResourceLibraryStatus? Status { get; set; }
}

/// <summary>添加合作学校成员。</summary>
public class AddResourceLibraryMemberDto
{
    /// <summary>合作学校租户 Id。</summary>
    public Guid MemberTenantId { get; set; }
}

/// <summary>资源库列表查询条件。</summary>
public class ResourceLibraryListRequestDto
{
    /// <summary>按主建专业筛选。</summary>
    public Guid? PrimaryMajorId { get; set; }

    /// <summary>true = 只看我参与共建的库（主建 + 合作）。</summary>
    public bool? OnlyJoined { get; set; }
}
