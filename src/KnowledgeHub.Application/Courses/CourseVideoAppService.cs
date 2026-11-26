using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程视频 + 时间轴知识点切片。
/// <para>
/// 存储策略：上传的文件进 FileStorage，并建一条**私有** <see cref="Resource"/>（Status=Draft、不可下载）
/// 承载文件元信息；这样文件只有一份，课程视频只负责教学语义（章节 / 知识点 / 打点）。
/// 播放由 <c>CourseVideoController</c> 按「课程可见性」鉴权，不受资源库审核状态影响
/// —— 挂进课程的视频属于课程内容，老师挂上即视为可用；要进资源库仍需走原有审核。
/// </para>
/// </summary>
[IgnoreAntiforgeryToken]
public class CourseVideoAppService : KnowledgeHubAppService, ICourseVideoAppService
{
    /// <summary>允许的视频扩展名（浏览器原生可播为主）。</summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".webm", ".mov", ".ogv"
    };

    /// <summary>单文件上限 2GB（Fernet 不参与，只防误传）。</summary>
    private const long MaxFileBytes = 2L * 1024 * 1024 * 1024;

    private readonly IRepository<CourseVideo, Guid> _videoRepository;
    private readonly IRepository<VideoKnowledgeMark, Guid> _markRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IDataFilter _dataFilter;

    public CourseVideoAppService(
        IRepository<CourseVideo, Guid> videoRepository,
        IRepository<VideoKnowledgeMark, Guid> markRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<Resource, Guid> resourceRepository,
        IFileStorageService fileStorage,
        IDataFilter dataFilter)
    {
        _videoRepository = videoRepository;
        _markRepository = markRepository;
        _courseRepository = courseRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _resourceRepository = resourceRepository;
        _fileStorage = fileStorage;
        _dataFilter = dataFilter;
    }

    public async Task<List<CourseVideoDto>> GetListAsync(Guid courseId, Guid? chapterId = null)
    {
        List<CourseVideo> videos;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _videoRepository.GetQueryableAsync();
            videos = await AsyncExecuter.ToListAsync(
                query.Where(x => x.CourseId == courseId)
                    .WhereIf(chapterId.HasValue, x => x.ChapterId == chapterId!.Value)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.CreationTime));
        }

        var marks = await LoadMarksAsync(videos.Select(x => x.Id).ToList());
        var kpNames = await LoadKnowledgeNamesAsync(
            videos.Where(x => x.KnowledgeResourceId.HasValue).Select(x => x.KnowledgeResourceId!.Value)
                .Concat(marks.Values.SelectMany(list => list.Where(m => m.KnowledgeResourceId.HasValue)
                    .Select(m => m.KnowledgeResourceId!.Value))));

        return videos.Select(v => MapToDto(v, marks.TryGetValue(v.Id, out var list) ? list : new(), kpNames)).ToList();
    }

    public async Task<CourseVideoDto> GetAsync(Guid id)
    {
        var video = await _videoRepository.GetAsync(id);
        var marks = await LoadMarksAsync(new List<Guid> { id });
        var markList = marks.TryGetValue(id, out var list) ? list : new List<VideoKnowledgeMark>();
        var kpNames = await LoadKnowledgeNamesAsync(
            new[] { video.KnowledgeResourceId }
                .Where(x => x.HasValue).Select(x => x!.Value)
                .Concat(markList.Where(m => m.KnowledgeResourceId.HasValue).Select(m => m.KnowledgeResourceId!.Value)));
        return MapToDto(video, markList, kpNames);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Video)]
    public async Task<CourseVideoDto> UploadAsync(
        Guid courseId,
        IFormFile file,
        string? title = null,
        Guid? chapterId = null,
        Guid? knowledgeResourceId = null,
        string? description = null,
        int sortOrder = 0)
    {
        if (file == null || file.Length == 0)
        {
            throw new UserFriendlyException("请选择要上传的视频文件。");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new UserFriendlyException($"不支持的视频格式：{extension}。请上传 mp4 / webm / mov。");
        }

        if (file.Length > MaxFileBytes)
        {
            throw new UserFriendlyException("视频文件超过 2GB，请压缩或先上传到外部点播平台后用外链。");
        }

        var course = await FindCourseAsync(courseId);
        var videoTitle = string.IsNullOrWhiteSpace(title)
            ? Path.GetFileNameWithoutExtension(file.FileName)
            : title.Trim();

        // 1) 落盘（目录按课程隔离，便于排查与清理）
        string relativePath;
        await using (var stream = file.OpenReadStream())
        {
            relativePath = await _fileStorage.SaveAsync(stream, file.FileName, $"course-videos/{courseId}");
        }

        // 2) 建一条私有资源承载文件（不进资源库、不参与审核、不允许下载）
        // 注意：Resource 没有 (Guid id) 构造函数，Id 由 ABP 在 Insert 时自动生成（EntityHelper.TrySetId），
        // 所以这里不要手动赋 Id，插完再读 resource.Id。
        var resource = new Resource
        {
            Name = videoTitle,
            OriginalFileName = file.FileName,
            FilePath = relativePath,
            FileSize = file.Length,
            FileExtension = extension.TrimStart('.').ToLowerInvariant(),
            ResourceType = ResourceType.Video,
            Status = ResourceStatus.Draft,
            IsDownloadable = false,
            TenantId = course.TenantId
        };
        await _resourceRepository.InsertAsync(resource);

        // 3) 课程视频
        var video = new CourseVideo(GuidGenerator.Create(), courseId, videoTitle)
        {
            ChapterId = chapterId,
            KnowledgeResourceId = knowledgeResourceId,
            Description = description,
            ResourceId = resource.Id,
            SortOrder = sortOrder,
            IsPublished = true,
            TenantId = course.TenantId
        };
        await _videoRepository.InsertAsync(video);

        // ABP 仓储默认 autoSave=false：不先落库，GetAsync 直接查库会找不到刚插入的视频
        await SaveNowAsync();
        return await GetAsync(video.Id);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Video)]
    public async Task<CourseVideoDto> UpdateAsync(Guid id, SaveCourseVideoInput input)
    {
        var video = await _videoRepository.GetAsync(id);
        if (video.CourseId != input.CourseId)
        {
            throw new UserFriendlyException("不能把视频移动到其它课程。");
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写视频标题。");
        }

        video.Title = input.Title.Trim();
        video.ChapterId = input.ChapterId;
        video.KnowledgeResourceId = input.KnowledgeResourceId;
        video.Description = input.Description;
        video.ExternalUrl = input.ExternalUrl;
        video.ThumbnailUrl = input.ThumbnailUrl;
        video.SortOrder = input.SortOrder;
        video.IsPublished = input.IsPublished;

        await _videoRepository.UpdateAsync(video);
        await SaveNowAsync();
        return await GetAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Video)]
    public async Task DeleteAsync(Guid id)
    {
        var video = await _videoRepository.GetAsync(id);

        var marks = await _markRepository.GetListAsync(x => x.CourseVideoId == id);
        foreach (var mark in marks)
        {
            await _markRepository.DeleteAsync(mark);
        }

        await _videoRepository.DeleteAsync(id);

        // 承载文件的那条私有 Resource 一起清掉，避免磁盘上留下没人引用的视频。
        // 只删「本站上传且仍是 Draft 私有」的那种；外链视频没有 ResourceId。
        if (video.ResourceId.HasValue)
        {
            var resource = await _resourceRepository.FindAsync(video.ResourceId.Value);
            if (resource != null && resource.Status == ResourceStatus.Draft)
            {
                if (!string.IsNullOrWhiteSpace(resource.FilePath))
                {
                    try
                    {
                        await _fileStorage.DeleteAsync(resource.FilePath);
                    }
                    catch
                    {
                        // 文件删失败不影响业务（磁盘清理可后续做），Resource 行照删
                    }
                }

                await _resourceRepository.DeleteAsync(resource.Id);
            }
        }
    }

    [Authorize(KnowledgeHubPermissions.Courses.Video)]
    public async Task ReorderAsync(Guid courseId, List<Guid> orderedIds)
    {
        if (orderedIds == null || orderedIds.Count == 0)
        {
            return;
        }

        List<CourseVideo> videos;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            videos = await _videoRepository.GetListAsync(x =>
                x.CourseId == courseId && orderedIds.Contains(x.Id));
        }

        var map = videos.ToDictionary(x => x.Id);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (!map.TryGetValue(orderedIds[i], out var video))
            {
                continue;
            }

            video.SortOrder = i;
            await _videoRepository.UpdateAsync(video);
        }
    }

    /// <summary>前端播放器回报真实时长（避免服务端为每个视频跑 ffmpeg 探测）。</summary>
    public async Task<CourseVideoDto> UpdateDurationAsync(Guid id, int durationSeconds)
    {
        var video = await _videoRepository.GetAsync(id);
        if (durationSeconds > 0 && video.DurationSeconds != durationSeconds)
        {
            video.DurationSeconds = durationSeconds;
            await _videoRepository.UpdateAsync(video);
            await SaveNowAsync();
        }

        return await GetAsync(id);
    }

    // ── 时间轴标记 ────────────────────────────────────────────────

    public async Task<List<VideoKnowledgeMarkDto>> GetMarksAsync(Guid courseVideoId)
    {
        var marks = await _markRepository.GetListAsync(x => x.CourseVideoId == courseVideoId);
        var kpNames = await LoadKnowledgeNamesAsync(
            marks.Where(m => m.KnowledgeResourceId.HasValue).Select(m => m.KnowledgeResourceId!.Value));

        return marks.OrderBy(x => x.PointSec).Select(m => MapMark(m, kpNames)).ToList();
    }

    [Authorize(KnowledgeHubPermissions.Courses.Video)]
    public async Task<VideoKnowledgeMarkDto> SaveMarkAsync(SaveVideoKnowledgeMarkInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Label))
        {
            throw new UserFriendlyException("请填写标记名称。");
        }

        if (input.PointSec < 0)
        {
            throw new UserFriendlyException("打点位置不能为负数。");
        }

        await _videoRepository.GetAsync(input.CourseVideoId); // 校验视频存在

        // 同一秒只保留一个标记：已存在则更新而不是插两条
        VideoKnowledgeMark? mark;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            mark = await _markRepository.FirstOrDefaultAsync(x =>
                x.CourseVideoId == input.CourseVideoId && x.PointSec == input.PointSec);
        }

        if (mark == null)
        {
            mark = new VideoKnowledgeMark(
                GuidGenerator.Create(), input.CourseVideoId, input.PointSec, input.Label.Trim());
            mark.KnowledgeResourceId = input.KnowledgeResourceId;
            mark.Note = input.Note;
            await _markRepository.InsertAsync(mark);
        }
        else
        {
            mark.Label = input.Label.Trim();
            mark.KnowledgeResourceId = input.KnowledgeResourceId;
            mark.Note = input.Note;
            await _markRepository.UpdateAsync(mark);
        }

        var kpNames = await LoadKnowledgeNamesAsync(
            mark.KnowledgeResourceId.HasValue ? new[] { mark.KnowledgeResourceId.Value } : Array.Empty<Guid>());
        return MapMark(mark, kpNames);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Video)]
    public async Task DeleteMarkAsync(Guid markId)
    {
        await _markRepository.DeleteAsync(markId);
    }

    // ── 内部 ─────────────────────────────────────────────────────

    /// <summary>同一 UoW 内「先写入再读详情」必须先落库（仓储查询不走变更追踪）。</summary>
    private async Task SaveNowAsync()
    {
        if (CurrentUnitOfWork is not null)
        {
            await CurrentUnitOfWork.SaveChangesAsync();
        }
    }

    private async Task<Course> FindCourseAsync(Guid courseId)
    {
        Course? course;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(courseId);
        }

        return course ?? throw new UserFriendlyException("课程不存在。");
    }

    private async Task<Dictionary<Guid, List<VideoKnowledgeMark>>> LoadMarksAsync(List<Guid> videoIds)
    {
        if (videoIds.Count == 0)
        {
            return new Dictionary<Guid, List<VideoKnowledgeMark>>();
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var marks = await _markRepository.GetListAsync(x => videoIds.Contains(x.CourseVideoId));
            return marks.GroupBy(x => x.CourseVideoId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.PointSec).ToList());
        }
    }

    private async Task<Dictionary<Guid, string>> LoadKnowledgeNamesAsync(IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var nodes = await _knowledgeResourceRepository.GetListAsync(x => list.Contains(x.Id));
        return nodes.ToDictionary(x => x.Id, x => x.Name ?? string.Empty);
    }

    private CourseVideoDto MapToDto(
        CourseVideo video,
        List<VideoKnowledgeMark> marks,
        Dictionary<Guid, string> kpNames)
    {
        return new CourseVideoDto
        {
            Id = video.Id,
            CourseId = video.CourseId,
            ChapterId = video.ChapterId,
            KnowledgeResourceId = video.KnowledgeResourceId,
            KnowledgeResourceName = video.KnowledgeResourceId.HasValue
                ? kpNames.GetValueOrDefault(video.KnowledgeResourceId.Value)
                : null,
            Title = video.Title,
            Description = video.Description,
            ResourceId = video.ResourceId,
            ExternalUrl = video.ExternalUrl,
            DurationSeconds = video.DurationSeconds,
            ThumbnailUrl = video.ThumbnailUrl,
            SortOrder = video.SortOrder,
            IsPublished = video.IsPublished,
            // 本站文件走流式端点（支持 Range），外链直接用
            PlayUrl = string.IsNullOrWhiteSpace(video.ExternalUrl)
                ? $"/api/course-video/{video.Id}/stream"
                : video.ExternalUrl,
            MarkCount = marks.Count,
            Marks = marks.Select(m => MapMark(m, kpNames)).ToList(),
            CreationTime = video.CreationTime,
            CreatorId = video.CreatorId,
            LastModificationTime = video.LastModificationTime,
            LastModifierId = video.LastModifierId
        };
    }

    private static VideoKnowledgeMarkDto MapMark(VideoKnowledgeMark mark, Dictionary<Guid, string> kpNames)
    {
        return new VideoKnowledgeMarkDto
        {
            Id = mark.Id,
            CourseVideoId = mark.CourseVideoId,
            PointSec = mark.PointSec,
            Label = mark.Label,
            KnowledgeResourceId = mark.KnowledgeResourceId,
            KnowledgeResourceName = mark.KnowledgeResourceId.HasValue
                ? kpNames.GetValueOrDefault(mark.KnowledgeResourceId.Value)
                : null,
            Note = mark.Note
        };
    }
}
