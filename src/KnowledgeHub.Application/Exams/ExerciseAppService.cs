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
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Exams;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class ExerciseAppService : ApplicationService, IExerciseAppService
{
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    // P2-4：题目-章节多对多关联仓库
    private readonly IRepository<ChapterExercise, Guid> _chapterExerciseRepository;

    public ExerciseAppService(
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<ChapterExercise, Guid> chapterExerciseRepository)
    {
        _exerciseRepository = exerciseRepository;
        _chapterExerciseRepository = chapterExerciseRepository;
    }

    public async Task<ExerciseDto> GetAsync(Guid id)
    {
        var exercise = await _exerciseRepository.GetAsync(id);
        // P2-4：填充该题的所有章节 ID（含主章节）
        var chapterIds = await GetChapterIdsAsync(exercise);
        return MapToDto(exercise, chapterIds);
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

        // P2-4：批量预加载所有相关章节映射（避免 N+1）
        var chapterMap = await GetChapterIdsBatchAsync(exercises.Select(e => e.Id).ToList());

        return new PagedResultDto<ExerciseDto>(
            totalCount,
            exercises.Select(e => MapToDto(e, chapterMap.GetValueOrDefault(e.Id, new List<Guid>()))).ToList()
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

        // P2-4：写入章节多对多关联。如果 ChapterId 与 ChapterIds 不一致，自动把 ChapterId 也加入。
        var allChapterIds = MergeChapterIds(input.ChapterId, input.ChapterIds);
        if (allChapterIds.Count > 0)
        {
            await SyncChapterExercisesAsync(exercise.Id, allChapterIds);
        }

        return MapToDto(exercise, allChapterIds);
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

        // P2-4：覆盖式同步章节关联
        var allChapterIds = MergeChapterIds(input.ChapterId, input.ChapterIds);
        await SyncChapterExercisesAsync(exercise.Id, allChapterIds);

        return MapToDto(exercise, allChapterIds);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _exerciseRepository.DeleteAsync(id);
        // P2-4：删除题目的同时清理章节关联（避免孤立数据）
        var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();
        var orphaned = ceQuery.Where(x => x.ExerciseId == id).ToList();
        if (orphaned.Count > 0)
        {
            foreach (var row in orphaned)
            {
                await _chapterExerciseRepository.DeleteAsync(row);
            }
        }
    }

    public async Task<List<ExerciseDto>> GetByCourseAsync(Guid courseId)
    {
        var query = await _exerciseRepository.GetQueryableAsync();
        var exercises = query
            .Where(x => x.CourseId == courseId)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Difficulty)
            .ToList();

        // P2-4：包含「主章节匹配」或「章节关联表匹配」的题目
        var chapterMap = await GetChapterIdsBatchAsync(exercises.Select(e => e.Id).ToList());
        return exercises
            .Select(e => MapToDto(e, chapterMap.GetValueOrDefault(e.Id, new List<Guid>())))
            .ToList();
    }

    public async Task<List<ExerciseDto>> GetByChapterAsync(Guid chapterId)
    {
        // P2-4：原本只查 Exercise.ChapterId == chapterId；现在扩展为：
        //   (Exercise.ChapterId == chapterId) OR (ChapterExercise.ChapterId == chapterId)
        var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
        var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();

        var matchedChapterExerciseIds = ceQuery
            .Where(x => x.ChapterId == chapterId)
            .Select(x => x.ExerciseId)
            .ToList();

        var exercises = exerciseQuery
            .Where(x => x.ChapterId == chapterId || matchedChapterExerciseIds.Contains(x.Id))
            .OrderBy(x => x.Difficulty)
            .ToList();

        var chapterMap = await GetChapterIdsBatchAsync(exercises.Select(e => e.Id).ToList());
        return exercises
            .Select(e => MapToDto(e, chapterMap.GetValueOrDefault(e.Id, new List<Guid>())))
            .ToList();
    }

    /// <summary>
    /// P2-4：合并主章节 + 章节列表（去重）。ChapterId 默认包含在 ChapterIds 中。
    /// </summary>
    private static List<Guid> MergeChapterIds(Guid? chapterId, List<Guid>? chapterIds)
    {
        var set = new HashSet<Guid>();
        if (chapterId.HasValue) set.Add(chapterId.Value);
        if (chapterIds != null)
        {
            foreach (var c in chapterIds)
            {
                if (c != Guid.Empty) set.Add(c);
            }
        }
        return set.ToList();
    }

    /// <summary>
    /// P2-4：查询某题的所有章节 ID（去重，主章节 + 关联表）。
    /// </summary>
    private async Task<List<Guid>> GetChapterIdsAsync(Exercise exercise)
    {
        var ids = new HashSet<Guid>();
        if (exercise.ChapterId.HasValue) ids.Add(exercise.ChapterId.Value);
        var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();
        var ceIds = ceQuery.Where(x => x.ExerciseId == exercise.Id).Select(x => x.ChapterId).ToList();
        foreach (var c in ceIds) ids.Add(c);
        return ids.ToList();
    }

    /// <summary>
    /// P2-4：批量查询多道题目的章节 ID 映射，避免 N+1。
    /// </summary>
    private async Task<Dictionary<Guid, List<Guid>>> GetChapterIdsBatchAsync(List<Guid> exerciseIds)
    {
        var result = new Dictionary<Guid, List<Guid>>();
        if (exerciseIds.Count == 0) return result;

        var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();
        var grouped = ceQuery
            .Where(x => exerciseIds.Contains(x.ExerciseId))
            .GroupBy(x => x.ExerciseId)
            .Select(g => new { ExerciseId = g.Key, ChapterIds = g.Select(x => x.ChapterId).ToList() })
            .ToList();

        // 同时也把 Exercise.ChapterId 加进去
        var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
        var exercises = exerciseQuery.Where(x => exerciseIds.Contains(x.Id)).ToList();
        var primaryMap = exercises.ToDictionary(x => x.Id, x => x.ChapterId);

        foreach (var exId in exerciseIds)
        {
            var set = new HashSet<Guid>();
            if (primaryMap.TryGetValue(exId, out var primary) && primary.HasValue)
            {
                set.Add(primary.Value);
            }
            var row = grouped.FirstOrDefault(x => x.ExerciseId == exId);
            if (row != null)
            {
                foreach (var c in row.ChapterIds) set.Add(c);
            }
            result[exId] = set.ToList();
        }
        return result;
    }

    /// <summary>
    /// P2-4：覆盖式同步题目-章节关联（先删后插）。
    /// 简单可靠，避免老数据残留。如果以后性能有问题可以改成 diff 增量。
    /// </summary>
    private async Task SyncChapterExercisesAsync(Guid exerciseId, List<Guid> chapterIds)
    {
        var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();
        var existing = ceQuery.Where(x => x.ExerciseId == exerciseId).ToList();

        var newIdSet = new HashSet<Guid>(chapterIds);
        var existingIdSet = new HashSet<Guid>(existing.Select(x => x.ChapterId));

        // 删除已不再关联的
        foreach (var row in existing)
        {
            if (!newIdSet.Contains(row.ChapterId))
            {
                await _chapterExerciseRepository.DeleteAsync(row);
            }
        }

        // 插入新关联
        var currentTenantId = CurrentTenant.Id;
        var sortOrder = 0;
        foreach (var chapterId in chapterIds)
        {
            if (existingIdSet.Contains(chapterId)) continue;
            var ce = new ChapterExercise(GuidGenerator.Create(), chapterId, exerciseId, sortOrder++)
            {
                TenantId = currentTenantId
            };
            await _chapterExerciseRepository.InsertAsync(ce);
        }
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

    private static ExerciseDto MapToDto(Exercise exercise, List<Guid>? chapterIds = null)
    {
        return new ExerciseDto
        {
            Id = exercise.Id,
            CourseId = exercise.CourseId,
            ChapterId = exercise.ChapterId,
            // P2-4：填充分章节 ID 列表
            ChapterIds = chapterIds ?? (exercise.ChapterId.HasValue ? new List<Guid> { exercise.ChapterId.Value } : new List<Guid>()),
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
