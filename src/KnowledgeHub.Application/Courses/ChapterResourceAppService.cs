using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Resources;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Courses;

public class ChapterResourceAppService : KnowledgeHubAppService, IChapterResourceAppService
{
    private readonly IRepository<ChapterResource, Guid> _chapterResourceRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;

    public ChapterResourceAppService(
        IRepository<ChapterResource, Guid> chapterResourceRepository,
        IRepository<Resource, Guid> resourceRepository)
    {
        _chapterResourceRepository = chapterResourceRepository;
        _resourceRepository = resourceRepository;
    }

    public async Task<List<ChapterResourceDto>> GetByChapterAsync(Guid chapterId)
    {
        var joins = await (
            from cr in await _chapterResourceRepository.GetQueryableAsync()
            join r in await _resourceRepository.GetQueryableAsync() on cr.ResourceId equals r.Id
            where cr.ChapterId == chapterId
            orderby cr.SortOrder
            select new ChapterResourceDto
            {
                Id = cr.Id,
                ChapterId = cr.ChapterId,
                ResourceId = cr.ResourceId,
                DisplayName = cr.DisplayName,
                SortOrder = cr.SortOrder,
                ResourceName = r.Name,
                OriginalFileName = r.OriginalFileName,
                FileExtension = r.FileExtension,
                FileSize = r.FileSize,
                ResourceType = r.ResourceType,
                IsDownloadable = r.IsDownloadable,
            }
        ).ToListAsync();

        return joins;
    }

    public async Task<ChapterResourceDto> CreateAsync(CreateChapterResourceDto input)
    {
        var resource = await _resourceRepository.GetAsync(input.ResourceId);
        if (resource == null)
        {
            throw new UserFriendlyException("指定的资源不存在");
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

    public async Task DeleteAsync(Guid id)
    {
        await _chapterResourceRepository.DeleteAsync(id);
    }
}
