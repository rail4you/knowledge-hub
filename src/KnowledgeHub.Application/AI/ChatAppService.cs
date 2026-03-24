using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.AI;

public class ChatAppService : KnowledgeHubAppService, IChatAppService, ITransientDependency
{
    private readonly ICurrentUser _currentUser;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatAppService> _logger;
    
    private const string DefaultInstructions = @"你是 KnowledgeHub 平台的智能教育助手。

你的职责：
- 回答关于课程、知识点的问题
- 帮助学生学习，解释概念
- 推荐学习路径
- 生成练习题（如果用户要求）

回答要求：
1. 回答简洁，专业
2. 如果涉及课程信息，先查询课程数据
3. 使用 Markdown 格式化回答";

    public ChatAppService(
        ICurrentUser currentUser,
        IConfiguration configuration,
        ILogger<ChatAppService> logger)
    {
        _currentUser = currentUser;
        _configuration = configuration;
        _logger = logger;
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
        
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
        
        AIAgent agent = client
            .GetChatClient(model)
            .AsAIAgent(
                instructions: DefaultInstructions,
                name: "KgEduAssistant");
        
        var fullContent = new StringBuilder();
        
        await foreach (var update in agent.RunStreamingAsync(input.Message, cancellationToken: CancellationToken.None))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                fullContent.Append(update.Text);
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
