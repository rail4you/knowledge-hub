using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI;
using KnowledgeHub.Application.AI.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

[Area("learning")]
[Route("api/learning/ai")]
public class AIController : AbpControllerBase
{
    private readonly IChatAppService _chatAppService;

    public AIController(IChatAppService chatAppService)
    {
        _chatAppService = chatAppService;
    }

    [HttpGet("resources")]
    public async Task<List<ResourceForChatDto>> GetResources()
    {
        return await _chatAppService.GetResourcesWithPageIndexAsync();
    }

    [HttpPost("chat")]
    [IgnoreAntiforgeryToken]
    public async Task Chat([FromBody] ChatInputDto input)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        await foreach (var chunk in _chatAppService.ChatStreamingAsync(input))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                var json = JsonSerializer.Serialize(chunk);
                await Response.WriteAsync($"data: {json}\n\n");
                await Response.Body.FlushAsync();
            }
            else if (chunk.IsComplete)
            {
                var completeJson = JsonSerializer.Serialize(chunk);
                await Response.WriteAsync($"data: {completeJson}\n\n");
                await Response.Body.FlushAsync();
                break;
            }
        }
    }
}
