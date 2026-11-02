using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

[IgnoreAntiforgeryToken]
public class ChapterResourceAppService : KnowledgeHubAppService, IChapterResourceAppService
{
    private readonly IRepository<ChapterResource, Guid> _chapterResourceRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<CourseResource, Guid> _courseResourceRepository;
    private readonly IDataFilter _dataFilter;

    public ChapterResourceAppService(
        IRepository<ChapterResource, Guid> chapterResourceRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<CourseResource, Guid> courseResourceRepository,
        IDataFilter dataFilter)
    {
        _chapterResourceRepository = chapterResourceRepository;
        _resourceRepository = resourceRepository;
        _chapterRepository = chapterRepository;
        _courseResourceRepository = courseResourceRepository;
        _dataFilter = dataFilter;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Default)]
    public async Task<List<ChapterResourceDto>> GetByChapterAsync(Guid chapterId)
    {
        var chapterResources = await _chapterResourceRepository.GetQueryableAsync();

        // 禁用多租户过滤器，确保跨租户关联的资源也能被查询到
        using (DataFilter.Disable<IMultiTenant>())
        {
            var resources = await _resourceRepository.GetQueryableAsync();

            var joins = await (
                from cr in chapterResources
                join r in resources on cr.ResourceId equals r.Id into crGroup
                from r in crGroup.DefaultIfEmpty()
                where cr.ChapterId == chapterId && r != null
                orderby cr.SortOrder
                select new ChapterResourceDto
                {
                    Id = cr.Id,
                    ChapterId = cr.ChapterId,
                    ResourceId = cr.ResourceId,
                    DisplayName = cr.DisplayName,
                    SortOrder = cr.SortOrder,
                    ResourceName = r != null ? r.Name : null,
                    OriginalFileName = r != null ? r.OriginalFileName : null,
                    FileExtension = r != null ? r.FileExtension : null,
                    FileSize = r != null ? r.FileSize : null,
                    ResourceType = r != null ? r.ResourceType : Resources.Enums.ResourceType.Document,
                    IsDownloadable = r != null && r.IsDownloadable,
                }
            ).ToListAsync();

            return joins;
        }
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ChapterResourceDto> CreateAsync(CreateChapterResourceDto input)
    {
        var resource = await _resourceRepository.GetAsync(input.ResourceId);
        if (resource == null)
        {
            throw new UserFriendlyException("指定的资源不存在");
        }

        var chapter = await _chapterRepository.FindAsync(input.ChapterId);
        if (chapter == null)
        {
            throw new UserFriendlyException("章节不存在。");
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var courseResourceQuery = await _courseResourceRepository.GetQueryableAsync();
            var linkedToCourse = await AsyncExecuter.AnyAsync(courseResourceQuery.Where(x =>
                x.CourseId == chapter.CourseId && x.ResourceId == input.ResourceId));
            if (!linkedToCourse)
            {
                throw new UserFriendlyException("该资源尚未关联到当前课程，请先在「课程资源」中关联。");
            }
        }

        var duplicateQuery = await _chapterResourceRepository.GetQueryableAsync();
        var duplicate = await AsyncExecuter.AnyAsync(duplicateQuery.Where(x =>
            x.ChapterId == input.ChapterId && x.ResourceId == input.ResourceId));
        if (duplicate)
        {
            throw new UserFriendlyException("该资源已关联到当前章节。");
        }

        var chapterResource = new ChapterResource(
            GuidGenerator.Create(),
            input.ChapterId,
            input.ResourceId,
            input.DisplayName,
            input.SortOrder
        );

        chapterResource = await _chapterResourceRepository.InsertAsync(chapterResource);

        return new ChapterResourceDto
        {
            Id = chapterResource.Id,
            ChapterId = chapterResource.ChapterId,
            ResourceId = chapterResource.ResourceId,
            DisplayName = chapterResource.DisplayName,
            SortOrder = chapterResource.SortOrder,
            ResourceName = resource.Name,
            OriginalFileName = resource.OriginalFileName,
            FileExtension = resource.FileExtension,
            FileSize = resource.FileSize,
            ResourceType = resource.ResourceType,
            IsDownloadable = resource.IsDownloadable,
        };
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        await _chapterResourceRepository.DeleteAsync(id);
    }
}
