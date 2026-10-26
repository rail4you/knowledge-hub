using System;
using System.Collections.Generic;
using KnowledgeHub.Practicums.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Practicums.Dtos;

public class PracticumTaskDto : EntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Requirement { get; set; }
    public DateTime? DueTime { get; set; }
    public decimal ScoreWeight { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
}

public class PracticumMaterialDto : EntityDto<Guid>
{
    public Guid ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PracticumMaterialType MaterialType { get; set; }
    public string ResourceUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class PracticumProjectDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public Guid? CourseId { get; set; }
    public string? CourseTitle { get; set; }
    /// <summary>主资源库（兼容字段）= 关联的多个库中的第一个。</summary>
    public Guid? LibraryId { get; set; }
    /// <summary>关联的全部教学资源库（多对多；实训只能关联「已有课程」的库）。</summary>
    public List<Guid> LibraryIds { get; set; } = new();

    /// <summary>关联资源库的 Id + 名称（列表展示用）。</summary>
    public List<KnowledgeHub.ResourceLibraries.Dtos.LibraryBriefDto> Libraries { get; set; } = new();
    public string? Major { get; set; }
    public string? ClassName { get; set; }
    public PracticumProjectStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsExpired { get; set; }
    public decimal MaxScore { get; set; }
    public bool AllowResubmission { get; set; }
    public int TaskCount { get; set; }
    public int MaterialCount { get; set; }
    public int EnrollmentCount { get; set; }
    public bool IsCurrentUserEnrolled { get; set; }
    public Guid? CurrentUserEnrollmentId { get; set; }
    public decimal? CurrentUserProgress { get; set; }
    public string? AgentName { get; set; }
    public string? AgentPrompt { get; set; }
}

public class PracticumProjectDetailDto : PracticumProjectDto
{
    public List<PracticumTaskDto> Tasks { get; set; } = new();
    public List<PracticumMaterialDto> Materials { get; set; } = new();
}

public class CreateUpdatePracticumTaskDto
{
    /// <summary>已有任务的 ID；为空表示新增任务。保存项目时必须回传，否则会被当作新任务重建。</summary>
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Requirement { get; set; }
    public DateTime? DueTime { get; set; }
    public decimal ScoreWeight { get; set; }
    public int SortOrder { get; set; }
}

public class CreateUpdatePracticumMaterialDto
{
    public Guid? TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PracticumMaterialType MaterialType { get; set; }
    public string ResourceUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class CreateUpdatePracticumProjectDto
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public Guid? CourseId { get; set; }
    /// <summary>
    /// 关联的教学资源库（多选）：实训与课程相关，只能选择「已有课程」的库；
    /// 已选课程时只能选择包含该课程的库。第一个库镜像到 <c>PracticumProject.LibraryId</c>。
    /// </summary>
    public List<Guid> LibraryIds { get; set; } = new();
    public string? Major { get; set; }
    public string? ClassName { get; set; }
    public PracticumProjectStatus Status { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal MaxScore { get; set; } = 100;
    public bool AllowResubmission { get; set; } = true;
    public string? AgentName { get; set; }
    public string? AgentPrompt { get; set; }
    public List<CreateUpdatePracticumTaskDto> Tasks { get; set; } = new();
    public List<CreateUpdatePracticumMaterialDto> Materials { get; set; } = new();
}

public class PagedPracticumProjectRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? CourseId { get; set; }
    public PracticumProjectStatus? Status { get; set; }
    /// <summary>按教学资源库筛选：命中关联表或主库字段。</summary>
    public Guid? LibraryId { get; set; }
}
