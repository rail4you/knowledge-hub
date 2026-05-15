using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.TeachingAgents.Dtos;
using KnowledgeHub.TeachingAgents.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace KnowledgeHub.TeachingAgents;

[Authorize(KnowledgeHubPermissions.TeachingAgents.Default)]
public class AgentRunAppService : KnowledgeHubAppService, IAgentRunAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<AgentRun, Guid> _runRepository;
    private readonly IRepository<AgentRunMessage, Guid> _messageRepository;
    private readonly IRepository<ClassroomAgentAssignment, Guid> _assignmentRepository;
    private readonly IRepository<ClassroomAgentTask, Guid> _taskRepository;
    private readonly IRepository<TeachingAgent, Guid> _teachingAgentRepository;
    private readonly IRepository<TeachingAgentVersion, Guid> _versionRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ITeachingAgentRuntimeClient _runtimeClient;

    public AgentRunAppService(
        IRepository<AgentRun, Guid> runRepository,
        IRepository<AgentRunMessage, Guid> messageRepository,
        IRepository<ClassroomAgentAssignment, Guid> assignmentRepository,
        IRepository<ClassroomAgentTask, Guid> taskRepository,
        IRepository<TeachingAgent, Guid> teachingAgentRepository,
        IRepository<TeachingAgentVersion, Guid> versionRepository,
        IRepository<IdentityUser, Guid> userRepository,
        ITeachingAgentRuntimeClient runtimeClient)
    {
        _runRepository = runRepository;
        _messageRepository = messageRepository;
        _assignmentRepository = assignmentRepository;
        _taskRepository = taskRepository;
        _teachingAgentRepository = teachingAgentRepository;
        _versionRepository = versionRepository;
        _userRepository = userRepository;
        _runtimeClient = runtimeClient;
    }

    public async Task<AgentRunDetailDto> CreateOrGetForAssignmentAsync(Guid assignmentId)
    {
        var assignment = await _assignmentRepository.GetAsync(assignmentId);
        var task = await _taskRepository.GetAsync(assignment.ClassroomAgentTaskId);
        await EnsureCanAccessAssignmentAsync(task, assignment);

        var run = await GetOrCreateRunEntityAsync(assignment);
        return await BuildRunDetailAsync(task, assignment, run);
    }

    public async Task<AgentRunDetailDto> GetDetailAsync(Guid runId)
    {
        var run = await _runRepository.GetAsync(runId);
        var assignment = await _assignmentRepository.GetAsync(run.ClassroomAgentAssignmentId);
        var task = await _taskRepository.GetAsync(assignment.ClassroomAgentTaskId);
        await EnsureCanAccessAssignmentAsync(task, assignment);

        return await BuildRunDetailAsync(task, assignment, run);
    }

    public async Task<ClassroomAgentAssignmentDto> SubmitAssignmentAsync(Guid assignmentId, SubmitAgentAssignmentDto input)
    {
        var assignment = await _assignmentRepository.GetAsync(assignmentId);
        var task = await _taskRepository.GetAsync(assignment.ClassroomAgentTaskId);
        await EnsureCanAccessAssignmentAsync(task, assignment);

        assignment.Status = ClassroomAgentAssignmentStatus.Submitted;
        assignment.SubmissionSummary = input.Summary;
        assignment.CompletedAt = Clock.Now;
        assignment.LastActiveAt = Clock.Now;
        await _assignmentRepository.UpdateAsync(assignment, autoSave: true);

        return await MapAssignmentAsync(assignment);
    }

    public async Task<ClassroomAgentAssignmentDto> MarkNeedTeacherHelpAsync(Guid assignmentId, NeedTeacherHelpDto input)
    {
        var assignment = await _assignmentRepository.GetAsync(assignmentId);
        var task = await _taskRepository.GetAsync(assignment.ClassroomAgentTaskId);
        await EnsureCanAccessAssignmentAsync(task, assignment);

        assignment.Status = ClassroomAgentAssignmentStatus.NeedsTeacherHelp;
        assignment.HelpReason = input.Reason;
        assignment.LastActiveAt = Clock.Now;
        await _assignmentRepository.UpdateAsync(assignment, autoSave: true);

        return await MapAssignmentAsync(assignment);
    }

    public async Task<AgentMessageChunkDto> SendMessageAsync(Guid assignmentId, SendAgentRunMessageDto input)
    {
        var assignment = await _assignmentRepository.GetAsync(assignmentId);
        var task = await _taskRepository.GetAsync(assignment.ClassroomAgentTaskId);
        await EnsureCanAccessAssignmentAsync(task, assignment);

        var run = await GetOrCreateRunEntityAsync(assignment);
        assignment.Status = ClassroomAgentAssignmentStatus.InProgress;
        assignment.StartedAt ??= Clock.Now;
        assignment.LastActiveAt = Clock.Now;
        await _assignmentRepository.UpdateAsync(assignment, autoSave: true);

        await CreateMessageAsync(run.Id, "user", input.Message, "[]");

        try
        {
            run.RuntimeStatus = AgentRunStatus.InProgress;
            run.LastError = null;
            await _runRepository.UpdateAsync(run, autoSave: true);

            var runtimeRequest = await BuildRuntimeRequestAsync(task, assignment, run);
            var runtimeResponse = await _runtimeClient.GenerateReplyAsync(runtimeRequest);
            var toolCallsJson = JsonSerializer.Serialize(runtimeResponse.ToolCalls, JsonOptions);

            await CreateMessageAsync(run.Id, "assistant", runtimeResponse.Content, toolCallsJson);
            run.RuntimeStatus = AgentRunStatus.Completed;
            run.EndedAt = Clock.Now;
            await _runRepository.UpdateAsync(run, autoSave: true);

            return new AgentMessageChunkDto
            {
                Content = runtimeResponse.Content,
                ThreadId = run.ThreadId,
                IsComplete = true
            };
        }
        catch (Exception ex)
        {
            run.RuntimeStatus = AgentRunStatus.Failed;
            run.LastError = ex.Message;
            run.EndedAt = Clock.Now;
            await _runRepository.UpdateAsync(run, autoSave: true);
            throw;
        }
    }

    private async Task EnsureCanAccessAssignmentAsync(ClassroomAgentTask task, ClassroomAgentAssignment assignment)
    {
        var canReview = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.TeachingAgents.Review);
        if (canReview || task.CreatorUserId == CurrentUser.Id)
        {
            return;
        }

        if (assignment.StudentId == CurrentUser.Id && task.PublishStatus == ClassroomAgentTaskPublishStatus.Published)
        {
            return;
        }

        throw new AbpAuthorizationException("You are not allowed to access this assignment.");
    }

    private async Task<AgentRun> GetOrCreateRunEntityAsync(ClassroomAgentAssignment assignment)
    {
        var query = await _runRepository.GetQueryableAsync();
        var existingRun = await query
            .Where(x => x.ClassroomAgentAssignmentId == assignment.Id)
            .OrderByDescending(x => x.CreationTime)
            .FirstOrDefaultAsync();

        if (existingRun != null)
        {
            return existingRun;
        }

        var run = new AgentRun(
            GuidGenerator.Create(),
            assignment.Id,
            GuidGenerator.Create().ToString("N"))
        {
            TenantId = CurrentTenant.Id,
            RuntimeStatus = AgentRunStatus.Pending,
            StartedAt = Clock.Now
        };

        await _runRepository.InsertAsync(run, autoSave: true);
        return run;
    }

    private async Task CreateMessageAsync(Guid runId, string role, string content, string toolCallsJson)
    {
        await _messageRepository.InsertAsync(new AgentRunMessage(
            GuidGenerator.Create(),
            runId,
            role,
            content)
        {
            TenantId = CurrentTenant.Id,
            ToolCallsJson = toolCallsJson
        }, autoSave: true);
    }

    private async Task<TeachingAgentRuntimeRequest> BuildRuntimeRequestAsync(
        ClassroomAgentTask task,
        ClassroomAgentAssignment assignment,
        AgentRun run)
    {
        var version = await _versionRepository.GetAsync(task.TeachingAgentVersionId);
        var history = await GetMessagesAsync(run.Id);

        return new TeachingAgentRuntimeRequest
        {
            ModelId = version.ModelId,
            Temperature = version.Temperature,
            SystemPrompt = version.SystemPrompt,
            WelcomeMessage = version.WelcomeMessage,
            TaskTitle = task.Title,
            TaskPrompt = task.TaskPrompt,
            TargetSnapshot = DeserializeSnapshot(task.TargetSnapshotJson) ?? new TaskTargetSnapshotDto(),
            Assignment = await MapAssignmentAsync(assignment),
            Skills = DeserializeSkills(version.SkillsJson),
            History = history.Select(x => new TeachingAgentRuntimeMessage
            {
                Role = x.Role,
                Content = x.Content
            }).ToList()
        };
    }

    private async Task<AgentRunDetailDto> BuildRunDetailAsync(
        ClassroomAgentTask task,
        ClassroomAgentAssignment assignment,
        AgentRun run)
    {
        var messages = await GetMessagesAsync(run.Id);
        var userNames = await GetUserNamesAsync(new List<Guid> { assignment.StudentId });
        var version = await _versionRepository.GetAsync(task.TeachingAgentVersionId);
        var agent = await _teachingAgentRepository.GetAsync(task.TeachingAgentId);

        var taskDto = new ClassroomAgentTaskDetailDto
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            TeachingAgentId = task.TeachingAgentId,
            TeachingAgentVersionId = task.TeachingAgentVersionId,
            TeachingAgentName = agent.Name,
            TeachingAgentVersionNumber = version.VersionNumber,
            TaskPrompt = task.TaskPrompt,
            TargetType = task.TargetType,
            TargetId = task.TargetId,
            TargetSnapshot = DeserializeSnapshot(task.TargetSnapshotJson),
            DueTime = task.DueTime,
            PublishStatus = task.PublishStatus,
            AssignmentCount = 1,
            CompletedCount = assignment.Status == ClassroomAgentAssignmentStatus.Submitted ? 1 : 0,
            NeedsHelpCount = assignment.Status == ClassroomAgentAssignmentStatus.NeedsTeacherHelp ? 1 : 0,
            Assignments = new List<ClassroomAgentAssignmentDto>
            {
                new ClassroomAgentAssignmentDto
                {
                    Id = assignment.Id,
                    ClassroomAgentTaskId = assignment.ClassroomAgentTaskId,
                    StudentId = assignment.StudentId,
                    StudentName = userNames.GetValueOrDefault(assignment.StudentId, string.Empty),
                    Status = assignment.Status,
                    StartedAt = assignment.StartedAt,
                    CompletedAt = assignment.CompletedAt,
                    LastActiveAt = assignment.LastActiveAt,
                    SubmissionSummary = assignment.SubmissionSummary,
                    HelpReason = assignment.HelpReason,
                    CreationTime = assignment.CreationTime,
                    CreatorId = assignment.CreatorId,
                    LastModificationTime = assignment.LastModificationTime,
                    LastModifierId = assignment.LastModifierId
                }
            },
            CreationTime = task.CreationTime,
            CreatorId = task.CreatorId,
            LastModificationTime = task.LastModificationTime,
            LastModifierId = task.LastModifierId
        };

        return new AgentRunDetailDto
        {
            Task = taskDto,
            Assignment = taskDto.Assignments[0],
            Run = new AgentRunDto
            {
                Id = run.Id,
                ClassroomAgentAssignmentId = run.ClassroomAgentAssignmentId,
                ThreadId = run.ThreadId,
                RuntimeStatus = run.RuntimeStatus,
                StartedAt = run.StartedAt,
                EndedAt = run.EndedAt,
                LastError = run.LastError,
                CreationTime = run.CreationTime,
                CreatorId = run.CreatorId,
                LastModificationTime = run.LastModificationTime,
                LastModifierId = run.LastModifierId
            },
            Messages = messages.Select(x => new AgentRunMessageDto
            {
                Id = x.Id,
                AgentRunId = x.AgentRunId,
                Role = x.Role,
                Content = x.Content,
                ToolCallsJson = x.ToolCallsJson,
                CreationTime = x.CreationTime,
                CreatorId = x.CreatorId,
                LastModificationTime = x.LastModificationTime,
                LastModifierId = x.LastModifierId
            }).ToList()
        };
    }

    private async Task<ClassroomAgentAssignmentDto> MapAssignmentAsync(ClassroomAgentAssignment assignment)
    {
        var userNames = await GetUserNamesAsync(new List<Guid> { assignment.StudentId });
        return new ClassroomAgentAssignmentDto
        {
            Id = assignment.Id,
            ClassroomAgentTaskId = assignment.ClassroomAgentTaskId,
            StudentId = assignment.StudentId,
            StudentName = userNames.GetValueOrDefault(assignment.StudentId, string.Empty),
            Status = assignment.Status,
            StartedAt = assignment.StartedAt,
            CompletedAt = assignment.CompletedAt,
            LastActiveAt = assignment.LastActiveAt,
            SubmissionSummary = assignment.SubmissionSummary,
            HelpReason = assignment.HelpReason,
            CreationTime = assignment.CreationTime,
            CreatorId = assignment.CreatorId,
            LastModificationTime = assignment.LastModificationTime,
            LastModifierId = assignment.LastModifierId
        };
    }

    private async Task<List<AgentRunMessage>> GetMessagesAsync(Guid runId)
    {
        var query = await _messageRepository.GetQueryableAsync();
        return await query
            .Where(x => x.AgentRunId == runId)
            .OrderBy(x => x.CreationTime)
            .ToListAsync();
    }

    private async Task<Dictionary<Guid, string>> GetUserNamesAsync(List<Guid> userIds)
    {
        var query = await _userRepository.GetQueryableAsync();
        return await query
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name ?? x.UserName);
    }

    private static TaskTargetSnapshotDto? DeserializeSnapshot(string value)
    {
        return JsonSerializer.Deserialize<TaskTargetSnapshotDto>(value, JsonOptions);
    }

    private static List<TeachingAgentSkillBindingDto> DeserializeSkills(string value)
    {
        return JsonSerializer.Deserialize<List<TeachingAgentSkillBindingDto>>(value, JsonOptions) ?? new List<TeachingAgentSkillBindingDto>();
    }
}
