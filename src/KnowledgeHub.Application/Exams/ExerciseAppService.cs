using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams.Dtos;
using KnowledgeHub.Exams.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using OpenAI;

namespace KnowledgeHub.Exams;

[Authorize]
[IgnoreAntiforgeryToken]
public class ExerciseAppService : ApplicationService, IExerciseAppService
{
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    // P2-4：题目-章节多对多关联仓库
    private readonly IRepository<ChapterExercise, Guid> _chapterExerciseRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExerciseAppService> _logger;
    private readonly ExerciseAiGenerator _exerciseAiGenerator;

    public ExerciseAppService(
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<ChapterExercise, Guid> chapterExerciseRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IConfiguration configuration,
        ILogger<ExerciseAppService> logger,
        ExerciseAiGenerator exerciseAiGenerator)
    {
        _exerciseRepository = exerciseRepository;
        _chapterExerciseRepository = chapterExerciseRepository;
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _configuration = configuration;
        _logger = logger;
        _exerciseAiGenerator = exerciseAiGenerator;
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

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
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
            QuestionAnalysis = input.QuestionAnalysis,
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

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
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
        exercise.QuestionAnalysis = input.QuestionAnalysis;
        exercise.Difficulty = input.Difficulty;
        exercise.Score = input.Score;

        await _exerciseRepository.UpdateAsync(exercise);

        // P2-4：覆盖式同步章节关联
        var allChapterIds = MergeChapterIds(input.ChapterId, input.ChapterIds);
        await SyncChapterExercisesAsync(exercise.Id, allChapterIds);

        return MapToDto(exercise, allChapterIds);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        // P3-8：禁用多租户过滤器以匹配 GetByCourseAsync 的查询范围
        using (DataFilter.Disable<IMultiTenant>())
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
    }

    public async Task<List<ExerciseDto>> GetByCourseAsync(Guid courseId)
    {
        List<Exercise> exercises;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _exerciseRepository.GetQueryableAsync();
            exercises = await query
                .Where(x => x.CourseId == courseId)
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Difficulty)
                .ToListAsync();
        }

        // P2-4：包含「主章节匹配」或「章节关联表匹配」的题目
        var chapterMap = await GetChapterIdsBatchAsync(exercises.Select(e => e.Id).ToList());
        return exercises
            .Select(e => MapToDto(e, chapterMap.GetValueOrDefault(e.Id, new List<Guid>())))
            .ToList();
    }

    public async Task<List<ExerciseDto>> GetByChapterAsync(Guid chapterId)
    {
        // 与 GetByCourseAsync 对齐：禁用多租户过滤器后按 ChapterId 精确匹配，
        // 避免课程与其习题分属不同租户（历史跨租户选课）时章节习题查不到。
        // 未选课的访问控制由前端学习页选课校验承担（提示后退回详情），此处只保证已选课的数据一致性。
        List<Exercise> exercises;
        using (DataFilter.Disable<IMultiTenant>())
        {
        // P2-4：原本只查 Exercise.ChapterId == chapterId；现在扩展为：
        //   (Exercise.ChapterId == chapterId) OR (ChapterExercise.ChapterId == chapterId)
        var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
        var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();

        var matchedChapterExerciseIds = ceQuery
            .Where(x => x.ChapterId == chapterId)
            .Select(x => x.ExerciseId)
            .ToList();

        exercises = exerciseQuery
            .Where(x => x.ChapterId == chapterId || matchedChapterExerciseIds.Contains(x.Id))
            .OrderBy(x => x.Difficulty)
            .ToList();
        }

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

    /// <summary>
    /// AI 生成习题：按课程 + 章节 + 题型 + 难度 + 教师自定义提示词生成，
    /// 直接保存入库（IsAiGenerated = true），归属到指定课程章节。
    /// 生成后可在习题管理中编辑 / 删除，在章节习题中按需关联。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.AI.ExerciseGenerate)]
    public async Task<List<ExerciseDto>> GenerateByAIAsync(GenerateExerciseInput input)
    {
        return await _exerciseAiGenerator.GenerateAsync(input);
    }

    public Task<GradingResultDto> GradeEssayAsync(GradeEssayInput input)
    {
        throw new NotImplementedException("AI grading requires AI service integration");
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<AiAnalyzeExerciseResultDto> AiAnalyzeAsync(AiAnalyzeExerciseInput input)
    {
        var result = new AiAnalyzeExerciseResultDto();
        if (input.ExerciseIds == null || input.ExerciseIds.Count == 0)
        {
            result.Errors.Add("请选择要分析的习题");
            return result;
        }

        List<Exercise> exercises;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _exerciseRepository.GetQueryableAsync();
            exercises = await query.Where(x => input.ExerciseIds.Contains(x.Id)).ToListAsync();
        }

        result.TotalCount = exercises.Count;

        var apiKey = _configuration["Qwen:ApiKey"]
            ?? throw new AbpException("Qwen:ApiKey is not configured");
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-plus";

        IChatClient chatClient = QwenClient.CreateChatClient(_configuration, model);

        foreach (var exercise in exercises)
        {
            try
            {
                var systemPrompt = @"你是一个专业的学科教师助手，负责分析习题并生成高质量的题目解析（包含答案解释）。

工作要求：
1. 如果题目没有答案（或答案不完整），你需要根据题目内容和选项生成准确的参考答案
2. 如果题目已有答案，你需要在题目解析中结合答案给出答案解析
3. 题目解析应包含：本题考查的知识点、解题思路和方法、易错点提示、答案解释
4. 答案和解析必须专业、准确、简洁
5. 对于选择题，答案格式为单个字母（如'A'）或多个字母（如'A,B,C'）
6. 对于判断题，答案为'对'或'错'
7. 对于问答题/填空题，答案直接给出文本

请严格按照以下 JSON 格式输出，不要输出任何其他内容：
{
  ""answer"": ""参考答案（如果原题没有答案则填充，否则保持空字符串）"",
  ""questionAnalysis"": ""题目解析（包含知识点分析、解题思路、易错点提示、答案解释等内容）""
}";

                var exerciseTypeName = exercise.Type switch
                {
                    ExerciseType.SingleChoice => "单选题",
                    ExerciseType.MultiChoice => "多选题",
                    ExerciseType.TrueFalse => "判断题",
                    ExerciseType.FillBlank => "填空题",
                    ExerciseType.ShortAnswer => "问答题",
                    ExerciseType.Essay => "论述题",
                    ExerciseType.CaseAnalysis => "案例分析",
                    _ => "未知题型"
                };

                var optionsText = string.Empty;
                if (!string.IsNullOrWhiteSpace(exercise.Options))
                {
                    try
                    {
                        var opts = JsonSerializer.Deserialize<List<string>>(exercise.Options);
                        if (opts != null && opts.Count > 0)
                        {
                            var letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                            optionsText = "\n选项：\n" + string.Join("\n", opts.Select((o, i) => $"{letters[i]}. {o}"));
                        }
                    }
                    catch { }
                }

                var hasAnswer = !string.IsNullOrWhiteSpace(exercise.Answer);

                var userPrompt = $@"请分析以下习题：

题型：{exerciseTypeName}
标题：{exercise.Title}
题目内容：{exercise.QuestionContent}
{optionsText}
{(hasAnswer ? $"当前答案：{exercise.Answer}" : "当前答案：（空缺）")}

{(hasAnswer ? "请基于已有答案生成题目解析。" : "请先给出正确的答案，再生成题目解析。")}";

                var chatOptions = new ChatOptions
                {
                    Instructions = systemPrompt,
                };

                var messages = new List<ChatMessage>
                {
                    new(ChatRole.User, userPrompt)
                };

                var responseBuilder = new StringBuilder();
                await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions, CancellationToken.None))
                {
                    if (update.Text != null)
                    {
                        responseBuilder.Append(update.Text);
                    }
                }

                var responseText = responseBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(responseText)) continue;

                // 去除可能的 markdown 代码块包裹
                var cleanJson = responseText;
                if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                {
                    cleanJson = cleanJson[7..];
                    var idx = cleanJson.LastIndexOf("```");
                    if (idx >= 0) cleanJson = cleanJson[..idx].TrimEnd();
                }
                else if (cleanJson.StartsWith("```"))
                {
                    cleanJson = cleanJson[3..];
                    var idx = cleanJson.LastIndexOf("```");
                    if (idx >= 0) cleanJson = cleanJson[..idx].TrimEnd();
                }

                try
                {
                    var aiResult = JsonSerializer.Deserialize<AiAnalysisResult>(cleanJson);
                    if (aiResult == null) continue;

                    var changed = false;

                    // 如果没有答案，填充 AI 生成的答案
                    if (!hasAnswer && !string.IsNullOrWhiteSpace(aiResult.Answer))
                    {
                        exercise.Answer = aiResult.Answer.Trim();
                        changed = true;
                    }

                    // 填充题目解析
                    if (!string.IsNullOrWhiteSpace(aiResult.QuestionAnalysis))
                    {
                        exercise.QuestionAnalysis = aiResult.QuestionAnalysis.Trim();
                        changed = true;
                    }

                    if (changed)
                    {
                        await _exerciseRepository.UpdateAsync(exercise);
                        result.UpdatedCount++;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning("AI 分析结果解析失败: {Error}, 原始文本: {Raw}", ex.Message, cleanJson[..Math.Min(cleanJson.Length, 200)]);
                    result.Errors.Add($"习题「{exercise.Title}」AI 返回解析失败");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI 分析习题失败: {Title}", exercise.Title);
                result.Errors.Add($"习题「{exercise.Title}」AI 分析失败: {ex.Message}");
            }
        }

        return result;
    }

    private sealed class AiAnalysisResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("questionAnalysis")]
        public string QuestionAnalysis { get; set; } = string.Empty;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task BatchRemoveAsync(List<Guid> ids)
    {
        if (ids == null || ids.Count == 0) return;
        // P3-8：禁用多租户过滤器，与 GetByCourseAsync 的查询范围一致
        using (DataFilter.Disable<IMultiTenant>())
        {
            await _exerciseRepository.DeleteAsync(e => ids.Contains(e.Id));
        }
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
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

            // 获取有数据的行（跳过表头行，表头从 1 开始计数）
            var skipCount = headerMap.DataStartRow;
            var rows = worksheet.RowsUsed().Skip(skipCount).ToList();
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

                    // 处理答案：选择题将数字转换为字母（1=A, 2=B, ..., 26=Z）
                    string processedAnswer = answer ?? string.Empty;
                    if (isChoiceType && !string.IsNullOrWhiteSpace(answer))
                    {
                        // 支持中英文逗号分隔
                        var answerParts = answer.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
                        var letterAnswers = new List<string>();
                        foreach (var part in answerParts)
                        {
                            var trimmed = part.Trim();
                            if (int.TryParse(trimmed, out var num) && num >= 1 && num <= 26)
                            {
                                // 1→A, 2→B, ..., 26→Z
                                letterAnswers.Add(((char)('A' + num - 1)).ToString());
                            }
                            else
                            {
                                letterAnswers.Add(trimmed);
                            }
                        }
                        processedAnswer = string.Join(",", letterAnswers);
                    }

                    // P3-7：读取题目解析列
                    var questionAnalysis = ReadCell(row, headerMap.QuestionAnalysis).Trim();

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
                        QuestionAnalysis = string.IsNullOrWhiteSpace(questionAnalysis) ? null : questionAnalysis,
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
        // 表头通常在第 2 行（第 1 行是指南，可能包含「题目类型」「答案」等子串触发误匹配）。
        // 从第 2 行开始扫描，最多扫到第 4 行。
        var headerRow = 1;
        int questionCol = 0, typeCol = 0, answerCol = 0, questionAnalysisCol = 0, mergedOptionsCol = 0;
        var optionCols = new List<int>();

        for (var r = 2; r <= Math.Min(4, worksheet.LastRowUsed()?.RowNumber() ?? 0); r++)
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
                else if (questionAnalysisCol == 0 && ContainsAny(header, "题目解析", "questionanalysis", "分析"))
                {
                    questionAnalysisCol = c; headerRow = r;
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
            QuestionAnalysis = questionAnalysisCol,
            OptionColumns = optionCols,
            OptionMergedColumn = mergedOptionsCol > 0 ? mergedOptionsCol : null,
            DataStartRow = headerRow
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
        public int QuestionAnalysis { get; set; }
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
            QuestionAnalysis = exercise.QuestionAnalysis,
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
