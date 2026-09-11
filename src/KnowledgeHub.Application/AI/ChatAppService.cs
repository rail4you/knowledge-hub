using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.AI;
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
using MEAIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using KnowledgeHubChatMessage = KnowledgeHub.AI.ChatMessage;

namespace KnowledgeHub.Application.AI;

// Not implementing IChatAppService to avoid ABP Castle DynamicProxy buffering IAsyncEnumerable.
// The controller injects this class directly for the streaming method.
public class ChatAppService : KnowledgeHubAppService
{
    private readonly ICurrentUser _currentUser;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatAppService> _logger;
    private readonly IRepository<PageContent, Guid> _pageContentRepository;
    private readonly IRepository<VideoIndexingJob, Guid> _videoIndexingJobRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IResourceCategoryRepository _categoryRepository;
    private readonly IMeiliSearchService _meiliSearchService;
    private readonly IRepository<ChatThread, Guid> _threadRepository;
    private readonly IRepository<KnowledgeHubChatMessage, Guid> _messageRepository;

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
5. 只输出给用户看的最终答案，不要输出思考过程、工具调用日志、折叠块或与答案无关的中间信息。

关于总结/概括类问题（""总结一下""、""概括""、""讲了什么""、""主要内容""）：
- 不要仅依赖 get_document 的 summary 字段——视频等资源的 summary 可能为空，已在 get_document 中通过 timeline_text/page_contents_preview 补齐；若仍为空，必须调用 search_document 获取正文。
- 对视频资源：调用 search_document（query 可用文档名或空，maxResults 10~20）按 time 索引返回的 start_time/end_time+event_description 按时间顺序组织成带时间轴的要点总结，形如 ""00:00:00-00:00:02：兔子躺在草地...""
- 对文档资源：调用 search_document 后基于 page_number/content 归纳要点。

关于文档结构 / 大纲 / 章节 / 目录类问题：
- 目前没有可用的章节结构数据，不要尝试调用相关工具，也不要编造目录。
- 直接调用 get_document 工具读取该文档的 summary 字段，把摘要中关于文档结构的描述作为答案回复给用户。
- 如果用户明确要求按页码定位具体章节，可结合 search_document 工具按关键词检索相关内容。
- 不要向用户透露任何内部技术细节（如 ""headings 为空""、""无 headings""、""结构未解析""、""索引缺失"" 等），用自然语言直接基于摘要回答即可。";

    private const int MaxToolCallRounds = 5;

    public ChatAppService(
        ICurrentUser currentUser,
        IConfiguration configuration,
        ILogger<ChatAppService> logger,
        IRepository<PageContent, Guid> pageContentRepository,
        IRepository<VideoIndexingJob, Guid> videoIndexingJobRepository,
        IRepository<Resource, Guid> resourceRepository,
        IResourceCategoryRepository categoryRepository,
        IMeiliSearchService meiliSearchService,
        IRepository<ChatThread, Guid> threadRepository,
        IRepository<KnowledgeHubChatMessage, Guid> messageRepository)
    {
        _currentUser = currentUser;
        _configuration = configuration;
        _logger = logger;
        _pageContentRepository = pageContentRepository;
        _videoIndexingJobRepository = videoIndexingJobRepository;
        _resourceRepository = resourceRepository;
        _categoryRepository = categoryRepository;
        _meiliSearchService = meiliSearchService;
        _threadRepository = threadRepository;
        _messageRepository = messageRepository;
    }

    // 仅供 SSE Controller 直接调用：Func 回调无法绑定为 HTTP 参数，
    // 必须对 Conventional Controller 隐藏，否则 /Abp/ServiceProxyScript 全站 500。
    [Volo.Abp.RemoteService(false)]
    public async Task ChatStreamingAsync(ChatInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var userId = _currentUser.Id ?? throw new AbpException("User not logged in");
        var threadIdStr = string.IsNullOrEmpty(input.ThreadId)
            ? Guid.NewGuid().ToString()
            : input.ThreadId;
        var threadGuid = Guid.Parse(threadIdStr);

        // Ensure thread exists
        var thread = await _threadRepository.FindAsync(threadGuid);
        if (thread == null)
        {
            var title = input.Message.Length > 50 ? input.Message[..50] + "…" : input.Message;
            thread = new ChatThread(threadGuid, userId, title, input.ResourceId);
            await _threadRepository.InsertAsync(thread);
        }

        // Save user message
        var userMsg = new KnowledgeHubChatMessage(Guid.NewGuid(), threadGuid, "user", input.Message);
        await _messageRepository.InsertAsync(userMsg);

        // Update thread title if this is the first user message
        if (thread.Messages.Count == 0)
        {
            var title = input.Message.Length > 50 ? input.Message[..50] + "…" : input.Message;
            thread.SetTitle(title);
        }

        var apiKey = _configuration["Qwen:ApiKey"]
            ?? throw new AbpException("Qwen:ApiKey is not configured");
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-plus";

        IChatClient chatClient = QwenClient.CreateChatClient(_configuration, model);
        chatClient = new FunctionInvokingChatClient(chatClient);

        List<AITool> tools;
        string instructions;

        if (input.ResourceId.HasValue)
        {
            tools = BuildTools(input.ResourceId);
            instructions = DocumentChatInstructions;
        }
        else
        {
            tools = BuildTools(null);
            instructions = DefaultInstructions;
        }

        var chatOptions = new ChatOptions
        {
            Instructions = instructions,
            Tools = tools,
        };

        var messages = new List<MEAIChatMessage>
        {
            new(ChatRole.User, input.Message)
        };

        var fullResponse = new StringBuilder();

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions, CancellationToken.None))
        {
            if (update.Text != null && update.Text.Length > 0)
            {
                fullResponse.Append(update.Text);
                await onChunk(new ChatMessageChunkDto
                {
                    Content = update.Text,
                    ThreadId = threadIdStr,
                    IsComplete = false
                });
            }
        }

        // Save assistant message
        var assistantContent = fullResponse.ToString();
        if (!string.IsNullOrWhiteSpace(assistantContent))
        {
            var assistantMsg = new KnowledgeHubChatMessage(Guid.NewGuid(), threadGuid, "assistant", assistantContent);
            await _messageRepository.InsertAsync(assistantMsg);
        }

        await onChunk(new ChatMessageChunkDto
        {
            Content = "",
            ThreadId = threadIdStr,
            IsComplete = true
        });
    }

    public async Task<List<ResourceForChatDto>> GetResourcesWithPageIndexAsync()
    {
        // 1. 先查哪些资源有页面索引（文档/PDF/PPT 等走文档索引）
        var pcQuery = await _pageContentRepository.GetQueryableAsync();
        var indexedResourceIds = await AsyncExecuter.ToListAsync(
            pcQuery.Select(pc => pc.ResourceId).Distinct());
        var indexedSet = new HashSet<Guid>(indexedResourceIds);

        // 1b. 视频资源走另一套索引：KhVideoIndexingJobs.Status = Completed。
        //     前端 chat 树只读 HasPageIndex 字段，所以把“视频已索引”也合并进同一集合。
        var vidQuery = await _videoIndexingJobRepository.GetQueryableAsync();
        var indexedVideoIds = await AsyncExecuter.ToListAsync(
            vidQuery.Where(v => v.Status == VideoIndexingJobStatus.Completed)
                    .Select(v => v.ResourceId).Distinct());
        foreach (var vid in indexedVideoIds)
        {
            indexedSet.Add(vid);
        }

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
                HasSummary = !string.IsNullOrWhiteSpace(r.Summary),
                CategoryId = r.CategoryId,
                CategoryName = categoryName
            };
        }).ToList();

        return result;
    }

    /// <summary>
    /// 获取当前用户的聊天线程列表，按最后更新时间倒序。
    /// </summary>
    public async Task<List<ChatThreadDto>> GetMyThreadsAsync()
    {
        var userId = _currentUser.Id ?? throw new AbpException("User not logged in");

        var threadQuery = await _threadRepository.GetQueryableAsync();
        var messageQuery = await _messageRepository.GetQueryableAsync();

        var threads = await AsyncExecuter.ToListAsync(
            threadQuery
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.LastModificationTime ?? t.CreationTime));

        if (threads.Count == 0)
            return new List<ChatThreadDto>();

        // Collect resource IDs for name lookup
        var resourceIds = threads
            .Where(t => t.ResourceId.HasValue)
            .Select(t => t.ResourceId!.Value)
            .Distinct()
            .ToList();

        var resourceNames = new Dictionary<Guid, string>();
        if (resourceIds.Count > 0)
        {
            var resourceQuery = await _resourceRepository.GetQueryableAsync();
            var resources = await AsyncExecuter.ToListAsync(
                resourceQuery.Where(r => resourceIds.Contains(r.Id)));
            foreach (var r in resources)
                resourceNames[r.Id] = r.Name;
        }

        var result = new List<ChatThreadDto>();
        foreach (var t in threads)
        {
            var msgCount = await AsyncExecuter.CountAsync(
                messageQuery.Where(m => m.ThreadId == t.Id));

            // Get last message preview
            var lastMsg = await AsyncExecuter.FirstOrDefaultAsync(
                messageQuery
                    .Where(m => m.ThreadId == t.Id)
                    .OrderByDescending(m => m.CreationTime));

            string? lastMsgPreview = null;
            if (lastMsg != null)
            {
                var preview = lastMsg.Content.Length > 60 ? lastMsg.Content[..60] + "…" : lastMsg.Content;
                lastMsgPreview = preview;
            }

            string? resourceName = null;
            if (t.ResourceId.HasValue)
                resourceNames.TryGetValue(t.ResourceId.Value, out resourceName);

            result.Add(new ChatThreadDto
            {
                Id = t.Id.ToString(),
                Title = t.Title,
                ResourceId = t.ResourceId,
                ResourceName = resourceName,
                MessageCount = (int)msgCount,
                LastMessage = lastMsgPreview,
                CreatedAt = t.CreationTime,
            });
        }

        return result;
    }

    /// <summary>
    /// 获取线程的完整消息列表。
    /// </summary>
    public async Task<ChatThreadDto> GetThreadAsync(string threadId)
    {
        var userId = _currentUser.Id ?? throw new AbpException("User not logged in");
        var threadGuid = Guid.Parse(threadId);

        var thread = await _threadRepository.FindAsync(threadGuid);
        if (thread == null || thread.UserId != userId)
            throw new AbpException("Thread not found");

        var messageQuery = await _messageRepository.GetQueryableAsync();
        var messages = await AsyncExecuter.ToListAsync(
            messageQuery
                .Where(m => m.ThreadId == threadGuid)
                .OrderBy(m => m.CreationTime));

        string? resourceName = null;
        if (thread.ResourceId.HasValue)
        {
            var resource = await _resourceRepository.FindAsync(thread.ResourceId.Value);
            resourceName = resource?.Name;
        }

        return new ChatThreadDto
        {
            Id = thread.Id.ToString(),
            Title = thread.Title,
            ResourceId = thread.ResourceId,
            ResourceName = resourceName,
            MessageCount = messages.Count,
            CreatedAt = thread.CreationTime,
            Messages = messages.Select(m => new ChatMessageDto
            {
                Id = m.Id.ToString(),
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreationTime,
            }).ToList()
        };
    }

    /// <summary>
    /// 删除单个线程及其所有消息。
    /// </summary>
    public async Task DeleteThreadAsync(Guid threadId)
    {
        var userId = _currentUser.Id ?? throw new AbpException("User not logged in");
        var thread = await _threadRepository.FindAsync(threadId);
        if (thread == null || thread.UserId != userId)
            throw new AbpException("Thread not found");

        var messageQuery = await _messageRepository.GetQueryableAsync();
        var messages = await AsyncExecuter.ToListAsync(
            messageQuery.Where(m => m.ThreadId == threadId));

        foreach (var msg in messages)
            await _messageRepository.DeleteAsync(msg);

        await _threadRepository.DeleteAsync(thread);
    }

    /// <summary>
    /// 清空当前用户所有线程。
    /// </summary>
    public async Task ClearAllThreadsAsync()
    {
        var userId = _currentUser.Id ?? throw new AbpException("User not logged in");

        var threadQuery = await _threadRepository.GetQueryableAsync();
        var threads = await AsyncExecuter.ToListAsync(
            threadQuery.Where(t => t.UserId == userId));

        var threadIds = threads.Select(t => t.Id).ToHashSet();

        var messageQuery = await _messageRepository.GetQueryableAsync();
        var messages = await AsyncExecuter.ToListAsync(
            messageQuery.Where(m => threadIds.Contains(m.ThreadId)));

        foreach (var msg in messages)
            await _messageRepository.DeleteAsync(msg);

        foreach (var t in threads)
            await _threadRepository.DeleteAsync(t);
    }

    public Task<ChatThreadDto> CreateThreadAsync()
    {
        throw new NotImplementedException("Use ChatStreamingAsync which auto-creates threads");
    }

    public Task SaveMessagesAsync(Guid threadId, string? title, Guid? resourceId, List<ChatMessageDto> messages)
    {
        throw new NotImplementedException("Messages are saved automatically during streaming");
    }

    private List<AITool> BuildTools(Guid? resourceId)
    {
        // MeiliSearch-backed document Q&A tools (RAG pattern).
        // When resourceId is set, all searches are scoped to that document.
        // Otherwise the tools perform cross-document search.
        var docTools = resourceId.HasValue
            ? new MeiliSearchDocumentTools(_meiliSearchService, _resourceRepository, resourceId.Value, _logger)
            : new MeiliSearchDocumentTools(_meiliSearchService, _resourceRepository, _logger);

        // 注：章节结构工具（GetDocumentStructure）暂时下线，因为它依赖的页面标题层级
        // 在很多文档里解析不出来（headings 为空数组），用户问大纲时体验差。
        // 后续要么改进索引解析，要么基于 AI 摘要做章节抽取。
        // 当前对大纲类问题改用 get_document 返回的 summary 字段回答。
        return new List<AITool>
        {
            AIFunctionFactory.Create(docTools.GetDocument),
            AIFunctionFactory.Create(docTools.GetPageContent),
            AIFunctionFactory.Create(docTools.SearchDocument),
        };
    }

}
