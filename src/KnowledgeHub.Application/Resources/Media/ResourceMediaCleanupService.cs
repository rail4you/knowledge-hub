using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources.Conversion;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Resources.Media;

/// <summary>
/// 资源媒体生成数据清理：删除生成物文件与登记行、媒体任务、解析页数据。
/// 供资源删除 / 换版本 / 回滚时统一调用，避免遗留 converted/、thumbnails/ 数据。
/// </summary>
public class ResourceMediaCleanupService : ITransientDependency
{
    private readonly IRepository<ResourceArtifact, Guid> _artifactRepository;
    private readonly IRepository<ResourceMediaJob, Guid> _mediaJobRepository;
    private readonly IRepository<PageContent, Guid> _pageContentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IOfficeConversionService _officeConversionService;
    private readonly ILogger<ResourceMediaCleanupService> _logger;

    public ResourceMediaCleanupService(
        IRepository<ResourceArtifact, Guid> artifactRepository,
        IRepository<ResourceMediaJob, Guid> mediaJobRepository,
        IRepository<PageContent, Guid> pageContentRepository,
        IFileStorageService fileStorageService,
        IOfficeConversionService officeConversionService,
        ILogger<ResourceMediaCleanupService> logger)
    {
        _artifactRepository = artifactRepository;
        _mediaJobRepository = mediaJobRepository;
        _pageContentRepository = pageContentRepository;
        _fileStorageService = fileStorageService;
        _officeConversionService = officeConversionService;
        _logger = logger;
    }

    /// <summary>清理某个资源版本的全部生成物与媒体任务。</summary>
    public async Task CleanupVersionAsync(Guid resourceVersionId)
    {
        var artifacts = await _artifactRepository.GetListAsync(x => x.ResourceVersionId == resourceVersionId);
        await DeleteArtifactFilesAsync(artifacts);
        if (artifacts.Count > 0)
        {
            await _artifactRepository.DeleteManyAsync(artifacts);
        }

        var jobs = await _mediaJobRepository.GetListAsync(x => x.ResourceVersionId == resourceVersionId);
        if (jobs.Count > 0)
        {
            await _mediaJobRepository.DeleteManyAsync(jobs);
        }
    }

    /// <summary>清理某个资源的全部生成物、媒体任务、解析页数据与转换缓存。</summary>
    public async Task CleanupResourceAsync(Guid resourceId)
    {
        var artifacts = await _artifactRepository.GetListAsync(x => x.ResourceId == resourceId);
        await DeleteArtifactFilesAsync(artifacts);
        if (artifacts.Count > 0)
        {
            await _artifactRepository.DeleteManyAsync(artifacts);
        }

        var jobs = await _mediaJobRepository.GetListAsync(x => x.ResourceId == resourceId);
        if (jobs.Count > 0)
        {
            await _mediaJobRepository.DeleteManyAsync(jobs);
        }

        var pages = await _pageContentRepository.GetListAsync(x => x.ResourceId == resourceId);
        if (pages.Count > 0)
        {
            await _pageContentRepository.DeleteManyAsync(pages);
        }

        // 转换缓存（converted/{id}.pdf/.meta/.light/.repaired 及页面目录）
        _officeConversionService.InvalidateCache(resourceId.ToString());
    }

    private async Task DeleteArtifactFilesAsync(List<ResourceArtifact> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            if (string.IsNullOrWhiteSpace(artifact.FilePath))
            {
                continue;
            }
            try
            {
                var full = Path.IsPathRooted(artifact.FilePath)
                    ? artifact.FilePath
                    : Path.Combine(_fileStorageService.RootPath, artifact.FilePath);
                if (File.Exists(full))
                {
                    File.Delete(full);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MediaCleanup] 删除生成物失败: {Path}", artifact.FilePath);
            }
        }
    }
}
