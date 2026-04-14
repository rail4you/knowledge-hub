using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Exams.Dtos;
using KnowledgeHub.Exams.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

    public async Task<ExerciseImportResultDto> ImportFromExcelAsync(Guid courseId, IFormFile file)
    {
        var result = new ExerciseImportResultDto();

        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            // 获取有数据的行（跳过前两行：说明和表头）
            var rows = worksheet.RowsUsed().Skip(2).ToList();
            result.TotalRows = rows.Count;

            if (rows.Count == 0)
            {
                result.Errors.Add("Excel 文件中没有数据行");
                return result;
            }

            foreach (var row in rows)
            {
                try
                {
                    // A=题目内容, B=题目类型, C=答案, D=选择题选项
                    var questionContent = row.Cell(1).GetString()?.Trim();
                    var typeValue = row.Cell(2).GetString()?.Trim();
                    var answer = row.Cell(3).GetString()?.Trim();
                    var options = row.Cell(4).GetString()?.Trim();

                    // 跳过空行
                    if (string.IsNullOrWhiteSpace(questionContent))
                    {
                        continue;
                    }

                    // 解析题目类型：1=单选题，2=多选题，3=填空题，4=问答题
                    ExerciseType exerciseType;
                    if (int.TryParse(typeValue, out var typeNum))
                    {
                        exerciseType = typeNum switch
                        {
                            1 => ExerciseType.SingleChoice,
                            2 => ExerciseType.MultiChoice,
                            3 => ExerciseType.FillBlank,
                            4 => ExerciseType.ShortAnswer,
                            _ => ExerciseType.ShortAnswer
                        };
                    }
                    else
                    {
                        exerciseType = ExerciseType.ShortAnswer;
                    }

                    // 处理选择题选项（换行符分隔）
                    string? optionsJson = null;
                    var isChoiceType = exerciseType == ExerciseType.SingleChoice || exerciseType == ExerciseType.MultiChoice;
                    if (isChoiceType && !string.IsNullOrWhiteSpace(options))
                    {
                        var optionList = options.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim())
                            .Where(o => !string.IsNullOrEmpty(o))
                            .ToList();
                        if (optionList.Count > 0)
                        {
                            optionsJson = JsonSerializer.Serialize(optionList);
                        }
                    }

                    // 处理答案：选择题将数字转换为字母（1=A, 2=B, 3=C, 4=D）
                    string processedAnswer = answer ?? string.Empty;
                    if (isChoiceType && !string.IsNullOrWhiteSpace(answer))
                    {
                        // 支持中英文逗号分隔
                        var answerParts = answer.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
                        var letterAnswers = new List<string>();
                        foreach (var part in answerParts)
                        {
                            var trimmed = part.Trim();
                            if (int.TryParse(trimmed, out var num))
                            {
                                var letter = num switch
                                {
                                    1 => "A",
                                    2 => "B",
                                    3 => "C",
                                    4 => "D",
                                    _ => trimmed
                                };
                                letterAnswers.Add(letter);
                            }
                            else
                            {
                                letterAnswers.Add(trimmed);
                            }
                        }
                        processedAnswer = string.Join(",", letterAnswers);
                    }

                    var exercise = new Exercise(
                        GuidGenerator.Create(),
                        courseId,
                        questionContent.Length > 100 ? questionContent.Substring(0, 100) : questionContent,
                        questionContent,
                        exerciseType,
                        processedAnswer
                    )
                    {
                        Options = optionsJson,
                        Difficulty = 2,
                        Score = 1
                    };

                    await _exerciseRepository.InsertAsync(exercise);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.FailCount++;
                    result.Errors.Add($"行 {row.RowNumber()}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"解析 Excel 文件失败: {ex.Message}");
        }

        return result;
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
