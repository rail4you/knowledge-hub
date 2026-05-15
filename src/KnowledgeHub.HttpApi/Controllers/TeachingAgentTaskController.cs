using System;
using System.Threading.Tasks;
using KnowledgeHub.TeachingAgents;
using KnowledgeHub.TeachingAgents.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

[Area("teaching")]
[Route("api/teaching-agent-tasks")]
public class TeachingAgentTaskController : AbpControllerBase
{
    private readonly IClassroomAgentTaskAppService _classroomAgentTaskAppService;

    public TeachingAgentTaskController(IClassroomAgentTaskAppService classroomAgentTaskAppService)
    {
        _classroomAgentTaskAppService = classroomAgentTaskAppService;
    }

    [HttpGet("options")]
    public Task<TaskCreationOptionsDto> GetCreateOptionsAsync()
    {
        return _classroomAgentTaskAppService.GetCreateOptionsAsync();
    }

    [HttpGet("teacher")]
    public Task<PagedResultDto<ClassroomAgentTaskDto>> GetTeacherTaskListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _classroomAgentTaskAppService.GetTeacherTaskListAsync(input);
    }

    [HttpGet("student")]
    public Task<PagedResultDto<StudentAgentTaskDto>> GetStudentTaskListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _classroomAgentTaskAppService.GetStudentTaskListAsync(input);
    }

    [HttpGet("{id:guid}")]
    public Task<ClassroomAgentTaskDetailDto> GetAsync(Guid id)
    {
        return _classroomAgentTaskAppService.GetTaskDetailAsync(id);
    }

    [HttpPost]
    public Task<ClassroomAgentTaskDto> CreateAsync([FromBody] CreateClassroomAgentTaskDto input)
    {
        return _classroomAgentTaskAppService.CreateAsync(input);
    }

    [HttpPost("{id:guid}/publish")]
    public Task<ClassroomAgentTaskDto> PublishAsync(Guid id)
    {
        return _classroomAgentTaskAppService.PublishAsync(id);
    }

    [HttpDelete("{id:guid}")]
    public Task DeleteAsync(Guid id)
    {
        return _classroomAgentTaskAppService.DeleteAsync(id);
    }
}
