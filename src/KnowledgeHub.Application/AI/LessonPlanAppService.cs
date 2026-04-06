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
public class LessonPlanAppService : KnowledgeHubAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LessonPlanAppService> _logger;
    private readonly IRepository<ResourcePageIndex, Guid> _pageIndexRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;

    private const string LessonPlanInstructions = @"你是一个专业的教案生成助手。你需要根据提供的文档内容和教学参数，生成一份结构化的教案。

严格要求：
1. 必须输出有效的 JSON 格式
2. 不要输出 JSON 以外的任何内容（不要用 markdown 代码块包裹）
3. 教案内容必须基于提供的文档内容，不要编造
4. 教学环节要合理分配时间，各环节时间总和应等于总课时

JSON 结构：
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
}";

    public LessonPlanAppService(
        IConfiguration configuration,
        ILogger<LessonPlanAppService> logger,
        IRepository<ResourcePageIndex, Guid> pageIndexRepository,
        IRepository<Resource, Guid> resourceRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _pageIndexRepository = pageIndexRepository;
        _resourceRepository = resourceRepository;
    }

    /// <summary>
    /// Stream the AI-generated lesson plan JSON back to the caller.
    /// </summary>
    public async Task GenerateStreamingAsync(LessonPlanGenerationInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = Guid.NewGuid().ToString();

        var apiKey = _configuration["Qwen:ApiKey"]
            ?? throw new AbpException("Qwen:ApiKey is not configured");
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-plus";

        // Fetch resource and page index
        var resource = await _resourceRepository.GetAsync(input.ResourceId);
        var pageIndexList = await _pageIndexRepository.GetListAsync(x => x.ResourceId == input.ResourceId);
        var latestPageIndex = pageIndexList.OrderByDescending(x => x.CreatedAt).FirstOrDefault();

        if (latestPageIndex == null)
        {
            await onChunk(new ChatMessageChunkDto
            {
                Content = JsonSerializer.Serialize(new { error = "该资源尚未生成页面索引，无法生成教案。" }),
                ThreadId = threadId,
                IsComplete = false
            });
            await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
            return;
        }

        // Build document context using DocumentChatTools
        var docTools = new DocumentChatTools(latestPageIndex.PageIndexJson, _logger);

        // Get full document structure
        var docMeta = docTools.GetDocument();
        var docStructure = docTools.GetDocumentStructure();

        // Get key content from the document (first ~30 pages or all if fewer)
        var maxPage = ExtractMaxPage(docMeta);
        var contentRange = maxPage > 0 ? $"1-{Math.Min(maxPage, 30)}" : "";
        var docContent = !string.IsNullOrEmpty(contentRange)
            ? docTools.GetPageContent(contentRange)
            : "";

        var userPrompt = $@"请根据以下文档内容和教学参数，生成一份完整的教案。

## 文档信息
{docMeta}

## 文档结构
{docStructure}

## 文档内容摘要
{(docContent.Length > 8000 ? docContent[..8000] : docContent)}

## 教学参数
- 课程主题：{input.Topic}
- 学科：{input.Subject ?? "通用"}
- 年级：{input.Grade ?? "通用"}
- 总课时：{input.Duration}分钟

请生成教案JSON。";

        var openaiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        IChatClient chatClient = openaiClient.GetChatClient(model).AsIChatClient();

        var chatOptions = new ChatOptions
        {
            Instructions = LessonPlanInstructions,
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
    /// Export a lesson plan JSON as a DOCX file.
    /// </summary>
    public byte[] ExportDocx(string lessonPlanJson)
    {
        var cleanJson = lessonPlanJson.Trim();
        // Strip markdown code block wrapper if present
        if (cleanJson.StartsWith("```"))
        {
            var firstNewline = cleanJson.IndexOf('\n');
            if (firstNewline >= 0) cleanJson = cleanJson[(firstNewline + 1)..];
            if (cleanJson.EndsWith("```"))
            {
                cleanJson = cleanJson[..^3].TrimEnd();
            }
        }

        var lessonPlan = JsonSerializer.Deserialize<LessonPlanDto>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new AbpException("无法解析教案JSON数据。");

        return LessonPlanDocxGenerator.Generate(lessonPlan);
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
