using System;
using System.Collections.Generic;
using KnowledgeHub.Courses.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Courses.Dtos;

public class KnowledgeResourceDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string ImportanceLevel { get; set; } = "normal";
    public int Difficulty { get; set; }
    public int SortOrder { get; set; }
    public string? Tags { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? ResourceId { get; set; }
    public string? OriginalFileName { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }

    /// <summary>节点类型（单元 / 知识项 / 知识元）。</summary>
    public KnowledgeNodeType NodeType { get; set; } = KnowledgeNodeType.Item;

    /// <summary>物化路径（0001.0002），可能为空（历史数据未重算）。</summary>
    public string? SortPath { get; set; }

    /// <summary>是否叶子语义（前端据此显示「可挂习题 / 视频」）。</summary>
    public bool IsLeafType { get; set; }

    public List<KnowledgeResourceDto> Children { get; set; } = new();
}

public class CreateUpdateKnowledgeResourceDto
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string ImportanceLevel { get; set; } = "normal";
    public int Difficulty { get; set; } = 1;
    public int SortOrder { get; set; }
    public string? Tags { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? ResourceId { get; set; }
    public KnowledgeNodeType NodeType { get; set; } = KnowledgeNodeType.Item;
}

/// <summary>移动知识点节点。</summary>
public class MoveKnowledgeResourceInput
{
    /// <summary>目标父节点；为空表示移到顶层。</summary>
    public Guid? ParentId { get; set; }

    /// <summary>目标位置（同级、0 基）。为空表示排到同级末尾。</summary>
    public int? Index { get; set; }
}

/// <summary>同级批量重排。</summary>
public class BatchReorderKnowledgeResourcesInput
{
    public Guid CourseId { get; set; }

    /// <summary>父节点；为空表示重排顶层。</summary>
    public Guid? ParentId { get; set; }

    /// <summary>按目标顺序排列的节点 Id。</summary>
    public List<Guid> OrderedIds { get; set; } = new();
}

/// <summary>知识点 Excel 导入结果。</summary>
public class KnowledgeResourceImportResultDto
{
    /// <summary>数据行数（不含前两行标题/表头）。</summary>
    public int TotalRows { get; set; }

    /// <summary>新建的单元数。</summary>
    public int CreatedUnits { get; set; }

    /// <summary>新建的知识项数（叶子）。</summary>
    public int CreatedItems { get; set; }

    /// <summary>复用的节点数（同父级同名，重复导入不会重复建）。</summary>
    public int ReusedCount { get; set; }

    /// <summary>重算物化路径时变更的节点数。</summary>
    public int SortPathsRegenerated { get; set; }

    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public int CreatedTotal => CreatedUnits + CreatedItems;
}
