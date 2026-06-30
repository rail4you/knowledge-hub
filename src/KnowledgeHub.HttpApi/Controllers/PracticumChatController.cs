using System;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using KnowledgeHub.Practicums;
using KnowledgeHub.Practicums.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;
using Volo.Abp.Users;

namespace KnowledgeHub.Controllers;

[Area("learning")]
[Route("api/learning/practicum-chat")]
[DisableAuditing]
public class PracticumChatController : AbpControllerBase
{
    private readonly PracticumChatAppService _chatAppService;
    private readonly PracticumChatConnectionManager _connectionManager;
    private readonly ILogger<PracticumChatController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PracticumChatController(
        PracticumChatAppService chatAppService,
        PracticumChatConnectionManager connectionManager,
        ILogger<PracticumChatController> logger)
    {
        _chatAppService = chatAppService;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// SSE stream endpoint. Client connects and receives new messages in real time.
    /// </summary>
    [HttpGet("stream/{projectId:guid}")]
    [IgnoreAntiforgeryToken]
    public async Task Stream(Guid projectId)
    {
        var httpContext = HttpContext;
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["Connection"] = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        try
        {
            await httpContext.Response.StartAsync();
        }
        catch
        {
            // Client already disconnected during setup
            return;
        }

        // Send initial connection event with connection count
        var connectedCount = _connectionManager.GetConnectionCount(projectId);
        var initJson = JsonSerializer.Serialize(
            new { type = "connected", connectedCount = connectedCount + 1 },
            JsonOptions);
        try
        {
            await httpContext.Response.WriteAsync($"data: {initJson}\n\n");
            await httpContext.Response.Body.FlushAsync();
        }
        catch { return; }

        var channel = Channel.CreateUnbounded<PracticumChatMessageDto>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        using var subscription = _connectionManager.Subscribe(projectId, channel);
        var reader = channel.Reader;

        try
        {
            while (await reader.WaitToReadAsync(HttpContext.RequestAborted))
            {
                while (reader.TryRead(out var message))
                {
                    var json = JsonSerializer.Serialize(message, JsonOptions);
                    await httpContext.Response.WriteAsync($"data: {json}\n\n");
                }
                await httpContext.Response.Body.FlushAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — normal
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSE stream error for project {ProjectId}", projectId);
        }
    }

    /// <summary>
    /// Send a chat message. If message starts with @AgentName, triggers AI response.
    /// </summary>
    [HttpPost("send")]
    [IgnoreAntiforgeryToken]
    public async Task<PracticumChatMessageDto> Send([FromBody] SendPracticumChatMessageDto input)
    {
        return await _chatAppService.SendAsync(input);
    }

    /// <summary>
    /// Get chat message history (cursor pagination, newest first).
    /// </summary>
    [HttpGet("messages/{projectId:guid}")]
    public async Task<IActionResult> GetMessages(Guid projectId, [FromQuery] Guid? beforeId, [FromQuery] int maxCount = 25)
    {
        var result = await _chatAppService.GetMessagesAsync(new GetPracticumChatMessagesDto
        {
            ProjectId = projectId,
            BeforeId = beforeId,
            MaxResultCount = maxCount
        });
        return new JsonResult(result);
    }
}
