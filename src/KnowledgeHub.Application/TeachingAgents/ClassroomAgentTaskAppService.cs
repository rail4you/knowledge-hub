using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.TeachingAgents.Dtos;
using KnowledgeHub.TeachingAgents.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.TeachingAgents;

[Authorize(KnowledgeHubPermissions.TeachingAgents.Default)]
public class ClassroomAgentTaskAppService : KnowledgeHubAppService, IClassroomAgentTaskAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<ClassroomAgentTask, Guid> _taskRepository;
    private readonly IRepository<ClassroomAgentAssignment, Guid> _assignmentRepository;
    private readonly IRepository<AgentRun, Guid> _runRepository;
    private readonly IRepository<AgentRunMessage, Guid> _messageRepository;
    private readonly IRepository<TeachingAgent, Guid> _teachingAgentRepository;
    private readonly IRepository<TeachingAgentVersion, Guid> _versionRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly TeachingAgentContextBuilder _contextBuilder;

    public ClassroomAgentTaskAppService(
        IRepository<ClassroomAgentTask, Guid> taskRepository,
        IRepository<ClassroomAgentAssignment, Guid> assignmentRepository,
        IRepository<AgentRun, Guid> runRepository,
        IRepository<AgentRunMessage, Guid> messageRepository,
        IRepository<TeachingAgent, Guid> teachingAgentRepository,
        IRepository<TeachingAgentVersion, Guid> versionRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        TeachingAgentContextBuilder contextBuilder)
    {
        _taskRepository = taskRepository;
        _assignmentRepository = assignmentRepository;
        _runRepository = runRepository;
        _messageRepository = messageRepository;
        _teachingAgentRepository = teachingAgentRepository;
        _versionRepository = versionRepository;
        _courseRepository = courseRepository;
        _resourceRepository = resourceRepository;
        _userRepository = userRepository;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _contextBuilder = contextBuilder;
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Assign)]
    public async Task<TaskCreationOptionsDto> GetCreateOptionsAsync()
    {
        var versionQuery = await _versionRepository.GetQueryableAsync();
        var publishedVersions = await versionQuery
            .Where(x => x.IsPublished)
            .OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)
            .ToListAsync();

        var agentIds = publishedVersions.Select(x => x.TeachingAgentId).Distinct().ToList();
        var agentQuery = await _teachingAgentRepository.GetQueryableAsync();
        var agents = await agentQuery.Where(x => agentIds.Contains(x.Id)).ToListAsync();
        var agentMap = agents.ToDictionary(x => x.Id);

        var courseQuery = await _courseRepository.GetQueryableAsync();
        var courses = await courseQuery
            .OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)
            .Take(50)
            .ToListAsync();

        var resourceQuery = await _resourceRepository.GetQueryableAsync();
        var resources = await resourceQuery
            .OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)
            .Take(80)
            .ToListAsync();

        var students = await GetStudentsAsync();

        return new TaskCreationOptionsDto
        {
            Agents = publishedVersions
                .Where(x => agentMap.ContainsKey(x.TeachingAgentId))
                .Select(x => new TeachingAgentOptionDto
                {
                    Id = agentMap[x.TeachingAgentId].Id,
                    Name = agentMap[x.TeachingAgentId].Name,
                    VersionId = x.Id,
                    VersionNumber = x.VersionNumber
                })
                .ToList(),
            Students = students,
            Courses = courses.Select(x => new CourseOptionDto
            {
                Id = x.Id,
                Title = x.Title
            }).ToList(),
            Resources = resources.Select(x => new ResourceOptionDto
            {
                Id = x.Id,
                Name = x.Name,
                ResourceType = x.ResourceType
            }).ToList()
        };
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Assign)]
    public async Task<ClassroomAgentTaskDto> CreateAsync(CreateClassroomAgentTaskDto input)
    {
        var currentUserId = CurrentUser.GetId();
        var version = await _versionRepository.GetAsync(input.TeachingAgentVersionId);
        var agent = await _teachingAgentRepository.GetAsync(version.TeachingAgentId);
        var targetSnapshot = await _contextBuilder.BuildAsync(input.TargetType, input.TargetId);

        var task = new ClassroomAgentTask(GuidGenerator.Create(), agent.Id, version.Id, input.Title)
        {
            TenantId = CurrentTenant.Id,
            Description = input.Description,
            TaskPrompt = input.TaskPrompt,
            TargetType = input.TargetType,
            TargetId = input.TargetId,
            TargetSnapshotJson = JsonSerializer.Serialize(targetSnapshot, JsonOptions),
            DueTime = input.DueTime,
            PublishStatus = ClassroomAgentTaskPublishStatus.Draft,
            CreatorUserId = currentUserId
        };

        await _taskRepository.InsertAsync(task, autoSave: true);

        var distinctStudentIds = input.StudentIds.Distinct().ToList();
        foreach (var studentId in distinctStudentIds)
        {
            await _assignmentRepository.InsertAsync(new ClassroomAgentAssignment(
                GuidGenerator.Create(),
                task.Id,
                studentId)
            {
                TenantId = CurrentTenant.Id,
                Status = ClassroomAgentAssignmentStatus.Pending
            }, autoSave: true);
        }

        return await BuildTaskDtoAsync(task, agent, version);
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Assign)]
    public async Task<ClassroomAgentTaskDto> PublishAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        await EnsureCanViewAsTeacherAsync(task);

        task.PublishStatus = ClassroomAgentTaskPublishStatus.Published;
        await _taskRepository.UpdateAsync(task, autoSave: true);

        var agent = await _teachingAgentRepository.GetAsync(task.TeachingAgentId);
        var version = await _versionRepository.GetAsync(task.TeachingAgentVersionId);
        return await BuildTaskDtoAsync(task, agent, version);
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Assign)]
    public async Task DeleteAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        await EnsureCanViewAsTeacherAsync(task);

        var assignments = await GetAssignmentsByTaskIdAsync(id);
        if (assignments.Count > 0)
        {
            var assignmentIds = assignments.Select(x => x.Id).ToList();
            var runQuery = await _runRepository.GetQueryableAsync();
            var runs = await runQuery
                .Where(x => assignmentIds.Contains(x.ClassroomAgentAssignmentId))
                .ToListAsync();

            if (runs.Count > 0)
            {
                var runIds = runs.Select(x => x.Id).ToList();
                var messageQuery = await _messageRepository.GetQueryableAsync();
                var messages = await messageQuery
                    .Where(x => runIds.Contains(x.AgentRunId))
                    .ToListAsync();

                if (messages.Count > 0)
                {
                    await _messageRepository.DeleteManyAsync(messages, autoSave: true);
                }

                await _runRepository.DeleteManyAsync(runs, autoSave: true);
            }

            await _assignmentRepository.DeleteManyAsync(assignments, autoSave: true);
        }

        await _taskRepository.DeleteAsync(task, autoSave: true);
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Assign)]
    public async Task<PagedResultDto<ClassroomAgentTaskDto>> GetTeacherTaskListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _taskRepository.GetQueryableAsync();
        if (!await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.TeachingAgents.Review))
        {
            query = query.Where(x => x.CreatorUserId == CurrentUser.GetId());
        }

        var totalCount = await query.LongCountAsync();
        var tasks = await query
            .OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return await BuildTeacherTaskResultAsync(tasks, totalCount);
    }

    public async Task<PagedResultDto<StudentAgentTaskDto>> GetStudentTaskListAsync(PagedAndSortedResultRequestDto input)
    {
        var studentId = CurrentUser.GetId();
        var assignmentQuery = await _assignmentRepository.GetQueryableAsync();
        var taskQuery = await _taskRepository.GetQueryableAsync();
        var publishedTaskIds = taskQuery
            .Where(x => x.PublishStatus == ClassroomAgentTaskPublishStatus.Published)
            .Select(x => x.Id);

        var filteredAssignments = assignmentQuery
            .Where(x => x.StudentId == studentId && publishedTaskIds.Contains(x.ClassroomAgentTaskId));

        var assignments = await filteredAssignments
            .OrderByDescending(x => x.LastActiveAt ?? x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        var totalCount = await filteredAssignments.LongCountAsync();
        if (assignments.Count == 0)
        {
            return new PagedResultDto<StudentAgentTaskDto>(totalCount, new List<StudentAgentTaskDto>());
        }

        var tasks = await GetTasksByIdsAsync(assignments.Select(x => x.ClassroomAgentTaskId).ToList());
        var versions = await GetVersionsByIdsAsync(tasks.Select(x => x.TeachingAgentVersionId).ToList());
        var agents = await GetAgentsByIdsAsync(tasks.Select(x => x.TeachingAgentId).ToList());
        var versionMap = versions.ToDictionary(x => x.Id);
        var agentMap = agents.ToDictionary(x => x.Id);
        var taskMap = tasks.ToDictionary(x => x.Id);

        var items = assignments
            .Where(x => taskMap.ContainsKey(x.ClassroomAgentTaskId))
            .Select(assignment =>
            {
                var task = taskMap[assignment.ClassroomAgentTaskId];
                var version = versionMap[task.TeachingAgentVersionId];
                var agent = agentMap[version.TeachingAgentId];

                return new StudentAgentTaskDto
                {
                    AssignmentId = assignment.Id,
                    TaskId = task.Id,
                    Title = task.Title,
                    Description = task.Description,
                    TeachingAgentName = agent.Name,
                    Status = assignment.Status,
                    DueTime = task.DueTime,
                    LastActiveAt = assignment.LastActiveAt
                };
            })
            .ToList();

        return new PagedResultDto<StudentAgentTaskDto>(totalCount, items);
    }

    public async Task<ClassroomAgentTaskDetailDto> GetTaskDetailAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        var canReview = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.TeachingAgents.Review);
        var assignments = await GetAssignmentsByTaskIdAsync(id);

        var isTaskCreator = task.CreatorUserId == CurrentUser.Id;
        var isAssignedStudent = assignments.Any(x => x.StudentId == CurrentUser.Id);

        if (!canReview && !isTaskCreator && !isAssignedStudent)
        {
            throw new AbpAuthorizationException("You are not allowed to view this classroom task.");
        }

        if (isAssignedStudent && task.PublishStatus != ClassroomAgentTaskPublishStatus.Published && !canReview && !isTaskCreator)
        {
            throw new AbpAuthorizationException("You are not allowed to view this classroom task.");
        }

        if (!canReview && !isTaskCreator)
        {
            assignments = assignments.Where(x => x.StudentId == CurrentUser.Id).ToList();
        }

        return await BuildTaskDetailAsync(task, assignments);
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Assign)]
    public async Task<ClassroomAgentAssignmentDto> RespondToStudentHelpAsync(Guid assignmentId, TeacherRespondDto input)
    {
        var assignment = await _assignmentRepository.GetAsync(assignmentId);
        var task = await _taskRepository.GetAsync(assignment.ClassroomAgentTaskId);
        await EnsureCanViewAsTeacherAsync(task);

        assignment.TeacherResponse = input.Response;
        assignment.Status = ClassroomAgentAssignmentStatus.InProgress;
        assignment.LastActiveAt = Clock.Now;
        await _assignmentRepository.UpdateAsync(assignment, autoSave: true);

        return await MapSingleAssignmentAsync(assignment, task);
    }

    private async Task EnsureCanViewAsTeacherAsync(ClassroomAgentTask task)
    {
        var canReview = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.TeachingAgents.Review);
        if (canReview || task.CreatorUserId == CurrentUser.Id)
        {
            return;
        }

        throw new AbpAuthorizationException("You are not allowed to manage this classroom task.");
    }

    private async Task<PagedResultDto<ClassroomAgentTaskDto>> BuildTeacherTaskResultAsync(List<ClassroomAgentTask> tasks, long totalCount)
    {
        if (tasks.Count == 0)
        {
            return new PagedResultDto<ClassroomAgentTaskDto>(totalCount, new List<ClassroomAgentTaskDto>());
        }

        var agents = await GetAgentsByIdsAsync(tasks.Select(x => x.TeachingAgentId).ToList());
        var versions = await GetVersionsByIdsAsync(tasks.Select(x => x.TeachingAgentVersionId).ToList());
        var assignments = await GetAssignmentsByTaskIdsAsync(tasks.Select(x => x.Id).ToList());

        var agentMap = agents.ToDictionary(x => x.Id);
        var versionMap = versions.ToDictionary(x => x.Id);

        var items = tasks.Select(task =>
        {
            var taskAssignments = assignments.Where(x => x.ClassroomAgentTaskId == task.Id).ToList();
            var agent = agentMap[task.TeachingAgentId];
            var version = versionMap[task.TeachingAgentVersionId];
            return MapTask(task, agent, version, taskAssignments);
        }).ToList();

        return new PagedResultDto<ClassroomAgentTaskDto>(totalCount, items);
    }

    private async Task<ClassroomAgentTaskDto> BuildTaskDtoAsync(
        ClassroomAgentTask task,
        TeachingAgent agent,
        TeachingAgentVersion version)
    {
        var assignments = await GetAssignmentsByTaskIdAsync(task.Id);
        return MapTask(task, agent, version, assignments);
    }

    private async Task<ClassroomAgentTaskDetailDto> BuildTaskDetailAsync(
        ClassroomAgentTask task,
        List<ClassroomAgentAssignment> assignments)
    {
        var agent = await _teachingAgentRepository.GetAsync(task.TeachingAgentId);
        var version = await _versionRepository.GetAsync(task.TeachingAgentVersionId);
        var studentIds = assignments.Select(x => x.StudentId).Distinct().ToList();
        var userNames = await GetUserNamesAsync(studentIds, task.TenantId);
        var dto = MapTask(task, agent, version, assignments);

        return new ClassroomAgentTaskDetailDto
        {
            Id = dto.Id,
            Title = dto.Title,
            Description = dto.Description,
            TeachingAgentId = dto.TeachingAgentId,
            TeachingAgentVersionId = dto.TeachingAgentVersionId,
            TeachingAgentName = dto.TeachingAgentName,
            TeachingAgentVersionNumber = dto.TeachingAgentVersionNumber,
            TaskPrompt = dto.TaskPrompt,
            TargetType = dto.TargetType,
            TargetId = dto.TargetId,
            TargetSnapshot = dto.TargetSnapshot,
            DueTime = dto.DueTime,
            PublishStatus = dto.PublishStatus,
            AssignmentCount = dto.AssignmentCount,
            CompletedCount = dto.CompletedCount,
            NeedsHelpCount = dto.NeedsHelpCount,
            CreationTime = dto.CreationTime,
            CreatorId = dto.CreatorId,
            LastModificationTime = dto.LastModificationTime,
            LastModifierId = dto.LastModifierId,
            Assignments = assignments.Select(x => new ClassroomAgentAssignmentDto
            {
                Id = x.Id,
                ClassroomAgentTaskId = x.ClassroomAgentTaskId,
                StudentId = x.StudentId,
                StudentName = userNames.GetValueOrDefault(x.StudentId, string.Empty),
                Status = x.Status,
                StartedAt = x.StartedAt,
                CompletedAt = x.CompletedAt,
                LastActiveAt = x.LastActiveAt,
                SubmissionSummary = x.SubmissionSummary,
                HelpReason = x.HelpReason,
                TeacherResponse = x.TeacherResponse,
                CreationTime = x.CreationTime,
                CreatorId = x.CreatorId,
                LastModificationTime = x.LastModificationTime,
                LastModifierId = x.LastModifierId
            }).ToList()
        };
    }

    private ClassroomAgentTaskDto MapTask(
        ClassroomAgentTask task,
        TeachingAgent agent,
        TeachingAgentVersion version,
        List<ClassroomAgentAssignment> assignments)
    {
        var completedCount = assignments.Count(x => x.Status == ClassroomAgentAssignmentStatus.Submitted);
        var needsHelpCount = assignments.Count(x => x.Status == ClassroomAgentAssignmentStatus.NeedsTeacherHelp);

        return new ClassroomAgentTaskDto
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
            AssignmentCount = assignments.Count,
            CompletedCount = completedCount,
            NeedsHelpCount = needsHelpCount,
            CreationTime = task.CreationTime,
            CreatorId = task.CreatorId,
            LastModificationTime = task.LastModificationTime,
            LastModifierId = task.LastModifierId
        };
    }

    private async Task<List<StudentOptionDto>> GetStudentsAsync()
    {
        var query = await _userRepository.GetQueryableAsync();
        query = query.Where(x => x.TenantId == _currentTenant.Id);
        var users = await query.OrderBy(x => x.UserName).ToListAsync();
        var students = new List<StudentOptionDto>();

        foreach (var user in users)
        {
            using (_currentTenant.Change(user.TenantId))
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains("Student"))
                {
                    continue;
                }

                students.Add(new StudentOptionDto
                {
                    Id = user.Id,
                    Name = !string.IsNullOrEmpty(user.Name) ? user.Name : user.UserName ?? string.Empty,
                    UserName = user.UserName ?? string.Empty
                });
            }
        }

        return students;
    }

    private async Task<List<ClassroomAgentTask>> GetTasksByIdsAsync(List<Guid> taskIds)
    {
        var query = await _taskRepository.GetQueryableAsync();
        return await query.Where(x => taskIds.Contains(x.Id)).ToListAsync();
    }

    private async Task<List<TeachingAgentVersion>> GetVersionsByIdsAsync(List<Guid> versionIds)
    {
        var query = await _versionRepository.GetQueryableAsync();
        return await query.Where(x => versionIds.Contains(x.Id)).ToListAsync();
    }

    private async Task<List<TeachingAgent>> GetAgentsByIdsAsync(List<Guid> agentIds)
    {
        var query = await _teachingAgentRepository.GetQueryableAsync();
        return await query.Where(x => agentIds.Contains(x.Id)).ToListAsync();
    }

    private async Task<List<ClassroomAgentAssignment>> GetAssignmentsByTaskIdsAsync(List<Guid> taskIds)
    {
        var query = await _assignmentRepository.GetQueryableAsync();
        return await query.Where(x => taskIds.Contains(x.ClassroomAgentTaskId)).ToListAsync();
    }

    private async Task<List<ClassroomAgentAssignment>> GetAssignmentsByTaskIdAsync(Guid taskId)
    {
        var query = await _assignmentRepository.GetQueryableAsync();
        return await query.Where(x => x.ClassroomAgentTaskId == taskId).ToListAsync();
    }

    private async Task<ClassroomAgentAssignmentDto> MapSingleAssignmentAsync(ClassroomAgentAssignment assignment, ClassroomAgentTask task)
    {
        var userNames = await GetUserNamesAsync(new List<Guid> { assignment.StudentId }, task.TenantId);
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
            TeacherResponse = assignment.TeacherResponse,
            CreationTime = assignment.CreationTime,
            CreatorId = assignment.CreatorId,
            LastModificationTime = assignment.LastModificationTime,
            LastModifierId = assignment.LastModifierId
        };
    }

    private async Task<Dictionary<Guid, string>> GetUserNamesAsync(List<Guid> userIds, Guid? tenantId = null)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        using (_currentTenant.Change(tenantId))
        {
            var query = await _userRepository.GetQueryableAsync();
            return await query
                .Where(x => userIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => !string.IsNullOrEmpty(x.Name) ? x.Name : x.UserName);
        }
    }

    private static TaskTargetSnapshotDto? DeserializeSnapshot(string value)
    {
        return JsonSerializer.Deserialize<TaskTargetSnapshotDto>(value, JsonOptions);
    }
}
