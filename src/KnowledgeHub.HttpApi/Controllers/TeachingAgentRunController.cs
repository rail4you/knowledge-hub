using System;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.TeachingAgents;
using KnowledgeHub.TeachingAgents.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;

namespace KnowledgeHub.Controllers;

[Area("teaching")]
[Route("api/teaching-agents/runs")]
[DisableAuditing]
public class TeachingAgentRunController : AbpControllerBase
{
    private readonly AgentRunAppService _agentRunAppService;

    public TeachingAgentRunController(AgentRunAppService agentRunAppService)
    {
        _agentRunAppService = agentRunAppService;
    }

    [HttpPost("{assignmentId:guid}/chat")]
    [Authorize(KnowledgeHubPermissions.TeachingAgents.Execute)]
    [IgnoreAntiforgeryToken]
    [Produces("text/event-stream")]
    public async Task<IActionResult> Chat(Guid assignmentId, [FromBody] SendAgentRunMessageDto input)
    {
        var httpContext = HttpContext;
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["Connection"] = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";
        await httpContext.Response.StartAsync();

        var chunk = await _agentRunAppService.SendMessageAsync(assignmentId, input);
        var json = JsonSerializer.Serialize(chunk, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await httpContext.Response.WriteAsync($"data: {json}\n\n");
        await httpContext.Response.Body.FlushAsync();
        await httpContext.Response.CompleteAsync();
        return new EmptyResult();
    }
}
