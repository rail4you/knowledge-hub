using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Classroom.Dtos;
using KnowledgeHub.Classroom.Enums;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Classroom;

/// <summary>
/// 小组任务：教师建任务 → 分组（手工 / 随机平均）→ 发布 → 每组一份提交 → 评分（组员共享）。
/// <para>
/// 「一组一份」由 (GroupTaskId, GroupId) 唯一索引保证；组内任一成员提交都覆盖同一行，
/// 便于"谁有空谁交"。分数按组给，不做组内个体差异（需要个体评分请用课程作业）。
/// </para>
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize]
public class GroupTaskAppService : KnowledgeHubAppService, IGroupTaskAppService
{
    private readonly IRepository<GroupTask, Guid> _taskRepository;
    private readonly IRepository<StudentGroup, Guid> _groupRepository;
    private readonly IRepository<StudentGroupMember, Guid> _memberRepository;
    private readonly IRepository<GroupTaskSubmission, Guid> _submissionRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly ClassroomSupport _support;
    private readonly IStudentActivityLogger _activityLogger;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentUser _currentUser;

    public GroupTaskAppService(
        IRepository<GroupTask, Guid> taskRepository,
        IRepository<StudentGroup, Guid> groupRepository,
        IRepository<StudentGroupMember, Guid> memberRepository,
        IRepository<GroupTaskSubmission, Guid> submissionRepository,
        IRepository<Resource, Guid> resourceRepository,
        ClassroomSupport support,
        IStudentActivityLogger activityLogger,
        IDataFilter dataFilter,
        ICurrentUser currentUser)
    {
        _taskRepository = taskRepository;
        _groupRepository = groupRepository;
        _memberRepository = memberRepository;
        _submissionRepository = submissionRepository;
        _resourceRepository = resourceRepository;
        _support = support;
        _activityLogger = activityLogger;
        _dataFilter = dataFilter;
        _currentUser = currentUser;
    }

    [Authorize(KnowledgeHubPermissions.Classroom.GroupTask)]
    public async Task<PagedResultDto<GroupTaskDto>> GetListAsync(Guid? courseId, int skipCount, int maxResultCount)
    {
        List<GroupTask> tasks;
        int total;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _taskRepository.GetQueryableAsync();
            query = query.WhereIf(courseId.HasValue, x => x.CourseId == courseId!.Value);
            total = await AsyncExecuter.CountAsync(query);
            tasks = await AsyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.CreationTime).Skip(skipCount).Take(maxResultCount));
        }

        var dtos = new List<GroupTaskDto>();
        foreach (var task in tasks)
        {
            var detail = await LoadDetailAsync(task);
            dtos.Add(detail);
        }

        return new PagedResultDto<GroupTaskDto>(total, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.GroupTask)]
    public async Task<GroupTaskDetailDto> GetDetailAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        return await LoadDetailAsync(task);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.GroupTask)]
    public async Task<GroupTaskDto> CreateAsync(CreateGroupTaskInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写任务标题。");
        }

        var course = await _support.GetCourseAsync(input.CourseId);
        var me = _support.CurrentUserIdOrThrow();

        var task = new GroupTask(
            GuidGenerator.Create(),
            input.CourseId,
            input.Title.Trim(),
            ClassroomSupport.NewToken(),
            me)
        {
            Description = input.Description,
            DueAt = input.DueAt,
            MaxScore = input.MaxScore <= 0 ? 100 : input.MaxScore,
            GroupMode = input.GroupMode,
            TenantId = course.TenantId
        };

        await _taskRepository.InsertAsync(task);

        // 建任务即分组：随机模式按选课名单平均分，手工模式留空待老师编辑
        if (input.GroupMode == GroupMode.RandomAuto)
        {
            await AutoAssignInternalAsync(task, input.GroupCount);
        }

        if (input.PublishImmediately)
        {
            task.Publish();
            await _taskRepository.UpdateAsync(task);
        }

        // ABP 仓储默认 autoSave=false：分组/成员是刚插入的，不 SaveChanges 直接查库会读不到
        await SaveNowAsync();
        return await LoadDetailAsync(task);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.GroupTask)]
    public async Task<GroupTaskDto> UpdateAsync(Guid id, CreateGroupTaskInput input)
    {
        var task = await _taskRepository.GetAsync(id);
        task.Title = input.Title.Trim();
        task.Description = input.Description;
        task.DueAt = input.DueAt;
        task.MaxScore = input.MaxScore <= 0 ? 100 : input.MaxScore;
        task.GroupMode = input.GroupMode;
        await _taskRepository.UpdateAsync(task);
        return await LoadDetailAsync(task);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.GroupTask)]
    public async Task<GroupTaskDto> PublishAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);

        var groups = await LoadGroupsAsync(id);
        if (groups.Count == 0)
        {
            throw new UserFriendlyException("还没有分组，请先分组或使用「随机分组」后再发布。");
        }

        task.Publish();
        await _taskRepository.UpdateAsync(task);
        return await LoadDetailAsync(task);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.GroupTask)]
    public async Task<GroupTaskDto> CloseAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        task.Close();
        await _taskRepository.UpdateAsync(task);
        return await LoadDetailAsync(task);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.GroupTask)]
    public async Task DeleteAsync(Guid id)
    {
        var groups = await LoadGroupsAsync(id);
        var groupIds = groups.Select(x => x.Id).ToList();

        List<StudentGroupMember> members;
        List<GroupTaskSubmission> submissions;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var memberQuery = await _memberRepository.GetQueryableAsync();
            members = groupIds.Count == 0
                ? new List<StudentGroupMember>()
                : await AsyncExecuter.ToListAsync(memberQuery.Where(x => groupIds.Contains(x.GroupId)));

            var submissionQuery = await _submissionRepository.GetQueryableAsync();
            submissions = await AsyncExecuter.ToListAsync(submissionQuery.Where(x => x.GroupTaskId == id));
        }

        foreach (var submission in submissions)
        {
            await _submissionRepository.DeleteAsync(submission);
        }

        foreach (var member in members)
        {
            await _memberRepository.DeleteAsync(member);
        }

        foreach (var group in groups)
        {
            await _groupRepository.DeleteAsync(group);
        }

        await _taskRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.GroupTask)]
    public async Task<GroupTaskDetailDto> AssignGroupsAsync(AssignGroupsInput input)
    {
        var task = await _taskRepository.GetAsync(input.GroupTaskId);

        // 覆盖式：清空旧分组再按入参重建
        await ClearGroupsAsync(task.Id);

        var enrolled = (await _support.GetEnrolledStudentIdsAsync(task.CourseId)).ToHashSet();
        var names = await _support.GetUserNamesAsync(input.Groups.SelectMany(x => x.StudentIds));

        var sort = 0;
        var assigned = new HashSet<Guid>();
        foreach (var groupInput in input.Groups)
        {
            var students = groupInput.StudentIds
                .Where(x => enrolled.Contains(x) && assigned.Add(x))
                .ToList();

            if (students.Count == 0)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(groupInput.Name) ? $"第 {sort + 1} 组" : groupInput.Name.Trim();
            var group = new StudentGroup(GuidGenerator.Create(), task.Id, name, sort++)
            {
                TenantId = task.TenantId
            };
            await _groupRepository.InsertAsync(group);

            foreach (var studentId in students)
            {
                names.TryGetValue(studentId, out var pair);
                await _memberRepository.InsertAsync(new StudentGroupMember(
                    GuidGenerator.Create(), group.Id, studentId)
                {
                    StudentName = pair.Name ?? pair.UserName,
                    TenantId = task.TenantId
                });
            }
        }

        task.GroupMode = GroupMode.TeacherAssigned;
        await _taskRepository.UpdateAsync(task);

        await SaveNowAsync();
        return await LoadDetailAsync(task);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.GroupTask)]
    public async Task<GroupTaskDetailDto> AutoAssignGroupsAsync(Guid id, int? groupCount)
    {
        var task = await _taskRepository.GetAsync(id);
        await AutoAssignInternalAsync(task, groupCount);
        await SaveNowAsync();
        return await LoadDetailAsync(task);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.GroupTask)]
    public async Task<GroupTaskSubmissionDto> GradeAsync(GradeGroupTaskInput input)
    {
        var submission = await _submissionRepository.GetAsync(input.SubmissionId);
        var task = await _taskRepository.GetAsync(submission.GroupTaskId);

        if (input.Score < 0 || input.Score > task.MaxScore)
        {
            throw new UserFriendlyException($"分数需在 0 ~ {task.MaxScore} 之间。");
        }

        submission.Grade(input.Score, input.Feedback);
        await _submissionRepository.UpdateAsync(submission);

        var group = await _groupRepository.FindAsync(submission.GroupId);
        return await MapSubmissionAsync(submission, group?.Name);
    }

    // ── 学生侧 ────────────────────────────────────────────────────

    [Authorize(KnowledgeHubPermissions.Classroom.Participate)]
    public async Task<List<GroupTaskDto>> GetMyTasksAsync([FromQuery] Guid? courseId = null)
    {
        var me = _support.CurrentUserIdOrThrow();

        List<StudentGroupMember> myMemberships;
        List<StudentGroup> allGroups;
        List<GroupTask> tasks;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var memberQuery = await _memberRepository.GetQueryableAsync();
            myMemberships = await AsyncExecuter.ToListAsync(memberQuery.Where(x => x.StudentId == me));

            if (myMemberships.Count == 0)
            {
                return new List<GroupTaskDto>();
            }

            var groupQuery = await _groupRepository.GetQueryableAsync();
            allGroups = await AsyncExecuter.ToListAsync(groupQuery);

            var taskQuery = await _taskRepository.GetQueryableAsync();
            tasks = await AsyncExecuter.ToListAsync(
                taskQuery
                    .Where(x => x.Status != GroupTaskStatus.Draft)
                    .WhereIf(courseId.HasValue, x => x.CourseId == courseId!.Value)
                    .OrderByDescending(x => x.CreationTime));
        }

        var myGroupIds = myMemberships.Select(x => x.GroupId).ToHashSet();
        var myTaskIds = allGroups
            .Where(g => myGroupIds.Contains(g.Id))
            .Select(g => g.GroupTaskId)
            .ToHashSet();

        var result = new List<GroupTaskDto>();
        foreach (var task in tasks.Where(t => myTaskIds.Contains(t.Id)))
        {
            result.Add(await LoadDetailAsync(task));
        }

        return result;
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Participate)]
    public async Task<GroupTaskDetailDto> GetMyTaskAsync(string token)
    {
        var task = await GetTaskByTokenAsync(token);
        await _support.EnsureEnrolledAsync(task.CourseId);
        return await LoadDetailAsync(task);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Participate)]
    public async Task<GroupTaskSubmissionDto> SubmitAsync(SubmitGroupTaskInput input)
    {
        var task = await GetTaskByTokenAsync(input.Token);
        await _support.EnsureEnrolledAsync(task.CourseId);

        if (task.Status == GroupTaskStatus.Draft)
        {
            throw new UserFriendlyException("任务还没有发布，暂时不能提交。");
        }

        if (!task.CanAcceptSubmission(DateTime.UtcNow))
        {
            throw new UserFriendlyException("任务已截止，如需补交请联系老师。");
        }

        if (string.IsNullOrWhiteSpace(input.Content) && !input.AttachmentResourceId.HasValue)
        {
            throw new UserFriendlyException("请填写提交内容或上传附件。");
        }

        var me = _support.CurrentUserIdOrThrow();
        var myGroup = await FindMyGroupAsync(task.Id, me);
        if (myGroup == null)
        {
            throw new UserFriendlyException("你还不在任何小组里，请联系老师完成分组。");
        }

        GroupTaskSubmission? submission;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _submissionRepository.GetQueryableAsync();
            submission = await AsyncExecuter.FirstOrDefaultAsync(
                query.Where(x => x.GroupTaskId == task.Id && x.GroupId == myGroup.Id));
        }

        var isNew = submission == null;
        if (submission == null)
        {
            submission = new GroupTaskSubmission(
                GuidGenerator.Create(), task.Id, myGroup.Id, me)
            {
                TenantId = task.TenantId
            };
        }

        submission.Content = input.Content;
        submission.AttachmentResourceId = input.AttachmentResourceId;
        submission.SubmittedById = me;
        submission.SubmittedByName = await _support.GetDisplayNameAsync(me);
        submission.SubmittedAt = DateTime.UtcNow;

        if (isNew)
        {
            await _submissionRepository.InsertAsync(submission);
        }
        else
        {
            await _submissionRepository.UpdateAsync(submission);
        }

        await _activityLogger.LogAsync(
            me, StudentActivityAction.SubmitGroupTask, task.CourseId, "group-task", task.Id,
            new { groupId = myGroup.Id, groupName = myGroup.Name });

        return await MapSubmissionAsync(submission, myGroup.Name);
    }

    // ── 内部 ──────────────────────────────────────────────────────

    /// <summary>
    /// 同一 UoW 内「先插入分组/成员，再读详情」必须先落库：
    /// <c>IRepository.GetQueryableAsync()</c> 直接查库，看不到未提交的插入。
    /// </summary>
    private async Task SaveNowAsync()
    {
        if (CurrentUnitOfWork is not null)
        {
            await CurrentUnitOfWork.SaveChangesAsync();
        }
    }

    private async Task<GroupTask> GetTaskByTokenAsync(string token)
    {
        GroupTask? task;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _taskRepository.GetQueryableAsync();
            task = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Token == token));
        }

        return task ?? throw new UserFriendlyException("任务链接无效或已失效。");
    }

    private async Task AutoAssignInternalAsync(GroupTask task, int? groupCount)
    {
        var enrolled = await _support.GetEnrolledStudentIdsAsync(task.CourseId);
        if (enrolled.Count == 0)
        {
            throw new UserFriendlyException("该课程还没有选课学生，无法分组。");
        }

        await ClearGroupsAsync(task.Id);

        // 组数：显式指定优先；否则每组 4 人；至少 1 组
        var count = groupCount.GetValueOrDefault(Math.Max(1, (int)Math.Ceiling(enrolled.Count / 4.0)));
        count = Math.Clamp(count, 1, enrolled.Count);

        var shuffled = enrolled.OrderBy(_ => Guid.NewGuid()).ToList();
        var names = await _support.GetUserNamesAsync(shuffled);

        var buckets = Enumerable.Range(0, count).Select(_ => new List<Guid>()).ToList();
        for (var i = 0; i < shuffled.Count; i++)
        {
            buckets[i % count].Add(shuffled[i]);
        }

        for (var i = 0; i < buckets.Count; i++)
        {
            var group = new StudentGroup(
                GuidGenerator.Create(), task.Id, $"第 {i + 1} 组", i)
            {
                TenantId = task.TenantId
            };
            await _groupRepository.InsertAsync(group);

            foreach (var studentId in buckets[i])
            {
                names.TryGetValue(studentId, out var pair);
                await _memberRepository.InsertAsync(new StudentGroupMember(
                    GuidGenerator.Create(), group.Id, studentId)
                {
                    StudentName = pair.Name ?? pair.UserName,
                    TenantId = task.TenantId
                });
            }
        }

        task.GroupMode = GroupMode.RandomAuto;
        await _taskRepository.UpdateAsync(task);
    }

    private async Task ClearGroupsAsync(Guid taskId)
    {
        var groups = await LoadGroupsAsync(taskId);
        var groupIds = groups.Select(x => x.Id).ToList();

        List<StudentGroupMember> members;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _memberRepository.GetQueryableAsync();
            members = groupIds.Count == 0
                ? new List<StudentGroupMember>()
                : await AsyncExecuter.ToListAsync(query.Where(x => groupIds.Contains(x.GroupId)));
        }

        foreach (var member in members)
        {
            await _memberRepository.DeleteAsync(member);
        }

        foreach (var group in groups)
        {
            await _groupRepository.DeleteAsync(group);
        }
    }

    private async Task<List<StudentGroup>> LoadGroupsAsync(Guid taskId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _groupRepository.GetQueryableAsync();
            return await AsyncExecuter.ToListAsync(
                query.Where(x => x.GroupTaskId == taskId).OrderBy(x => x.SortOrder));
        }
    }

    private async Task<StudentGroup?> FindMyGroupAsync(Guid taskId, Guid studentId)
    {
        var groups = await LoadGroupsAsync(taskId);
        if (groups.Count == 0)
        {
            return null;
        }

        var groupIds = groups.Select(x => x.Id).ToList();
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _memberRepository.GetQueryableAsync();
            var member = await AsyncExecuter.FirstOrDefaultAsync(
                query.Where(x => groupIds.Contains(x.GroupId) && x.StudentId == studentId));
            return member == null ? null : groups.FirstOrDefault(x => x.Id == member.GroupId);
        }
    }

    private async Task<GroupTaskDetailDto> LoadDetailAsync(GroupTask task)
    {
        var groups = await LoadGroupsAsync(task.Id);
        var groupIds = groups.Select(x => x.Id).ToList();

        List<StudentGroupMember> members;
        List<GroupTaskSubmission> submissions;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var memberQuery = await _memberRepository.GetQueryableAsync();
            members = groupIds.Count == 0
                ? new List<StudentGroupMember>()
                : await AsyncExecuter.ToListAsync(memberQuery.Where(x => groupIds.Contains(x.GroupId)));

            var submissionQuery = await _submissionRepository.GetQueryableAsync();
            submissions = await AsyncExecuter.ToListAsync(submissionQuery.Where(x => x.GroupTaskId == task.Id));
        }

        var userIds = members.Select(x => x.StudentId).Distinct();
        var names = await _support.GetUserNamesAsync(userIds);
        var submissionByGroup = submissions.GroupBy(x => x.GroupId).ToDictionary(g => g.Key, g => g.First());

        // 附件名批量回填
        var resourceIds = submissions
            .Where(x => x.AttachmentResourceId.HasValue)
            .Select(x => x.AttachmentResourceId!.Value)
            .Distinct()
            .ToList();
        var fileNameMap = new Dictionary<Guid, string?>();
        if (resourceIds.Count > 0)
        {
            List<Resource> resources;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                resources = await _resourceRepository.GetListAsync(x => resourceIds.Contains(x.Id));
            }

            fileNameMap = resources.ToDictionary(x => x.Id, x => (string?)x.OriginalFileName);
        }

        var detail = new GroupTaskDetailDto
        {
            Id = task.Id,
            CourseId = task.CourseId,
            CourseTitle = await _support.GetCourseTitleAsync(task.CourseId),
            Title = task.Title,
            Description = task.Description,
            Token = task.Token,
            Status = task.Status,
            GroupMode = task.GroupMode,
            PublishAt = task.PublishAt,
            DueAt = task.DueAt,
            MaxScore = task.MaxScore,
            GroupCount = groups.Count,
            SubmittedGroupCount = submissions.Count,
            StudentCount = members.Count,
            CreationTime = task.CreationTime,
            CreatorId = task.CreatorId,
            LastModificationTime = task.LastModificationTime,
            LastModifierId = task.LastModifierId
        };

        foreach (var group in groups)
        {
            var dto = new StudentGroupDto
            {
                Id = group.Id,
                Name = group.Name,
                SortOrder = group.SortOrder
            };

            foreach (var member in members.Where(x => x.GroupId == group.Id))
            {
                names.TryGetValue(member.StudentId, out var pair);
                dto.Members.Add(new GroupMemberDto
                {
                    StudentId = member.StudentId,
                    StudentName = member.StudentName ?? pair.Name ?? pair.UserName,
                    StudentUserName = pair.UserName
                });
            }

            if (submissionByGroup.TryGetValue(group.Id, out var submission))
            {
                dto.Submission = await MapSubmissionAsync(
                    submission,
                    group.Name,
                    fileNameMap.TryGetValue(submission.AttachmentResourceId ?? Guid.Empty, out var name) ? name : null);
            }

            detail.Groups.Add(dto);
        }

        return detail;
    }

    private static Task<GroupTaskSubmissionDto> MapSubmissionAsync(
        GroupTaskSubmission submission, string? groupName, string? fileName = null)
    {
        return Task.FromResult(new GroupTaskSubmissionDto
        {
            Id = submission.Id,
            GroupTaskId = submission.GroupTaskId,
            GroupId = submission.GroupId,
            GroupName = groupName,
            Content = submission.Content,
            AttachmentResourceId = submission.AttachmentResourceId,
            AttachmentFileName = fileName,
            SubmittedById = submission.SubmittedById,
            SubmittedByName = submission.SubmittedByName,
            SubmittedAt = submission.SubmittedAt,
            Score = submission.Score,
            Feedback = submission.Feedback,
            GradedAt = submission.GradedAt
        });
    }
}
