using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 资源库模板（可实例化的配置，而不是每个专业重写一套页面）。
///
/// <para>
/// 模板包含：栏目及顺序、启用模块、专业目录骨架、资源元数据要求、任务交付清单、
/// 审核阶段、默认共享规则。建库时把模板 <c>ConfigJson</c> 复制进
/// <see cref="ResourceLibrary.ConfigSnapshotJson"/>，之后模板升级<b>不</b>覆盖已运行的库。
/// </para>
/// </summary>
public class ResourceLibraryTemplate : FullAuditedAggregateRoot<Guid>
{
    public const int MaxCodeLength = 64;
    public const int MaxNameLength = 256;

    /// <summary>模板编码（稳定标识，跨版本不变）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>模板名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>版本号（同一 Code 递增）。</summary>
    public int Version { get; set; } = 1;

    public string? Description { get; set; }

    /// <summary>配置快照（JSON：modules / catalogSkeleton / requiredFields / reviewStages / defaultPolicy）。</summary>
    public string ConfigJson { get; set; } = "{}";

    public LibraryTemplateStatus Status { get; set; } = LibraryTemplateStatus.Draft;

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), MaxNameLength);
    }

    public void Publish()
    {
        Status = LibraryTemplateStatus.Published;
    }

    public void Disable()
    {
        Status = LibraryTemplateStatus.Disabled;
    }
}

/// <summary>
/// 共建任务模板：以一门课程为单位，规定要交付哪些东西、各多少份。
///
/// <para>
/// 模板只是"预置值"，不作为国家或项目验收标准；实例化到具体任务后数量可改。
/// </para>
/// </summary>
public class BuildTaskTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxCodeLength = 64;
    public const int MaxNameLength = 256;

    /// <summary>所属资源库（为空表示平台级公共模板）。</summary>
    public Guid? LibraryId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>关联课程（可为空：通用模板）。</summary>
    public Guid? CourseId { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? TenantId { get; set; }

    public ICollection<BuildTaskTemplateItem> Items { get; set; } = new List<BuildTaskTemplateItem>();
}

/// <summary>任务模板里的一个交付项（课程标准 1 份 / 课件 5 个 / 微课 2 个 …）。</summary>
public class BuildTaskTemplateItem : Entity<Guid>
{
    public BuildTaskTemplateItem()
    {
    }

    public BuildTaskTemplateItem(Guid id)
        : base(id)
    {
    }

    public const int MaxItemNameLength = 200;
    public const int MaxAcceptanceLength = 1000;

    public Guid TemplateId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    /// <summary>期望的内容类型（资源 / 课程 / 实训）。</summary>
    public LibraryContentType ContentType { get; set; } = LibraryContentType.Resource;

    /// <summary>要求数量。</summary>
    public int RequiredCount { get; set; } = 1;

    /// <summary>验收标准（文字说明）。</summary>
    public string? Acceptance { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// 共建任务：某门课程的建设分工（主责校 + 协作校 + 期限 + 验收人）。
///
/// <para>
/// <b>任务验收 ≠ 资源审核</b>：验收只表示"这一项交付物按要求交了"，
/// 资源能否进入资源库/共享仍要走准入审核（<c>ResourcePublication</c>）。
/// </para>
/// </summary>
public class BuildTask : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxRequirementsLength = 4000;

    public Guid LibraryId { get; set; }

    /// <summary>目标课程（目录节点，可空）。</summary>
    public Guid? CourseId { get; set; }

    /// <summary>来源任务模板。</summary>
    public Guid? TemplateId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>主责学校（租户）。</summary>
    public Guid OwnerSchoolId { get; set; }

    /// <summary>主责负责人。</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>验收人（主责校管理员或库 Manager）。</summary>
    public Guid? ReviewerUserId { get; set; }

    public DateTime? DueAt { get; set; }

    /// <summary>交付要求（补充说明）。</summary>
    public string? Requirements { get; set; }

    public BuildTaskStatus Status { get; set; } = BuildTaskStatus.Draft;

    public Guid? TenantId { get; set; }

    public ICollection<BuildTaskItem> Items { get; set; } = new List<BuildTaskItem>();
    public ICollection<BuildTaskCollaborator> Collaborators { get; set; } = new List<BuildTaskCollaborator>();
}

/// <summary>任务实例的交付项（由模板项实例化，数量可调整）。</summary>
public class BuildTaskItem : Entity<Guid>
{
    public BuildTaskItem()
    {
    }

    public BuildTaskItem(Guid id)
        : base(id)
    {
    }

    public Guid TaskId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public LibraryContentType ContentType { get; set; } = LibraryContentType.Resource;

    public int RequiredCount { get; set; } = 1;

    /// <summary>已验收通过的数量（由验收动作维护，不能由前端直接改）。</summary>
    public int AcceptedCount { get; set; }

    public string? Acceptance { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>协作学校 / 教师（显式授权：只能看/交自己参与的任务）。</summary>
public class BuildTaskCollaborator : Entity<Guid>, IMultiTenant
{
    public BuildTaskCollaborator()
    {
    }

    public BuildTaskCollaborator(Guid id)
        : base(id)
    {
    }

    public Guid TaskId { get; set; }

    public Guid SchoolId { get; set; }

    public Guid? UserId { get; set; }

    public BuildTaskCapability Capability { get; set; } = BuildTaskCapability.Contribute;

    public Guid? TenantId { get; set; }
}

/// <summary>交付记录：把某个资源/课程/实训交给某个交付项（验收通过才计入进度）。</summary>
public class BuildTaskDelivery : CreationAuditedEntity<Guid>, IMultiTenant
{
    public BuildTaskDelivery()
    {
    }

    public BuildTaskDelivery(Guid id)
        : base(id)
    {
    }

    public const int MaxCommentLength = 1000;

    public Guid TaskItemId { get; set; }

    public Guid TaskId { get; set; }

    /// <summary>交付的内容（资源 / 课程 / 实训）。</summary>
    public LibraryContentType ContentType { get; set; }

    public Guid ContentId { get; set; }

    /// <summary>关联的库准入记录（内容进库仍需审核）。</summary>
    public Guid? PublicationId { get; set; }

    public Guid SubmittedBy { get; set; }

    /// <summary>提交人所属学校。</summary>
    public Guid? SubmitterSchoolId { get; set; }

    public BuildTaskDeliveryStatus Status { get; set; } = BuildTaskDeliveryStatus.Submitted;

    public string? Comment { get; set; }

    public Guid? ReviewerUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public Guid? TenantId { get; set; }
}

/// <summary>
/// 库级授权（替代"新增系统角色"的做法）。
///
/// <para>
/// 建库 / 改库 / 审核仍由院校管理员与联盟管理员两个角色负责；
/// 库内的"谁能建任务、谁能提交"用本表表达，不污染 ABP 角色体系。
/// </para>
/// </summary>
public class LibraryRoleAssignment : CreationAuditedEntity<Guid>, IMultiTenant
{
    public LibraryRoleAssignment()
    {
    }

    public LibraryRoleAssignment(Guid id)
        : base(id)
    {
    }

    public Guid LibraryId { get; set; }

    public Guid UserId { get; set; }

    public Guid? TenantId { get; set; }

    public LibraryRole Role { get; set; } = LibraryRole.Contributor;
}
