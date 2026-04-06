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
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.AI;

public class ChatAppService : KnowledgeHubAppService, IChatAppService, ITransientDependency
{
    private readonly ICurrentUser _currentUser;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatAppService> _logger;
    private readonly PageIndexTools _pageIndexTools;

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
4. 当搜索工具返回结果时，基于搜索结果给出准确、有依据的回答";

    private const int MaxToolCallRounds = 5;

    public ChatAppService(
        ICurrentUser currentUser,
        IConfiguration configuration,
        ILogger<ChatAppService> logger,
        PageIndexTools pageIndexTools)
    {
        _currentUser = currentUser;
        _configuration = configuration;
        _logger = logger;
        _pageIndexTools = pageIndexTools;
    }

    public Task<ChatThreadDto> CreateThreadAsync()
    {
        return Task.FromResult(new ChatThreadDto
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.Now,
            Messages = new List<ChatMessageDto>()
        });
    }

    public async IAsyncEnumerable<ChatMessageChunkDto> ChatStreamingAsync(ChatInputDto input)
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

        // Use FunctionInvokingChatClient to automatically handle tool calls
        chatClient = new FunctionInvokingChatClient(chatClient);

        var tools = BuildTools();

        var chatOptions = new ChatOptions
        {
            Instructions = DefaultInstructions,
            Tools = tools,
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, input.Message)
        };

        // Streaming with automatic tool invocation via FunctionInvokingChatClient
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions, CancellationToken.None))
        {
            if (update.Text != null && update.Text.Length > 0)
            {
                yield return new ChatMessageChunkDto
                {
                    Content = update.Text,
                    ThreadId = threadId,
                    IsComplete = false
                };
            }
        }

        yield return new ChatMessageChunkDto
        {
            Content = "",
            ThreadId = threadId,
            IsComplete = true
        };
    }

    private List<AITool> BuildTools()
    {
        return new List<AITool>
        {
            AIFunctionFactory.Create(
                _pageIndexTools.SearchPageIndex,
                name: "SearchPageIndex",
                description: "搜索文档的页面索引结构。当用户询问文档目录、章节、内容结构时使用此工具。"),
            AIFunctionFactory.Create(
                _pageIndexTools.GetDocumentStructure,
                name: "GetDocumentStructure",
                description: "获取指定文档的完整页面索引树。当用户想了解某文档的详细结构（目录、章节层次）时使用。"),
        };
    }

    public Task<ChatThreadDto> GetThreadAsync(string threadId)
    {
        if (!Guid.TryParse(threadId, out _))
        {
            throw new UserFriendlyException("Invalid thread ID");
        }

        return Task.FromResult(new ChatThreadDto
        {
            Id = threadId,
            CreatedAt = DateTime.Now,
            Messages = new List<ChatMessageDto>()
        });
    }

    public Task<List<ChatThreadDto>> GetMyThreadsAsync()
    {
        return Task.FromResult(new List<ChatThreadDto>());
    }
}
