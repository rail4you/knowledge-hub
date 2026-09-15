using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
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
// 说明：本服务不直接暴露 HTTP（公开方法均标注 [RemoteService(false)]，由 AIController
// 以 [Authorize(AI.LessonPlan)] 代理），且被 AiGenerationJob 后台任务直接调用，
// 因此不在类级加 [Authorize]（后台任务无用户上下文）。
public class LessonPlanAppService : KnowledgeHubAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LessonPlanAppService> _logger;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<PageContent, Guid> _pageContentRepository;
    private readonly IMeiliSearchService _meiliSearchService;

    /// <summary>章节解析时送入模型的正文上限，避免超出上下文。</summary>
    private const int MaxSourceCharsForParsing = 30000;
    /// <summary>逐章生成时作为背景送入模型的正文上限。</summary>
    private const int MaxSourceCharsForGeneration = 12000;
    /// <summary>视频时间轴索引单次最多取回的片段数（每段约几秒，500 段已覆盖较长视频）。</summary>
    private const int MaxVideoTimelineSegments = 500;

    private const string LessonPlanInstructionsTemplate = @"你是大学教师/教学设计师，需要根据给定的""文档内容""和""教学参数""，按中国大学教案（又称""教学设计""）的标准体例，生成一份完整的课堂教案。

严格要求：
1. 必须输出合法 JSON（不要用 markdown 代码块包裹，不要任何多余文字）
2. 教案内容必须基于""文档内容""，可适度引申但不得编造与文档无关的具体数据/公式
3. 教学环节时间总和必须严格等于下方「教学参数」中给出的课时值（分钟）；JSON 顶层的 duration 也必须等于该课时值，绝对不要使用下方 schema 示例里的 45
4. 教学目标按布鲁姆分类法分三层：知识目标 / 能力目标 / 素质目标
5. 教学方法、教学活动、作业均要可操作、可观察
6. 若 ""教师附加要求"" 非空，必须严格遵循其全部约束（如强调课程思政、双语教学、工程实践等）
7. 学科、授课对象必须严格使用「教学参数」中给定的值，不要自行改写或编造

JSON 结构（沿用现有 schema，其中数值仅为格式示例）：
{
  ""title"": ""教案标题"",
  ""subject"": ""学科（= 教学参数中的学科）"",
  ""grade"": ""授课对象（= 教学参数中的授课对象）"",
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
- [[TITLE_RULE]]
- 上述 schema 中的 duration（45）与各 section.duration（10）只是格式占位；实际输出时顶层 duration 必须等于「教学参数」的课时，各 section.duration 之和也必须等于该课时
- objectives 不少于 6 条（每类目标各 2 条以上，知识 / 能力 / 素质三类）
- sections 至少 5 个环节（导入 / 讲授 / 案例或讨论 / 课堂练习 / 总结与答疑）
- 每个 section 的 content 含三部分：教师活动 + 学生活动 + 设计意图
- activities 数组给出可观察的具体动作（提问、分组讨论、上台演算、随堂测验等）
- homework 区分""必做""与""选做/拓展""，不少于 3 条
- methods 选择性采用：讲授法、案例法、讨论法、探究法、演示法、练习法
- 难度与重点须紧扣文档内容所揭示的核心概念

## 授课对象差异化要求（严格遵循）
根据「授课对象」参数调整教案的深度、侧重点与风格：

- **大一**：偏基础入门。教学目标侧重学科认知建立与基础概念掌握；教学活动以讲授、演示、课堂讨论为主；难点不宜过深；案例选取贴近学生日常生活经验；作业以巩固型为主。
- **大二**：基础与能力并重。在概念理解基础上增加分析应用环节；适当引入案例分析与小组讨论；作业兼顾巩固与拓展。
- **大三**：侧重专业能力深化。教学目标增加综合分析与评价层次；教学活动增加探究法、案例法比重；引入行业实际案例或项目模拟；作业强调综合应用。
- **大四**：偏综合与高阶。教学目标应覆盖评价与创造层次；强调学科前沿、行业动态、实践创新；教学活动以研讨法、项目法、案例分析为主；内容要有批判性思维训练和综合问题解决训练；作业以研究性、综合性为主，可包含项目方案或设计报告。
- **研究生**：学术研究导向。强调文献与前沿；以研讨法为主；作业与研究课题相关。
- **博士**：高深学术与创新。强调原创性与前沿探索；以独立研究指导为主。";

    // 教案标题规则（两种模式共用同一套指令，仅标题规则不同）：
    // - 多章节：章节有明确归属，title 体现「课程 + 章节」。
    // - 单章节：章节位置未确定，title 只体现文档主题，禁止出现章/节/课程级标识。
    private const string LessonPlanTitleRuleMulti = @"- title 要形如 ""《xxx》第X章 xxx —— xxx""（明显体现课程与主题）";
    private const string LessonPlanTitleRuleSingle = @"- title 要基于文档名称（即下方「文档元信息」中的「名称」），形如 ""《文档名》教学教案""，只体现文档主题；本模式没有确定的章/节定位，禁止在 title 或教案内容中出现「第X章」「第X节」「课程教案」等课程级/章节级标识，也不要编造章节编号";

    private static readonly string LessonPlanInstructions =
        LessonPlanInstructionsTemplate.Replace("[[TITLE_RULE]]", LessonPlanTitleRuleMulti);

    /// <summary>单章节教案指令：标题仅采用文章/文档名称，不出现课程、章节定位标识。</summary>
    private static readonly string LessonPlanSingleInstructions =
        LessonPlanInstructionsTemplate.Replace("[[TITLE_RULE]]", LessonPlanTitleRuleSingle);

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
        IRepository<PageContent, Guid> pageContentRepository,
        IMeiliSearchService meiliSearchService)
    {
        _configuration = configuration;
        _logger = logger;
        _resourceRepository = resourceRepository;
        _pageContentRepository = pageContentRepository;
        _meiliSearchService = meiliSearchService;
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

        // 1. 读取资源
        var resource = await _resourceRepository.FindAsync(input.ResourceId);
        if (resource == null)
        {
            await EmitErrorAsync(onChunk, threadId, $"未找到资源: {input.ResourceId}");
            return;
        }

        // 2. 教学依据：优先全文索引（PageContent），缺失时回退到 AI 摘要。
        // 与多章节 / 章节解析共用 GetResourceSourceTextAsync，口径一致。
        var sourceText = await GetResourceSourceTextAsync(resource, MaxSourceCharsForGeneration);
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            await EmitErrorAsync(onChunk, threadId,
                "该资源没有可用于生成的正文或摘要，请先在资源详情页生成摘要或等待文档索引完成后再使用教案功能。");
            return;
        }

        // 3. 组合 User Prompt（以全文/摘要为教学依据）
        var userPrompt = BuildSingleChapterUserPrompt(resource, sourceText, input);

        var chatClient = await CreateChatClient();
        var chatOptions = new ChatOptions
        {
            Instructions = LessonPlanSingleInstructions,
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

    private static string BuildSingleChapterUserPrompt(Resource resource, string sourceText, LessonPlanGenerationInputDto input)
    {
        var topicLine = string.IsNullOrWhiteSpace(input.Topic)
            ? $"- 章节标题：未提供（教案主题直接采用文档名称「{resource.Name}」，不编造章节号或课程名）"
            : $"- 章节标题：{input.Topic}";

        return $@"## 文档内容（教学依据）
{sourceText}

## 文档元信息
- 名称：{resource.Name}
{(string.IsNullOrWhiteSpace(resource.Description) ? "" : $"- 描述：{resource.Description}")}

## 教学参数
{topicLine}
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

        var chatClient = await CreateChatClient();
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
        Func<LessonPlanStreamEventDto, Task> onChunk,
        CancellationToken cancellationToken = default)
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
            var chatClient = await CreateChatClient();
            var total = chapters.Count;

            await EmitProgressAsync(onChunk, "正在规划课程总览…", 5);

            var overview = await RunWithHeartbeatAsync(
                ct => GenerateCourseOverviewAsync(chatClient, input, chapters, sourceText, ct),
                elapsed => EmitProgressAsync(onChunk, $"正在规划课程总览…（已等待{elapsed}秒）", 5),
                cancellationToken);

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
                cancellationToken.ThrowIfCancellationRequested();
                var chapter = chapters[i];
                var progress = 5 + (int)(i / (double)total * 90);
                var chapterIndex = i + 1;
                var caption = $"正在生成第 {chapterIndex}/{total} 章：{chapter.Title}";

                await EmitProgressAsync(onChunk, caption, progress, chapterIndex, total);

                // 单章大模型调用常需 30~120 秒，期间每 5 秒发一次心跳，
                // 既让前端进度条/文案持续活动（避免看似“卡死”），也防止中间代理因空闲掐掉 SSE 连接。
                var plan = await RunWithHeartbeatAsync(
                    ct => GenerateChapterPlanAsync(chatClient, input, chapter, sourceText, ct),
                    elapsed => EmitProgressAsync(onChunk, $"{caption}（已等待{elapsed}秒）", progress, chapterIndex, total),
                    cancellationToken);

                result.Chapters.Add(new LessonPlanChapterPlanDto
                {
                    Order = chapter.Order,
                    ChapterTitle = chapter.Title,
                    LessonPlan = plan
                });

                await EmitProgressAsync(onChunk, $"第 {chapterIndex}/{total} 章已完成：{chapter.Title}",
                    5 + (int)((i + 1) / (double)total * 90), chapterIndex, total);
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
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Multi-chapter lesson plan generation cancelled by client");
            await EmitErrorAsync(onChunk, "已取消生成。");
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
        string sourceText,
        CancellationToken cancellationToken = default)
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

        var text = await CompleteAsync(chatClient, CourseOverviewInstructions, userPrompt, 2048, cancellationToken);
        return Deserialize<CourseOverviewDto>(text) ?? new CourseOverviewDto();
    }

    private async Task<LessonPlanDto> GenerateChapterPlanAsync(
        IChatClient chatClient,
        MultiChapterLessonPlanGenerationInputDto input,
        LessonPlanChapterDto chapter,
        string sourceText,
        CancellationToken cancellationToken = default)
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

        var text = await CompleteAsync(chatClient, LessonPlanInstructions, userPrompt, 8192, cancellationToken);
        var plan = Deserialize<LessonPlanDto>(text)
            ?? throw new AbpException($"第 {chapter.Order} 章「{chapter.Title}」教案解析失败，请重试。");

        if (string.IsNullOrWhiteSpace(plan.Title)) plan.Title = chapter.Title;

        // 严格对应输入参数：学科 / 授课对象 / 单章课时 以用户输入为准，
        // 并把 AI 生成的环节时长缩放到与课时一致（AI 常锚定 schema 示例的 45 分钟）。
        ApplyInputOverrides(plan, input.Subject, input.Grade, input.Duration);

        return plan;
    }

    // ====================================================================
    // 导出
    // ====================================================================

    /// <summary>
    /// Export a lesson plan JSON as a DOCX file.
    /// </summary>
    [Volo.Abp.RemoteService(false)]
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
    [Volo.Abp.RemoteService(false)]
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

    private async Task<IChatClient> CreateChatClient()
    {
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-flash";

        return await QwenClient.CreateChatClient(_configuration, model);
    }

    private static async Task<string> CompleteAsync(
        IChatClient chatClient,
        string instructions,
        string userPrompt,
        int maxOutputTokens,
        CancellationToken cancellationToken = default)
    {
        var options = new ChatOptions
        {
            Instructions = instructions,
            Temperature = 0.4f,
            MaxOutputTokens = maxOutputTokens
        };

        var messages = new List<ChatMessage> { new(ChatRole.User, userPrompt) };

        // 与前端取消/断开联动：客户端 abort 会触发 RequestAborted，这里的 linked CTS 会同步取消，
        // 避免用户取消后后端仍在空跑大模型调用。
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(5));
        var response = await chatClient.GetResponseAsync(messages, options, cts.Token);
        return response.Text ?? string.Empty;
    }

    /// <summary>
    /// 在长时间大模型调用期间每 5 秒发送一次心跳进度，避免前端看起来“卡死”，
    /// 也防止 SSE 空闲过久被中间代理掐断。work 抛出的异常会原样透出。
    /// </summary>
    private static async Task<T> RunWithHeartbeatAsync<T>(
        Func<CancellationToken, Task<T>> run,
        Func<int, Task> heartbeat,
        CancellationToken cancellationToken)
    {
        var work = run(cancellationToken);
        var elapsed = 0;
        while (!work.IsCompleted)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            if (work.IsCompleted) break;
            elapsed += 5;
            try
            {
                await heartbeat(elapsed);
            }
            catch
            {
                // 心跳写失败（多为客户端已断开），交给 work 本身的等待去感知取消。
                break;
            }
        }
        return await work;
    }

    /// <summary>
    /// 优先读取文档全文（PageContent 按页拼接）；视频资源无 PageContent，
    /// 回退到 MeiliSearch 里的时间轴文本索引（SourceType = video，带起止时间戳）。
    /// 两者都缺失时才回退到 AI 摘要。
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

        if (string.IsNullOrWhiteSpace(text) && IsVideoResource(resource))
        {
            text = await BuildVideoTimelineTextAsync(resource);
        }

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

    /// <summary>
    /// 从 MeiliSearch 视频时间轴索引取该资源的全部片段，按开始时间排序，
    /// 拼成「[起 - 止] 事件描述」的文本，供 AI 作为教学依据（与文档问答的 get_document 口径一致）。
    /// </summary>
    private async Task<string> BuildVideoTimelineTextAsync(Resource resource)
    {
        try
        {
            var search = await _meiliSearchService.SearchAsync(new SearchQueryDto
            {
                Query = "",
                ResourceId = resource.Id,
                MaxResultCount = MaxVideoTimelineSegments,
                SkipCount = 0,
            });

            var lines = search.Items
                .Where(x => string.Equals(x.SourceType, "video", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.StartTime)
                .Select(x => $"[{x.StartTime} - {x.EndTime}] {x.EventDescription}")
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            if (lines.Count == 0) return string.Empty;

            return "【视频时间轴索引】\n" + string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BuildVideoTimelineTextAsync failed for resource {ResourceId}", resource.Id);
            return string.Empty;
        }
    }

    private static bool IsVideoResource(Resource resource)
    {
        if (resource.ResourceType == KnowledgeHub.Resources.Enums.ResourceType.Video) return true;

        var ext = (resource.FileExtension ?? "").Trim();
        if (ext.StartsWith(".")) ext = ext.Substring(1);
        return VideoExtensions.Contains(ext.ToLowerInvariant());
    }

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mov", "qt", "avi", "mkv", "wmv", "flv", "webm", "m4v", "mpg", "mpeg", "3gp"
    };

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

    /// <summary>
    /// 单章节结果 JSON 规范化：把 AI 输出中的学科 / 授课对象 / 课时强制替换为用户输入值，
    /// 并按目标课时等比缩放各教学环节时长，保证"输入字段严格对应、环节总和 = 课时"。
    /// 解析失败时原样返回（交给前端兜底）。
    /// </summary>
    [Volo.Abp.RemoteService(false)]
    public string NormalizeSingleLessonPlan(string rawJson, string? subject, string? grade, int duration)
    {
        var plan = Deserialize<LessonPlanDto>(rawJson);
        if (plan == null) return rawJson;

        ApplyInputOverrides(plan, subject, grade, duration);

        return JsonSerializer.Serialize(plan, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// 用用户输入覆盖单份教案的学科 / 授课对象 / 课时，并缩放教学环节时长。
    /// </summary>
    private static void ApplyInputOverrides(LessonPlanDto plan, string? subject, string? grade, int duration)
    {
        if (plan == null) return;

        if (!string.IsNullOrWhiteSpace(subject)) plan.Subject = subject!;
        if (!string.IsNullOrWhiteSpace(grade)) plan.Grade = grade!;
        if (duration > 0) plan.Duration = duration;

        RescaleSectionDurations(plan.Sections, duration);
    }

    /// <summary>
    /// 将各环节时长等比缩放到总和 = 目标课时。使用最大余数法，避免四舍五入后总和偏差。
    /// </summary>
    private static void RescaleSectionDurations(List<TeachingSectionDto>? sections, int target)
    {
        if (sections == null || sections.Count == 0 || target <= 0) return;

        var sum = sections.Sum(s => Math.Max(0, s.Duration));

        if (sum <= 0)
        {
            var each = target / sections.Count;
            var rem = target - each * sections.Count;
            for (var i = 0; i < sections.Count; i++)
            {
                sections[i].Duration = each + (i < rem ? 1 : 0);
            }
            return;
        }

        if (sum == target) return;

        var scaled = sections.Select(s => Math.Max(0, s.Duration) * (double)target / sum).ToList();
        var result = scaled.Select(x => (int)Math.Floor(x)).ToList();
        var remainder = target - result.Sum();

        var order = Enumerable.Range(0, sections.Count)
            .OrderByDescending(i => scaled[i] - result[i])
            .ToList();

        for (var i = 0; i < remainder && i < order.Count; i++)
        {
            result[order[i]]++;
        }

        for (var i = 0; i < sections.Count; i++)
        {
            sections[i].Duration = result[i];
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
