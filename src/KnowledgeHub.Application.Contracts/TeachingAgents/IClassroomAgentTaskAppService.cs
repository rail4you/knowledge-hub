using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.TeachingAgents.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.TeachingAgents;

public interface IClassroomAgentTaskAppService : IApplicationService
{
    Task<TaskCreationOptionsDto> GetCreateOptionsAsync();
    Task<List<ExerciseOptionDto>> GetCourseExercisesAsync(Guid courseId);
    Task<ClassroomAgentTaskDto> CreateAsync(CreateClassroomAgentTaskDto input);
    Task<ClassroomAgentTaskDto> PublishAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<PagedResultDto<ClassroomAgentTaskDto>> GetTeacherTaskListAsync(PagedAndSortedResultRequestDto input);
    Task<PagedResultDto<StudentAgentTaskDto>> GetStudentTaskListAsync(PagedAndSortedResultRequestDto input);
    Task<ClassroomAgentTaskDetailDto> GetTaskDetailAsync(Guid id);

    /// <summary>
    /// 互动课堂课表：把「课堂智能体任务」按时间排进日/周/月视图。
    ///
    /// <para>每条任务产出两个时间点：assign（布置日 = 创建时间）与 due（截止时间，若设置了 DueTime）。</para>
    /// </summary>
    Task<ClassroomTimetableDto> GetTimetableAsync(ClassroomTimetableQueryDto input);
    Task<ClassroomAgentAssignmentDto> RespondToStudentHelpAsync(Guid assignmentId, TeacherRespondDto input);
}
