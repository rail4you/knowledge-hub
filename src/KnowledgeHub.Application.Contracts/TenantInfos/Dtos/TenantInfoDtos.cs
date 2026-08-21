using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KnowledgeHub.TenantInfos.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.TenantInfos.Dtos;

/// <summary>租户信息列表项 —— 强调「每个租户关联一条展示信息」，用于全局管理员管理所有租户的展示数据</summary>
public class TenantInfoListItemDto
{
    /// <summary>关联的 ABP 租户 Id</summary>
    public Guid TenantId { get; set; }

    /// <summary>租户名称（ABP Tenant.Name，登录标识）</summary>
    public string TenantName { get; set; } = string.Empty;

    /// <summary>该租户是否已配置关联信息</summary>
    public bool HasInfo { get; set; }

    /// <summary>租户类型：专业/项目</summary>
    public TenantType Type { get; set; }

    /// <summary>展示名称（即该租户资源库的显示名称）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>展示描述</summary>
    public string? Description { get; set; }

    /// <summary>封面图数量</summary>
    public int CoverImageCount { get; set; }

    /// <summary>特色项目数量</summary>
    public int SpecialProjectCount { get; set; }

    /// <summary>该租户下的专业数</summary>
    public int MajorCount { get; set; }

    /// <summary>该租户下的课程数</summary>
    public int CourseCount { get; set; }
}

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
