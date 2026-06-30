using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Courses;

public class KnowledgeResourceAppService : KnowledgeHubAppService, IKnowledgeResourceAppService
{
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;

    public KnowledgeResourceAppService(
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<Chapter, Guid> chapterRepository)
    {
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _chapterRepository = chapterRepository;
    }

    public async Task<List<KnowledgeResourceDto>> GetByCourseAsync(Guid courseId)
    {
        var query = await _knowledgeResourceRepository.GetQueryableAsync();
        var resources = query
            .Where(x => x.CourseId == courseId)
            .OrderBy(x => x.SortOrder)
            .ToList();

        return resources.Select(MapToDto).ToList();
    }

    public async Task<List<KnowledgeResourceDto>> GetByChapterAsync(Guid chapterId)
    {
        var query = await _knowledgeResourceRepository.GetQueryableAsync();
        var resources = query
            .Where(x => x.ChapterId == chapterId)
            .OrderBy(x => x.SortOrder)
            .ToList();

        return resources.Select(MapToDto).ToList();
    }

    public async Task<RelatedCoursesResultDto> GetRelatedCoursesAsync(Guid knowledgeResourceId)
    {
        var resource = await _knowledgeResourceRepository.GetAsync(knowledgeResourceId);

        var chapterQuery = await _chapterRepository.GetQueryableAsync();

        var relatedChapters = chapterQuery
            .Where(x => x.Id == resource.ChapterId)
            .Select(x => new { ChapterId = x.Id, ChapterTitle = x.Title, x.CourseId })
            .ToList();

        return new RelatedCoursesResultDto
        {
            KnowledgeResourceId = knowledgeResourceId,
            Name = resource.Name,
            Courses = relatedChapters
                .GroupBy(x => x.CourseId)
                .Select(g => new RelatedCourseInfoDto
                {
                    CourseId = g.Key,
                    Chapters = g.Select(c => new RelatedChapterInfoDto
                    {
                        ChapterId = c.ChapterId,
                        ChapterTitle = c.ChapterTitle ?? "未知章节"
                    }).ToList()
                }).ToList()
        };
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<KnowledgeResourceDto> CreateAsync(CreateUpdateKnowledgeResourceDto input)
    {
        var resource = new KnowledgeResource(
            GuidGenerator.Create(),
            input.CourseId,
            input.Name)
        {
            ChapterId = input.ChapterId,
            Description = input.Description,
            Content = input.Content,
            ImportanceLevel = input.ImportanceLevel,
            Difficulty = input.Difficulty,
            SortOrder = input.SortOrder,
            Tags = input.Tags,
            ParentId = input.ParentId,
            ResourceId = input.ResourceId
        };

        await _knowledgeResourceRepository.InsertAsync(resource);
        return MapToDto(resource);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<KnowledgeResourceDto> UpdateAsync(Guid id, CreateUpdateKnowledgeResourceDto input)
    {
        var resource = await _knowledgeResourceRepository.GetAsync(id);
        resource.Name = input.Name;
        resource.ChapterId = input.ChapterId;
        resource.Description = input.Description;
        resource.Content = input.Content;
        resource.ImportanceLevel = input.ImportanceLevel;
        resource.Difficulty = input.Difficulty;
        resource.SortOrder = input.SortOrder;
        resource.Tags = input.Tags;
        resource.ParentId = input.ParentId;
        resource.ResourceId = input.ResourceId;

        await _knowledgeResourceRepository.UpdateAsync(resource);
        return MapToDto(resource);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        await _knowledgeResourceRepository.DeleteAsync(id);
    }

    private static KnowledgeResourceDto MapToDto(KnowledgeResource resource)
    {
        return new KnowledgeResourceDto
        {
            Id = resource.Id,
            CourseId = resource.CourseId,
            ChapterId = resource.ChapterId,
            Name = resource.Name,
            Description = resource.Description,
            Content = resource.Content,
            ImportanceLevel = resource.ImportanceLevel,
            Difficulty = resource.Difficulty,
            SortOrder = resource.SortOrder,
            Tags = resource.Tags,
            ParentId = resource.ParentId,
            ResourceId = resource.ResourceId
        };
    }
}
