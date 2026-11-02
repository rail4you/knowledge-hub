using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Courses;

[IgnoreAntiforgeryToken]
public class CourseLessonPlanAppService : KnowledgeHubAppService, ICourseLessonPlanAppService
{
    private readonly IRepository<CourseLessonPlan, Guid> _lessonPlanRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;

    public CourseLessonPlanAppService(
        IRepository<CourseLessonPlan, Guid> lessonPlanRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository)
    {
        _lessonPlanRepository = lessonPlanRepository;
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
    }

    public async Task<PagedResultDto<CourseLessonPlanDto>> GetListAsync(CourseLessonPlanQueryDto input)
    {
        var query = await _lessonPlanRepository.GetQueryableAsync();

        if (input.CourseId.HasValue)
        {
            query = query.Where(x => x.CourseId == input.CourseId.Value);
        }
        if (input.ChapterId.HasValue)
        {
            query = query.Where(x => x.ChapterId == input.ChapterId.Value);
        }
        if (input.Source.HasValue)
        {
            query = query.Where(x => x.Source == input.Source.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var kw = input.Filter.Trim();
            query = query.Where(x =>
                x.Title.Contains(kw) ||
                (x.Description != null && x.Description.Contains(kw)) ||
                (x.ChapterTitle != null && x.ChapterTitle.Contains(kw)) ||
                (x.FileName != null && x.FileName.Contains(kw)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var dtos = items.Select(MapToDto).ToList();
        await FillCourseNamesAsync(dtos);

        return new PagedResultDto<CourseLessonPlanDto>(totalCount, dtos);
    }

    public async Task<CourseLessonPlanDto> GetAsync(Guid id)
    {
        var entity = await _lessonPlanRepository.GetAsync(id);
        var dto = MapToDto(entity);
        await FillCourseNamesAsync(new List<CourseLessonPlanDto> { dto });
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseLessonPlanDto> CreateAsync(CreateCourseLessonPlanDto input)
    {
        var course = await _courseRepository.FindAsync(input.CourseId);
        if (course == null)
        {
            throw new UserFriendlyException("课程不存在");
        }

        if (string.IsNullOrWhiteSpace(input.FileUrl))
        {
            throw new UserFriendlyException("请先上传教案文件");
        }

        var chapterTitle = input.ChapterTitle;
        if (input.ChapterId.HasValue)
        {
            var chapter = await _chapterRepository.FindAsync(input.ChapterId.Value);
            if (chapter != null)
            {
                chapterTitle = chapter.Title;
            }
        }

        var entity = new CourseLessonPlan(GuidGenerator.Create(), input.CourseId, input.Title.Trim())
        {
            TenantId = CurrentTenant.Id,
            ChapterId = input.ChapterId,
            ChapterTitle = chapterTitle,
            Description = input.Description,
            Source = CourseLessonPlanSource.Upload,
            FileUrl = input.FileUrl,
            FileName = input.FileName,
            FileExtension = input.FileExtension,
            FileSize = input.FileSize,
            CreatorName = CurrentUser.Name ?? CurrentUser.UserName
        };

        await _lessonPlanRepository.InsertAsync(entity);

        var dto = MapToDto(entity);
        dto.CourseName = course.Title;
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseLessonPlanDto> UpdateAsync(Guid id, UpdateCourseLessonPlanDto input)
    {
        var entity = await _lessonPlanRepository.GetAsync(id);
        entity.Title = input.Title.Trim();
        entity.Description = input.Description;
        entity.ChapterId = input.ChapterId;

        if (input.ChapterId.HasValue)
        {
            var chapter = await _chapterRepository.FindAsync(input.ChapterId.Value);
            entity.ChapterTitle = chapter?.Title ?? input.ChapterTitle;
        }
        else
        {
            entity.ChapterTitle = null;
        }

        await _lessonPlanRepository.UpdateAsync(entity);

        var dto = MapToDto(entity);
        await FillCourseNamesAsync(new List<CourseLessonPlanDto> { dto });
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        await _lessonPlanRepository.DeleteAsync(id);
    }

    private static CourseLessonPlanDto MapToDto(CourseLessonPlan entity)
    {
        return new CourseLessonPlanDto
        {
            Id = entity.Id,
            CourseId = entity.CourseId,
            ChapterId = entity.ChapterId,
            ChapterTitle = entity.ChapterTitle,
            Title = entity.Title,
            Description = entity.Description,
            Source = entity.Source,
            FileUrl = entity.FileUrl,
            FileName = entity.FileName,
            FileExtension = entity.FileExtension,
            FileSize = entity.FileSize,
            ContentJson = entity.ContentJson,
            AiTaskId = entity.AiTaskId,
            CreatorName = entity.CreatorName,
            CreationTime = entity.CreationTime,
            CreatorId = entity.CreatorId,
            LastModificationTime = entity.LastModificationTime,
            LastModifierId = entity.LastModifierId
        };
    }

    private async Task FillCourseNamesAsync(List<CourseLessonPlanDto> dtos)
    {
        if (dtos.Count == 0) return;

        var courseIds = dtos.Select(x => x.CourseId).Distinct().ToList();
        var courseQuery = await _courseRepository.GetQueryableAsync();
        var courses = await AsyncExecuter.ToListAsync(
            courseQuery.Where(x => courseIds.Contains(x.Id)).Select(x => new { x.Id, x.Title }));

        var nameMap = courses.ToDictionary(x => x.Id, x => x.Title);
        foreach (var dto in dtos)
        {
            if (nameMap.TryGetValue(dto.CourseId, out var name))
            {
                dto.CourseName = name;
            }
        }
    }
}
