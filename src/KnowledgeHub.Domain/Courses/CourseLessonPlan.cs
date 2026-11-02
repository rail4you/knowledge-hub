using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程教案：教师上传的教案文件，或由 AI 基于课程章节生成的教案。
/// </summary>
public class CourseLessonPlan : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    /// <summary>所属章节（可空：整门课程教案）。</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>章节标题快照（章节被删除后仍可展示）。</summary>
    public string? ChapterTitle { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>教案来源：教师上传 / AI 生成。</summary>
    public CourseLessonPlanSource Source { get; set; } = CourseLessonPlanSource.Upload;

    // ── 上传文件（Source = Upload 时使用） ──
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }

    // ── AI 教案内容（Source = Ai 时使用） ──
    /// <summary>AI 生成的教案 JSON（结构与 AI 中心教案一致）。</summary>
    public string? ContentJson { get; set; }

    /// <summary>关联的 AI 生成任务 Id（来源可追溯 / 防重复落库）。</summary>
    public Guid? AiTaskId { get; set; }

    /// <summary>创建人姓名快照（列表展示用）。</summary>
    public string? CreatorName { get; set; }

    protected CourseLessonPlan()
    {
    }

    public CourseLessonPlan(Guid id, Guid courseId, string title) : base(id)
    {
        CourseId = courseId;
        Title = title;
    }
}

public enum CourseLessonPlanSource : byte
{
    /// <summary>教师上传。</summary>
    Upload = 0,

    /// <summary>AI 生成。</summary>
    Ai = 1
}
