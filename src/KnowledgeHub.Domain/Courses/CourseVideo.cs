using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程视频（课堂/预习用的教学视频）。
/// <para>
/// 与 kg-edu 的差异：不引 Mux / 第三方点播，文件走本站 <see cref="Resources.Resource"/> + FileStorage，
/// 播放由 <c>CourseVideoController</c> 流式输出（支持 Range，可拖拽进度）。
/// </para>
/// <para>
/// <see cref="VideoKnowledgeMark"/> 是「时间轴知识点切片」：把视频按秒打点到知识点，
/// 学生看到某个标记就能跳到那一秒，或直接去练该知识点的题。
/// </para>
/// </summary>
public class CourseVideo : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    /// <summary>所属章节（可选）。</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>主知识点（可选；标记级另有更细的关联）。</summary>
    public Guid? KnowledgeResourceId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>承载文件的资源 Id（本站上传时创建）。</summary>
    public Guid? ResourceId { get; set; }

    /// <summary>外链地址（与本站上传二选一，用于已有 CDN/点播平台的视频）。</summary>
    public string? ExternalUrl { get; set; }

    /// <summary>时长（秒）。上传时不探测，由前端播放器 loadedmetadata 回报后回填。</summary>
    public int DurationSeconds { get; set; }

    /// <summary>封面图地址（可空，播放器 poster；为空时浏览器取首帧）。</summary>
    public string? ThumbnailUrl { get; set; }

    public int SortOrder { get; set; }

    /// <summary>是否对学生可见。</summary>
    public bool IsPublished { get; set; } = true;

    private CourseVideo() { }

    public CourseVideo(Guid id, Guid courseId, string title) : base(id)
    {
        CourseId = courseId;
        Title = title;
    }
}
