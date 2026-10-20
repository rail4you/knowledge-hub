using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
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
// 以 [Authorize(AI.CaseAnalysis)] 代理），且被 AiGenerationJob 后台任务直接调用，
// 因此不在类级加 [Authorize]（后台任务无用户上下文）。
public class CaseAnalysisAppService : KnowledgeHubAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CaseAnalysisAppService> _logger;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IMeiliSearchService _meiliSearchService;

    /// <summary>视频时间轴最多取多少个片段作为案例依据（与教案生成一致）。</summary>
    private const int MaxVideoTimelineSegments = 500;

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "mov", "qt", "avi", "mkv", "wmv", "flv", "webm", "m4v", "mpg", "mpeg", "3gp"
    };

    private const string CaseAnalysisInstructions = @"你是一个专业的案例分析助手。你需要根据提供的""案例参考文本""和""分析关注点"",生成一份结构化的多维度案例分析报告。

严格要求：
1. 必须输出有效 JSON（不要用 markdown 代码块包裹,不要任何多余文字）
2. 分析内容必须基于""案例参考文本"",可适度引申但不得编造与参考文本无关的具体数据/事实
3. 分析要深入、有洞察力,结合参考文本中的实际信息
4. 所有 JSON 字段值必须使用简体中文表达
5. 输出内容中禁止出现任何英文单词、英文缩写、拉丁字母、英文标题、英文项目符号或中英混写
6. 参考文本中的英文术语、英文缩写、产品名、方法名、模型名、框架名、岗位名、部门名等,必须改写为纯中文表达;如果无法直译,也要使用中文释义,不能保留英文原文
7. 时间、数字、百分比、金额可以保留阿拉伯数字,但单位与说明必须使用中文
8. `severity` 字段只能填写 ""高""/""中""/""低""
9. 如果你发现自己将要输出任何英文字符,必须先改写成中文后再输出
10. 若 ""分析关注点"" 非空,必须在关键问题/解决方案/洞察/建议中重点体现该关注点
11. 若参考文本为视频时间轴（每行形如 ""[起 - 止] 事件描述""）,可据此判断事件先后顺序与阶段划分;时间点必须原样引用,不得杜撰未出现的时间点
12. 行业、时间范围、相关方必须从参考文本中真实提取,不得凭空捏造

JSON 结构:
{
  ""title"": ""案例标题"",
  ""summary"": ""案例摘要(150字以内)"",
  ""background"": {
    ""industry"": ""所属行业"",
    ""timeframe"": ""时间范围"",
    ""context"": ""背景描述"",
    ""stakeholders"": [""相关方1"", ""相关方2""]
  },
  ""keyIssues"": [
    {
      ""id"": ""1"",
      ""title"": ""问题标题"",
      ""description"": ""问题描述"",
      ""impact"": ""影响分析"",
      ""severity"": ""高/中/低""
    }
  ],
  ""solutions"": [
    {
      ""id"": ""1"",
      ""title"": ""方案标题"",
      ""description"": ""方案描述"",
      ""steps"": [""步骤1"", ""步骤2""],
      ""expectedOutcome"": ""预期效果""
    }
  ],
  ""keyInsights"": [""洞察1"", ""洞察2""],
  ""recommendations"": [""建议1"", ""建议2""]
}

补充要求:
- 至少识别 3 个关键问题,每个问题给出 description / impact / severity
- 至少给出 2 个解决方案,每个方案 steps 不少于 3 步
- keyInsights 与 recommendations 各不少于 3 条,具有可操作性";

    public CaseAnalysisAppService(
        IConfiguration configuration,
        ILogger<CaseAnalysisAppService> logger,
        IRepository<Resource, Guid> resourceRepository,
        IMeiliSearchService meiliSearchService)
    {
        _configuration = configuration;
        _logger = logger;
        _resourceRepository = resourceRepository;
        _meiliSearchService = meiliSearchService;
    }

    // 仅供 SSE Controller 直接调用：Func 回调无法绑定为 HTTP 参数，
    // 必须对 Conventional Controller 隐藏，否则 /Abp/ServiceProxyScript 全站 500。
    [Volo.Abp.RemoteService(false)]
    public async Task GenerateStreamingAsync(CaseAnalysisGenerationInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = Guid.NewGuid().ToString();

        // 1. 读取资源
        var resource = await _resourceRepository.FindAsync(input.ResourceId);
        if (resource == null)
        {
            await EmitErrorAsync(onChunk, threadId, $"未找到资源: {input.ResourceId}");
            return;
        }

        // 2. 案例依据：视频取时间轴文本，文档取 AI 摘要
        var isVideo = IsVideoResource(resource);
        var referenceText = string.Empty;
        var referenceTitle = "文档摘要(唯一案例依据)";
        var extraSection = string.Empty;

        if (isVideo)
        {
            var timelineText = await BuildVideoTimelineTextAsync(resource);
            if (!string.IsNullOrWhiteSpace(timelineText))
            {
                referenceText = timelineText;
                referenceTitle = "视频时间轴文本(唯一案例依据)";
            }

            // 视频若同时有 AI 摘要，作为补充信息一起给模型
            if (!string.IsNullOrWhiteSpace(resource.Summary))
            {
                extraSection = $@"## 视频摘要(补充参考)
{resource.Summary}

";
            }
        }

        if (string.IsNullOrWhiteSpace(referenceText) && !string.IsNullOrWhiteSpace(resource.Summary))
        {
            referenceText = resource.Summary!;
            referenceTitle = "文档摘要(唯一案例依据)";
        }

        if (string.IsNullOrWhiteSpace(referenceText))
        {
            await EmitErrorAsync(onChunk, threadId, isVideo
                ? "该视频尚未生成时间轴文本,请先完成视频索引后再使用案例分析功能。"
                : "该资源尚未生成 AI 摘要,请先在资源详情页生成摘要后再使用案例分析功能。");
            return;
        }

        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-flash";

        // 3. 组合 User Prompt(案例参考文本为唯一案例依据)
        var userPrompt = $@"## {referenceTitle}
{referenceText}

{extraSection}## 资源元信息
- 名称:{resource.Name}
- 类型:{(isVideo ? "视频" : "文档")}
{(string.IsNullOrWhiteSpace(resource.Description) ? "" : $"- 描述:{resource.Description}")}

{(string.IsNullOrWhiteSpace(input.FocusArea) ? "" : $@"## 分析关注点(教师指定)
{input.FocusArea}
")}

请按 SystemPrompt 中规定的 JSON 结构输出案例分析。";

        IChatClient chatClient = await QwenClient.CreateChatClient(_configuration, model);

        var chatOptions = new ChatOptions
        {
            Instructions = CaseAnalysisInstructions,
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

    /// <summary>
    /// 从 MeiliSearch 视频时间轴索引取该资源的全部片段，按开始时间排序，
    /// 拼成「[起 - 止] 事件描述」的文本，供 AI 作为案例依据（与教案生成口径一致）。
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

    /// <summary>
    /// 流式输出一个错误块后立即 complete。
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

    [Volo.Abp.RemoteService(false)]
    public byte[] ExportDocx(string caseAnalysisJson)
    {
        var cleanJson = caseAnalysisJson.Trim();
        if (cleanJson.StartsWith("```"))
        {
            var firstNewline = cleanJson.IndexOf('\n');
            if (firstNewline >= 0) cleanJson = cleanJson[(firstNewline + 1)..];
            if (cleanJson.EndsWith("```"))
            {
                cleanJson = cleanJson[..^3].TrimEnd();
            }
        }

        var caseAnalysis = JsonSerializer.Deserialize<CaseAnalysisDto>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new AbpException("无法解析案例分析JSON数据。");

        return CaseAnalysisDocxGenerator.Generate(caseAnalysis);
    }
}