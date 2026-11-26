using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Classroom.Dtos;
using KnowledgeHub.Classroom.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Classroom;

/// <summary>
/// 免登录短链（扫码）token 解析。
/// <para>
/// 设计要点：
/// ① 匿名可调用 —— 学生扫码后先看到"这是什么场次"，再决定是否登录参与；
/// ② token 里**不带租户** —— service 端按 token 查出所属租户再 <c>CurrentTenant.Change</c>，
///    因此 URL 很短，也不会被浏览器残留的 <c>__tenant</c> cookie 污染；
/// ③ 只回会话元信息，**绝不返回名单 / 成绩 / 题目** —— token 一旦泄露也不能枚举个人信息。
/// </para>
/// </summary>
[IgnoreAntiforgeryToken]
public class ClassroomTokenAppService : KnowledgeHubAppService, IClassroomTokenAppService
{
    private readonly IRepository<CheckInSession, Guid> _checkInSessionRepository;
    private readonly IRepository<CheckInRecord, Guid> _checkInRecordRepository;
    private readonly IRepository<GroupTask, Guid> _groupTaskRepository;
    private readonly IRepository<StudentGroup, Guid> _studentGroupRepository;
    private readonly IRepository<StudentGroupMember, Guid> _studentGroupMemberRepository;
    private readonly IRepository<GroupTaskSubmission, Guid> _groupTaskSubmissionRepository;
    private readonly IRepository<PracticeSession, Guid> _practiceSessionRepository;
    private readonly IRepository<PracticeAttempt, Guid> _practiceAttemptRepository;
    private readonly IRepository<Courses.Course, Guid> _courseRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentUser _currentUser;

    public ClassroomTokenAppService(
        IRepository<CheckInSession, Guid> checkInSessionRepository,
        IRepository<CheckInRecord, Guid> checkInRecordRepository,
        IRepository<GroupTask, Guid> groupTaskRepository,
        IRepository<StudentGroup, Guid> studentGroupRepository,
        IRepository<StudentGroupMember, Guid> studentGroupMemberRepository,
        IRepository<GroupTaskSubmission, Guid> groupTaskSubmissionRepository,
        IRepository<PracticeSession, Guid> practiceSessionRepository,
        IRepository<PracticeAttempt, Guid> practiceAttemptRepository,
        IRepository<Courses.Course, Guid> courseRepository,
        IDataFilter dataFilter,
        ICurrentUser currentUser)
    {
        _checkInSessionRepository = checkInSessionRepository;
        _checkInRecordRepository = checkInRecordRepository;
        _groupTaskRepository = groupTaskRepository;
        _studentGroupRepository = studentGroupRepository;
        _studentGroupMemberRepository = studentGroupMemberRepository;
        _groupTaskSubmissionRepository = groupTaskSubmissionRepository;
        _practiceSessionRepository = practiceSessionRepository;
        _practiceAttemptRepository = practiceAttemptRepository;
        _courseRepository = courseRepository;
        _dataFilter = dataFilter;
        _currentUser = currentUser;
    }

    [AllowAnonymous]
    public async Task<ClassroomTokenSessionDto> ResolveAsync(ResolveClassroomTokenInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Token))
        {
            throw new UserFriendlyException("链接无效。");
        }

        var token = input.Token.Trim();

        return input.Kind switch
        {
            ClassroomTokenKind.CheckIn => await ResolveCheckInAsync(token),
            ClassroomTokenKind.GroupTask => await ResolveGroupTaskAsync(token),
            ClassroomTokenKind.Practice => await ResolvePracticeAsync(token),
            _ => throw new UserFriendlyException("不支持的链接类型。")
        };
    }

    private async Task<ClassroomTokenSessionDto> ResolveCheckInAsync(string token)
    {
        CheckInSession? session;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            session = await _checkInSessionRepository.FirstOrDefaultAsync(x => x.Token == token);
        }

        if (session == null)
        {
            throw new UserFriendlyException("签到码无效或已失效。");
        }

        var dto = await BuildBaseAsync(
            ClassroomTokenKind.CheckIn, session.Id, session.TenantId, session.CourseId, session.Title);

        dto.Description = session.Description;
        dto.StartTime = session.StartTime;
        dto.EndTime = session.EndTime;

        var isOpen = session.IsOpen(DateTime.UtcNow);
        dto.IsOpen = isOpen;
        dto.Status = isOpen ? "进行中" : "已结束";

        var me = _currentUser.Id;
        if (me.HasValue)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var query = await _checkInRecordRepository.GetQueryableAsync();
                var existing = await AsyncExecuter.FirstOrDefaultAsync(
                    query.Where(x => x.SessionId == session.Id && x.StudentId == me.Value));
                dto.Participated = existing != null;
            }
        }

        return dto;
    }

    private async Task<ClassroomTokenSessionDto> ResolvePracticeAsync(string token)
    {
        PracticeSession? session;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            session = await _practiceSessionRepository.FirstOrDefaultAsync(x => x.Token == token);
        }

        if (session == null)
        {
            throw new UserFriendlyException("练习链接无效或已失效。");
        }

        var dto = await BuildBaseAsync(
            ClassroomTokenKind.Practice, session.Id, session.TenantId, session.CourseId, session.Title);

        dto.QuestionCount = session.QuestionCount;
        dto.StartTime = session.StartedAt;
        dto.EndTime = session.EndedAt;
        dto.IsOpen = session.IsOpen;
        dto.Status = session.IsOpen ? "进行中" : "已结束";

        var me = _currentUser.Id;
        if (me.HasValue)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var query = await _practiceAttemptRepository.GetQueryableAsync();
                var attempt = await AsyncExecuter.FirstOrDefaultAsync(
                    query.Where(x => x.SessionId == session.Id && x.StudentId == me.Value));
                dto.Participated = attempt != null;
            }
        }

        return dto;
    }

    private async Task<ClassroomTokenSessionDto> ResolveGroupTaskAsync(string token)
    {
        GroupTask? task;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            task = await _groupTaskRepository.FirstOrDefaultAsync(x => x.Token == token);
        }

        if (task == null)
        {
            throw new UserFriendlyException("任务链接无效或已失效。");
        }

        var dto = await BuildBaseAsync(
            ClassroomTokenKind.GroupTask, task.Id, task.TenantId, task.CourseId, task.Title);

        dto.Description = task.Description;
        dto.DueAt = task.DueAt;
        dto.MaxScore = task.MaxScore;
        dto.IsOpen = task.CanAcceptSubmission(DateTime.UtcNow);
        dto.Status = task.Status switch
        {
            GroupTaskStatus.Draft => "未发布",
            GroupTaskStatus.Published => dto.IsOpen ? "进行中" : "已截止",
            _ => "已结束"
        };

        var me = _currentUser.Id;
        if (me.HasValue)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var groupQuery = await _studentGroupRepository.GetQueryableAsync();
                var groupIds = await AsyncExecuter.ToListAsync(
                    groupQuery.Where(x => x.GroupTaskId == task.Id).Select(x => x.Id));

                if (groupIds.Count > 0)
                {
                    var memberQuery = await _studentGroupMemberRepository.GetQueryableAsync();
                    var member = await AsyncExecuter.FirstOrDefaultAsync(
                        memberQuery.Where(x => groupIds.Contains(x.GroupId) && x.StudentId == me.Value));

                    if (member != null)
                    {
                        var group = await AsyncExecuter.FirstOrDefaultAsync(
                            groupQuery.Where(x => x.Id == member.GroupId));
                        dto.MyGroupName = group?.Name;

                        var submissionQuery = await _groupTaskSubmissionRepository.GetQueryableAsync();
                        var submission = await AsyncExecuter.FirstOrDefaultAsync(
                            submissionQuery.Where(x => x.GroupTaskId == task.Id && x.GroupId == member.GroupId));
                        dto.Participated = submission != null;
                    }
                }
            }
        }

        return dto;
    }

    /// <summary>公共部分：课程名 + 学校名（跨租户读取，禁用过滤器）。</summary>
    private async Task<ClassroomTokenSessionDto> BuildBaseAsync(
        ClassroomTokenKind kind, Guid sessionId, Guid? tenantId, Guid courseId, string title)
    {
        string? courseTitle = null;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var course = await _courseRepository.FindAsync(courseId);
            courseTitle = course?.Title;
        }

        return new ClassroomTokenSessionDto
        {
            Kind = kind,
            SessionId = sessionId,
            TenantId = tenantId ?? Guid.Empty,
            CourseId = courseId,
            CourseTitle = courseTitle,
            Title = title
        };
    }
}
