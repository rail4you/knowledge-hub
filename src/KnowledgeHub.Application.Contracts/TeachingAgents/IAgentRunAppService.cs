using System;
using System.Threading.Tasks;
using KnowledgeHub.TeachingAgents.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.TeachingAgents;

public interface IAgentRunAppService : IApplicationService
{
    Task<AgentRunDetailDto> CreateOrGetForAssignmentAsync(Guid assignmentId);
    Task<AgentRunDetailDto> GetDetailAsync(Guid runId);
    Task<ClassroomAgentAssignmentDto> SubmitAssignmentAsync(Guid assignmentId, SubmitAgentAssignmentDto input);
    Task<ClassroomAgentAssignmentDto> MarkNeedTeacherHelpAsync(Guid assignmentId, NeedTeacherHelpDto input);
}
