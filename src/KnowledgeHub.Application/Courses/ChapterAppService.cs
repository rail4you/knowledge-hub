using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using KnowledgeHub.Courses;

namespace KnowledgeHub.Courses;

public class ChapterAppService : ApplicationService, IChapterAppService
{
    private readonly IRepository<Chapter, Guid> _chapterRepository;

    public ChapterAppService(IRepository<Chapter, Guid> chapterRepository)
    {
        _chapterRepository = chapterRepository;
    }

    public async Task<ChapterDto> GetAsync(Guid id)
    {
        var chapter = await _chapterRepository.GetAsync(id);
        return ObjectMapper.Map<Chapter, ChapterDto>(chapter);
    }

    public async Task<PagedResultDto<ChapterDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var chapters = query.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        
        return new PagedResultDto<ChapterDto>(
            query.Count(),
            ObjectMapper.Map<List<Chapter>, List<ChapterDto>>(chapters)
        );
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ChapterDto> CreateAsync(CreateUpdateChapterDto input)
    {
        var chapter = new Chapter(
            GuidGenerator.Create(),
            input.CourseId,
            input.Title,
            input.SortOrder,
            input.ParentId
        );
        chapter.Description = input.Description;
        
        await _chapterRepository.InsertAsync(chapter);
        return ObjectMapper.Map<Chapter, ChapterDto>(chapter);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ChapterDto> UpdateAsync(Guid id, CreateUpdateChapterDto input)
    {
        var chapter = await _chapterRepository.GetAsync(id);
        chapter.Title = input.Title;
        chapter.Description = input.Description;
        chapter.SortOrder = input.SortOrder;
        chapter.SetParent(input.ParentId);
        
        await _chapterRepository.UpdateAsync(chapter);
        return ObjectMapper.Map<Chapter, ChapterDto>(chapter);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        await _chapterRepository.DeleteAsync(id);
    }

    public async Task<List<ChapterDto>> GetChaptersByCourseAsync(Guid courseId)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var chapters = query.Where(c => c.CourseId == courseId).OrderBy(c => c.SortOrder).ToList();
        return ObjectMapper.Map<List<Chapter>, List<ChapterDto>>(chapters);
    }

    public async Task<List<ChapterDto>> GetChapterTreeAsync(Guid courseId)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var allChapters = query.Where(c => c.CourseId == courseId).OrderBy(c => c.SortOrder).ToList();
        
        var chapterDtos = ObjectMapper.Map<List<Chapter>, List<ChapterDto>>(allChapters);
        return BuildTree(chapterDtos);
    }

    private List<ChapterDto> BuildTree(List<ChapterDto> chapters)
    {
        var lookup = chapters.ToDictionary(c => c.Id, c => c);
        var roots = new List<ChapterDto>();
        
        foreach (var chapter in chapters)
        {
            if (chapter.ParentId == null || !lookup.ContainsKey(chapter.ParentId.Value))
            {
                roots.Add(chapter);
            }
            else
            {
                var parent = lookup[chapter.ParentId.Value];
                parent.Children.Add(chapter);
            }
        }
        
        return roots;
    }

    public async Task DeleteByCourseAsync(Guid courseId)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        var chapters = query.Where(c => c.CourseId == courseId).ToList();
        
        foreach (var chapter in chapters)
        {
            await _chapterRepository.DeleteAsync(chapter);
        }
    }
}
