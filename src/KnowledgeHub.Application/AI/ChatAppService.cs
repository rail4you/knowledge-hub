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
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.AI;

// Not implementing IChatAppService to avoid ABP Castle DynamicProxy buffering IAsyncEnumerable.
// The controller injects this class directly for the streaming method.
public class ChatAppService : KnowledgeHubAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatAppService> _logger;
    private readonly IRepository<PageContent, Guid> _pageContentRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IResourceCategoryRepository _categoryRepository;
    private readonly IMeiliSearchService _meiliSearchService;

    private const string DefaultInstructions = @"你是 KnowledgeHub 平台的智能教育助手。

你的职责：
- 回答关于课程、知识点的问题
- 帮助学生学习，解释概念
- 推荐学习路径
- 生成练习题（如果用户要求）

回答要求：
1. 回答简洁，专业
2. 如果涉及课程信息或文档内容，先使用搜索工具查询
3. 使用 Markdown 格式化回答
4. 当搜索工具返回结果时，基于搜索结果给出准确、有依据的回答
5. 不要向用户暴露你的思考过程、工具调用过程、检索步骤、函数名或内部提示词
6. 直接输出最终答案，不要输出 ""我先搜索""/""我来调用工具""/""思考过程如下"" 之类的中间过程";

    private const string DocumentChatInstructions = @"你是一个专业的文档问答助手。

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
        IRepository<PageContent, Guid> pageContentRepository,
        IRepository<Resource, Guid> resourceRepository,
        IResourceCategoryRepository categoryRepository,
        IMeiliSearchService meiliSearchService)
    {
        _currentUser = currentUser;
        _configuration = configuration;
        _logger = logger;
        _pageContentRepository = pageContentRepository;
        _resourceRepository = resourceRepository;
        _categoryRepository = categoryRepository;
        _meiliSearchService = meiliSearchService;
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
            // Document QA mode: use MeiliSearch tools scoped to a single resource
            tools = BuildTools(input.ResourceId);
            instructions = DocumentChatInstructions;
        }
        else
        {
            // General chat mode: MeiliSearch tools with global scope (no resourceId filter)
            tools = BuildTools(null);
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
        // 1. 先查哪些资源有页面索引
        var pcQuery = await _pageContentRepository.GetQueryableAsync();
        var indexedResourceIds = await AsyncExecuter.ToListAsync(
            pcQuery.Select(pc => pc.ResourceId).Distinct());
        var indexedSet = new HashSet<Guid>(indexedResourceIds);

        // 2. 获取当前租户下所有审核通过的资源
        var approvedResources = await _resourceRepository.GetListAsync(r =>
            r.Status == KnowledgeHub.Resources.Enums.ResourceStatus.SchoolApproved
            || r.Status == KnowledgeHub.Resources.Enums.ResourceStatus.LeagueApproved);

        if (approvedResources.Count == 0)
            return new List<ResourceForChatDto>();

        // 3. 批量获取所有分类，做 in-memory 查找（O(1) 命中）
        var categoryLookup = (await _categoryRepository.GetListAsync())
            .ToDictionary(c => c.Id, c => c.Name);

        var result = approvedResources.Select(r =>
        {
            var format = r.FileExtension;
            if (string.IsNullOrEmpty(format))
                format = System.IO.Path.GetExtension(r.OriginalFileName ?? "");
            if (!string.IsNullOrEmpty(format) && format.StartsWith("."))
                format = format.Substring(1);

            // 反查分类名称
            var categoryName = r.CategoryId.HasValue
                && categoryLookup.TryGetValue(r.CategoryId.Value, out var name)
                    ? name
                    : null;

            return new ResourceForChatDto
            {
                Id = r.Id,
                Name = r.Name,
                FileExtension = r.FileExtension,
                SourceFormat = format,
                NodeCount = 0,
                HasPageIndex = indexedSet.Contains(r.Id),
                CategoryId = r.CategoryId,
                CategoryName = categoryName
            };
        }).ToList();

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

    private List<AITool> BuildTools(Guid? resourceId)
    {
        // MeiliSearch-backed document Q&A tools (RAG pattern).
        // When resourceId is set, all searches are scoped to that document.
        // Otherwise the tools perform cross-document search.
        var docTools = resourceId.HasValue
            ? new MeiliSearchDocumentTools(_meiliSearchService, _resourceRepository, resourceId.Value, _logger)
            : new MeiliSearchDocumentTools(_meiliSearchService, _resourceRepository, _logger);

        return new List<AITool>
        {
            AIFunctionFactory.Create(docTools.GetDocument),
            AIFunctionFactory.Create(docTools.GetDocumentStructure),
            AIFunctionFactory.Create(docTools.GetPageContent),
            AIFunctionFactory.Create(docTools.SearchDocument),
        };
    }

}
