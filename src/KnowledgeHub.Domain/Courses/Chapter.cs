using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace KnowledgeHub.Courses;

public class Chapter : Entity<Guid>
{
    public Guid CourseId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }

    // ── 协作创作溯源（多校共建课程时，章节可能由不同学校的老师创建） ──
    /// <summary>创建者用户 Id。</summary>
    public Guid? CreatorId { get; set; }

    /// <summary>创建者姓名快照（避免列表页 N+1 查询）。</summary>
    public string? CreatorName { get; set; }

    /// <summary>创建者所属学校租户 Id。</summary>
    public Guid? CreatorTenantId { get; set; }

    /// <summary>创建者所属学校名称快照。</summary>
    public string? CreatorTenantName { get; set; }

    /// <summary>最近一次编辑者用户 Id / 姓名 / 学校（协作场景用来展示「谁在维护」）。</summary>
    public Guid? LastEditorId { get; set; }
    public string? LastEditorName { get; set; }
    
    public Chapter? Parent { get; set; }
    public ICollection<Chapter> Children { get; set; }
    public ICollection<KnowledgeResource> KnowledgeResources { get; set; }
    
    private Chapter() { }
    
    public Chapter(Guid id, Guid courseId, string title, int sortOrder, Guid? parentId = null) : base(id)
    {
        CourseId = courseId;
        ParentId = parentId;
        Title = title;
        SortOrder = sortOrder;
        KnowledgeResources = new List<KnowledgeResource>();
        Children = new List<Chapter>();
    }
    
    public void SetParent(Guid? parentId)
    {
        ParentId = parentId;
    }
}
