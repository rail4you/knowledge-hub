using System;
using System.Threading.Tasks;
using KnowledgeHub.TeachingAgents;
using KnowledgeHub.TeachingAgents.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

[Area("teaching")]
[Route("api/teaching-agent-assignments")]
public class TeachingAgentAssignmentController : AbpControllerBase
{
    private readonly IAgentRunAppService _agentRunAppService;

    public TeachingAgentAssignmentController(IAgentRunAppService agentRunAppService)
    {
        _agentRunAppService = agentRunAppService;
    }

    [HttpGet("{assignmentId:guid}/run")]
    public Task<AgentRunDetailDto> GetRunAsync(Guid assignmentId)
    {
        return _agentRunAppService.CreateOrGetForAssignmentAsync(assignmentId);
    }

    [HttpPost("{assignmentId:guid}/submit")]
    public Task<ClassroomAgentAssignmentDto> SubmitAsync(Guid assignmentId, [FromBody] SubmitAgentAssignmentDto input)
    {
        return _agentRunAppService.SubmitAssignmentAsync(assignmentId, input);
    }

    [HttpPost("{assignmentId:guid}/need-help")]
    public Task<ClassroomAgentAssignmentDto> MarkNeedTeacherHelpAsync(Guid assignmentId, [FromBody] NeedTeacherHelpDto input)
    {
        return _agentRunAppService.MarkNeedTeacherHelpAsync(assignmentId, input);
    }
}
