using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.TeachingAgents.Dtos;
using KnowledgeHub.TeachingAgents.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.TeachingAgents;

[Authorize(KnowledgeHubPermissions.TeachingAgents.Default)]
[IgnoreAntiforgeryToken]
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
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    /// <summary>课表 hover 显示「学校」用。</summary>
    private readonly Volo.Abp.TenantManagement.ITenantRepository _tenantRepository;
    private readonly IRepository<TeachingClass, Guid> _classRepository;
    private readonly IRepository<TeachingClassMember, Guid> _memberRepository;
    private readonly IRepository<Major, Guid> _majorRepository;
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
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        Volo.Abp.TenantManagement.ITenantRepository tenantRepository,
        IRepository<TeachingClass, Guid> classRepository,
        IRepository<TeachingClassMember, Guid> memberRepository,
        IRepository<Major, Guid> majorRepository,
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
        _exerciseRepository = exerciseRepository;
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _classRepository = classRepository;
        _memberRepository = memberRepository;
        _majorRepository = majorRepository;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _contextBuilder = contextBuilder;
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Assign)]
    public async Task<TaskCreationOptionsDto> GetCreateOptionsAsync()
    {
        // 可用于分发的智能体：
        // - 自己创建的（含 Private / School / Public）；
        // - 同租户标记为 School（校内共享）的智能体；
        // - 跨租户标记为 Public（全局公开）的智能体。
        // 他人的 Private 不可见、不可用于分发。
        // 由于 TeachingAgent 实现 IMultiTenant，ABP 仓储默认按当前租户过滤，
        // 这里必须 DataFilter.Disable<IMultiTenant>() 后再手动加可见性条件。
        List<TeachingAgentVersion> publishedVersions;
        List<TeachingAgent> agents;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var versionQuery = await _versionRepository.GetQueryableAsync();
            var allPublishedVersions = await versionQuery
                .Where(x => x.IsPublished)
                .ToListAsync();

            var candidateAgentIds = allPublishedVersions.Select(x => x.TeachingAgentId).Distinct().ToList();
            var agentQuery = await _teachingAgentRepository.GetQueryableAsync();
            var candidateAgents = await agentQuery.Where(x => candidateAgentIds.Contains(x.Id)).ToListAsync();

            var currentTenantId = CurrentTenant.Id;
            var currentUserId = CurrentUser.GetId();
            var allowedAgentIds = candidateAgents
                .Where(a => a.OwnerUserId == currentUserId
                            || (a.TenantId == currentTenantId && a.Visibility == TeachingAgentVisibility.School)
                            || a.Visibility == TeachingAgentVisibility.Public)
                .Select(a => a.Id)
                .ToHashSet();

            agents = candidateAgents.Where(a => allowedAgentIds.Contains(a.Id)).ToList();
            publishedVersions = allPublishedVersions
                .Where(v => allowedAgentIds.Contains(v.TeachingAgentId))
                .OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)
                .ToList();
        }

        var agentMap = agents.ToDictionary(x => x.Id);

        var courseQuery = await _courseRepository.GetQueryableAsync();
        var courses = await courseQuery
            .OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)
            .Take(50)
            .ToListAsync();

        // 各课程含有的习题数：用于前端「习题集」任务标注/禁用无内容的课程。
        var courseIds = courses.Select(x => x.Id).ToList();
        var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
        var exerciseCounts = await exerciseQuery
            .Where(x => courseIds.Contains(x.CourseId))
            .GroupBy(x => x.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count() })
            .ToListAsync();
        var exerciseCountMap = exerciseCounts.ToDictionary(x => x.CourseId, x => x.Count);

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
                Title = x.Title,
                ExerciseCount = exerciseCountMap.TryGetValue(x.Id, out var count) ? count : 0
            }).ToList(),
            Resources = resources.Select(x => new ResourceOptionDto
            {
                Id = x.Id,
                Name = x.Name,
                ResourceType = x.ResourceType
            }).ToList()
        };
    }

    /// <summary>
    /// 习题集任务的可选习题：按课程返回其全部习题，前端「先选课程、再选该课程习题」使用。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.TeachingAgents.Assign)]
    public async Task<List<ExerciseOptionDto>> GetCourseExercisesAsync(Guid courseId)
    {
        var query = await _exerciseRepository.GetQueryableAsync();
        var list = await query
            .Where(x => x.CourseId == courseId)
            .OrderByDescending(x => x.CreationTime)
            .ToListAsync();
        return list.Select(x => new ExerciseOptionDto
        {
            Id = x.Id,
            Title = x.Title,
            Type = (int)x.Type,
            Difficulty = x.Difficulty,
            Score = x.Score
        }).ToList();
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Assign)]
    public async Task<ClassroomAgentTaskDto> CreateAsync(CreateClassroomAgentTaskDto input)
    {
        var currentUserId = CurrentUser.GetId();

        // 空值校验：标题与任务提示词必填，避免产出无意义任务。
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("任务标题不能为空。");
        }
        if (string.IsNullOrWhiteSpace(input.TaskPrompt))
        {
            throw new UserFriendlyException("任务提示词不能为空。");
        }

        // 跨租户 Public agent 可被 qidi 老师用来创建课堂任务；
        // 需要禁用多租户过滤后加载 version 与 agent，再校验可见性。
        TeachingAgentVersion version;
        TeachingAgent agent;
        using (DataFilter.Disable<IMultiTenant>())
        {
            version = await _versionRepository.GetAsync(input.TeachingAgentVersionId);
            agent = await _teachingAgentRepository.GetAsync(version.TeachingAgentId);
        }
        await EnsureCanUseAgentAsync(agent);

        if (!version.IsPublished)
        {
            throw new UserFriendlyException("只能使用已发布的智能体版本。");
        }

        var targetSnapshot = await _contextBuilder.BuildAsync(input.TargetType, input.TargetId);

        // 习题集任务：先选课程再选该课程的习题。
        // TargetId 为课程，Task 上下文按选中的 ExerciseIds 过滤（只包含教师指定的习题）。
        if (input.TargetType == ClassroomAgentTaskTargetType.ExerciseSet)
        {
            if (input.ExerciseIds.Count == 0)
            {
                throw new UserFriendlyException("请为该习题任务至少选择一道习题。");
            }
            var idSet = input.ExerciseIds.ToHashSet();
            targetSnapshot.Exercises = targetSnapshot.Exercises
                .Where(e => idSet.Contains(e.Id))
                .ToList();
            if (targetSnapshot.Exercises.Count == 0)
            {
                throw new UserFriendlyException("所选习题不属于该课程，无法创建任务。");
            }
        }

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

        // 快照会写入数据库 varchar(1000000)，序列化后超限会抛 Postgres 22001（500）。
        // 这里在入库前校验并给出可读提示，避免以后扩充习题集再次遇到隐性 500。
        if (task.TargetSnapshotJson.Length > 900000)
        {
            throw new UserFriendlyException(
                "当前任务目标内容较多（习题/知识点超过存储上限），请减少选择的习题数量后重试。");
        }

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

        // 任务可能引用跨租户 Public agent（例如 qidi 老师用 guozhou 公开的智能体建任务），
        // 加载 agent / version 时必须禁用多租户过滤，否则会抛 EntityNotFound。
        TeachingAgent agent;
        TeachingAgentVersion version;
        using (DataFilter.Disable<IMultiTenant>())
        {
            agent = await _teachingAgentRepository.GetAsync(task.TeachingAgentId);
            version = await _versionRepository.GetAsync(task.TeachingAgentVersionId);
        }
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

        // 版本或智能体可能因跨租户、删除等原因无法解析，跳过脏数据而不是整页 500
        var items = new List<StudentAgentTaskDto>();
        foreach (var assignment in assignments)
        {
            if (!taskMap.TryGetValue(assignment.ClassroomAgentTaskId, out var task))
            {
                continue;
            }
            if (!versionMap.TryGetValue(task.TeachingAgentVersionId, out var version))
            {
                continue;
            }
            if (!agentMap.TryGetValue(version.TeachingAgentId, out var agent))
            {
                continue;
            }

            items.Add(new StudentAgentTaskDto
            {
                AssignmentId = assignment.Id,
                TaskId = task.Id,
                Title = task.Title,
                Description = task.Description,
                TeachingAgentName = agent.Name,
                Status = assignment.Status,
                DueTime = task.DueTime,
                LastActiveAt = assignment.LastActiveAt
            });
        }

        return new PagedResultDto<StudentAgentTaskDto>(totalCount, items);
    }

    /// <summary>
    /// 互动课堂课表：以「课堂智能体任务」为数据源，产出按时间排列的课表条目。
    ///
    /// <para>每条任务最多两个时间点：assign（布置日 = 创建时间）与 due（截止时间）；
    /// 课程名与学校名分别来自任务目标快照与租户档案，供前端 hover 显示。</para>
    /// </summary>
    public async Task<ClassroomTimetableDto> GetTimetableAsync(ClassroomTimetableQueryDto input)
    {
        var from = input.From;
        var to = input.To;
        if (to <= from)
        {
            throw new UserFriendlyException("课表查询区间的结束时间必须晚于开始时间。");
        }

        // 区间放宽 1 天：布置日按创建时间算，避免边界/时区把当天任务漏掉
        var queryFrom = from.AddDays(-1);
        var queryTo = to.AddDays(1);

        List<ClassroomAgentTask> tasks;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var q = await _taskRepository.GetQueryableAsync();
            tasks = q.Where(t => (t.CreationTime >= queryFrom && t.CreationTime <= queryTo)
                                 || (t.DueTime.HasValue && t.DueTime >= queryFrom && t.DueTime <= queryTo))
                .ToList();
        }

        if (CurrentTenant.Id.HasValue)
        {
            tasks = tasks.Where(t => t.TenantId == CurrentTenant.Id).ToList();
        }

        if (input.PublishedOnly)
        {
            tasks = tasks.Where(t => t.PublishStatus == ClassroomAgentTaskPublishStatus.Published).ToList();
        }

        var taskIds = tasks.Select(t => t.Id).ToList();
        var assignments = taskIds.Count == 0
            ? new List<ClassroomAgentAssignment>()
            : (await _assignmentRepository.GetListAsync(a => taskIds.Contains(a.ClassroomAgentTaskId)));

        var courseIds = tasks.Select(ResolveCourseId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var courses = courseIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _courseRepository.GetListAsync(c => courseIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Title);

        var schoolNames = await ResolveSchoolNamesAsync(tasks);

        var agentIds = tasks.Select(t => t.TeachingAgentId).Distinct().ToList();
        var agents = agentIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _teachingAgentRepository.GetListAsync(a => agentIds.Contains(a.Id))).ToDictionary(a => a.Id, a => a.Name);

        var now = DateTime.Now;
        var items = new List<ClassroomTimetableItemDto>();

        foreach (var task in tasks)
        {
            var assigned = assignments.Where(a => a.ClassroomAgentTaskId == task.Id).ToList();
            var snapshotCourseId = ResolveCourseId(task);
            var courseName = snapshotCourseId.HasValue && courses.TryGetValue(snapshotCourseId.Value, out var cn)
                ? cn
                : DeserializeSnapshot(task.TargetSnapshotJson)?.Course?.Title;

            if (input.CourseId.HasValue && snapshotCourseId != input.CourseId)
            {
                continue;
            }

            void AddItem(string kind, DateTime atRaw)
            {
                var at = ToLocal(atRaw);
                if (at < from || at > to)
                {
                    return;
                }

                items.Add(new ClassroomTimetableItemDto
                {
                    TaskId = task.Id,
                    Title = task.Title,
                    Description = task.Description,
                    CourseId = snapshotCourseId,
                    CourseName = courseName,
                    AgentName = agents.GetValueOrDefault(task.TeachingAgentId),
                    TargetType = (int)task.TargetType,
                    Kind = kind,
                    At = at,
                    EndAt = kind == "due" ? at : null,
                    SchoolName = schoolNames.GetValueOrDefault(task.TenantId ?? Guid.Empty),
                    PublishStatus = (int)task.PublishStatus,
                    AssignmentCount = assigned.Count,
                    CompletedCount = assigned.Count(a => a.Status == ClassroomAgentAssignmentStatus.Submitted),
                    NeedsHelpCount = assigned.Count(a => a.Status == ClassroomAgentAssignmentStatus.NeedsTeacherHelp),
                    IsOverdue = task.DueTime.HasValue && ToLocal(task.DueTime.Value) < now
                                && assigned.Any(a => a.Status != ClassroomAgentAssignmentStatus.Submitted),
                });
            }

            AddItem("assign", task.CreationTime);
            if (task.DueTime.HasValue)
            {
                AddItem("due", task.DueTime.Value);
            }
        }

        var courseOptions = tasks
            .Select(ResolveCourseId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .Select(id => new ClassroomTimetableCourseDto
            {
                Id = id,
                Name = courses.GetValueOrDefault(id) ?? "未命名课程",
                TaskCount = tasks.Count(t => ResolveCourseId(t) == id),
            })
            .OrderByDescending(c => c.TaskCount)
            .ToList();

        return new ClassroomTimetableDto
        {
            From = from,
            To = to,
            TotalCount = items.Count,
            TaskCount = items.Select(i => i.TaskId).Distinct().Count(),
            CourseCount = courseOptions.Count,
            OverdueCount = items.Where(i => i.Kind == "due" && i.IsOverdue).Select(i => i.TaskId).Distinct().Count(),
            Items = items.OrderBy(i => i.At).ToList(),
            Courses = courseOptions,
        };
    }

    /// <summary>任务目标对应的课程 Id（课程任务 = TargetId；资源/习题集 = 快照里的课程）。</summary>
    private static Guid? ResolveCourseId(ClassroomAgentTask task) =>
        task.TargetType == ClassroomAgentTaskTargetType.Course ? task.TargetId : null;

    private static DateTime ToLocal(DateTime utc) =>
        utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

    /// <summary>租户 Id → 学校名（课表 hover 显示「学校」用）。</summary>
    private async Task<Dictionary<Guid, string>> ResolveSchoolNamesAsync(List<ClassroomAgentTask> tasks)
    {
        var tenantIds = tasks.Where(t => t.TenantId.HasValue).Select(t => t.TenantId!.Value).Distinct().ToList();
        var result = new Dictionary<Guid, string>();
        if (tenantIds.Count == 0)
        {
            return result;
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            foreach (var id in tenantIds)
            {
                var tenant = await _tenantRepository.FindAsync(id);
                if (tenant != null)
                {
                    result[id] = tenant.Name;
                }
            }
        }

        return result;
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

    private async Task EnsureCanUseAgentAsync(TeachingAgent agent)
    {
        // 可用于分发的智能体可见性（与 GetCreateOptionsAsync 保持一致）：
        // - 自己创建的智能体；
        // - 同租户标记为 School（校内共享）的智能体；
        // - 跨租户标记为 Public（全局公开）的智能体。
        // 他人的 Private / 跨租户的 School 均不可使用，避免越权泄露。
        if (agent.OwnerUserId == CurrentUser.Id)
        {
            return;
        }

        if (agent.TenantId == CurrentTenant.Id && agent.Visibility == TeachingAgentVisibility.School)
        {
            return;
        }

        if (agent.Visibility == TeachingAgentVisibility.Public)
        {
            return;
        }

        throw new UserFriendlyException("该智能体不可用于分发任务。");
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

        // 同学生端：版本或智能体无法解析时跳过脏数据，避免整页 500
        var items = new List<ClassroomAgentTaskDto>();
        foreach (var task in tasks)
        {
            if (!agentMap.TryGetValue(task.TeachingAgentId, out var agent))
            {
                continue;
            }
            if (!versionMap.TryGetValue(task.TeachingAgentVersionId, out var version))
            {
                continue;
            }

            var taskAssignments = assignments.Where(x => x.ClassroomAgentTaskId == task.Id).ToList();
            items.Add(MapTask(task, agent, version, taskAssignments));
        }

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
        // 任务可能引用跨租户 Public agent（例如 qidi 老师用 guozhou 公开的智能体建任务），
        // 因此加载 agent / version 时需禁用多租户过滤。
        TeachingAgent agent;
        TeachingAgentVersion version;
        using (DataFilter.Disable<IMultiTenant>())
        {
            agent = await _teachingAgentRepository.GetAsync(task.TeachingAgentId);
            version = await _versionRepository.GetAsync(task.TeachingAgentVersionId);
        }
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
            WelcomeMessage = dto.WelcomeMessage,
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
            WelcomeMessage = version.WelcomeMessage,
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

        // 教学班归属：批量取班级成员 → 班级 → 专业，避免逐个学生查库（N+1）。
        // 一个学生只认一个教学班（取最早加入的那个），与「一个班可有多名学生」对称。
        var classByStudent = await GetClassMapByStudentAsync();

        foreach (var user in users)
        {
            using (_currentTenant.Change(user.TenantId))
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains("Student"))
                {
                    continue;
                }

                classByStudent.TryGetValue(user.Id, out var classInfo);

                // 专业：优先取教学班的专业，其次取用户档案上的专业
                var majorId = classInfo?.MajorId ?? GetUserMajorId(user);
                var majorName = majorId.HasValue ? await GetMajorNameAsync(majorId.Value) : null;

                students.Add(new StudentOptionDto
                {
                    Id = user.Id,
                    Name = !string.IsNullOrEmpty(user.Name) ? user.Name : user.UserName ?? string.Empty,
                    UserName = user.UserName ?? string.Empty,
                    ClassId = classInfo?.ClassId,
                    ClassName = classInfo?.ClassName,
                    MajorId = majorId,
                    MajorName = majorName
                });
            }
        }

        return students;
    }

    /// <summary>学生 → 教学班（含班级名与所属专业）。</summary>
    private sealed record StudentClassInfo(Guid ClassId, string ClassName, Guid? MajorId);

    /// <summary>
    /// 批量取「学生 → 教学班」映射。
    ///
    /// <para>学生同时被多个班收录时，取加入时间最早的一个（先来先得），
    /// 避免前端筛选出现「一个学生命中多个班级」导致重复计数。</para>
    /// </summary>
    private async Task<Dictionary<Guid, StudentClassInfo>> GetClassMapByStudentAsync()
    {
        var result = new Dictionary<Guid, StudentClassInfo>();
        if (!_currentTenant.Id.HasValue)
        {
            return result;
        }

        var memberQuery = await _memberRepository.GetQueryableAsync();
        var members = await memberQuery
            .Where(x => x.TenantId == _currentTenant.Id)
            .OrderBy(x => x.JoinedAt)
            .ToListAsync();

        if (members.Count == 0)
        {
            return result;
        }

        var classIds = members.Select(x => x.ClassId).Distinct().ToList();
        var classQuery = await _classRepository.GetQueryableAsync();
        var classes = await classQuery.Where(x => classIds.Contains(x.Id)).ToListAsync();
        var classMap = classes.ToDictionary(x => x.Id, x => x);

        foreach (var member in members)
        {
            if (result.ContainsKey(member.StudentId))
            {
                continue;
            }

            if (!classMap.TryGetValue(member.ClassId, out var cls))
            {
                continue;
            }

            result[member.StudentId] = new StudentClassInfo(cls.Id, cls.Name, cls.MajorId);
        }

        return result;
    }

    /// <summary>读取用户档案上的专业 Id（ExtraProperties 扩展字段）。</summary>
    private static Guid? GetUserMajorId(Volo.Abp.Identity.IdentityUser user)
    {
        if (!user.HasProperty("MajorId"))
        {
            return null;
        }

        return user.GetProperty<Guid?>("MajorId");
    }

    /// <summary>专业名（同一批次内缓存，避免逐个学生查专业表）。</summary>
    private readonly Dictionary<Guid, string?> _majorNameCache = new();

    private async Task<string?> GetMajorNameAsync(Guid majorId)
    {
        if (_majorNameCache.TryGetValue(majorId, out var cached))
        {
            return cached;
        }

        var major = await _majorRepository.FindAsync(majorId);
        var name = major?.Name;
        _majorNameCache[majorId] = name;
        return name;
    }

    private async Task<List<ClassroomAgentTask>> GetTasksByIdsAsync(List<Guid> taskIds)
    {
        var query = await _taskRepository.GetQueryableAsync();
        return await query.Where(x => taskIds.Contains(x.Id)).ToListAsync();
    }

    private async Task<List<TeachingAgentVersion>> GetVersionsByIdsAsync(List<Guid> versionIds)
    {
        if (versionIds.Count == 0)
        {
            return new List<TeachingAgentVersion>();
        }

        // ClassroomAgentTask 可能引用跨租户 Public agent 的版本（qidi 老师用 guozhou 公开的智能体建任务），
        // 因此必须禁用多租户过滤读取。ToListAsync 必须在 using 块内执行。
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _versionRepository.GetQueryableAsync();
            return await query.Where(x => versionIds.Contains(x.Id)).ToListAsync();
        }
    }

    private async Task<List<TeachingAgent>> GetAgentsByIdsAsync(List<Guid> agentIds)
    {
        if (agentIds.Count == 0)
        {
            return new List<TeachingAgent>();
        }

        // 同样支持跨租户 Public agent 读取。
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _teachingAgentRepository.GetQueryableAsync();
            return await query.Where(x => agentIds.Contains(x.Id)).ToListAsync();
        }
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
