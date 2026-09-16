using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
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
    public async Task<AgentMessageChunkDto> Chat(Guid assignmentId, [FromBody] SendAgentRunMessageDto input)
    {
        // 一次性返回完整回复（智能体在一次调用后整体给出结果），由前端保持 loading 指示。
        return await _agentRunAppService.SendMessageAsync(assignmentId, input);
    }
}
