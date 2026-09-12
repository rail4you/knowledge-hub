using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
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
public class CaseAnalysisAppService : KnowledgeHubAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CaseAnalysisAppService> _logger;
    private readonly IRepository<Resource, Guid> _resourceRepository;

    private const string CaseAnalysisInstructions = @"你是一个专业的案例分析助手。你需要根据提供的""文档摘要""和""分析关注点"",生成一份结构化的多维度案例分析报告。

严格要求：
1. 必须输出有效 JSON（不要用 markdown 代码块包裹,不要任何多余文字）
2. 分析内容必须基于""文档摘要"",可适度引申但不得编造与摘要无关的具体数据/事实
3. 分析要深入、有洞察力,结合摘要中的实际信息
4. 所有 JSON 字段值必须使用简体中文表达
5. 输出内容中禁止出现任何英文单词、英文缩写、拉丁字母、英文标题、英文项目符号或中英混写
6. 摘要中的英文术语、英文缩写、产品名、方法名、模型名、框架名、岗位名、部门名等,必须改写为纯中文表达;如果无法直译,也要使用中文释义,不能保留英文原文
7. 时间、数字、百分比、金额可以保留阿拉伯数字,但单位与说明必须使用中文
8. `severity` 字段只能填写 ""高""/""中""/""低""
9. 如果你发现自己将要输出任何英文字符,必须先改写成中文后再输出
10. 若 ""分析关注点"" 非空,必须在关键问题/解决方案/洞察/建议中重点体现该关注点

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
- keyInsights 与 recommendations 各不少于 3 条,具有可操作性
- 行业、时间范围、相关方必须从摘要中真实提取,不得凭空捏造";

    public CaseAnalysisAppService(
        IConfiguration configuration,
        ILogger<CaseAnalysisAppService> logger,
        IRepository<Resource, Guid> resourceRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _resourceRepository = resourceRepository;
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

        // 2. 摘要缺失校验
        if (string.IsNullOrWhiteSpace(resource.Summary))
        {
            await EmitErrorAsync(onChunk, threadId,
                "该资源尚未生成 AI 摘要,请先在资源详情页生成摘要后再使用案例分析功能。");
            return;
        }

        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-flash";

        // 3. 组合 User Prompt(以 Resource.Summary 为唯一案例依据)
        var userPrompt = $@"## 文档摘要(唯一案例依据)
{resource.Summary}

## 文档元信息
- 名称:{resource.Name}
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