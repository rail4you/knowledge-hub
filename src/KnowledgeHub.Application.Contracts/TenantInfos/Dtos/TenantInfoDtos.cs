using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KnowledgeHub.TenantInfos.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.TenantInfos.Dtos;

public class TenantInfoDto : FullAuditedEntityDto<Guid>
{
    public Guid TenantId { get; set; }
    public TenantType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> CoverImageList { get; set; } = new();
    public string? TalentTrainingPlan { get; set; }
    public string? ProfessionalTeachingStandards { get; set; }
    public List<SpecialProjectItem> SpecialProjectList { get; set; } = new();
    public int MajorCount { get; set; }
    public int CourseCount { get; set; }
}

public class SpecialProjectItem
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreateUpdateTenantInfoDto
{
    [Required]
    [StringLength(256)]
    public string Name { get; set; } = string.Empty;

    public TenantType Type { get; set; } = TenantType.Professional;

    [StringLength(4000)]
    public string? Description { get; set; }

    /// <summary>封面图片 URL 列表</summary>
    public List<string> CoverImageList { get; set; } = new();

    [StringLength(8000)]
    public string? TalentTrainingPlan { get; set; }

    [StringLength(8000)]
    public string? ProfessionalTeachingStandards { get; set; }

    /// <summary>特色项目列表</summary>
    public List<SpecialProjectItem> SpecialProjectList { get; set; } = new();
}

/// <summary>租户首页知识图谱 DTO</summary>
public class TenantKnowledgeGraphDto
{
    /// <summary>中心节点（租户/资源库）</summary>
    public TenantGraphNodeDto CenterNode { get; set; } = new();
    public List<TenantGraphNodeDto> Majors { get; set; } = new();
    /// <summary>所有节点（包含中心、专业、课程）</summary>
    public List<TenantGraphNodeDto> AllNodes { get; set; } = new();
    public List<TenantGraphRelationDto> Relations { get; set; } = new();
}

public class TenantGraphNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty; // "tenant", "major", "course"
    public string? Description { get; set; }
    public int ChildrenCount { get; set; }
}

public class TenantGraphRelationDto
{
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string RelationType { get; set; } = "contains";
    public string? Label { get; set; }
}
