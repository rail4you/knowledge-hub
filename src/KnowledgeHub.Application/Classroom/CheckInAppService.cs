using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Classroom.Dtos;
using KnowledgeHub.Classroom.Enums;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Permissions;
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
/// 签到。教师发起场次 → 生成短链/二维码 → 学生在有效期内扫码签到。
/// <para>
/// 幂等：同一学生对同一场次重复提交返回 <c>IsNew=false</c>，不报错也不产生第二条记录
/// （(SessionId, StudentId) 唯一索引兜底并发）。
/// </para>
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize]
public class CheckInAppService : KnowledgeHubAppService, ICheckInAppService
{
    private const double DefaultRadiusMeters = 500;

    private readonly IRepository<CheckInSession, Guid> _sessionRepository;
    private readonly IRepository<CheckInRecord, Guid> _recordRepository;
    private readonly ClassroomSupport _support;
    private readonly IStudentActivityLogger _activityLogger;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentUser _currentUser;

    public CheckInAppService(
        IRepository<CheckInSession, Guid> sessionRepository,
        IRepository<CheckInRecord, Guid> recordRepository,
        ClassroomSupport support,
        IStudentActivityLogger activityLogger,
        IDataFilter dataFilter,
        ICurrentUser currentUser)
    {
        _sessionRepository = sessionRepository;
        _recordRepository = recordRepository;
        _support = support;
        _activityLogger = activityLogger;
        _dataFilter = dataFilter;
        _currentUser = currentUser;
    }

    [Authorize(KnowledgeHubPermissions.Classroom.CheckIn)]
    public async Task<PagedResultDto<CheckInSessionDto>> GetListAsync(Guid? courseId, int skipCount, int maxResultCount)
    {
        List<CheckInSession> items;
        int total;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _sessionRepository.GetQueryableAsync();
            query = query.WhereIf(courseId.HasValue, x => x.CourseId == courseId!.Value);
            total = await AsyncExecuter.CountAsync(query);
            items = await AsyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.StartTime).Skip(skipCount).Take(maxResultCount));
        }

        var dtos = new List<CheckInSessionDto>(items.Count);
        foreach (var session in items)
        {
            dtos.Add(await MapWithCountsAsync(session));
        }

        return new PagedResultDto<CheckInSessionDto>(total, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.CheckIn)]
    public async Task<CheckInSessionDto> GetAsync(Guid id)
    {
        var session = await _sessionRepository.GetAsync(id);
        return await MapWithCountsAsync(session);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.CheckIn)]
    public async Task<CheckInSessionDto> CreateAsync(CreateCheckInSessionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写签到标题。");
        }

        var course = await _support.GetCourseAsync(input.CourseId);
        var me = _support.CurrentUserIdOrThrow();

        var session = new CheckInSession(
            GuidGenerator.Create(),
            input.CourseId,
            input.Title.Trim(),
            ClassroomSupport.NewToken(),
            me)
        {
            Description = input.Description,
            RequireLocation = input.RequireLocation,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            RadiusMeters = input.RadiusMeters,
            StartTime = DateTime.UtcNow
        };

        if (input.DurationMinutes.HasValue && input.DurationMinutes.Value > 0)
        {
            session.EndTime = DateTime.UtcNow.AddMinutes(input.DurationMinutes.Value);
        }

        // 课程归属租户跟随课程，不跟随操作者（共建库课程由他校老师发起时也不写错租户）
        session.TenantId = course.TenantId;

        await _sessionRepository.InsertAsync(session);
        return await MapWithCountsAsync(session);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.CheckIn)]
    public async Task<CheckInSessionDto> CloseAsync(Guid id)
    {
        var session = await _sessionRepository.GetAsync(id);
        session.Close();
        await _sessionRepository.UpdateAsync(session);
        return await MapWithCountsAsync(session);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.CheckIn)]
    public async Task DeleteAsync(Guid id)
    {
        List<CheckInRecord> records;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _recordRepository.GetQueryableAsync();
            records = await AsyncExecuter.ToListAsync(query.Where(x => x.SessionId == id));
        }

        foreach (var record in records)
        {
            await _recordRepository.DeleteAsync(record);
        }

        await _sessionRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.CheckIn)]
    public async Task<CheckInRosterDto> GetRosterAsync(Guid sessionId)
    {
        var session = await _sessionRepository.GetAsync(sessionId);
        var enrolled = await _support.GetEnrolledStudentIdsAsync(session.CourseId);

        List<CheckInRecord> records;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _recordRepository.GetQueryableAsync();
            records = await AsyncExecuter.ToListAsync(
                query.Where(x => x.SessionId == sessionId).OrderBy(x => x.CheckedInAt));
        }

        var userIds = records.Select(x => x.StudentId).Concat(enrolled).Distinct();
        var names = await _support.GetUserNamesAsync(userIds);

        var checkedIds = records.Select(x => x.StudentId).ToHashSet();
        var roster = new CheckInRosterDto { SessionId = sessionId };

        foreach (var record in records)
        {
            names.TryGetValue(record.StudentId, out var pair);
            roster.CheckedIn.Add(new CheckInRecordDto
            {
                Id = record.Id,
                SessionId = record.SessionId,
                StudentId = record.StudentId,
                StudentName = record.StudentName ?? pair.Name ?? pair.UserName,
                StudentUserName = pair.UserName,
                CheckedInAt = record.CheckedInAt,
                Location = record.Location
            });
        }

        foreach (var studentId in enrolled.Where(x => !checkedIds.Contains(x)))
        {
            names.TryGetValue(studentId, out var pair);
            roster.Absent.Add(new CheckInAbsentDto
            {
                StudentId = studentId,
                StudentName = pair.Name ?? pair.UserName,
                StudentUserName = pair.UserName
            });
        }

        return roster;
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Participate)]
    public async Task<DoCheckInResultDto> DoCheckInAsync(DoCheckInInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Token))
        {
            throw new UserFriendlyException("签到链接无效。");
        }

        // token → 场次（token 不含租户，按 token 反查后切租户上下文）
        CheckInSession? session;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            session = await _sessionRepository.FirstOrDefaultAsync(x => x.Token == input.Token);
        }

        if (session == null)
        {
            throw new UserFriendlyException("签到码无效或已失效。");
        }

        var me = _support.CurrentUserIdOrThrow();
        if (_currentUser.TenantId.HasValue && session.TenantId.HasValue &&
            _currentUser.TenantId.Value != session.TenantId.Value)
        {
            throw new UserFriendlyException("这个签到码不属于你所在的学校。");
        }

        if (!session.IsOpen(DateTime.UtcNow))
        {
            throw new UserFriendlyException("签到已结束。");
        }

        // 与讨论/课堂练习/小组任务口径一致：只有选修了该课程的学生才计入签到
        await _support.EnsureEnrolledAsync(session.CourseId);

        // 定位校验（只校验半径，不做人脸核验）
        if (session.RequireLocation && session.Latitude.HasValue && session.Longitude.HasValue)
        {
            if (!input.Latitude.HasValue || !input.Longitude.HasValue)
            {
                throw new UserFriendlyException("本次签到要求定位，请允许浏览器获取位置后重试。");
            }

            var distance = DistanceMeters(
                session.Latitude.Value, session.Longitude.Value, input.Latitude.Value, input.Longitude.Value);
            var radius = session.RadiusMeters ?? DefaultRadiusMeters;
            if (distance > radius)
            {
                throw new UserFriendlyException($"你当前距离签到点约 {Math.Round(distance)} 米，超出允许范围 {radius} 米。");
            }
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _recordRepository.GetQueryableAsync();
            var existing = await AsyncExecuter.FirstOrDefaultAsync(
                query.Where(x => x.SessionId == session.Id && x.StudentId == me));

            if (existing != null)
            {
                return new DoCheckInResultDto
                {
                    SessionId = session.Id,
                    Title = session.Title,
                    CheckedInAt = existing.CheckedInAt,
                    IsNew = false
                };
            }
        }

        var record = new CheckInRecord(GuidGenerator.Create(), session.Id, me)
        {
            StudentName = await _support.GetDisplayNameAsync(me),
            CheckedInAt = DateTime.UtcNow,
            TenantId = session.TenantId,
            Location = input.Latitude.HasValue && input.Longitude.HasValue
                ? $"{input.Latitude},{input.Longitude}"
                : null
        };

        await _recordRepository.InsertAsync(record);

        await _activityLogger.LogAsync(
            me, StudentActivityAction.CheckIn, session.CourseId, "check-in", session.Id);

        return new DoCheckInResultDto
        {
            SessionId = session.Id,
            Title = session.Title,
            CheckedInAt = record.CheckedInAt,
            IsNew = true
        };
    }

    public async Task<List<MyCheckInRecordDto>> GetMyRecordsAsync([FromQuery] Guid? courseId = null)
    {
        var me = _support.CurrentUserIdOrThrow();

        List<CheckInRecord> records;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _recordRepository.GetQueryableAsync();
            records = await AsyncExecuter.ToListAsync(
                query.Where(x => x.StudentId == me).OrderByDescending(x => x.CheckedInAt).Take(200));
        }

        if (records.Count == 0)
        {
            return new List<MyCheckInRecordDto>();
        }

        var sessionIds = records.Select(x => x.SessionId).Distinct().ToList();
        List<CheckInSession> sessions;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _sessionRepository.GetQueryableAsync();
            sessions = await AsyncExecuter.ToListAsync(query.Where(x => sessionIds.Contains(x.Id)));
        }

        var sessionMap = sessions.ToDictionary(x => x.Id);
        var result = new List<MyCheckInRecordDto>();
        foreach (var record in records)
        {
            if (!sessionMap.TryGetValue(record.SessionId, out var session))
            {
                continue;
            }

            if (courseId.HasValue && session.CourseId != courseId.Value)
            {
                continue;
            }

            result.Add(new MyCheckInRecordDto
            {
                SessionId = session.Id,
                SessionTitle = session.Title,
                CourseId = session.CourseId,
                CourseTitle = await _support.GetCourseTitleAsync(session.CourseId),
                CheckedInAt = record.CheckedInAt,
                StartTime = session.StartTime
            });
        }

        return result;
    }

    private async Task<CheckInSessionDto> MapWithCountsAsync(CheckInSession session)
    {
        var enrolled = await _support.GetEnrolledStudentIdsAsync(session.CourseId);

        List<CheckInRecord> records;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _recordRepository.GetQueryableAsync();
            records = await AsyncExecuter.ToListAsync(query.Where(x => x.SessionId == session.Id));
        }

        var me = _currentUser.Id;
        return new CheckInSessionDto
        {
            Id = session.Id,
            CourseId = session.CourseId,
            CourseTitle = await _support.GetCourseTitleAsync(session.CourseId),
            Title = session.Title,
            Description = session.Description,
            Token = session.Token,
            Status = session.IsOpen(DateTime.UtcNow) ? CheckInSessionStatus.Active : CheckInSessionStatus.Closed,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            RequireLocation = session.RequireLocation,
            Latitude = session.Latitude,
            Longitude = session.Longitude,
            RadiusMeters = session.RadiusMeters,
            CheckedInCount = records.Count,
            ExpectedCount = enrolled.Count,
            ICheckedIn = me.HasValue && records.Any(x => x.StudentId == me.Value),
            CreationTime = session.CreationTime,
            CreatorId = session.CreatorId,
            LastModificationTime = session.LastModificationTime,
            LastModifierId = session.LastModifierId
        };
    }

    /// <summary>Haversine 距离（米）。</summary>
    private static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadius = 6371000;
        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double deg) => deg * Math.PI / 180.0;
}
