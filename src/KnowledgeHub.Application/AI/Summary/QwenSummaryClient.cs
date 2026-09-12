using System;
using System.ClientModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Application.AI.Summary;

/// <summary>
/// 封装非流式 Qwen 调用，用于一次性生成 summary + keywords。
/// 参考 ChatAppService.cs 的 OpenAIClient 构造模式，但使用 GetResponseAsync 而非流式版本。
/// </summary>
public class QwenSummaryClient : ITransientDependency
{
    private readonly IConfiguration _configuration;

    public QwenSummaryClient(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>实际使用的模型（用量记录用，与 CompleteAsync 一致）。</summary>
    public string ResolvedModel =>
        _configuration["Qwen:SummaryModel"]
            ?? _configuration["Qwen:Model"]
            ?? "qwen-flash";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:SummaryModel"]
            ?? _configuration["Qwen:Model"]
            ?? "qwen-flash";

        IChatClient chatClient = await QwenClient.CreateChatClient(_configuration, model);

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        };

        var options = new ChatOptions
        {
            Temperature = 0.3f,
            MaxOutputTokens = 1024,
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(60));

        var response = await chatClient.GetResponseAsync(messages, options, cts.Token);
        return response.Text ?? string.Empty;
    }
}