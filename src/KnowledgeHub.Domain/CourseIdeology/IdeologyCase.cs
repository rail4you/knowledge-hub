using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeHub.CourseIdeology.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.CourseIdeology;

/// <summary>「思政案例 ↔ 思政元素」多对多关联。</summary>
public class IdeologyCaseElement : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CaseId { get; set; }

    public Guid ElementId { get; set; }

    protected IdeologyCaseElement()
    {
    }

    public IdeologyCaseElement(Guid id, Guid caseId, Guid elementId, Guid? tenantId = null) : base(id)
    {
        CaseId = caseId;
        ElementId = elementId;
        TenantId = tenantId;
    }
}

/// <summary>
/// 思政案例：一段「可进课堂」的思政素材（历史事件 / 国家工程 / 法规条文 / 企业案例 …），
/// **强绑到某一门课程（可再绑到某一章节）**，并标注思政元素、来源出处与融入方式。
///
/// <para>本模块是插件式的：本实体只持有 <c>CourseId</c> / <c>ChapterId</c> 两个外键，
/// 不给课程实体增加任何字段，删掉模块的表即完全解耦。</para>
/// </summary>
public class IdeologyCase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public const int MaxTitleLength = 200;
    public const int MaxSummaryLength = 2000;
    public const int MaxSourceTextLength = 500;
    public const int MaxSourceUrlLength = 500;

    public Guid? TenantId { get; set; }

    /// <summary>所属课程（必填：思政内容一律挂在课程上）。</summary>
    public Guid CourseId { get; set; }

    /// <summary>关联章节（可空 = 课程级通用案例）。</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>章节标题快照（章节改名/删除后仍可展示）。</summary>
    public string? ChapterTitle { get; set; }

    /// <summary>案例标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>案例说明（教师视角的简述：这个案例讲什么、怎么用）。</summary>
    public string? Summary { get; set; }

    /// <summary>
    /// 结构化内容 JSON（camelCase）：
    /// <c>{ entryPoint, brief, discussionQuestions: [], integrationSuggestion, sourceText }</c>
    /// </summary>
    public string? ContentJson { get; set; }

    public IdeologySourceType SourceType { get; set; } = IdeologySourceType.Textbook;

    /// <summary>出处（**必填**：教材章节 / 报道标题+媒体 / 法规名称 / 企业名称）。</summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>出处链接（可选）。</summary>
    public string? SourceUrl { get; set; }

    /// <summary>素材发生 / 发布时间（用于「时政时效」提醒）。</summary>
    public DateTime? SourceDate { get; set; }

    public IdeologyUsageMethod UsageMethod { get; set; } = IdeologyUsageMethod.Import;

    public IdeologyCaseStatus Status { get; set; } = IdeologyCaseStatus.Draft;

    /// <summary>是否由 AI 生成（AI 产出一律先落草稿，必须人工核验后发布）。</summary>
    public bool AiGenerated { get; set; }

    /// <summary>AI 生成时教师提供的素材快照（留痕：便于回溯"当时依据什么生成的"）。</summary>
    public string? AiMaterial { get; set; }

    /// <summary>创建人姓名快照（列表展示用）。</summary>
    public string? CreatorName { get; set; }

    /// <summary>发布时间（草稿为空）。</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>关联的思政元素。</summary>
    public List<IdeologyCaseElement> Elements { get; set; } = new();

    protected IdeologyCase()
    {
    }

    public IdeologyCase(Guid id, Guid courseId, string title) : base(id)
    {
        CourseId = courseId;
        Title = title;
    }

    /// <summary>整组替换元素关联（调用方负责 SaveChanges）。</summary>
    public void SetElements(IEnumerable<Guid>? elementIds)
    {
        var next = (elementIds ?? Enumerable.Empty<Guid>()).Where(x => x != Guid.Empty).Distinct().ToList();

        Elements.RemoveAll(x => !next.Contains(x.ElementId));
        var existing = Elements.Select(x => x.ElementId).ToHashSet();
        foreach (var elementId in next.Where(x => !existing.Contains(x)))
        {
            Elements.Add(new IdeologyCaseElement(Guid.NewGuid(), Id, elementId, TenantId));
        }
    }

    public void Publish(DateTime now)
    {
        Status = IdeologyCaseStatus.Published;
        PublishedAt = now;
    }

    public void Archive()
    {
        Status = IdeologyCaseStatus.Archived;
    }

    public void ToDraft()
    {
        Status = IdeologyCaseStatus.Draft;
        PublishedAt = null;
    }
}
