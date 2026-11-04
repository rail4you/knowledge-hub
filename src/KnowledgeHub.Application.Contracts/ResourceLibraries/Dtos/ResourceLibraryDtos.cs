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

    /// <summary>
    /// 是否为「主建专业默认教师」（非显式分配）。
    /// 专业资源库默认由主建专业的全部教师负责，这类记录只读、不可取消分配。
    /// </summary>
    public bool IsImplicit { get; set; }

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

    /// <summary>是否对外开放自学（其他学员需手机号验证 + 实名认证后可学习）。</summary>
    public bool OpenToExternalLearners { get; set; }

    /// <summary>PRD 形态名（校级私有库 / 校级共享库 / 校际共建库）。</summary>
    public string FormName { get; set; } = string.Empty;

    /// <summary>跨校共享范围（仅限校内 / 仅限参建单位 / 不受限）。</summary>
    public ResourceLibrarySharingScope SharingScope { get; set; } = ResourceLibrarySharingScope.Unrestricted;

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

    /// <summary>建库所用模板 Id（为空 = 手工建的库）。</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>建库时冻结的模板版本号（展示「本库按哪个版本的模板建的」）。</summary>
    public int? TemplateVersion { get; set; }
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

    /// <summary>
    /// 是否对外开放自学（其他学员经手机号验证 + 实名认证后可学习）。
    /// 不传则按形态默认：校级私有库 false，校级共享库 / 校际共建库 true。
    /// </summary>
    public bool? OpenToExternalLearners { get; set; }

    public int? SortOrder { get; set; }

    /// <summary>
    /// 建库所用模板（V2.0）。给了就把模板配置复制进库的 ConfigSnapshotJson，
    /// 之后模板升级不影响本库。
    /// </summary>
    public Guid? TemplateId { get; set; }

    /// <summary>建库时冻结的模板版本号（用于展示「本库按哪个版本的模板建的」）。</summary>
    public int? TemplateVersion { get; set; }
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

    /// <summary>是否对外开放自学（不传表示不改动）。</summary>
    public bool? OpenToExternalLearners { get; set; }

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

/// <summary>资源库轻量引用（课程 / 实训等内容的「关联资源库」展示用）。</summary>
public class LibraryBriefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 「可关联的资源库」选项（课程 / 实训表单的多选表格数据源）。
/// </summary>
public class LibraryAttachOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>库可见性：0 私有 / 1 公开 / 2 院校共建（前端据此显示不同图标）。</summary>
    public int Visibility { get; set; }

    /// <summary>库内课程数（实训只能关联「已有课程」的库，前端可提示）。</summary>
    public int CourseCount { get; set; }
}

/// <summary>
/// 候选参建学校（建库 / 改库时选择「关联学校」的数据源）。
/// </summary>
public class ResourceLibraryPartnerTenantDto
{
    /// <summary>租户 Id。</summary>
    public Guid Id { get; set; }

    /// <summary>学校展示名（优先学校档案名，其次租户档案名，最后租户 slug）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>租户 slug（后台排查用）。</summary>
    public string? TenantName { get; set; }
}
