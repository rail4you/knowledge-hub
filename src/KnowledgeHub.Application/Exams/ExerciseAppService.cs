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

            // P1-5 修复：按表头名称解析列，支持「选项在 4 个独立列」和「选项合并在一列」两种模板
            var headerMap = ResolveExerciseHeader(worksheet);

            // 获取有数据的行（跳过说明/表头）
            var dataStartRow = headerMap.DataStartRow;
            var rows = worksheet.RowsUsed().Skip(dataStartRow).ToList();
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
                    var questionContent = ReadCell(row, headerMap.Question).Trim();
                    var typeValue = ReadCell(row, headerMap.Type).Trim();
                    var answer = ReadCell(row, headerMap.Answer).Trim();

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
                        // 兼容字符串：单选/多选/填空/问答
                        exerciseType = typeValue switch
                        {
                            "单选" or "单选题" or "single" => ExerciseType.SingleChoice,
                            "多选" or "多选题" or "multi" => ExerciseType.MultiChoice,
                            "填空" or "填空题" or "fill" => ExerciseType.FillBlank,
                            "问答" or "问答题" or "short" => ExerciseType.ShortAnswer,
                            _ => ExerciseType.ShortAnswer
                        };
                    }

                    // P1-5 关键修复：选择题选项解析
                    var isChoiceType = exerciseType == ExerciseType.SingleChoice || exerciseType == ExerciseType.MultiChoice;
                    string? optionsJson = null;
                    if (isChoiceType)
                    {
                        var optionList = new List<string>();

                        // 优先：独立列（选项A / OptionA / A / 1）
                        if (headerMap.OptionColumns.Count > 0)
                        {
                            foreach (var col in headerMap.OptionColumns)
                            {
                                var v = ReadCell(row, col).Trim();
                                if (!string.IsNullOrEmpty(v)) optionList.Add(v);
                            }
                        }

                        // 回退：合并列（按换行分隔）
                        if (optionList.Count == 0 && headerMap.OptionMergedColumn.HasValue)
                        {
                            var merged = ReadCell(row, headerMap.OptionMergedColumn.Value);
                            optionList = merged.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.Trim())
                                .Where(o => !string.IsNullOrEmpty(o))
                                .ToList();
                        }

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

    private static string ReadCell(IXLRow row, int colIndex)
    {
        return colIndex <= 0 ? string.Empty : (row.Cell(colIndex).GetString() ?? string.Empty);
    }

    /// <summary>
    /// P1-5：解析表头，确定各列含义。返回 0 表示该列缺失。
    /// 支持两种模板：
    ///   1. 新模板：题干 / 题型 / 选项A / 选项B / 选项C / 选项D / 答案
    ///   2. 旧模板：题目内容 / 题目类型 / 答案 / 选择题选项（按换行分隔）
    /// </summary>
    private static ExerciseHeaderMap ResolveExerciseHeader(IXLWorksheet worksheet)
    {
        // 表头可能不在第 1 行：扫描前 3 行找首个包含「题目/题干」的
        var headerRow = 1;
        int questionCol = 0, typeCol = 0, answerCol = 0, mergedOptionsCol = 0;
        var optionCols = new List<int>();

        for (var r = 1; r <= Math.Min(3, worksheet.LastRowUsed()?.RowNumber() ?? 0); r++)
        {
            var row = worksheet.Row(r);
            for (var c = 1; c <= Math.Min(12, worksheet.LastColumnUsed()?.ColumnNumber() ?? 0); c++)
            {
                var header = (row.Cell(c).GetString() ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(header)) continue;
                if (questionCol == 0 && ContainsAny(header, "题目内容", "题干", "question"))
                {
                    questionCol = c; headerRow = r;
                }
                else if (typeCol == 0 && ContainsAny(header, "题目类型", "题型", "type"))
                {
                    typeCol = c; headerRow = r;
                }
                else if (answerCol == 0 && ContainsAny(header, "答案", "answer"))
                {
                    answerCol = c; headerRow = r;
                }
                else if (ContainsAny(header, "选项A", "OptionA", "选项1"))
                {
                    optionCols.Add(c); headerRow = r;
                }
                else if (ContainsAny(header, "选项B", "OptionB", "选项2"))
                {
                    optionCols.Add(c); headerRow = r;
                }
                else if (ContainsAny(header, "选项C", "OptionC", "选项3"))
                {
                    optionCols.Add(c); headerRow = r;
                }
                else if (ContainsAny(header, "选项D", "OptionD", "选项4"))
                {
                    optionCols.Add(c); headerRow = r;
                }
                else if (mergedOptionsCol == 0 && ContainsAny(header, "选择题选项", "选项", "options"))
                {
                    mergedOptionsCol = c; headerRow = r;
                }
            }
            if (questionCol > 0 && typeCol > 0) break;
        }

        // 按列号升序，确保选项 A→B→C→D 顺序
        optionCols.Sort();

        if (questionCol == 0 || typeCol == 0 || answerCol == 0)
        {
            throw new UserFriendlyException(
                "习题模板表头缺少「题目内容 / 题目类型 / 答案」三列。请使用最新模板。");
        }

        return new ExerciseHeaderMap
        {
            Question = questionCol,
            Type = typeCol,
            Answer = answerCol,
            OptionColumns = optionCols,
            OptionMergedColumn = mergedOptionsCol > 0 ? mergedOptionsCol : null,
            DataStartRow = headerRow + 1
        };
    }

    private static bool ContainsAny(string value, params string[] keywords)
    {
        foreach (var kw in keywords)
        {
            if (value.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private sealed class ExerciseHeaderMap
    {
        public int Question { get; set; }
        public int Type { get; set; }
        public int Answer { get; set; }
        public List<int> OptionColumns { get; set; } = new();
        public int? OptionMergedColumn { get; set; }
        public int DataStartRow { get; set; }
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
