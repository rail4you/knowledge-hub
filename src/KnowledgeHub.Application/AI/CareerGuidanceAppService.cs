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
public class CareerGuidanceAppService : KnowledgeHubAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CareerGuidanceAppService> _logger;
    private readonly IRepository<ResourcePageIndex, Guid> _pageIndexRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;

    private const string CareerGuidanceInstructions = @"你是一个专业的职业规划顾问。你需要根据提供的简历/个人文档内容，生成一份结构化的职业规划建议报告。

严格要求：
1. 必须输出有效的 JSON 格式
2. 不要输出 JSON 以外的任何内容（不要用 markdown 代码块包裹）
3. 分析内容必须基于提供的文档内容，不要编造
4. 建议要具体、可操作，结合文档中的实际信息

JSON 结构：
{
  ""title"": ""职业规划建议报告标题"",
  ""assessment"": {
    ""careerMatchScore"": 85,
    ""strengths"": [""优势1"", ""优势2""],
    ""areasForImprovement"": [""待提升1"", ""待提升2""],
    ""summary"": ""综合评估摘要(150字以内)""
  },
  ""recommendedPaths"": [
    {
      ""title"": ""职业方向名称"",
      ""description"": ""方向描述"",
      ""matchScore"": 90,
      ""requiredSkills"": [""技能1"", ""技能2""],
      ""salaryRange"": ""薪资范围"",
      ""growthPotential"": ""发展潜力描述""
    }
  ],
  ""skillGaps"": [
    {
      ""skill"": ""技能名"",
      ""currentLevel"": ""当前水平"",
      ""targetLevel"": ""目标水平"",
      ""priority"": ""高/中/低""
    }
  ],
  ""actionPlan"": [
    {
      ""id"": ""1"",
      ""title"": ""行动项标题"",
      ""description"": ""描述"",
      ""timeline"": ""时间线"",
      ""priority"": ""高/中/低""
    }
  ],
  ""nextSteps"": [""下一步1"", ""下一步2""]
}";

    public CareerGuidanceAppService(
        IConfiguration configuration,
        ILogger<CareerGuidanceAppService> logger,
        IRepository<ResourcePageIndex, Guid> pageIndexRepository,
        IRepository<Resource, Guid> resourceRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _pageIndexRepository = pageIndexRepository;
        _resourceRepository = resourceRepository;
    }

    public async Task GenerateStreamingAsync(CareerGuidanceGenerationInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
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
                Content = JsonSerializer.Serialize(new { error = "该资源尚未生成页面索引，无法生成职业规划建议。" }),
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

        var goalSection = !string.IsNullOrWhiteSpace(input.CareerGoal)
            ? $"\n## 职业目标\n{input.CareerGoal}"
            : "";

        var userPrompt = $@"请根据以下简历/个人文档内容，生成一份全面的职业规划建议报告。

## 文档信息
{docMeta}

## 文档结构
{docStructure}

## 文档内容摘要
{(docContent.Length > 8000 ? docContent[..8000] : docContent)}
{goalSection}

请生成职业规划建议JSON。";

        var openaiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        IChatClient chatClient = openaiClient.GetChatClient(model).AsIChatClient();

        var chatOptions = new ChatOptions
        {
            Instructions = CareerGuidanceInstructions,
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

    public byte[] ExportDocx(string careerGuidanceJson)
    {
        var cleanJson = careerGuidanceJson.Trim();
        if (cleanJson.StartsWith("```"))
        {
            var firstNewline = cleanJson.IndexOf('\n');
            if (firstNewline >= 0) cleanJson = cleanJson[(firstNewline + 1)..];
            if (cleanJson.EndsWith("```"))
            {
                cleanJson = cleanJson[..^3].TrimEnd();
            }
        }

        var careerGuidance = JsonSerializer.Deserialize<CareerGuidanceDto>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new AbpException("无法解析职业规划JSON数据。");

        return CareerGuidanceDocxGenerator.Generate(careerGuidance);
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
