using System;
using System.Collections.Generic;
using KnowledgeHub.Courses.Enums;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程知识点（课程内容的主干结构）。
/// <para>
/// 层级语义对齐 kg-edu 的 subject → unit → cell：
/// <see cref="ParentId"/> 为空即顶层单元；<see cref="NodeType"/> 区分单元 / 知识项 / 知识元。
/// </para>
/// <para>
/// <see cref="SortPath"/> 是「物化路径」（形如 <c>0001.0002.0001</c>），用于：
/// ① 一次查询即按树序返回；② 同级重排只重算受影响子树；③ 并列 <see cref="SortOrder"/> 时稳定排序。
/// 它是派生数据：写路径（创建/移动/重排）自动维护，缺失时排序会退化到 SortOrder，不影响正确性。
/// </para>
/// </summary>
public class KnowledgeResource : FullAuditedEntity<Guid>
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
    public float[]? Embedding { get; set; }

    /// <summary>节点类型（单元 / 知识项 / 知识元）。默认知识项，兼容历史数据。</summary>
    public KnowledgeNodeType NodeType { get; set; } = KnowledgeNodeType.Item;

    /// <summary>物化路径（0001.0002 形式），由写路径维护，可能为空。</summary>
    public string? SortPath { get; set; }

    public KnowledgeResource? Parent { get; set; }
    public ICollection<KnowledgeResource> Children { get; set; }
    
    private KnowledgeResource() 
    { 
        Children = new List<KnowledgeResource>();
    }

    public KnowledgeResource(Guid id, Guid courseId, string name) : base(id)
    {
        CourseId = courseId;
        Name = name;
        Children = new List<KnowledgeResource>();
    }

    public void SetEmbedding(float[] embedding)
    {
        Embedding = embedding;
    }

    /// <summary>是否是叶子节点语义（知识项 / 知识元）。</summary>
    public bool IsLeafType => NodeType != KnowledgeNodeType.Unit;
}
