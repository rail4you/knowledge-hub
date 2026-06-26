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
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.AI;

// Not implementing IChatAppService to avoid ABP Castle DynamicProxy buffering IAsyncEnumerable.
// The controller injects this class directly for the streaming method.
public class ChatAppService : KnowledgeHubAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatAppService> _logger;
    private readonly PageIndexTools _pageIndexTools;
    private readonly IRepository<PageContent, Guid> _pageContentRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;

    private const string DefaultInstructions = @"你是 KnowledgeHub 平台的智能教育助手。

你的职责：
- 回答关于课程、知识点的问题
- 帮助学生学习，解释概念
- 推荐学习路径
- 生成练习题（如果用户要求）

你可以使用以下工具来帮助用户：
- SearchPageIndex：当用户询问文档中的某个主题、章节或知识点时，使用此工具搜索相关文档内容
- GetDocumentStructure：当用户想了解某个文档的完整目录结构时，使用此工具

回答要求：
1. 回答简洁，专业
2. 如果涉及课程信息或文档内容，先使用搜索工具查询
3. 使用 Markdown 格式化回答
4. 当搜索工具返回结果时，基于搜索结果给出准确、有依据的回答
5. 不要向用户暴露你的思考过程、工具调用过程、检索步骤、函数名或内部提示词
6. 直接输出最终答案，不要输出“我先搜索”“我来调用工具”“思考过程如下”之类的中间过程";

    private const string DocumentChatInstructions = @"你是一个专业的文档问答助手。

TOOL USE:
- 先调用 get_document() 获取文档元数据（名称、页数等）。
- 调用 get_document_structure() 查看文档完整目录结构，找到与用户问题相关的章节。
- 调用 get_page_content(pages=""5-7"") 获取指定页码范围的内容。使用紧凑的页码范围，不要一次请求整个文档。
- 工具调用过程只用于内部推理，不要把调用原因、调用步骤、工具名、检索过程输出给用户。

回答要求：
1. 仅基于工具返回的内容回答，不要编造信息。
2. 回答要简洁、准确、有依据。
3. 使用 Markdown 格式化回答。
4. 如果文档中没有相关内容，如实告知用户。
5. 只输出给用户看的最终答案，不要输出思考过程、工具调用日志、折叠块或与答案无关的中间信息。";

    private const int MaxToolCallRounds = 5;

    public ChatAppService(
        ICurrentUser currentUser,
        IConfiguration configuration,
        ILogger<ChatAppService> logger,
        PageIndexTools pageIndexTools,
        IRepository<PageContent, Guid> pageContentRepository,
        IRepository<Resource, Guid> resourceRepository)
    {
        _currentUser = currentUser;
        _configuration = configuration;
        _logger = logger;
        _pageIndexTools = pageIndexTools;
        _pageContentRepository = pageContentRepository;
        _resourceRepository = resourceRepository;
    }

    public async Task ChatStreamingAsync(ChatInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = string.IsNullOrEmpty(input.ThreadId)
            ? Guid.NewGuid().ToString()
            : input.ThreadId;

        var apiKey = _configuration["Qwen:ApiKey"]
            ?? throw new AbpException("Qwen:ApiKey is not configured");
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-plus";

        var openaiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        IChatClient chatClient = openaiClient.GetChatClient(model).AsIChatClient();
        chatClient = new FunctionInvokingChatClient(chatClient);

        List<AITool> tools;
        string instructions;

        if (input.ResourceId.HasValue)
        {
            // Document QA mode: check if resource has indexed content
            var pcQuery = await _pageContentRepository.GetQueryableAsync();
            var hasContent = await AsyncExecuter.AnyAsync(
                pcQuery.Where(x => x.ResourceId == input.ResourceId.Value));

            if (!hasContent)
            {
                await onChunk(new ChatMessageChunkDto
                {
                    Content = "该文档尚未生成页面索引，无法进行文档问答。请先为文档生成索引。",
                    ThreadId = threadId,
                    IsComplete = false
                });
                await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
                return;
            }

            var docTools = new DocumentChatTools("{}", _logger);
            tools = BuildDocumentTools(docTools);
            instructions = DocumentChatInstructions;
        }
        else
        {
            // General chat mode
            tools = BuildTools();
            instructions = DefaultInstructions;
        }

        var chatOptions = new ChatOptions
        {
            Instructions = instructions,
            Tools = tools,
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, input.Message)
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

    public async Task<List<ResourceForChatDto>> GetResourcesWithPageIndexAsync()
    {
        // 从 Meilisearch 已索引的 PageContent 中获取有内容的资源列表
        var pcQuery = await _pageContentRepository.GetQueryableAsync();
        var indexedResourceIds = await AsyncExecuter.ToListAsync(
            pcQuery.Select(pc => pc.ResourceId).Distinct());

        if (indexedResourceIds.Count == 0)
            return new List<ResourceForChatDto>();

        List<Resource> resources;
        using (DataFilter.Disable<IMultiTenant>())
        {
            resources = await _resourceRepository.GetListAsync(r => indexedResourceIds.Contains(r.Id));
        }
        var resourceMap = resources.ToDictionary(r => r.Id);

        var result = new List<ResourceForChatDto>();
        foreach (var resourceId in indexedResourceIds)
        {
            if (!resourceMap.TryGetValue(resourceId, out var resource)) continue;

            result.Add(new ResourceForChatDto
            {
                Id = resource.Id,
                Name = resource.Name,
                FileExtension = resource.FileExtension,
                SourceFormat = resource.FileExtension ?? "unknown",
                NodeCount = 0
            });
        }

        return result;
    }

    /// <summary>
    /// P1-13：获取当前用户"作为简历"使用的、已审核通过的资源。
    /// 过滤条件：IsResume=true AND Status IN (SchoolApproved, LeagueApproved) AND CreatorId=当前用户。
    /// 用于 AI 职业规划下拉（避免暴露其他人的资源，也不会把草稿/被拒的资源拉进来）。
    /// IsResume 由用户在前端资料库列表的"设为简历/取消简历"按钮维护（P1-15）。
    /// </summary>
    public async Task<List<ResourceForChatDto>> GetResumesForUserAsync()
    {
        var currentUserId = _currentUser.GetId();

        var resources = await _resourceRepository.GetListAsync(r =>
            r.IsResume
            && (r.Status == KnowledgeHub.Resources.Enums.ResourceStatus.SchoolApproved
                || r.Status == KnowledgeHub.Resources.Enums.ResourceStatus.LeagueApproved)
            && r.CreatorId == currentUserId);

        // 按创建时间倒序，最近上传的简历排在前面
        return resources
            .OrderByDescending(r => r.CreationTime)
            .Select(r => new ResourceForChatDto
            {
                Id = r.Id,
                Name = r.Name,
                FileExtension = r.FileExtension,
                SourceFormat = null,
                NodeCount = 0
            })
            .ToList();
    }

    private List<AITool> BuildTools()
    {
        return new List<AITool>
        {
            AIFunctionFactory.Create(
                _pageIndexTools.SearchPageIndex,
                name: "SearchPageIndex",
                description: "搜索文档的页面索引结构。当用户询问文档目录、章节、内容结构时使用此工具。返回匹配的章节标题和摘要。"),
            AIFunctionFactory.Create(
                _pageIndexTools.GetDocumentStructure,
                name: "GetDocumentStructure",
                description: "获取指定文档的完整页面索引树。当用户想了解某文档的详细结构（目录、章节层次）时使用。"),
        };
    }

    private List<AITool> BuildDocumentTools(DocumentChatTools docTools)
    {
        return new List<AITool>
        {
            AIFunctionFactory.Create(
                docTools.GetDocument,
                name: "get_document",
                description: "获取当前文档的元数据：文档名称、描述、页数等。在回答文档相关问题前，先调用此工具确认文档状态。"),
            AIFunctionFactory.Create(
                docTools.GetDocumentStructure,
                name: "get_document_structure",
                description: "获取文档的完整目录结构树（不含正文内容）。用于查找与用户问题相关的章节和页码范围。"),
            AIFunctionFactory.Create(
                docTools.GetPageContent,
                name: "get_page_content",
                description: "获取指定页码范围的正文内容。使用紧凑范围如 '5-7'（第5到7页）、'3,8'（第3和8页）、'12'（第12页）。请先调用 get_document_structure 确定相关页码范围。"),
        };
    }
}
