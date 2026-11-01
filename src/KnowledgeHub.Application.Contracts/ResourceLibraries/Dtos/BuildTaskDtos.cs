using System;
using System.Collections.Generic;
using KnowledgeHub.ResourceLibraries;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.ResourceLibraries.Dtos;

/// <summary>资源库模板（建库向导用）。</summary>
public class LibraryTemplateDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string? Description { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public LibraryTemplateStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
}

public class CreateLibraryTemplateDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ConfigJson { get; set; } = "{}";
}

/// <summary>任务模板里的交付项。</summary>
public class BuildTaskTemplateItemDto
{
    public Guid Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public LibraryContentType ContentType { get; set; }
    public string ContentTypeName { get; set; } = string.Empty;
    public int RequiredCount { get; set; }
    public string? Acceptance { get; set; }
    public int SortOrder { get; set; }
}

public class BuildTaskTemplateDto
{
    public Guid Id { get; set; }
    public Guid? LibraryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? CourseId { get; set; }
    public bool IsActive { get; set; }
    public List<BuildTaskTemplateItemDto> Items { get; set; } = new();
}

public class CreateBuildTaskTemplateDto
{
    public Guid? LibraryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? CourseId { get; set; }
    public List<CreateBuildTaskTemplateItemDto> Items { get; set; } = new();
}

public class CreateBuildTaskTemplateItemDto
{
    public string ItemName { get; set; } = string.Empty;
    public LibraryContentType ContentType { get; set; } = LibraryContentType.Resource;
    public int RequiredCount { get; set; } = 1;
    public string? Acceptance { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>任务实例的交付项 + 进度。</summary>
public class BuildTaskItemDto
{
    public Guid Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public LibraryContentType ContentType { get; set; }
    public string ContentTypeName { get; set; } = string.Empty;
    public int RequiredCount { get; set; }
    public int AcceptedCount { get; set; }
    public int SubmittedCount { get; set; }
    public string? Acceptance { get; set; }
}

/// <summary>交付记录。</summary>
public class BuildTaskDeliveryDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid TaskItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public LibraryContentType ContentType { get; set; }
    public Guid ContentId { get; set; }
    public string? ContentName { get; set; }
    public Guid? PublicationId { get; set; }
    public Guid SubmittedBy { get; set; }
    public string? SubmitterName { get; set; }
    public BuildTaskDeliveryStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime CreationTime { get; set; }
}

/// <summary>共建任务（列表 / 详情）。</summary>
public class BuildTaskDto
{
    public Guid Id { get; set; }
    public Guid LibraryId { get; set; }
    public string? LibraryName { get; set; }
    public Guid? CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid OwnerSchoolId { get; set; }
    public string? OwnerSchoolName { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? OwnerUserName { get; set; }
    public Guid? ReviewerUserId { get; set; }
    public DateTime? DueAt { get; set; }
    public string? Requirements { get; set; }
    public BuildTaskStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;

    /// <summary>总要求数 / 已验收数 / 已提交待验收数。</summary>
    public int RequiredTotal { get; set; }
    public int AcceptedTotal { get; set; }
    public int SubmittedPending { get; set; }

    /// <summary>进度百分比（0-100）。</summary>
    public int ProgressPercent { get; set; }

    public bool IsOverdue { get; set; }

    public List<BuildTaskItemDto> Items { get; set; } = new();
    public List<BuildTaskDeliveryDto> Deliveries { get; set; } = new();

    /// <summary>当前用户能否提交交付物。</summary>
    public bool CanSubmit { get; set; }

    /// <summary>当前用户能否验收。</summary>
    public bool CanReview { get; set; }
}

public class BuildTaskListRequestDto : PagedAndSortedResultRequestDto
{
    public Guid? LibraryId { get; set; }
    public BuildTaskStatus? Status { get; set; }

    /// <summary>只看我参与的（主责 / 协作 / 被指派教师）。</summary>
    public bool OnlyMine { get; set; }

    public string? Filter { get; set; }
}

/// <summary>建任务入参（可从任务模板实例化）。</summary>
public class CreateBuildTaskDto
{
    public Guid LibraryId { get; set; }

    /// <summary>任务模板（可选）：给了就按模板生成交付项。</summary>
    public Guid? TemplateId { get; set; }

    public Guid? CourseId { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>主责学校（默认当前租户）。</summary>
    public Guid? OwnerSchoolId { get; set; }

    public Guid? OwnerUserId { get; set; }
    public Guid? ReviewerUserId { get; set; }
    public DateTime? DueAt { get; set; }
    public string? Requirements { get; set; }

    /// <summary>协作学校。</summary>
    public List<Guid> CollaboratorSchoolIds { get; set; } = new();

    /// <summary>显式交付项（未使用模板时必填）。</summary>
    public List<CreateBuildTaskTemplateItemDto> Items { get; set; } = new();
}

/// <summary>提交交付物。</summary>
public class SubmitBuildTaskDeliveryDto
{
    public Guid TaskId { get; set; }
    public Guid TaskItemId { get; set; }
    public LibraryContentType ContentType { get; set; } = LibraryContentType.Resource;
    public Guid ContentId { get; set; }
    public string? Comment { get; set; }
}

/// <summary>验收 / 退回交付物。</summary>
public class ReviewBuildTaskDeliveryDto
{
    public Guid DeliveryId { get; set; }
    public bool Accept { get; set; }
    public string? Comment { get; set; }
}
