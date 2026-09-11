using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams.Dtos;
using KnowledgeHub.Exams.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.Exams;

/// <summary>
/// 习题 AI 生成核心逻辑（不含 [Authorize]，可供后台作业直接调用）。
/// ExerciseAppService.GenerateByAIAsync 与 AI 后台任务共用。
/// </summary>
public class ExerciseAiGenerator : ITransientDependency
{
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<ChapterExercise, Guid> _chapterExerciseRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExerciseAiGenerator> _logger;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public ExerciseAiGenerator(
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<ChapterExercise, Guid> chapterExerciseRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IConfiguration configuration,
        ILogger<ExerciseAiGenerator> logger,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _exerciseRepository = exerciseRepository;
        _chapterExerciseRepository = chapterExerciseRepository;
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _configuration = configuration;
        _logger = logger;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    [UnitOfWork]
    public async Task<List<ExerciseDto>> GenerateAsync(GenerateExerciseInput input)
    {
        if (input.CourseId == Guid.Empty)
        {
            throw new UserFriendlyException("请先选择课程");
        }

        var count = Math.Clamp(input.Count <= 0 ? 5 : input.Count, 1, 20);
        var difficulty = Math.Clamp(input.Difficulty <= 0 ? 2 : input.Difficulty, 1, 5);

        var course = await _courseRepository.GetAsync(input.CourseId);

        // 合并主章节 + 章节列表（去重），并校验章节归属本课程
        var allChapterIds = MergeChapterIds(input.ChapterId, input.ChapterIds);
        List<Chapter> chapters = new();
        if (allChapterIds.Count > 0)
        {
            var chapterQuery = await _chapterRepository.GetQueryableAsync();
            chapters = await chapterQuery.Where(x => allChapterIds.Contains(x.Id)).ToListAsync();
            var foreign = chapters.Where(x => x.CourseId != input.CourseId).Select(x => x.Title).ToList();
            if (foreign.Count > 0)
            {
                throw new UserFriendlyException($"以下章节不属于当前课程：{string.Join("、", foreign)}");
            }
            var primaryChapterId = input.ChapterId ?? allChapterIds.FirstOrDefault();
            allChapterIds = MergeChapterIds(primaryChapterId == Guid.Empty ? null : primaryChapterId, allChapterIds);
        }
        Guid? primaryChapter = allChapterIds.Count > 0 ? allChapterIds[0] : null;

        var typeName = input.Type switch
        {
            ExerciseType.SingleChoice => "单选题",
            ExerciseType.MultiChoice => "多选题",
            ExerciseType.TrueFalse => "判断题",
            ExerciseType.FillBlank => "填空题",
            ExerciseType.ShortAnswer => "问答题",
            ExerciseType.Essay => "论述题",
            ExerciseType.CaseAnalysis => "案例分析",
            _ => "单选题"
        };
        var difficultyName = difficulty switch
        {
            1 => "入门",
            2 => "简单",
            3 => "中等",
            4 => "困难",
            _ => "专家"
        };
        var isChoice = input.Type is ExerciseType.SingleChoice or ExerciseType.MultiChoice;

        var chapterScope = chapters.Count > 0
            ? $"章节范围：{string.Join("、", chapters.Select(x => $"《{x.Title}》"))}（共 {chapters.Count} 个章节，题目知识点必须落在这些章节内）"
            : "章节范围：整门课程（覆盖课程主要知识点）";
        var topicHint = string.IsNullOrWhiteSpace(input.TopicHint) ? string.Empty : $"\n主题提示：{input.TopicHint.Trim()}";
        var customPrompt = string.IsNullOrWhiteSpace(input.CustomPrompt) ? string.Empty : $"\n教师自定义要求：{input.CustomPrompt.Trim()}";

        var systemPrompt = @"你是一名资深学科教师，擅长根据课程与章节内容命制高质量习题。
必须严格输出 JSON 数组，不要输出任何解释、前言或 markdown 代码块标记。
数组中每个元素即一道题，字段如下：
{
  ""title"": ""题目标题（20字以内，概括考点）"",
  ""questionContent"": ""题干全文"",
  ""options"": [""选项A文本"", ""选项B文本"", ""选项C文本"", ""选项D文本""]（仅选择题必填，4个选项；非选择题给空数组）"",
  ""answer"": ""参考答案（单选如'A'；多选如'A,B'；判断用'对'或'错'；填空/问答/论述/案例直接给文本答案）"",
  ""questionAnalysis"": ""题目解析（考查知识点、解题思路、易错点）"",
  ""score"": 1（本题分值整数，建议 1-10）
}
要求：选项互斥且有干扰性；答案准确；解析简洁专业。";

        var userPrompt = $@"请为以下课程命制 {count} 道{typeName}（难度：{difficultyName}）：

课程：《{course.Title}》{(string.IsNullOrWhiteSpace(course.Description) ? string.Empty : $"（简介：{course.Description}）")}
{chapterScope}{topicHint}{customPrompt}

请直接输出 JSON 数组。";

        var apiKey = _configuration["Qwen:ApiKey"]
            ?? throw new AbpException("Qwen:ApiKey is not configured");
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-plus";

        IChatClient chatClient = QwenClient.CreateChatClient(_configuration, model);

        var messages = new List<ChatMessage> { new(ChatRole.User, userPrompt) };
        var chatOptions = new ChatOptions { Instructions = systemPrompt };
        string responseText;
        try
        {
            var response = await chatClient.GetResponseAsync(messages, chatOptions, CancellationToken.None);
            responseText = (response.Text ?? string.Empty).Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 生成习题调用模型失败：课程 {Course}", course.Title);
            throw new UserFriendlyException($"AI 生成失败：{ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new UserFriendlyException("AI 未返回任何内容，请重试");
        }

        var cleanJson = StripMarkdownFences(responseText);
        List<AiGeneratedExerciseItem>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<AiGeneratedExerciseItem>>(
                cleanJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("AI 生成习题 JSON 解析失败：{Error}，原文前200字：{Raw}",
                ex.Message, cleanJson[..Math.Min(cleanJson.Length, 200)]);
            throw new UserFriendlyException("AI 返回格式异常，请重试");
        }

        if (items == null || items.Count == 0)
        {
            throw new UserFriendlyException("AI 未生成有效习题，请调整条件后重试");
        }

        var result = new List<ExerciseDto>();
        var currentTenantId = _currentTenant.Id;
        foreach (var item in items.Take(count))
        {
            if (string.IsNullOrWhiteSpace(item.QuestionContent))
            {
                continue;
            }

            string? optionsJson = null;
            if (isChoice && item.Options != null && item.Options.Count > 0)
            {
                var cleanOptions = item.Options
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o.Trim())
                    .Take(6)
                    .ToList();
                if (cleanOptions.Count >= 2)
                {
                    optionsJson = JsonSerializer.Serialize(cleanOptions);
                }
            }

            var title = string.IsNullOrWhiteSpace(item.Title)
                ? (item.QuestionContent.Length > 30 ? item.QuestionContent[..30] : item.QuestionContent)
                : item.Title.Trim();

            var exercise = new Exercise(
                _guidGenerator.Create(),
                input.CourseId,
                title,
                item.QuestionContent.Trim(),
                input.Type,
                (item.Answer ?? string.Empty).Trim())
            {
                ChapterId = primaryChapter,
                KnowledgeResourceId = input.KnowledgeResourceId,
                Options = optionsJson,
                QuestionAnalysis = string.IsNullOrWhiteSpace(item.QuestionAnalysis) ? null : item.QuestionAnalysis.Trim(),
                Difficulty = difficulty,
                Score = item.Score is >= 1 and <= 100 ? item.Score.Value : 1,
                IsAiGenerated = true,
                TenantId = currentTenantId
            };

            await _exerciseRepository.InsertAsync(exercise);

            if (allChapterIds.Count > 0)
            {
                await SyncChapterExercisesAsync(exercise.Id, allChapterIds);
            }

            result.Add(MapToDto(exercise, allChapterIds));
        }

        if (result.Count == 0)
        {
            throw new UserFriendlyException("AI 生成的习题均无效，请重试");
        }

        return result;
    }

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

    private async Task SyncChapterExercisesAsync(Guid exerciseId, List<Guid> chapterIds)
    {
        var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();
        var existing = ceQuery.Where(x => x.ExerciseId == exerciseId).ToList();

        var newIdSet = new HashSet<Guid>(chapterIds);
        var existingIdSet = new HashSet<Guid>(existing.Select(x => x.ChapterId));

        foreach (var row in existing)
        {
            if (!newIdSet.Contains(row.ChapterId))
            {
                await _chapterExerciseRepository.DeleteAsync(row);
            }
        }

        var currentTenantId = _currentTenant.Id;
        var sortOrder = 0;
        foreach (var chapterId in chapterIds)
        {
            if (existingIdSet.Contains(chapterId)) continue;
            var ce = new ChapterExercise(_guidGenerator.Create(), chapterId, exerciseId, sortOrder++)
            {
                TenantId = currentTenantId
            };
            await _chapterExerciseRepository.InsertAsync(ce);
        }
    }

    private static string StripMarkdownFences(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            t = t[7..];
            var idx = t.LastIndexOf("```", StringComparison.Ordinal);
            if (idx >= 0) t = t[..idx];
        }
        else if (t.StartsWith("```", StringComparison.Ordinal))
        {
            t = t[3..];
            var idx = t.LastIndexOf("```", StringComparison.Ordinal);
            if (idx >= 0) t = t[..idx];
        }
        return t.Trim();
    }

    private static ExerciseDto MapToDto(Exercise exercise, List<Guid>? chapterIds = null)
    {
        return new ExerciseDto
        {
            Id = exercise.Id,
            CourseId = exercise.CourseId,
            ChapterId = exercise.ChapterId,
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

    private sealed class AiGeneratedExerciseItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("questionContent")]
        public string QuestionContent { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("options")]
        public List<string>? Options { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("questionAnalysis")]
        public string QuestionAnalysis { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("score")]
        public int? Score { get; set; }
    }
}
