using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.Resources;
using KnowledgeHub.TeachingAgents.Dtos;
using KnowledgeHub.TeachingAgents.Enums;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace KnowledgeHub.TeachingAgents;

public class TeachingAgentContextBuilder : ITransientDependency
{
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceCategory, Guid> _resourceCategoryRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;

    public TeachingAgentContextBuilder(
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceCategory, Guid> resourceCategoryRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<IdentityUser, Guid> userRepository)
    {
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _resourceRepository = resourceRepository;
        _resourceCategoryRepository = resourceCategoryRepository;
        _exerciseRepository = exerciseRepository;
        _userRepository = userRepository;
    }

    public async Task<TaskTargetSnapshotDto> BuildAsync(ClassroomAgentTaskTargetType targetType, Guid targetId)
    {
        return targetType switch
        {
            ClassroomAgentTaskTargetType.Course => await BuildCourseSnapshotAsync(targetId),
            ClassroomAgentTaskTargetType.Resource => await BuildResourceSnapshotAsync(targetId),
            ClassroomAgentTaskTargetType.ExerciseSet => await BuildExerciseSnapshotAsync(targetId),
            _ => new TaskTargetSnapshotDto { TargetType = targetType }
        };
    }

    private async Task<TaskTargetSnapshotDto> BuildCourseSnapshotAsync(Guid courseId)
    {
        var courseContext = await BuildCourseContextAsync(courseId);
        return new TaskTargetSnapshotDto
        {
            TargetType = ClassroomAgentTaskTargetType.Course,
            Course = courseContext
        };
    }

    private async Task<TaskTargetSnapshotDto> BuildResourceSnapshotAsync(Guid resourceId)
    {
        var resource = await _resourceRepository.GetAsync(resourceId);
        ResourceCategory? category = null;
        if (resource.CategoryId.HasValue)
        {
            category = await _resourceCategoryRepository.FindAsync(resource.CategoryId.Value);
        }

        return new TaskTargetSnapshotDto
        {
            TargetType = ClassroomAgentTaskTargetType.Resource,
            Resource = new TeachingAgentResourceContextDto
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                ResourceType = resource.ResourceType,
                CategoryName = category?.Name,
                FileExtension = resource.FileExtension,
                OriginalFileName = resource.OriginalFileName
            }
        };
    }

    private async Task<TaskTargetSnapshotDto> BuildExerciseSnapshotAsync(Guid courseId)
    {
        var courseContext = await BuildCourseContextAsync(courseId);
        var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
        var exercises = await exerciseQuery
            .Where(x => x.CourseId == courseId)
            .OrderByDescending(x => x.CreationTime)
            .Take(30)
            .ToListAsync();

        return new TaskTargetSnapshotDto
        {
            TargetType = ClassroomAgentTaskTargetType.ExerciseSet,
            Course = courseContext,
            Exercises = exercises.Select(x => new TeachingAgentExerciseContextDto
            {
                Id = x.Id,
                Title = x.Title,
                QuestionContent = x.QuestionContent,
                Type = x.Type,
                Difficulty = x.Difficulty,
                Score = x.Score,
                ChapterId = x.ChapterId,
                AnswerExplanation = x.AnswerExplanation
            }).ToList()
        };
    }

    private async Task<TeachingAgentCourseContextDto> BuildCourseContextAsync(Guid courseId)
    {
        var course = await _courseRepository.GetAsync(courseId);
        var chapterQuery = await _chapterRepository.GetQueryableAsync();
        var resourceQuery = await _knowledgeResourceRepository.GetQueryableAsync();

        var chapters = await chapterQuery
            .Where(x => x.CourseId == courseId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        var knowledgeResources = await resourceQuery
            .Where(x => x.CourseId == courseId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        string? teacherName = null;
        if (course.TeacherId.HasValue)
        {
            var teacher = await _userRepository.FindAsync(course.TeacherId.Value);
            teacherName = teacher?.Name ?? teacher?.UserName;
        }

        return new TeachingAgentCourseContextDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            TeacherName = teacherName,
            Major = course.Major,
            Semester = course.Semester,
            Credits = course.Credits,
            Difficulty = course.Difficulty,
            Chapters = chapters.Select(x => new TeachingAgentCourseChapterDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                SortOrder = x.SortOrder
            }).ToList(),
            KnowledgeResources = knowledgeResources.Select(x => new TeachingAgentKnowledgeResourceDto
            {
                Id = x.Id,
                ChapterId = x.ChapterId,
                Name = x.Name,
                Description = x.Description,
                Content = x.Content,
                ImportanceLevel = x.ImportanceLevel,
                Difficulty = x.Difficulty
            }).ToList()
        };
    }
}
