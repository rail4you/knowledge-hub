using System;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.Resources.Media;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// 把 AI 生成的教学图片 / 短视频登记为「正式资源」，进入与上传资源完全一致的审核流程：
/// 1. 创建 <see cref="Resource"/>，Status = PendingReview，IsAiGenerated = true；
/// 2. 创建首个 <see cref="ResourceVersion"/>（当前版本）；
/// 3. 写入资源 ↔ 课程关联（<see cref="CourseResource"/>），课程资源页按课程即可看到；
/// 4. 入队媒体处理（缩略图 / 预览），与上传资源同一套流程。
/// 审核通过（SchoolApproved / LeagueApproved）之前，学生端不可见、课程内不可用。
/// </summary>
public class AiMediaResourceRegistrar : ITransientDependency
{
    public IAbpLazyServiceProvider LazyServiceProvider { get; set; } = default!;

    private IDataFilter DataFilter => LazyServiceProvider.LazyGetRequiredService<IDataFilter>();

    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceVersion, Guid> _versionRepository;
    private readonly IRepository<CourseResource, Guid> _courseResourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly ResourceMediaJobManager _mediaJobManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AiMediaResourceRegistrar> _logger;

    public AiMediaResourceRegistrar(
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceVersion, Guid> versionRepository,
        IRepository<CourseResource, Guid> courseResourceRepository,
        IRepository<Course, Guid> courseRepository,
        ResourceMediaJobManager mediaJobManager,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        ILogger<AiMediaResourceRegistrar> logger)
    {
        _resourceRepository = resourceRepository;
        _versionRepository = versionRepository;
        _courseResourceRepository = courseResourceRepository;
        _courseRepository = courseRepository;
        _mediaJobManager = mediaJobManager;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    /// <summary>
    /// 登记 AI 生成的媒体资源。返回新建资源 Id；课程不存在/参数缺失时返回 null（不影响生成结果本身）。
    /// </summary>
    public async Task<Guid?> RegisterAsync(
        Guid? courseId,
        ResourceType resourceType,
        string fileUrl,
        string fileExtension,
        string? title,
        string? prompt,
        string? aiTaskId = null)
    {        if (!courseId.HasValue || courseId.Value == Guid.Empty || string.IsNullOrWhiteSpace(fileUrl))
        {
            return null;
        }

        Course? course;
        using (DataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(courseId.Value);
        }
        if (course == null)
        {
            _logger.LogWarning("AI 媒体资源登记跳过：课程不存在 {CourseId}", courseId);
            return null;
        }

        var name = BuildResourceName(title, prompt, resourceType);
        var resource = new Resource
        {
            TenantId = _currentTenant.Id,
            Name = name,
            Description = Truncate(prompt, 2000),
            ResourceType = resourceType,
            FilePath = fileUrl,
            FileExtension = fileExtension,
            OriginalFileName = name,
            Status = ResourceStatus.PendingReview,
            CurrentVersion = 1,
            IsDownloadable = true,
            IsAiGenerated = true
        };

        await _resourceRepository.InsertAsync(resource);

        var version = new ResourceVersion
        {
            ResourceId = resource.Id,
            Version = 1,
            FilePath = resource.FilePath,
            FileSize = resource.FileSize,
            UpdateContent = "AI 生成（初版）",
            IsCurrentVersion = true
        };
        await _versionRepository.InsertAsync(version);

        var link = new CourseResource(_guidGenerator.Create(), courseId.Value, resource.Id)
        {
            TenantId = _currentTenant.Id
        };
        await _courseResourceRepository.InsertAsync(link);

        // 缩略图 / 预览：与上传资源同一套媒体处理流程
        try
        {
            await _mediaJobManager.EnqueueAsync(resource.Id, version.Id, resource: resource);
        }
        catch (Exception ex)
        {
            // 媒体处理失败不应回滚资源登记：资源仍可审核，预览后续可重试
            _logger.LogWarning(ex, "AI 媒体资源入队媒体处理失败 {ResourceId}", resource.Id);
        }

        return resource.Id;
    }

    private static string BuildResourceName(string? title, string? prompt, ResourceType type)
    {
        var fallback = type == ResourceType.Video ? "AI 生成教学短视频" : "AI 生成教学图片";
        var raw = !string.IsNullOrWhiteSpace(title)
            ? title!
            : !string.IsNullOrWhiteSpace(prompt)
                ? prompt!
                : fallback;

        raw = raw.Trim().Replace("\r", " ").Replace("\n", " ");
        if (raw.Length > 100)
        {
            raw = raw[..100];
        }
        // 前缀标明来源，避免与教师上传的同名资源混淆
        var prefix = type == ResourceType.Video ? "【AI短视频】" : "【AI图片】";
        var name = raw.StartsWith(prefix, StringComparison.Ordinal) ? raw : prefix + raw;
        return Truncate(name, 256) ?? fallback;
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length <= max ? v : v[..max];
    }
}
