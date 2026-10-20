using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.LearningAnalytics.Dtos;

/// <summary>向量检索引擎标识</summary>
public static class KnowledgeVectorEngines
{
    public const string Pgvector = "pgvector";
    public const string Memory = "memory";
}

// ══════════════════════════════════════════════════════════
//  知识点管理（归一化闭环）
// ══════════════════════════════════════════════════════════

public class GetKnowledgePointListInput : PagedAndSortedResultRequestDto
{
    public Guid CourseId { get; set; }
    /// <summary>按名称/描述模糊搜索</summary>
    public string? Filter { get; set; }
    /// <summary>0 已确认 / 1 待确认（灰区）；null 表示全部</summary>
    public int? Status { get; set; }
    /// <summary>Seed / Llm / Manual</summary>
    public string? Source { get; set; }
}

public class KnowledgePointDetailDto
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterTitle { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SyllabusOrder { get; set; }
    /// <summary>0 已确认 / 1 待确认</summary>
    public int Status { get; set; }
    /// <summary>Seed / Llm / Manual</summary>
    public string Source { get; set; } = "Seed";
    /// <summary>被合并后指向的标准知识点</summary>
    public Guid? CanonicalId { get; set; }
    public string? CanonicalName { get; set; }
    /// <summary>关联习题数</summary>
    public int ExerciseCount { get; set; }
    /// <summary>作为前置知识点被引用次数</summary>
    public int PrerequisiteOutCount { get; set; }
    /// <summary>作为后续知识点引用次数</summary>
    public int PrerequisiteInCount { get; set; }
    /// <summary>是否已生成语义向量</summary>
    public bool HasEmbedding { get; set; }
    /// <summary>该知识点是否有自定义判定线</summary>
    public bool HasCustomThreshold { get; set; }
    /// <summary>生效达标线（0~1）</summary>
    public double TargetLine { get; set; } = 0.75;
    /// <summary>生效薄弱线（0~1）</summary>
    public double WeakLine { get; set; } = 0.6;
    public DateTime CreationTime { get; set; }
}

public class UpdateKnowledgePointInput
{
    public string Name { get; set; } = string.Empty;
    public Guid? ChapterId { get; set; }
    public string? Description { get; set; }
    public int SyllabusOrder { get; set; }
}

/// <summary>知识点 ↔ 习题关联</summary>
public class KpExerciseLinkDto
{
    public Guid Id { get; set; }
    public Guid ExerciseId { get; set; }
    public string ExerciseTitle { get; set; } = string.Empty;
    public string? QuestionContent { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterTitle { get; set; }
    public int ExerciseScore { get; set; }
    public double Weight { get; set; }
    /// <summary>Seed / Llm / Manual</summary>
    public string Source { get; set; } = "Seed";
    /// <summary>关联到的知识点</summary>
    public Guid KnowledgePointId { get; set; }
    /// <summary>关联到的知识点名称（展示用）</summary>
    public string KnowledgePointName { get; set; } = string.Empty;
    /// <summary>知识点状态：0 已确认 / 1 待确认</summary>
    public int KnowledgePointStatus { get; set; }
}

public class AddKpExerciseLinkInput
{
    public Guid KnowledgePointId { get; set; }
    public Guid ExerciseId { get; set; }
    /// <summary>权重 0~1；同一题多个知识点时会自动归一化</summary>
    public double Weight { get; set; } = 1;
}

public class UpdateKpExerciseLinkInput
{
    public Guid LinkId { get; set; }
    public double Weight { get; set; }
}

// ══════════════════════════════════════════════════════════
//  习题 ↔ 知识点 映射（一题多知识点 + 批量权重）
// ══════════════════════════════════════════════════════════

/// <summary>一道习题及其关联的知识点（可多个）</summary>
public class KpExerciseMappingDto
{
    public Guid ExerciseId { get; set; }
    public string ExerciseTitle { get; set; } = string.Empty;
    public string? QuestionContent { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterTitle { get; set; }
    public int ExerciseScore { get; set; }
    /// <summary>该题关联的知识点（同一题可关联多个）</summary>
    public List<KpExerciseLinkDto> Links { get; set; } = new();
}

/// <summary>保存时单个知识点的权重项</summary>
public class KpExerciseLinkItemInput
{
    public Guid KnowledgePointId { get; set; }
    /// <summary>相对权重（保存后同一题内自动归一化为 1）</summary>
    public double Weight { get; set; } = 1;
}

/// <summary>批量保存某道习题的知识点关联</summary>
public class SaveKpExerciseLinksInput
{
    public Guid ExerciseId { get; set; }
    /// <summary>
    /// 期望保留的关联清单（可增可删）。
    /// 未出现在清单中的既有关联会被移除；清单内的权重会被批量写入。
    /// </summary>
    public List<KpExerciseLinkItemInput> Links { get; set; } = new();
}

/// <summary>知识点生成（全量刷新）任务提交结果</summary>
public class KpGenerationResultDto
{
    public Guid CourseId { get; set; }
    /// <summary>是否已入后台队列</summary>
    public bool Queued { get; set; }
    /// <summary>参与生成的章节数</summary>
    public int ChapterCount { get; set; }
    /// <summary>参与生成的习题数</summary>
    public int ExerciseCount { get; set; }
    /// <summary>新建知识点数</summary>
    public int CreatedKnowledgePoints { get; set; }
    /// <summary>新建习题-知识点关联数</summary>
    public int CreatedLinks { get; set; }
    /// <summary>重建语义向量的知识点数</summary>
    public int RebuiltEmbeddings { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class MergeKnowledgePointsInput
{
    /// <summary>被合并的知识点（合并后隐藏，CanonicalId 指向保留项）</summary>
    public Guid SourceId { get; set; }
    /// <summary>保留的知识点</summary>
    public Guid TargetId { get; set; }
}

public class KnowledgePointMergeResultDto
{
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }
    /// <summary>迁移的习题关联数</summary>
    public int MovedExerciseLinks { get; set; }
    /// <summary>与目标已有同题关联合并（权重相加）的数量</summary>
    public int MergedExerciseLinks { get; set; }
    /// <summary>迁移的掌握度记录数</summary>
    public int MovedMasteries { get; set; }
    /// <summary>迁移的先修关系数</summary>
    public int MovedPrerequisites { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SplitKnowledgePointInput
{
    public Guid KnowledgePointId { get; set; }
    public string NewName { get; set; } = string.Empty;
    public Guid? ChapterId { get; set; }
    /// <summary>要从原知识点拆出的习题</summary>
    public List<Guid> ExerciseIds { get; set; } = new();
}

public class KpMergeSuggestionDto
{
    public Guid SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public int SourceStatus { get; set; }
    public Guid TargetId { get; set; }
    public string TargetName { get; set; } = string.Empty;
    public int TargetStatus { get; set; }
    /// <summary>相似度 0~1</summary>
    public double Similarity { get; set; }
    /// <summary>embedding（语义）/ char（字符近似，未配置向量服务时）</summary>
    public string Method { get; set; } = "char";
}

public class RebuildEmbeddingsResultDto
{
    public int Total { get; set; }
    public int Updated { get; set; }
    /// <summary>向量来源：embedding-service / qwen / none</summary>
    public string Provider { get; set; } = "none";
    /// <summary>检索引擎：pgvector / memory</summary>
    public string Engine { get; set; } = KnowledgeVectorEngines.Memory;
    public int? Dimension { get; set; }
    /// <summary>成功写入 pgvector 的向量数</summary>
    public int VectorStored { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>向量引擎状态（前端展示「pgvector 已启用 / 进程内余弦」）</summary>
public class KnowledgeVectorStatusDto
{
    /// <summary>pgvector / memory</summary>
    public string Engine { get; set; } = KnowledgeVectorEngines.Memory;
    public int? Dimension { get; set; }
    public bool Available { get; set; }
    /// <summary>已写入向量的知识点数</summary>
    public int EmbeddedCount { get; set; }
    /// <summary>课程内知识点总数（可按需统计）</summary>
    public int KnowledgePointCount { get; set; }
    public string? Message { get; set; }
}

// ══════════════════════════════════════════════════════════
//  知识点图谱（先修关系 + 合并关系）
// ══════════════════════════════════════════════════════════

public class KnowledgePointGraphNodeDto
{
    public KnowledgePointDetailDto Kp { get; set; } = new();
    /// <summary>是否来自其他课程（跨课程先修依赖）</summary>
    public bool IsExternal { get; set; }
    public string? CourseTitle { get; set; }
    /// <summary>拓扑层级（0 为最前置），前端布局直接用；无依赖节点为 0</summary>
    public int Level { get; set; }
}

public class KnowledgePointGraphEdgeDto
{
    public Guid FromId { get; set; }
    public Guid ToId { get; set; }
    /// <summary>prerequisite 先修 / merge 已合并</summary>
    public string Type { get; set; } = "prerequisite";
    /// <summary>0 待确认 / 1 已生效（仅先修边有意义）</summary>
    public int Status { get; set; }
    public double Strength { get; set; }
    public string? Note { get; set; }
}

public class KnowledgePointGraphDto
{
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public List<KnowledgePointGraphNodeDto> Nodes { get; set; } = new();
    public List<KnowledgePointGraphEdgeDto> Edges { get; set; } = new();
    /// <summary>跨课程前置节点数</summary>
    public int ExternalNodeCount { get; set; }
    /// <summary>待确认的先修边数</summary>
    public int ProposedEdgeCount { get; set; }
    /// <summary>已归档（被合并）的节点数</summary>
    public int MergedNodeCount { get; set; }
}
