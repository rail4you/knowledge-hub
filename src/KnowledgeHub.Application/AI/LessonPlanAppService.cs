using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Application.AI;

// Not implementing an ABP interface to avoid Castle DynamicProxy buffering issues with IAsyncEnumerable.
// The controller injects this class directly.
public class LessonPlanAppService : KnowledgeHubAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LessonPlanAppService> _logger;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<PageContent, Guid> _pageContentRepository;

    /// <summary>章节解析时送入模型的正文上限，避免超出上下文。</summary>
    private const int MaxSourceCharsForParsing = 30000;
    /// <summary>逐章生成时作为背景送入模型的正文上限。</summary>
    private const int MaxSourceCharsForGeneration = 12000;

    private const string LessonPlanInstructions = @"你是大学教师/教学设计师，需要根据给定的""文档摘要""和""教学参数""，按中国大学教案（又称""教学设计""）的标准体例，生成一份完整的课堂教案。

严格要求：
1. 必须输出合法 JSON（不要用 markdown 代码块包裹，不要任何多余文字）
2. 教案内容必须基于""文档摘要""，可适度引申但不得编造与摘要无关的具体数据/公式
3. 教学环节时间总和必须等于总课时 duration（分钟）
4. 教学目标按布鲁姆分类法分三层：知识目标 / 能力目标 / 素质目标
5. 教学方法、教学活动、作业均要可操作、可观察
6. 若 ""教师附加要求"" 非空，必须严格遵循其全部约束（如强调课程思政、双语教学、工程实践等）

JSON 结构（沿用现有 schema）：
{
  ""title"": ""教案标题"",
  ""subject"": ""学科"",
  ""grade"": ""年级"",
  ""duration"": 45,
  ""objectives"": [""教学目标1"", ""教学目标2""],
  ""keyPoints"": [""教学重点1""],
  ""difficulties"": [""教学难点1""],
  ""sections"": [
    {
      ""name"": ""环节名称"",
      ""duration"": 10,
      ""content"": ""环节内容描述"",
      ""activities"": [""活动1""]
    }
  ],
  ""methods"": [""教学方法1""],
  ""resources"": [""教学资源1""],
  ""assessment"": [""评估方法1""],
  ""homework"": [""课后作业1""]
}

补充要求：
- title 要形如 ""《xxx》第X章 xxx —— xxx""（明显体现课程与主题）
- objectives 不少于 6 条（每类目标各 2 条以上，知识 / 能力 / 素质三类）
- sections 至少 5 个环节（导入 / 讲授 / 案例或讨论 / 课堂练习 / 总结与答疑）
- 每个 section 的 content 含三部分：教师活动 + 学生活动 + 设计意图
- activities 数组给出可观察的具体动作（提问、分组讨论、上台演算、随堂测验等）
- homework 区分""必做""与""选做/拓展""，不少于 3 条
- methods 选择性采用：讲授法、案例法、讨论法、探究法、演示法、练习法
- 难度与重点须紧扣摘要所揭示的核心概念

## 授课对象差异化要求（严格遵循）
根据「授课对象」参数调整教案的深度、侧重点与风格：

- **大一**：偏基础入门。教学目标侧重学科认知建立与基础概念掌握；教学活动以讲授、演示、课堂讨论为主；难点不宜过深；案例选取贴近学生日常生活经验；作业以巩固型为主。
- **大二**：基础与能力并重。在概念理解基础上增加分析应用环节；适当引入案例分析与小组讨论；作业兼顾巩固与拓展。
- **大三**：侧重专业能力深化。教学目标增加综合分析与评价层次；教学活动增加探究法、案例法比重；引入行业实际案例或项目模拟；作业强调综合应用。
- **大四**：偏综合与高阶。教学目标应覆盖评价与创造层次；强调学科前沿、行业动态、实践创新；教学活动以研讨法、项目法、案例分析为主；内容要有批判性思维训练和综合问题解决训练；作业以研究性、综合性为主，可包含项目方案或设计报告。
- **研究生**：学术研究导向。强调文献与前沿；以研讨法为主；作业与研究课题相关。
- **博士**：高深学术与创新。强调原创性与前沿探索；以独立研究指导为主。";

    private const string ChapterParseInstructions = @"你是课程内容结构分析专家，需要从给定的""文档内容""中识别出完整的章节结构（章 / 节 / 单元 / 专题）。

严格要求：
1. 必须输出合法 JSON（不要用 markdown 代码块包裹，不要任何多余文字）
2. 按文档原有的顺序识别章节；若文档没有显式章节划分，则按主题合理切分为 3-20 个章节
3. 每个章节给出简洁、能体现教学内容的标题
4. 每个章节的 summary 用 2-4 条要点概括本章核心内容（用中文分号""；""分隔），供后续逐章生成教案使用
5. 不要遗漏重要主题，也不要过度拆分
6. 若""解析附加要求""非空，必须严格遵循

JSON 结构：
{
  ""courseTitle"": ""课程 / 文档标题"",
  ""chapters"": [
    { ""order"": 1, ""title"": ""第一章 xxx"", ""summary"": ""要点一；要点二；要点三"" }
  ]
}";

    private const string CourseOverviewInstructions = @"你是大学教学设计师。请根据给定的""课程信息""与""章节列表""，输出整门课程的教学总览。

严格要求：
1. 必须输出合法 JSON（不要用 markdown 代码块包裹，不要任何多余文字）
2. courseTitle 形如 ""《课程名》课程教案""
3. courseObjectives 为课程总体教学目标，不少于 5 条，覆盖知识 / 能力 / 素质三个层次，且与章节内容呼应
4. 不要逐章展开，只输出课程层面的总览

JSON 结构：
{
  ""courseTitle"": ""《xxx》课程教案"",
  ""courseObjectives"": [""课程总体目标1"", ""课程总体目标2""]
}";

    public LessonPlanAppService(
        IConfiguration configuration,
        ILogger<LessonPlanAppService> logger,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<PageContent, Guid> pageContentRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _resourceRepository = resourceRepository;
        _pageContentRepository = pageContentRepository;
    }

    // ====================================================================
    // 单章节：整份文档作为章节内容（保留原有方案）
    // ====================================================================

    // 仅供 SSE Controller 直接调用：Func 回调无法绑定为 HTTP 参数，
    // 必须对 Conventional Controller 隐藏，否则 /Abp/ServiceProxyScript 全站 500。
    [Volo.Abp.RemoteService(false)]
    public async Task GenerateStreamingAsync(LessonPlanGenerationInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = Guid.NewGuid().ToString();

        // 1. 读取资源（包含 AI 摘要）
        var resource = await _resourceRepository.FindAsync(input.ResourceId);
        if (resource == null)
        {
            await EmitErrorAsync(onChunk, threadId, $"未找到资源: {input.ResourceId}");
            return;
        }

        // 2. 摘要缺失校验
        if (string.IsNullOrWhiteSpace(resource.Summary))
        {
            await EmitErrorAsync(onChunk, threadId,
                "该资源尚未生成 AI 摘要，请先在资源详情页生成摘要后再使用教案功能。");
            return;
        }

        // 3. 组合 User Prompt（以 Resource.Summary 为唯一教学依据）
        var userPrompt = BuildSingleChapterUserPrompt(resource, input);

        var chatClient = CreateChatClient();
        var chatOptions = new ChatOptions
        {
            Instructions = LessonPlanInstructions,
            MaxOutputTokens = 8192
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, userPrompt)
        };

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions, CancellationToken.None))
        {
            if (update.Text != null && update.Text.Length > 0)
            {
                await onChunk(new ChatMessageChunkDto
                {
                    Content = update.Text,
                    ThreadId = threadId,
                    IsComplete = false
                });
            }
        }

        await onChunk(new ChatMessageChunkDto
        {
            Content = "",
            ThreadId = threadId,
            IsComplete = true
        });
    }

    private static string BuildSingleChapterUserPrompt(Resource resource, LessonPlanGenerationInputDto input)
    {
        return $@"## 文档摘要（唯一教学依据）
{resource.Summary}

## 文档元信息
- 名称：{resource.Name}
{(string.IsNullOrWhiteSpace(resource.Description) ? "" : $"- 描述：{resource.Description}")}

## 教学参数
- 课程主题：{input.Topic}
- 学科：{(string.IsNullOrWhiteSpace(input.Subject) ? "未指定" : input.Subject)}
- 授课对象：{(string.IsNullOrWhiteSpace(input.Grade) ? "未指定" : input.Grade)}（注意：必须严格按 SystemPrompt 中「授课对象差异化要求」调整教学内容深度、风格与侧重点）
- 课时：{input.Duration} 分钟

{(string.IsNullOrWhiteSpace(input.CustomPrompt) ? "" : $@"## 教师附加要求
{input.CustomPrompt}
")}

请按 SystemPrompt 中规定的 JSON 结构输出教案。";
    }

    // ====================================================================
    // 多章节 步骤一：从文档中解析章节，供用户预览 / 编辑
    // ====================================================================

    [Volo.Abp.RemoteService(false)]
    public async Task ParseChaptersStreamingAsync(
        LessonPlanChapterParseInputDto input,
        Func<LessonPlanStreamEventDto, Task> onChunk)
    {
        var resource = await _resourceRepository.FindAsync(input.ResourceId);
        if (resource == null)
        {
            await EmitErrorAsync(onChunk, $"未找到资源: {input.ResourceId}");
            return;
        }

        var sourceText = await GetResourceSourceTextAsync(resource, MaxSourceCharsForParsing);
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            await EmitErrorAsync(onChunk,
                "该资源没有可用于解析的正文或摘要，请先在资源详情页生成摘要后再使用多章节功能。");
            return;
        }

        var userPrompt = $@"## 文档元信息
- 名称：{resource.Name}
{(string.IsNullOrWhiteSpace(resource.Description) ? "" : $"- 描述：{resource.Description}")}

{(string.IsNullOrWhiteSpace(input.CustomPrompt) ? "" : $@"## 解析附加要求
{input.CustomPrompt}
")}

## 文档内容
{sourceText}

请按 SystemPrompt 中规定的 JSON 结构输出从本文档中识别出的章节列表。";

        var chatClient = CreateChatClient();
        var chatOptions = new ChatOptions
        {
            Instructions = ChapterParseInstructions,
            Temperature = 0.3f,
            MaxOutputTokens = 4096
        };

        var messages = new List<ChatMessage> { new(ChatRole.User, userPrompt) };

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions, CancellationToken.None))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                await onChunk(new LessonPlanStreamEventDto { Content = update.Text });
            }
        }

        await onChunk(new LessonPlanStreamEventDto
        {
            Message = "章节解析完成",
            Progress = 100,
            IsComplete = true
        });
    }

    // ====================================================================
    // 多章节 步骤二：生成""课程总览 + 每章独立教案""的整体教案
    // ====================================================================

    [Volo.Abp.RemoteService(false)]
    public async Task GenerateMultiChapterStreamingAsync(
        MultiChapterLessonPlanGenerationInputDto input,
        Func<LessonPlanStreamEventDto, Task> onChunk)
    {
        var resource = await _resourceRepository.FindAsync(input.ResourceId);
        if (resource == null)
        {
            await EmitErrorAsync(onChunk, $"未找到资源: {input.ResourceId}");
            return;
        }

        var chapters = (input.Chapters ?? new List<LessonPlanChapterDto>())
            .Where(c => !string.IsNullOrWhiteSpace(c.Title))
            .OrderBy(c => c.Order)
            .ToList();

        if (chapters.Count == 0)
        {
            await EmitErrorAsync(onChunk, "请至少保留一个章节后再生成教案。");
            return;
        }

        for (var i = 0; i < chapters.Count; i++)
        {
            chapters[i].Order = i + 1;
        }

        var sourceText = await GetResourceSourceTextAsync(resource, MaxSourceCharsForGeneration);

        try
        {
            var chatClient = CreateChatClient();
            var total = chapters.Count;

            await EmitProgressAsync(onChunk, "正在规划课程总览…", 5);

            var overview = await GenerateCourseOverviewAsync(chatClient, input, chapters, sourceText);

            var result = new MultiChapterLessonPlanDto
            {
                CourseTitle = string.IsNullOrWhiteSpace(overview?.CourseTitle)
                    ? $"{input.Topic}课程教案"
                    : overview!.CourseTitle,
                Subject = input.Subject ?? string.Empty,
                Grade = input.Grade ?? string.Empty,
                Duration = Math.Max(1, input.Duration) * total,
                CourseObjectives = overview?.CourseObjectives ?? new List<string>()
            };

            for (var i = 0; i < total; i++)
            {
                var chapter = chapters[i];
                var progress = 5 + (int)(i / (double)total * 90);

                await EmitProgressAsync(onChunk,
                    $"正在生成第 {i + 1}/{total} 章：{chapter.Title}", progress, i + 1, total);

                var plan = await GenerateChapterPlanAsync(chatClient, input, chapter, sourceText);

                result.Chapters.Add(new LessonPlanChapterPlanDto
                {
                    Order = chapter.Order,
                    ChapterTitle = chapter.Title,
                    LessonPlan = plan
                });
            }

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await onChunk(new LessonPlanStreamEventDto
            {
                Message = "整体教案生成完成",
                Progress = 100,
                IsComplete = true,
                ResultJson = json
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Multi-chapter lesson plan generation failed");
            await EmitErrorAsync(onChunk, $"生成失败：{ex.Message}");
        }
    }

    private async Task<CourseOverviewDto> GenerateCourseOverviewAsync(
        IChatClient chatClient,
        MultiChapterLessonPlanGenerationInputDto input,
        List<LessonPlanChapterDto> chapters,
        string sourceText)
    {
        var chapterList = string.Join("\n", chapters.Select(c =>
            $"{c.Order}. {c.Title}{(string.IsNullOrWhiteSpace(c.Summary) ? "" : $"（{c.Summary}）")}"));

        var userPrompt = $@"## 课程信息
- 课程主题：{input.Topic}
- 学科：{(string.IsNullOrWhiteSpace(input.Subject) ? "未指定" : input.Subject)}
- 授课对象：{(string.IsNullOrWhiteSpace(input.Grade) ? "未指定" : input.Grade)}
- 章节数：{chapters.Count}
- 单章课时：{input.Duration} 分钟

{(string.IsNullOrWhiteSpace(input.CustomPrompt) ? "" : $@"## 教师附加要求
{input.CustomPrompt}
")}
## 章节列表
{chapterList}

{(string.IsNullOrWhiteSpace(sourceText) ? "" : $@"## 文档背景（节选）
{sourceText}
")}
请按 SystemPrompt 中规定的 JSON 结构输出课程总览。";

        var text = await CompleteAsync(chatClient, CourseOverviewInstructions, userPrompt, 2048);
        return Deserialize<CourseOverviewDto>(text) ?? new CourseOverviewDto();
    }

    private async Task<LessonPlanDto> GenerateChapterPlanAsync(
        IChatClient chatClient,
        MultiChapterLessonPlanGenerationInputDto input,
        LessonPlanChapterDto chapter,
        string sourceText)
    {
        var userPrompt = $@"## 本章节信息
- 章节：{chapter.Order}. {chapter.Title}
{(string.IsNullOrWhiteSpace(chapter.Summary) ? "" : $"- 章节要点：{chapter.Summary}")}

## 教学参数
- 课程主题：{input.Topic}
- 学科：{(string.IsNullOrWhiteSpace(input.Subject) ? "未指定" : input.Subject)}
- 授课对象：{(string.IsNullOrWhiteSpace(input.Grade) ? "未指定" : input.Grade)}（注意：必须严格按 SystemPrompt 中「授课对象差异化要求」调整教学内容深度、风格与侧重点）
- 课时：{input.Duration} 分钟

{(string.IsNullOrWhiteSpace(input.CustomPrompt) ? "" : $@"## 教师附加要求
{input.CustomPrompt}
")}
{(string.IsNullOrWhiteSpace(sourceText) ? "" : $@"## 文档背景（节选，仅作参考，本章内容以「本章节信息」为准）
{sourceText}
")}
请为「{chapter.Title}」生成一份完整、可执行的课堂教案，按 SystemPrompt 中规定的单章节 JSON 结构输出。不许合并其他章节内容。";

        var text = await CompleteAsync(chatClient, LessonPlanInstructions, userPrompt, 8192);
        var plan = Deserialize<LessonPlanDto>(text)
            ?? throw new AbpException($"第 {chapter.Order} 章「{chapter.Title}」教案解析失败，请重试。");

        if (string.IsNullOrWhiteSpace(plan.Title)) plan.Title = chapter.Title;
        if (plan.Duration <= 0) plan.Duration = input.Duration;

        return plan;
    }

    // ====================================================================
    // 导出
    // ====================================================================

    /// <summary>
    /// Export a lesson plan JSON as a DOCX file.
    /// </summary>
    public byte[] ExportDocx(string lessonPlanJson)
    {
        var cleanJson = ExtractJson(lessonPlanJson);
        if (string.IsNullOrWhiteSpace(cleanJson))
        {
            throw new AbpException("教案JSON数据为空。");
        }

        var lessonPlan = JsonSerializer.Deserialize<LessonPlanDto>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new AbpException("无法解析教案JSON数据。");

        return LessonPlanDocxGenerator.Generate(lessonPlan);
    }

    /// <summary>
    /// Export a multi-chapter lesson plan JSON as a DOCX file.
    /// </summary>
    public byte[] ExportMultiChapterDocx(string lessonPlanJson)
    {
        var cleanJson = ExtractJson(lessonPlanJson);
        if (string.IsNullOrWhiteSpace(cleanJson))
        {
            throw new AbpException("多章节教案JSON数据为空。");
        }

        var plan = JsonSerializer.Deserialize<MultiChapterLessonPlanDto>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new AbpException("无法解析多章节教案JSON数据。");

        return LessonPlanDocxGenerator.GenerateMultiChapter(plan);
    }

    // ====================================================================
    // 内部辅助
    // ====================================================================

    private IChatClient CreateChatClient()
    {
        var apiKey = _configuration["Qwen:ApiKey"]
            ?? throw new AbpException("Qwen:ApiKey is not configured");
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-plus";

        var openaiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        return openaiClient.GetChatClient(model).AsIChatClient();
    }

    private static async Task<string> CompleteAsync(
        IChatClient chatClient,
        string instructions,
        string userPrompt,
        int maxOutputTokens)
    {
        var options = new ChatOptions
        {
            Instructions = instructions,
            Temperature = 0.4f,
            MaxOutputTokens = maxOutputTokens
        };

        var messages = new List<ChatMessage> { new(ChatRole.User, userPrompt) };

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var response = await chatClient.GetResponseAsync(messages, options, cts.Token);
        return response.Text ?? string.Empty;
    }

    /// <summary>
    /// 优先读取文档全文（PageContent 按页拼接），无全文时回退到 AI 摘要。
    /// </summary>
    private async Task<string> GetResourceSourceTextAsync(Resource resource, int maxChars)
    {
        var query = await _pageContentRepository.GetQueryableAsync();
        var pages = await AsyncExecuter.ToListAsync(
            query.Where(p => p.ResourceId == resource.Id).OrderBy(p => p.PageNumber));

        var sb = new StringBuilder();
        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.Content)) continue;
            sb.Append("【第").Append(page.PageNumber).Append("页】\n")
              .Append(page.Content.Trim()).Append("\n\n");
        }

        var text = sb.ToString();

        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(resource.Summary))
        {
            text = resource.Summary!;
        }

        if (text.Length > maxChars)
        {
            text = text[..maxChars] + "\n\n……（文档过长，已截断）";
        }

        return text.Trim();
    }

    private static string ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var text = raw.Trim();

        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0) text = text[(firstNewline + 1)..];
            if (text.EndsWith("```")) text = text[..^3].TrimEnd();
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            text = text.Substring(start, end - start + 1);
        }

        return text.Trim();
    }

    private static T? Deserialize<T>(string raw) where T : class
    {
        var clean = ExtractJson(raw);
        if (string.IsNullOrWhiteSpace(clean)) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(clean, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static Task EmitProgressAsync(
        Func<LessonPlanStreamEventDto, Task> onChunk,
        string message,
        int progress,
        int? chapterIndex = null,
        int? chapterTotal = null)
    {
        return onChunk(new LessonPlanStreamEventDto
        {
            Message = message,
            Progress = progress,
            ChapterIndex = chapterIndex,
            ChapterTotal = chapterTotal
        });
    }

    private static async Task EmitErrorAsync(Func<LessonPlanStreamEventDto, Task> onChunk, string message)
    {
        await onChunk(new LessonPlanStreamEventDto
        {
            IsError = true,
            Message = message
        });
        await onChunk(new LessonPlanStreamEventDto
        {
            IsComplete = true,
            IsError = true,
            Message = message
        });
    }

    /// <summary>
    /// 流式输出一个错误块后立即 complete（单章节沿用旧的 Chunk DTO）。
    /// </summary>
    private static async Task EmitErrorAsync(Func<ChatMessageChunkDto, Task> onChunk, string threadId, string message)
    {
        await onChunk(new ChatMessageChunkDto
        {
            Content = JsonSerializer.Serialize(new { error = message }),
            ThreadId = threadId,
            IsComplete = false
        });
        await onChunk(new ChatMessageChunkDto
        {
            Content = "",
            ThreadId = threadId,
            IsComplete = true
        });
    }
}
