using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

[IgnoreAntiforgeryToken]
public class KnowledgeResourceAppService : KnowledgeHubAppService, IKnowledgeResourceAppService
{
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<CourseResource, Guid> _courseResourceRepository;
    private readonly IDataFilter _dataFilter;

    public KnowledgeResourceAppService(
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<CourseResource, Guid> courseResourceRepository,
        IDataFilter dataFilter)
    {
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _chapterRepository = chapterRepository;
        _courseResourceRepository = courseResourceRepository;
        _dataFilter = dataFilter;
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
        await ValidateResourceLinkAsync(input.CourseId, input.ChapterId, input.ResourceId);

        if (input.ResourceId.HasValue && input.ResourceId.Value != Guid.Empty)
        {
            var duplicateQuery = await _knowledgeResourceRepository.GetQueryableAsync();
            var exists = await AsyncExecuter.AnyAsync(duplicateQuery.Where(x =>
                x.ChapterId == input.ChapterId &&
                x.ResourceId == input.ResourceId.Value));
            if (exists)
            {
                throw new UserFriendlyException("该资源已关联到当前章节。");
            }
        }

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
        await ValidateResourceLinkAsync(input.CourseId, input.ChapterId, input.ResourceId);

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

    /// <summary>
    /// 章节资源必须来自课程资源池：章节属于该课程，且资源已通过 CourseResource 关联到该课程。
    /// 这样可以避免「章节里挂了资源、课程资源池里却没有」的脏数据。
    /// </summary>
    private async Task ValidateResourceLinkAsync(Guid courseId, Guid? chapterId, Guid? resourceId)
    {
        if (!resourceId.HasValue || resourceId.Value == Guid.Empty)
        {
            return;
        }

        if (chapterId.HasValue && chapterId.Value != Guid.Empty)
        {
            var chapter = await _chapterRepository.FindAsync(chapterId.Value);
            if (chapter == null)
            {
                throw new UserFriendlyException("章节不存在。");
            }
            if (chapter.CourseId != courseId)
            {
                throw new UserFriendlyException("章节不属于当前课程。");
            }
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var linkQuery = await _courseResourceRepository.GetQueryableAsync();
            var linked = await AsyncExecuter.AnyAsync(linkQuery.Where(x =>
                x.CourseId == courseId && x.ResourceId == resourceId.Value));
            if (!linked)
            {
                throw new UserFriendlyException("该资源尚未关联到当前课程，请先在「课程资源」中关联。");
            }
        }
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
