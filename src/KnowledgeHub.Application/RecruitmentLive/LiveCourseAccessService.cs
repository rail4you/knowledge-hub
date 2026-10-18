using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using RecruitmentLiveEntity = KnowledgeHub.RecruitmentLive.RecruitmentLive;
using LiveType = KnowledgeHub.RecruitmentLive.LiveType;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.RecruitmentLiveService;

/// <summary>用户在某个直播间里的角色</summary>
public enum LiveAccessRole
{
    /// <summary>无权进入</summary>
    None = 0,

    /// <summary>教师侧（直播创建人 / 课程主讲教师 / 管理员代管）</summary>
    Teacher = 1,

    /// <summary>学生侧（受邀参与者 / 选课学生）</summary>
    Student = 2
}

/// <summary>
/// 课程直播的可见性与进入判定。
///
/// 规则：课程直播面向「该课程的选课学生 + 课程教师 + 直播创建人 + 管理员」，
/// 因此选了同一门课的学生之间可以互看互连，教师也能进入自己课程的直播。
/// 招聘直播（LiveType.Recruitment）不走这里，仍以参与者表为准。
/// </summary>
public interface ILiveCourseAccessService
{
    /// <summary>判定用户在某个直播间中的角色（非课程直播一律返回 None）</summary>
    Task<LiveAccessRole> ResolveRoleAsync(RecruitmentLiveEntity live, Guid userId);

    /// <summary>用户已选（未退课）的课程 ID 列表</summary>
    Task<List<Guid>> GetEnrolledCourseIdsAsync(Guid userId);

    /// <summary>用户是否已选某门课（未退课）</summary>
    Task<bool> IsEnrolledAsync(Guid userId, Guid courseId);
}

public class LiveCourseAccessService : ILiveCourseAccessService
{
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly ICourseRepository _courseRepository;

    public LiveCourseAccessService(
        IRepository<StudentCourse, Guid> studentCourseRepository,
        ICourseRepository courseRepository)
    {
        _studentCourseRepository = studentCourseRepository;
        _courseRepository = courseRepository;
    }

    public async Task<LiveAccessRole> ResolveRoleAsync(RecruitmentLiveEntity live, Guid userId)
    {
        if (live.Type != LiveType.Course || !live.CourseId.HasValue)
        {
            return LiveAccessRole.None;
        }

        // 直播创建人：任何类型的直播创建者都算教师侧
        if (live.TeacherId == userId)
        {
            return LiveAccessRole.Teacher;
        }

        // 课程主讲教师：可以进入自己所授课程的任何一场课程直播（哪怕不是他创建的）
        var course = await _courseRepository.FirstOrDefaultAsync(c => c.Id == live.CourseId.Value);
        if (course?.TeacherId == userId)
        {
            return LiveAccessRole.Teacher;
        }

        return await IsEnrolledAsync(userId, live.CourseId.Value)
            ? LiveAccessRole.Student
            : LiveAccessRole.None;
    }

    public async Task<List<Guid>> GetEnrolledCourseIdsAsync(Guid userId)
    {
        var query = await _studentCourseRepository.GetQueryableAsync();
        return await query
            .Where(sc => sc.StudentId == userId && sc.Status != StudentCourseStatus.Dropped)
            .Select(sc => sc.CourseId)
            .ToListAsync();
    }

    public async Task<bool> IsEnrolledAsync(Guid userId, Guid courseId)
    {
        return await _studentCourseRepository.AnyAsync(sc =>
            sc.StudentId == userId &&
            sc.CourseId == courseId &&
            sc.Status != StudentCourseStatus.Dropped);
    }
}
