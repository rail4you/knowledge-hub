using System;
using System.Collections.Generic;
using KnowledgeHub.ResourceLibraries;

namespace KnowledgeHub.ResourceLibraries.Dtos;

/// <summary>资源库成员（共建学校 / 关联学校）。</summary>
public class ResourceLibraryMemberDto
{
    public Guid Id { get; set; }
    public Guid MemberTenantId { get; set; }
    public string? MemberTenantName { get; set; }
    public ResourceLibraryMemberRole Role { get; set; }
    public DateTime JoinedTime { get; set; }
}

/// <summary>被分配到教学资源库的教师。</summary>
public class ResourceLibraryTeacherDto
{
    public Guid Id { get; set; }
    public Guid LibraryId { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }

    /// <summary>教师所属学校租户 Id。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>教师所属学校名称。</summary>
    public string? TenantName { get; set; }

    /// <summary>是否已分配到目标资源库（用于「可分配教师」列表标记）。</summary>
    public bool IsAssigned { get; set; }

    public DateTime AssignedTime { get; set; }
}

/// <summary>教学资源库（管理端 / 门户视角）。</summary>
public class ResourceLibraryDto
{
    public Guid Id { get; set; }

    /// <summary>库归属租户（主学校）。</summary>
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }

    /// <summary>主建专业 Id（库的专业维度；留空表示不限专业）。</summary>
    public Guid? PrimaryMajorId { get; set; }

    /// <summary>主建专业名称。</summary>
    public string? PrimaryMajorName { get; set; }

    /// <summary>主学校租户 Id。</summary>
    public Guid OwnerTenantId { get; set; }

    /// <summary>主学校名称。</summary>
    public string? OwnerTenantName { get; set; }

    /// <summary>建设负责人（自然人姓名，文本字段）。</summary>
    public string? Principal { get; set; }

    public ResourceLibraryStatus Status { get; set; }

    /// <summary>库可见性：私有 / 公开 / 院校共建。</summary>
    public ResourceLibraryVisibility Visibility { get; set; }

    /// <summary>是否在门户公开展示（与 <see cref="Visibility"/> 正交）。</summary>
    public bool IsPublic { get; set; }

    public int SortOrder { get; set; }

    public int ResourceCount { get; set; }
    public int CourseCount { get; set; }
    public int PracticumCount { get; set; }
    public int MemberCount { get; set; }

    /// <summary>已分配教师数。</summary>
    public int TeacherCount { get; set; }

    /// <summary>当前租户在该库中的角色：0=主建，1=合作，-1=非成员。</summary>
    public int CurrentRole { get; set; } = -1;

    /// <summary>当前租户是否为主学校。</summary>
    public bool IsOwner { get; set; }

    /// <summary>当前用户是否被分配到该库（私有库的核心可见性依据）。</summary>
    public bool IsAssigned { get; set; }

    /// <summary>当前用户是否可以看到该库。</summary>
    public bool CanView { get; set; }

    /// <summary>当前用户是否可以向该库共享内容。</summary>
    public bool CanContribute { get; set; }

    public List<ResourceLibraryMemberDto> Members { get; set; } = new();
}

/// <summary>创建教学资源库。</summary>
public class CreateResourceLibraryDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }

    /// <summary>主建专业 Id（库的专业维度；留空表示不限专业）。</summary>
    public Guid? PrimaryMajorId { get; set; }

    /// <summary>建设负责人（自然人姓名）。</summary>
    public string? Principal { get; set; }

    /// <summary>库可见性，默认私有。</summary>
    public ResourceLibraryVisibility? Visibility { get; set; }

    /// <summary>
    /// 关联学校租户 Id 列表（仅院校共建库有效）。创建时会自动写入成员表。
    /// </summary>
    public List<Guid> PartnerTenantIds { get; set; } = new();

    /// <summary>是否在门户公开展示。</summary>
    public bool? IsPublic { get; set; }

    public int? SortOrder { get; set; }
}

/// <summary>更新教学资源库。</summary>
public class UpdateResourceLibraryDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public Guid? PrimaryMajorId { get; set; }

    /// <summary>建设负责人（自然人姓名）。</summary>
    public string? Principal { get; set; }

    /// <summary>库可见性：私有 / 公开 / 院校共建。</summary>
    public ResourceLibraryVisibility? Visibility { get; set; }

    /// <summary>
    /// 关联学校租户 Id 列表（仅院校共建库有效）。
    /// 传 null 表示不改动；传空列表表示清空关联学校。
    /// </summary>
    public List<Guid>? PartnerTenantIds { get; set; }

    /// <summary>是否在门户公开展示。</summary>
    public bool? IsPublic { get; set; }

    public int? SortOrder { get; set; }
    public ResourceLibraryStatus? Status { get; set; }
}

/// <summary>添加共建成员（关联学校）。</summary>
public class AddResourceLibraryMemberDto
{
    /// <summary>关联学校租户 Id。</summary>
    public Guid MemberTenantId { get; set; }
}

/// <summary>把教师分配到教学资源库。</summary>
public class AssignResourceLibraryTeacherDto
{
    /// <summary>教师用户 Id。</summary>
    public Guid UserId { get; set; }
}

/// <summary>可分配教师列表查询条件（本校教师）。</summary>
public class ResourceLibraryTeacherListRequestDto
{
    /// <summary>按姓名 / 用户名模糊过滤。</summary>
    public string? Filter { get; set; }

    /// <summary>只显示尚未分配到该库的教师。</summary>
    public Guid? LibraryId { get; set; }
}

/// <summary>教学资源库列表查询条件。</summary>
public class ResourceLibraryListRequestDto
{
    /// <summary>按主建专业筛选。</summary>
    public Guid? PrimaryMajorId { get; set; }

    /// <summary>true = 只看我参与共建的库（主建 + 合作）。</summary>
    public bool? OnlyJoined { get; set; }

    /// <summary>按可见性筛选。</summary>
    public ResourceLibraryVisibility? Visibility { get; set; }
}
