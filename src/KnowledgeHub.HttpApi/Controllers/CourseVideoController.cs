using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Controllers;

/// <summary>
/// 课程视频流式播放端点。
/// <para>
/// 为什么不用 <c>IPathToPdfService</c>/<c>IRemoteStreamContent</c>：
/// ① ABP 的 RemoteStreamContent 不支持 Range，浏览器无法拖拽进度；
/// ② 也不能复用 <c>/api/resource-file/{id}/preview</c> —— 那个端点按「资源库审核状态」鉴权，
///    挂进课程但还没进资源库审核的视频会被它挡成 403。
/// </para>
/// <para>
/// 鉴权口径：**课程可见性**（本课教师 / 管理员 / 已选课学生），与「资源库审核」解耦。
/// 挂在课程里的视频属于课程教学内容，老师挂上即视为可用；要进资源库共享仍需走原有审核。
/// </para>
/// </summary>
[Route("api/course-video")]
[Authorize]
public class CourseVideoController : AbpControllerBase
{
    private readonly IRepository<CourseVideo, Guid> _videoRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IDataFilter _dataFilter;
    private readonly IPermissionChecker _permissionChecker;

    public CourseVideoController(
        IRepository<CourseVideo, Guid> videoRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IFileStorageService fileStorage,
        IDataFilter dataFilter,
        IPermissionChecker permissionChecker)
    {
        _videoRepository = videoRepository;
        _resourceRepository = resourceRepository;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _fileStorage = fileStorage;
        _dataFilter = dataFilter;
        _permissionChecker = permissionChecker;
    }

    /// <summary>流式播放（支持 Range：可拖拽、可断点续传）。</summary>
    [HttpGet("{id}/stream")]
    [AllowAnonymous] // 匿名请求一律 401；真正的鉴权在下方按课程可见性判定
    public async Task<IActionResult> Stream(Guid id)
    {
        if (!CurrentUser.IsAuthenticated)
        {
            return Unauthorized();
        }

        CourseVideo? video;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            video = await _videoRepository.FindAsync(id);
        }

        if (video == null)
        {
            return NotFound(new { message = "视频不存在" });
        }

        var allowed = await CanAccessAsync(video);
        if (!allowed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "无权观看该视频" });
        }

        // 外链视频：直接用 302 交给浏览器（也便于前端 <video src> 统一处理）
        if (string.IsNullOrWhiteSpace(video.ResourceId?.ToString()) && !string.IsNullOrWhiteSpace(video.ExternalUrl))
        {
            return Redirect(video.ExternalUrl!);
        }

        if (!video.ResourceId.HasValue)
        {
            return NotFound(new { message = "视频文件不存在" });
        }

        Resource? resource;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            resource = await _resourceRepository.FindAsync(video.ResourceId.Value);
        }

        if (resource == null || string.IsNullOrWhiteSpace(resource.FilePath))
        {
            return NotFound(new { message = "视频文件不存在" });
        }

        var fullPath = Path.Combine(_fileStorage.RootPath, resource.FilePath);
        if (!System.IO.File.Exists(fullPath))
        {
            Logger.LogWarning("[CourseVideo] 文件缺失 {Path} (video {VideoId})", fullPath, id);
            return NotFound(new { message = "视频文件不存在" });
        }

        var fileName = resource.OriginalFileName ?? Path.GetFileName(fullPath);
        var contentType = GetVideoContentType(fileName);

        // enableRangeProcessing：Range 请求返回 206，浏览器可拖拽进度
        return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// 课程可见性判定：教师 / 管理员 / 已选课学生。
    /// 不依赖资源审核状态（见类注释）。
    /// </summary>
    private async Task<bool> CanAccessAsync(CourseVideo video)
    {
        var userId = CurrentUser.Id;
        if (!userId.HasValue)
        {
            return false;
        }

        // 管理员 / 有视频管理权限的老师
        if (await _permissionChecker.IsGrantedAsync(KnowledgeHubPermissions.Courses.Video) ||
            await _permissionChecker.IsGrantedAsync(KnowledgeHubPermissions.Courses.Edit))
        {
            return true;
        }

        Course? course;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(video.CourseId);
        }

        if (course == null)
        {
            return false;
        }

        // 课程负责人
        if (course.TeacherId.HasValue && course.TeacherId.Value == userId.Value)
        {
            return true;
        }

        // 已选课学生（排除退课）
        bool enrolled;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _studentCourseRepository.GetQueryableAsync();
            enrolled = await query.AnyAsync(x =>
                x.CourseId == video.CourseId &&
                x.StudentId == userId.Value &&
                x.Status != StudentCourseStatus.Dropped);
        }

        return enrolled && video.IsPublished;
    }

    private static string GetVideoContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".ogv" => "video/ogg",
            _ => "application/octet-stream"
        };
    }
}
