using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Application.AI.Tools;
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
public class CaseAnalysisAppService : KnowledgeHubAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CaseAnalysisAppService> _logger;
    private readonly IRepository<ResourcePageIndex, Guid> _pageIndexRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;

    private const string CaseAnalysisInstructions = @"你是一个专业的案例分析助手。你需要根据提供的文档内容，生成一份结构化的多维度案例分析报告。

严格要求：
1. 必须输出有效的 JSON 格式
2. 不要输出 JSON 以外的任何内容（不要用 markdown 代码块包裹）
3. 分析内容必须基于提供的文档内容，不要编造
4. 分析要深入、有洞察力，结合文档中的实际信息
5. 所有 JSON 字段值必须使用简体中文表达
6. 输出内容中禁止出现任何英文单词、英文缩写、拉丁字母、英文标题、英文项目符号或中英混写
7. 文档中的英文术语、英文缩写、产品名、方法名、模型名、框架名、岗位名、部门名等，必须改写为纯中文表达；如果无法直译，也要使用中文释义，不能保留英文原文
8. 时间、数字、百分比、金额可以保留阿拉伯数字，但单位与说明必须使用中文
9. `severity` 字段只能填写“高”“中”“低”
10. 如果你发现自己将要输出任何英文字符，必须先改写成中文后再输出

JSON 结构：
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
}";

    public CaseAnalysisAppService(
        IConfiguration configuration,
        ILogger<CaseAnalysisAppService> logger,
        IRepository<ResourcePageIndex, Guid> pageIndexRepository,
        IRepository<Resource, Guid> resourceRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _pageIndexRepository = pageIndexRepository;
        _resourceRepository = resourceRepository;
    }

    public async Task GenerateStreamingAsync(CaseAnalysisGenerationInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = Guid.NewGuid().ToString();

        var apiKey = _configuration["Qwen:ApiKey"]
            ?? throw new AbpException("Qwen:ApiKey is not configured");
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-plus";

        var resource = await _resourceRepository.GetAsync(input.ResourceId);
        var pageIndexList = await _pageIndexRepository.GetListAsync(x => x.ResourceId == input.ResourceId);
        var latestPageIndex = pageIndexList.OrderByDescending(x => x.CreatedAt).FirstOrDefault();

        if (latestPageIndex == null)
        {
            await onChunk(new ChatMessageChunkDto
            {
                Content = JsonSerializer.Serialize(new { error = "该资源尚未生成页面索引，无法生成案例分析。" }),
                ThreadId = threadId,
                IsComplete = false
            });
            await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
            return;
        }

        var docTools = new DocumentChatTools(latestPageIndex.PageIndexJson, _logger);

        var docMeta = docTools.GetDocument();
        var docStructure = docTools.GetDocumentStructure();

        var maxPage = ExtractMaxPage(docMeta);
        var contentRange = maxPage > 0 ? $"1-{Math.Min(maxPage, 30)}" : "";
        var docContent = !string.IsNullOrEmpty(contentRange)
            ? docTools.GetPageContent(contentRange)
            : "";

        var focusAreaSection = !string.IsNullOrWhiteSpace(input.FocusArea)
            ? $"\n## 分析关注点\n{input.FocusArea}"
            : "";

        var userPrompt = $@"请根据以下文档内容，生成一份全面的案例分析报告。

输出约束：
- 仅输出一个合法 JSON 对象
- JSON 的字段名按指定结构保持不变
- JSON 的所有字段值必须为简体中文
- 不允许出现任何英文单词、英文缩写、拉丁字母或中英混写
- 若原文包含英文概念，必须改写为中文释义
- `severity` 只能使用：高、中、低

## 文档信息
{docMeta}

## 文档结构
{docStructure}

## 文档内容摘要
{(docContent.Length > 8000 ? docContent[..8000] : docContent)}
{focusAreaSection}

请生成案例分析JSON。";

        var openaiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        IChatClient chatClient = openaiClient.GetChatClient(model).AsIChatClient();

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

    private static int ExtractMaxPage(string docMetaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(docMetaJson);
            if (doc.RootElement.TryGetProperty("page_count", out var pc))
            {
                return pc.GetInt32();
            }
        }
        catch { }
        return 0;
    }
}
