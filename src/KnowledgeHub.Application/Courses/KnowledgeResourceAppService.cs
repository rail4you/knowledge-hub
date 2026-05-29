using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Dtos;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Courses;

[Microsoft.AspNetCore.Authorization.AllowAnonymous]
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
            ParentId = resource.ParentId
        };
    }
}
