using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Exams.Dtos;
using KnowledgeHub.Exams.Enums;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Exams;

[AllowAnonymous]
public class ExerciseAppService : ApplicationService, IExerciseAppService
{
    private readonly IRepository<Exercise, Guid> _exerciseRepository;

    public ExerciseAppService(IRepository<Exercise, Guid> exerciseRepository)
    {
        _exerciseRepository = exerciseRepository;
    }

    public async Task<ExerciseDto> GetAsync(Guid id)
    {
        var exercise = await _exerciseRepository.GetAsync(id);
        return MapToDto(exercise);
    }

    public async Task<PagedResultDto<ExerciseDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _exerciseRepository.GetQueryableAsync();
        
        var totalCount = query.Count();
        var exercises = query
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<ExerciseDto>(
            totalCount,
            exercises.Select(MapToDto).ToList()
        );
    }

    public async Task<ExerciseDto> CreateAsync(CreateUpdateExerciseDto input)
    {
        var exercise = new Exercise(
            GuidGenerator.Create(),
            input.CourseId,
            input.Title,
            input.QuestionContent,
            input.Type,
            input.Answer
        )
        {
            ChapterId = input.ChapterId,
            KnowledgeResourceId = input.KnowledgeResourceId,
            Options = input.Options,
            AnswerExplanation = input.AnswerExplanation,
            Difficulty = input.Difficulty,
            Score = input.Score
        };

        await _exerciseRepository.InsertAsync(exercise);
        return MapToDto(exercise);
    }

    public async Task<ExerciseDto> UpdateAsync(Guid id, CreateUpdateExerciseDto input)
    {
        var exercise = await _exerciseRepository.GetAsync(id);
        
        exercise.Title = input.Title;
        exercise.QuestionContent = input.QuestionContent;
        exercise.Type = input.Type;
        exercise.Answer = input.Answer;
        exercise.ChapterId = input.ChapterId;
        exercise.KnowledgeResourceId = input.KnowledgeResourceId;
        exercise.Options = input.Options;
        exercise.AnswerExplanation = input.AnswerExplanation;
        exercise.Difficulty = input.Difficulty;
        exercise.Score = input.Score;

        await _exerciseRepository.UpdateAsync(exercise);
        return MapToDto(exercise);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _exerciseRepository.DeleteAsync(id);
    }

    public async Task<List<ExerciseDto>> GetByCourseAsync(Guid courseId)
    {
        var query = await _exerciseRepository.GetQueryableAsync();
        var exercises = query
            .Where(x => x.CourseId == courseId)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Difficulty)
            .ToList();
        
        return exercises.Select(MapToDto).ToList();
    }

    public async Task<List<ExerciseDto>> GetByChapterAsync(Guid chapterId)
    {
        var query = await _exerciseRepository.GetQueryableAsync();
        var exercises = query
            .Where(x => x.ChapterId == chapterId)
            .OrderBy(x => x.Difficulty)
            .ToList();
        
        return exercises.Select(MapToDto).ToList();
    }

    public Task<List<ExerciseDto>> GenerateByAIAsync(GenerateExerciseInput input)
    {
        throw new NotImplementedException("AI generation requires AI service integration");
    }

    public Task<GradingResultDto> GradeEssayAsync(GradeEssayInput input)
    {
        throw new NotImplementedException("AI grading requires AI service integration");
    }

    private static ExerciseDto MapToDto(Exercise exercise)
    {
        return new ExerciseDto
        {
            Id = exercise.Id,
            CourseId = exercise.CourseId,
            ChapterId = exercise.ChapterId,
            KnowledgeResourceId = exercise.KnowledgeResourceId,
            Title = exercise.Title,
            QuestionContent = exercise.QuestionContent,
            Type = exercise.Type,
            Options = exercise.Options,
            Answer = exercise.Answer,
            AnswerExplanation = exercise.AnswerExplanation,
            Difficulty = exercise.Difficulty,
            Score = exercise.Score,
            IsAiGenerated = exercise.IsAiGenerated,
            CreationTime = exercise.CreationTime,
            CreatorId = exercise.CreatorId,
            LastModificationTime = exercise.LastModificationTime,
            LastModifierId = exercise.LastModifierId
        };
    }
}
