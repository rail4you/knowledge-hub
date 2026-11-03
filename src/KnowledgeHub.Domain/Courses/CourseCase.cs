using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程案例库中的一条案例。
/// 三种进入方式：教师手动录入 / Excel 批量导入 / AI 基于课程章节生成。
/// 结构化内容统一存放在 <see cref="ContentJson"/>（与 AI 中心「案例分析」同一套 schema），
/// 因此手动填写、导入与 AI 生成的数据在详情页可以用同一套渲染。
/// </summary>
public class CourseCase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    /// <summary>所属章节（可空：整门课程的案例）。</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>章节标题快照（章节被删除后仍可展示）。</summary>
    public string? ChapterTitle { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>案例摘要（列表展示 / 检索用）。</summary>
    public string? Summary { get; set; }

    /// <summary>所属行业（案例背景字段，可为空）。</summary>
    public string? Industry { get; set; }

    /// <summary>时间范围（案例背景字段，可为空）。</summary>
    public string? Timeframe { get; set; }

    /// <summary>
    /// 结构化案例内容 JSON，schema 与 AI 中心案例分析一致：
    /// title / summary / background / keyIssues / solutions / keyInsights / recommendations。
    /// </summary>
    public string? ContentJson { get; set; }

    /// <summary>案例来源：手动录入 / 批量导入 / AI 生成。</summary>
    public CourseCaseSource Source { get; set; } = CourseCaseSource.Manual;

    /// <summary>关联的 AI 生成任务 Id（来源可追溯 / 防重复落库）。</summary>
    public Guid? AiTaskId { get; set; }

    /// <summary>创建人姓名快照（列表展示用）。</summary>
    public string? CreatorName { get; set; }

    protected CourseCase()
    {
    }

    public CourseCase(Guid id, Guid courseId, string title) : base(id)
    {
        CourseId = courseId;
        Title = title;
    }
}

public enum CourseCaseSource : byte
{
    /// <summary>教师手动录入。</summary>
    Manual = 0,

    /// <summary>Excel 批量导入。</summary>
    Import = 1,

    /// <summary>AI 生成。</summary>
    Ai = 2
}
