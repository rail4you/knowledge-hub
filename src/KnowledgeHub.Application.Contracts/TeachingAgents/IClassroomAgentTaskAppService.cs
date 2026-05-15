using System;
using System.Threading.Tasks;
using KnowledgeHub.TeachingAgents.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.TeachingAgents;

public interface IClassroomAgentTaskAppService : IApplicationService
{
    Task<TaskCreationOptionsDto> GetCreateOptionsAsync();
    Task<ClassroomAgentTaskDto> CreateAsync(CreateClassroomAgentTaskDto input);
    Task<ClassroomAgentTaskDto> PublishAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<PagedResultDto<ClassroomAgentTaskDto>> GetTeacherTaskListAsync(PagedAndSortedResultRequestDto input);
    Task<PagedResultDto<StudentAgentTaskDto>> GetStudentTaskListAsync(PagedAndSortedResultRequestDto input);
    Task<ClassroomAgentTaskDetailDto> GetTaskDetailAsync(Guid id);
}
