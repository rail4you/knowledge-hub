using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Courses.Dtos;

/// <summary>
/// 课程视频 + 时间轴知识点切片。
/// <para>
/// 播放不在这里做（ABP 的 <c>IRemoteStreamContent</c> 不支持 Range，视频拖拽会失效）；
/// 播放走 <c>CourseVideoController</c> 的 <c>GET /api/course-video/{id}/stream</c>（PhysicalFile + enableRangeProcessing）。
/// </para>
/// </summary>
public interface ICourseVideoAppService : IApplicationService
{
    Task<List<CourseVideoDto>> GetListAsync(Guid courseId, Guid? chapterId = null);
    Task<CourseVideoDto> GetAsync(Guid id);

    /// <summary>
    /// 上传视频（multipart，字段名 file）。
    /// 参数扁平化而不是用带 IFormFile 的 DTO：ABP 约定控制器对「含 IFormFile 的复杂类型」默认按 JSON 绑定，
    /// 会直接把请求判成 415；扁平参数（或独立 IFormFile 参数）才会走 form 绑定（与作业上传一致）。
    /// </summary>
    Task<CourseVideoDto> UploadAsync(
        Guid courseId,
        IFormFile file,
        string? title = null,
        Guid? chapterId = null,
        Guid? knowledgeResourceId = null,
        string? description = null,
        int sortOrder = 0);

    Task<CourseVideoDto> UpdateAsync(Guid id, SaveCourseVideoInput input);
    Task DeleteAsync(Guid id);

    /// <summary>同级重排（拖拽/上移下移后提交）。</summary>
    Task ReorderAsync(Guid courseId, List<Guid> orderedIds);

    /// <summary>前端播放器拿到真实时长后回填（避免服务端跑 ffmpeg 探测）。</summary>
    Task<CourseVideoDto> UpdateDurationAsync(Guid id, int durationSeconds);

    // ── 时间轴标记 ──
    Task<List<VideoKnowledgeMarkDto>> GetMarksAsync(Guid courseVideoId);
    Task<VideoKnowledgeMarkDto> SaveMarkAsync(SaveVideoKnowledgeMarkInput input);
    Task DeleteMarkAsync(Guid markId);
}

public class CourseVideoDto : FullAuditedEntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public string? CourseTitle { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? KnowledgeResourceId { get; set; }
    public string? KnowledgeResourceName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ResourceId { get; set; }
    public string? ExternalUrl { get; set; }
    public int DurationSeconds { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }

    /// <summary>播放地址（本站文件 → /api/course-video/{id}/stream；外链 → ExternalUrl 原值）。</summary>
    public string? PlayUrl { get; set; }

    /// <summary>原始文件名（列表展示 / 排查用）。</summary>
    public string? OriginalFileName { get; set; }

    public int MarkCount { get; set; }

    /// <summary>详情接口才返回标记。</summary>
    public List<VideoKnowledgeMarkDto> Marks { get; set; } = new();
}

public class VideoKnowledgeMarkDto
{
    public Guid Id { get; set; }
    public Guid CourseVideoId { get; set; }
    public int PointSec { get; set; }
    public string Label { get; set; } = string.Empty;
    public Guid? KnowledgeResourceId { get; set; }
    public string? KnowledgeResourceName { get; set; }
    public string? Note { get; set; }

    /// <summary>时间轴展示用：mm:ss。</summary>
    public string TimeText => TimeSpan.FromSeconds(Math.Max(0, PointSec)).ToString(@"mm\:ss");
}

public class SaveCourseVideoInput
{
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? KnowledgeResourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ExternalUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}

public class SaveVideoKnowledgeMarkInput
{
    public Guid CourseVideoId { get; set; }
    public int PointSec { get; set; }
    public string Label { get; set; } = string.Empty;
    public Guid? KnowledgeResourceId { get; set; }
    public string? Note { get; set; }
}
