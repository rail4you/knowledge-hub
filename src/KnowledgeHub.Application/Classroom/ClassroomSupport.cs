using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Classroom;

/// <summary>
/// 课堂互动四件套共用的小工具：token 生成、课程校验、选课名单、学生姓名批量解析。
/// <para>
/// 抽出来是为了让四个 AppService 不必各自复制一遍「关租户过滤器 + 名字回填」的逻辑，
/// 也保证「谁算选课学生」这套口径只有一处定义。
/// </para>
/// </summary>
public class ClassroomSupport : ITransientDependency
{
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IDataFilter _dataFilter;

    public ClassroomSupport(
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        ICurrentUser currentUser,
        IPermissionChecker permissionChecker,
        IDataFilter dataFilter)
    {
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
        _dataFilter = dataFilter;
    }

    /// <summary>
    /// 生成短链 token：22 位 URL-safe 随机串（约 132 bit 熵）。
    /// 不用 GUID 字符串是为了短链好看、二维码不密。
    /// </summary>
    public static string NewToken()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>取课程（跨租户），不存在抛友好错误。</summary>
    public async Task<Course> GetCourseAsync(Guid courseId)
    {
        Course? course;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(courseId);
        }

        return course ?? throw new UserFriendlyException("课程不存在。");
    }

    /// <summary>课程标题（批量场景用单查，量小）。</summary>
    public async Task<string?> GetCourseTitleAsync(Guid courseId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var course = await _courseRepository.FindAsync(courseId);
            return course?.Title;
        }
    }

    /// <summary>选课学生 Id 集合（不含已退课）——「应参与人数」的口径。</summary>
    public async Task<List<Guid>> GetEnrolledStudentIdsAsync(Guid courseId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _studentCourseRepository.GetQueryableAsync();
            return query
                .Where(x => x.CourseId == courseId && x.Status != StudentCourseStatus.Dropped)
                .Select(x => x.StudentId)
                .Distinct()
                .ToList();
        }
    }

    /// <summary>学生是否选修该课程（学生侧写操作的统一闸门）。</summary>
    public async Task EnsureEnrolledAsync(Guid courseId)
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var enrolled = await GetEnrolledStudentIdsAsync(courseId);
        if (!enrolled.Contains(studentId))
        {
            throw new UserFriendlyException("未选修该课程，不能参与该课程的互动活动。");
        }
    }

    /// <summary>
    /// 「可以参与互动」的判定：**本课教师 / 有课程编辑权限的管理员** 直接放行，其余必须是选课学生。
    /// <para>
    /// 与 <see cref="EnsureEnrolledAsync"/> 的区别：讨论答疑必须有老师能回答，
    /// 如果一律要求"已选课"，老师会被自己建的答疑区挡在门外（E2E 回归时发现的真实问题）。
    /// 签到 / 课堂练习 / 小组任务仍用 <see cref="EnsureEnrolledAsync"/> ——
    /// 它们会写"学生记录"（计入名单与统计），老师参与会污染数据。
    /// </para>
    /// </summary>
    public async Task EnsureCanParticipateAsync(Guid courseId)
    {
        var me = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");

        if (await _permissionChecker.IsGrantedAsync(KnowledgeHub.Permissions.KnowledgeHubPermissions.Courses.Edit))
        {
            return; // 教师 / 院校管理员
        }

        var course = await GetCourseAsync(courseId);
        if (course.TeacherId.HasValue && course.TeacherId.Value == me)
        {
            return; // 本课负责人
        }

        await EnsureEnrolledAsync(courseId);
    }

    /// <summary>当前登录用户 Id（未登录抛错）。</summary>
    public Guid CurrentUserIdOrThrow() =>
        _currentUser.Id ?? throw new UserFriendlyException("请先登录。");

    /// <summary>批量取学生显示名 + 用户名（跨租户；缺失时回退 用户名 / Id 前 8 位）。</summary>
    public async Task<Dictionary<Guid, (string? Name, string? UserName)>> GetUserNamesAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, (string?, string?)>();
        }

        List<IdentityUser> users;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            users = await _userRepository.GetListAsync(x => ids.Contains(x.Id));
        }

        return users.ToDictionary(
            x => x.Id,
            x => ((string?)(string.IsNullOrWhiteSpace(x.Name) ? null : x.Name), (string?)x.UserName));
    }

    /// <summary>单个用户的显示名。</summary>
    public async Task<string?> GetDisplayNameAsync(Guid userId)
    {
        var map = await GetUserNamesAsync(new[] { userId });
        if (!map.TryGetValue(userId, out var pair))
        {
            return null;
        }

        return pair.Name ?? pair.UserName;
    }
}
