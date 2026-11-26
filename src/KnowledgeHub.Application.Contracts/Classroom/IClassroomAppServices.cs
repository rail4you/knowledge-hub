using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Classroom.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Classroom;

/// <summary>课程讨论答疑。</summary>
public interface IDiscussionAppService : IApplicationService
{
    Task<PagedResultDto<DiscussionDto>> GetListAsync(GetDiscussionListInput input);
    Task<DiscussionDto> GetAsync(Guid id);
    Task<DiscussionDto> CreateAsync(CreateDiscussionInput input);
    Task<DiscussionReplyDto> ReplyAsync(CreateDiscussionReplyInput input);
    Task<DiscussionDto> SetResolvedAsync(Guid id, bool resolved);
    Task<DiscussionDto> SetPinnedAsync(Guid id, bool pinned);
    Task<DiscussionDto> AcceptReplyAsync(Guid id, Guid replyId);
    Task DeleteAsync(Guid id);
    Task DeleteReplyAsync(Guid replyId);
}

/// <summary>签到。</summary>
public interface ICheckInAppService : IApplicationService
{
    Task<PagedResultDto<CheckInSessionDto>> GetListAsync(Guid? courseId, int skipCount, int maxResultCount);
    Task<CheckInSessionDto> CreateAsync(CreateCheckInSessionInput input);
    Task<CheckInSessionDto> CloseAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<CheckInRosterDto> GetRosterAsync(Guid sessionId);
    Task<CheckInSessionDto> GetAsync(Guid id);

    // 学生侧
    Task<DoCheckInResultDto> DoCheckInAsync(DoCheckInInput input);
    Task<List<MyCheckInRecordDto>> GetMyRecordsAsync(Guid? courseId = null);
}

/// <summary>课堂练习（教师发起 → 学生短链作答 → 实时统计）。</summary>
public interface IClassroomPracticeAppService : IApplicationService
{
    Task<PagedResultDto<PracticeSessionDto>> GetListAsync(Guid? courseId, int skipCount, int maxResultCount);
    Task<PracticeSessionDto> CreateAsync(CreatePracticeSessionInput input);
    Task<PracticeSessionDto> CloseAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<PracticeStatsDto> GetStatsAsync(Guid id);
    Task<PracticeSessionDto> GetAsync(Guid id);

    // 学生侧（token 维度，需登录）
    Task<PracticePaperDto> GetPaperAsync(string token);

    /// <summary>学生侧：我所在课程里可参与的课堂练习场次（含刚结束的，便于回看结果）。</summary>
    Task<List<PracticeSessionDto>> GetMySessionsAsync(Guid? courseId = null);
    Task<PracticeAttemptResultDto> SubmitAsync(SubmitPracticeInput input);
}

/// <summary>小组任务。</summary>
public interface IGroupTaskAppService : IApplicationService
{
    Task<PagedResultDto<GroupTaskDto>> GetListAsync(Guid? courseId, int skipCount, int maxResultCount);
    Task<GroupTaskDetailDto> GetDetailAsync(Guid id);
    Task<GroupTaskDto> CreateAsync(CreateGroupTaskInput input);
    Task<GroupTaskDto> UpdateAsync(Guid id, CreateGroupTaskInput input);
    Task<GroupTaskDto> PublishAsync(Guid id);
    Task<GroupTaskDto> CloseAsync(Guid id);
    Task DeleteAsync(Guid id);

    /// <summary>手工分组（覆盖式）。</summary>
    Task<GroupTaskDetailDto> AssignGroupsAsync(AssignGroupsInput input);
    /// <summary>随机平均分组（覆盖式）。</summary>
    Task<GroupTaskDetailDto> AutoAssignGroupsAsync(Guid id, int? groupCount);

    Task<GroupTaskSubmissionDto> GradeAsync(GradeGroupTaskInput input);

    // 学生侧
    Task<GroupTaskDetailDto> GetMyTaskAsync(string token);

    /// <summary>学生侧：我所在小组的任务列表（Draft 不返回）。</summary>
    Task<List<GroupTaskDto>> GetMyTasksAsync(Guid? courseId = null);
    Task<GroupTaskSubmissionDto> SubmitAsync(SubmitGroupTaskInput input);
}

/// <summary>
/// 免登录短链（扫码）token 解析。
/// 匿名可调用，只回会话元信息，**绝不返回名单 / 成绩**。
/// </summary>
public interface IClassroomTokenAppService : IApplicationService
{
    Task<ClassroomTokenSessionDto> ResolveAsync(ResolveClassroomTokenInput input);
}
