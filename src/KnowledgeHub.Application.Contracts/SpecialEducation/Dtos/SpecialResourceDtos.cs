using System;
using System.Collections.Generic;
using KnowledgeHub.SpecialEducation;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.SpecialEducation.Dtos;

public class SpecialEduResourceDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = string.Empty;
    public SpecialEduCategory Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public string ModalityName { get; set; } = string.Empty;
    public Guid? TeachingDesignId { get; set; }
    public Guid? IepPlanId { get; set; }
    public Guid? CourseId { get; set; }
    public string ContentText { get; set; } = string.Empty;
    public List<string> Content { get; set; } = new();
    public List<ResourcePairDto> Pairs { get; set; } = new();
    public string RawJson { get; set; } = string.Empty;
    public int VersionNumber { get; set; } = 1;
    public SpecialEduPlanStatus Status { get; set; }
    public string? ReviewComment { get; set; }
    public Guid? ReviewerUserId { get; set; }
    public string? ReviewerName { get; set; }
}

public class ResourcePairDto
{
    public string Text { get; set; } = string.Empty;
    public string Pinyin { get; set; } = string.Empty;
    public string Braille { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public class GenerateSpecialResourceInputDto
{
    public SpecialEduCategory Category { get; set; }
    public string Modality { get; set; } = SpecialEduResourceModality.Text;
    public Guid? TeachingDesignId { get; set; }
    public Guid? IepPlanId { get; set; }
    public Guid? CourseId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string StudentTraits { get; set; } = string.Empty;
    public string? CustomPrompt { get; set; }
}

public class SaveSpecialResourceInputDto
{
    public Guid? Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public SpecialEduCategory Category { get; set; }
    public string Modality { get; set; } = SpecialEduResourceModality.Text;
    public Guid? TeachingDesignId { get; set; }
    public Guid? IepPlanId { get; set; }
    public Guid? CourseId { get; set; }
    public string ResultJson { get; set; } = string.Empty;
    public string SourceInputJson { get; set; } = "{}";
}

public class GetSpecialResourceListInputDto : PagedAndSortedResultRequestDto
{
    public SpecialEduCategory? Category { get; set; }
    public string? Modality { get; set; }
    /// <summary>排除指定类型（多模态列表页排除盲文对照，盲文有独立页面）。</summary>
    public string? ExcludeModality { get; set; }
    public SpecialEduPlanStatus? Status { get; set; }
    public string? Keyword { get; set; }
}

public class SubmitSpecialResourceForReviewInputDto
{
    public Guid Id { get; set; }
    /// <summary>指派的审核教师（租户内教师）。为空则仅提交待审核。</summary>
    public Guid? ReviewerUserId { get; set; }
}

public class ReviewSpecialResourceInputDto
{
    public Guid Id { get; set; }
    public bool Approved { get; set; }
    public string? Comment { get; set; }
}

public class BatchExportSpecialResourceInputDto
{
    public List<Guid> Ids { get; set; } = new();
}

public class SpecialEduTenantStateDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class SetSpecialEduTenantEnabledDto
{
    public Guid TenantId { get; set; }
    public bool Enabled { get; set; }
}
