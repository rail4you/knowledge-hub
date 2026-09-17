using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.Learning.Dtos;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Majors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Learning;

[IgnoreAntiforgeryToken]
[Authorize]
public class LearningAppService : ApplicationService, ILearningAppService
{
    private readonly IRepository<StudentCourse> _studentCourseRepository;
    private readonly IRepository<LearningProgress> _progressRepository;
    private readonly IRepository<KnowledgeMastery> _masteryRepository;
    private readonly IRepository<StudentExerciseRecord> _exerciseRecordRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IRepository<KnowledgeResource> _knowledgeResourceRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<ChapterExercise, Guid> _chapterExerciseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly ICurrentUser _currentUser;

    public LearningAppService(
        IRepository<StudentCourse> studentCourseRepository,
        IRepository<LearningProgress> progressRepository,
        IRepository<KnowledgeMastery> masteryRepository,
        IRepository<StudentExerciseRecord> exerciseRecordRepository,
        ICourseRepository courseRepository,
        IRepository<KnowledgeResource> knowledgeResourceRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<ChapterExercise, Guid> chapterExerciseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        ICurrentUser currentUser)
    {
        _studentCourseRepository = studentCourseRepository;
        _progressRepository = progressRepository;
        _masteryRepository = masteryRepository;
        _exerciseRecordRepository = exerciseRecordRepository;
        _courseRepository = courseRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _exerciseRepository = exerciseRepository;
        _chapterExerciseRepository = chapterExerciseRepository;
        _chapterRepository = chapterRepository;
        _currentUser = currentUser;
    }

    public async Task<LearningDashboardDto> GetDashboardAsync()
    {
        var studentId = _currentUser.Id ?? throw new Volo.Abp.AbpException("User not found");
        
        // 已退课不计入仪表盘（与详情页 IsEnrolled 口径一致：Dropped 视为未选课）
        var studentCourses = (await _studentCourseRepository.GetListAsync(x => x.StudentId == studentId))
            .Where(x => x.Status != StudentCourseStatus.Dropped)
            .ToList();

        // 口径统一：排除课程已被删除的孤儿选课，与 GetMyCoursesAsync（找不到 course 则跳过）一致。
        // 否则 TotalCourses 会大于“我的课程”列表数（曾出现 Hero 显示 10 但列表只有 7）。
        if (studentCourses.Count > 0)
        {
            var enrolledCourseIds = studentCourses.Select(x => x.CourseId).Distinct().ToList();
            var effectiveTenantId = CurrentTenant.Id ?? _currentUser.TenantId;
            List<Guid> existingCourseIds;
            using (DataFilter.Disable<Volo.Abp.MultiTenancy.IMultiTenant>())
            {
                var existingCourses = await _courseRepository.GetListAsync(c => enrolledCourseIds.Contains(c.Id));
                existingCourseIds = existingCourses
                    .Where(c => !effectiveTenantId.HasValue || c.TenantId == effectiveTenantId)
                    .Select(c => c.Id)
                    .ToList();
            }
            var existingSet = new HashSet<Guid>(existingCourseIds);
            studentCourses = studentCourses.Where(x => existingSet.Contains(x.CourseId)).ToList();
        }
        
        // 从 LearningProgress 表计算真实学习时长（分钟）
        var allProgress = await _progressRepository.GetListAsync(x => x.StudentId == studentId);
        var totalMinutes = allProgress.Sum(x => (decimal)x.TimeSpent.TotalMinutes);

        // 统计习题练习次数和资源学习次数
        var exerciseRecords = await _exerciseRecordRepository.GetListAsync(x => x.StudentId == studentId);
        var totalExerciseCount = exerciseRecords.Count;
        var totalResourceCount = allProgress.Count(x => x.ResourceId.HasValue);
        
        var dashboard = new LearningDashboardDto
        {
            TotalCourses = studentCourses.Count,
            CompletedCourses = studentCourses.Count(x => x.Status == StudentCourseStatus.Completed),
            InProgressCourses = studentCourses.Count(x => x.Status == StudentCourseStatus.InProgress),
            NotStartedCourses = studentCourses.Count(x => x.Status == StudentCourseStatus.Enrolled),
            TotalLearningTime = totalMinutes,
            TotalExerciseRecords = totalExerciseCount,
            TotalResourceActivities = totalResourceCount,
            AverageProgress = studentCourses.Count > 0 
                ? studentCourses.Average(x => x.Progress) 
                : 0
        };
        
        // 最近 7 天每日学习活动次数（习题提交 + 资源学习）
        var today = DateTime.UtcNow.Date;
        var last7Days = Enumerable.Range(0, 7)
            .Select(offset => today.AddDays(-offset))
            .Reverse()
            .ToList();
        
        dashboard.DailyTimeLabels = last7Days
            .Select(d => d.ToString("MM-dd"))
            .ToList();

        // 按日期统计学习活动次数：习题提交 + 资源学习
        var exerciseByDay = exerciseRecords
            .GroupBy(r => r.CreationTime.Date)
            .ToDictionary(g => g.Key, g => g.Count());
        var progressByDay = allProgress
            .GroupBy(p => p.LastAccessAt.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        dashboard.DailyTimeValues = last7Days
            .Select(day =>
            {
                var count = 0;
                exerciseByDay.TryGetValue(day, out var exCount);
                progressByDay.TryGetValue(day, out var pCount);
                return (decimal)(exCount + pCount);
            })
            .ToList();

        // 课程名 lookup：用最近学习列表涉及的 5 门课程做一次批量查询，
        // 比 GetMyCoursesAsync 里的 N+1 循环（每门课一次 FindAsync）更高效。
        var recentCourseIds = studentCourses
            .OrderByDescending(x => x.LastModificationTime)
            .Take(5)
            .Select(sc => sc.CourseId)
            .Distinct()
            .ToList();
        var recentCourses = recentCourseIds.Count == 0
            ? new List<KnowledgeHub.Courses.Course>()
            : await _courseRepository.GetListAsync(c => recentCourseIds.Contains(c.Id));
        var courseMap = recentCourses.ToDictionary(c => c.Id, c => c.Title ?? "未命名课程");

        dashboard.RecentLearning = studentCourses
            .OrderByDescending(x => x.LastModificationTime)
            .Take(5)
            .Select(sc => new RecentLearningDto
            {
                CourseId = sc.CourseId,
                // 关键修复：原实现硬编码 "Course " + GUID 前 8 位作为课程名，
                // 导致学生仪表盘"最近学习"显示 "Course 3a219eac" 这种不可读字串。
                // 改为按 CourseId 查 Course 表，用真实 Title；找不到时回退到默认占位。
                CourseName = courseMap.TryGetValue(sc.CourseId, out var name) ? name : "未命名课程",
                Progress = sc.Progress,
                LastAccessAt = sc.LastModificationTime ?? DateTime.UtcNow
            })
            .ToList();

        return dashboard;
    }

    public async Task<List<StudentCourseListItemDto>> GetMyCoursesAsync()
    {
        var studentId = _currentUser.Id ?? throw new Volo.Abp.AbpException("User not found");

        // 已退课不属于“我的课程”（与详情页 IsEnrolled 口径一致）
        var studentCourses = (await _studentCourseRepository.GetListAsync(x => x.StudentId == studentId))
            .Where(x => x.Status != StudentCourseStatus.Dropped)
            .ToList();

        var result = new List<StudentCourseListItemDto>();
        var effectiveTenantId = CurrentTenant.Id ?? _currentUser.TenantId;

        var majorIds = new HashSet<Guid>();
        // 课程查询禁用租户过滤器：历史跨租户选课的课程仍需解析，否则“我的课程”会丢数据
        using (DataFilter.Disable<Volo.Abp.MultiTenancy.IMultiTenant>())
        {
        foreach (var sc in studentCourses)
        {
            var course = await _courseRepository.FindAsync(sc.CourseId);
            if (course?.MajorId is Guid mid)
            {
                majorIds.Add(mid);
            }
        }
        }

        var majorMap = new Dictionary<Guid, string>();
        if (majorIds.Count > 0)
        {
            var majorRepo = LazyServiceProvider.LazyGetRequiredService<IRepository<Major, Guid>>();
            var majorQuery = await majorRepo.GetQueryableAsync();
            var majors = await AsyncExecuter.ToListAsync(majorQuery.Where(m => majorIds.Contains(m.Id)));
            foreach (var m in majors)
            {
                majorMap[m.Id] = m.Name;
            }
        }

        foreach (var sc in studentCourses)
        {
            Courses.Course? course;
            using (DataFilter.Disable<Volo.Abp.MultiTenancy.IMultiTenant>())
            {
                course = await _courseRepository.FindAsync(sc.CourseId);
            }
            if (course != null)
            {
                if (effectiveTenantId.HasValue && course.TenantId != effectiveTenantId)
                {
                    continue;
                }
                string? majorName = null;
                if (course.MajorId.HasValue && majorMap.TryGetValue(course.MajorId.Value, out var name))
                {
                    majorName = name;
                }
                result.Add(new StudentCourseListItemDto
                {
                    Id = sc.Id,
                    CourseId = sc.CourseId,
                    CourseTitle = course.Title,
                    CourseCoverImageUrl = course.CoverImageUrl,
                    MajorId = course.MajorId,
                    MajorName = majorName,
                    Semester = course.Semester,
                    Status = sc.Status,
                    EnrolledAt = sc.EnrolledAt,
                    StartedAt = sc.StartedAt,
                    CompletedAt = sc.CompletedAt,
                    Progress = sc.Progress,
                    Credits = course.Credits
                });
            }
        }

        // 统一口径：学习进度 = 该学生已学习章节习题数 / 全部可学习章节习题数。
        // 与教师端统计对齐：只计已关联到章节的习题（主章节 + 复用关联表去重），题库备选题不计入分母；
        // 已学习数 = 该生提交记录去重后与分母取交集，避免已删除习题导致分子越界。
        if (result.Count > 0)
        {
            var progressMap = await GetExerciseProgressMapAsync(studentId, result.Select(r => r.CourseId).Distinct().ToList());
            foreach (var item in result)
            {
                if (progressMap.TryGetValue(item.CourseId, out var p))
                {
                    item.CompletedExerciseCount = p.CompletedCount;
                    item.TotalExerciseCount = p.TotalCount;
                    item.Progress = p.TotalCount > 0
                        ? Math.Round((decimal)p.CompletedCount / p.TotalCount * 100, 2)
                        : 0;
                }
            }
        }

        return result;
    }

    public async Task<CourseExerciseProgressDto> GetMyExerciseProgressAsync(Guid courseId)
    {
        var studentId = _currentUser.Id ?? throw new Volo.Abp.AbpException("User not found");
        var map = await GetExerciseProgressMapAsync(studentId, new List<Guid> { courseId });
        if (map.TryGetValue(courseId, out var p))
        {
            return new CourseExerciseProgressDto
            {
                CourseId = courseId,
                CompletedCount = p.CompletedCount,
                TotalCount = p.TotalCount
            };
        }
        return new CourseExerciseProgressDto { CourseId = courseId };
    }

    /// <summary>
    /// 批量计算某学生多门课程的习题进度（已学习数/总数）。
    /// 分母 = 学生能学到的全部习题：已关联到本课程章节的习题（主章节 + 复用关联表去重）。
    /// 未挂到任何章节的备选题学生在章节学习页根本打不开，不计入分母（与教师端统计口径一致，
    /// 也与学习页按章节分组汇总的本地计算一致）。
    /// 分子 = 该生提交记录去重后与分母取交集，避免已删除习题导致越界。
    /// </summary>
    private async Task<Dictionary<Guid, (int CompletedCount, int TotalCount)>> GetExerciseProgressMapAsync(
        Guid studentId, List<Guid> courseIds)
    {
        var map = courseIds.Distinct().ToDictionary(id => id, _ => (CompletedCount: 0, TotalCount: 0));
        if (courseIds.Count == 0)
        {
            return map;
        }

        List<Exercise> exercises;
        List<Chapter> chapters;
        List<ChapterExercise> links;
        List<StudentExerciseRecord> records;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
            exercises = await exerciseQuery.Where(e => courseIds.Contains(e.CourseId)).ToListAsync();

            var chapterQuery = await _chapterRepository.GetQueryableAsync();
            chapters = await chapterQuery.Where(c => courseIds.Contains(c.CourseId)).ToListAsync();

            var chapterIds = chapters.Select(c => c.Id).ToList();
            var exerciseIds = new HashSet<Guid>(exercises.Select(e => e.Id));
            if (chapterIds.Count > 0)
            {
                var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();
                links = await ceQuery
                    .Where(ce => chapterIds.Contains(ce.ChapterId) || exerciseIds.Contains(ce.ExerciseId))
                    .ToListAsync();
            }
            else
            {
                links = new List<ChapterExercise>();
            }

            var recordQuery = await _exerciseRecordRepository.GetQueryableAsync();
            records = await recordQuery
                .Where(r => r.StudentId == studentId && courseIds.Contains(r.CourseId))
                .ToListAsync();
        }

        var chapterCourseMap = chapters.ToDictionary(c => c.Id, c => c.CourseId);
        var exerciseCourseMap = exercises.ToDictionary(e => e.Id, e => e.CourseId);

        // 题目 -> 所属章节（主章节 + 复用关联表，且章节必须属于同一课程）
        var exerciseChapters = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var e in exercises)
        {
            if (e.ChapterId.HasValue
                && chapterCourseMap.TryGetValue(e.ChapterId.Value, out var cc)
                && cc == e.CourseId)
            {
                if (!exerciseChapters.TryGetValue(e.Id, out var set))
                {
                    set = new HashSet<Guid>();
                    exerciseChapters[e.Id] = set;
                }
                set.Add(e.ChapterId.Value);
            }
        }
        foreach (var ce in links)
        {
            if (!exerciseCourseMap.TryGetValue(ce.ExerciseId, out var ec))
            {
                continue;
            }
            if (!chapterCourseMap.TryGetValue(ce.ChapterId, out var cc) || cc != ec)
            {
                continue;
            }
            if (!exerciseChapters.TryGetValue(ce.ExerciseId, out var set))
            {
                set = new HashSet<Guid>();
                exerciseChapters[ce.ExerciseId] = set;
            }
            set.Add(ce.ChapterId);
        }

        var totalByCourse = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var kv in exerciseChapters)
        {
            var courseId = exerciseCourseMap[kv.Key];
            if (!totalByCourse.TryGetValue(courseId, out var set))
            {
                set = new HashSet<Guid>();
                totalByCourse[courseId] = set;
            }
            set.Add(kv.Key);
        }

        var completedByCourse = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var r in records)
        {
            // 每条提交即视为已学习（SetAnswer 必写 CompletedAt）；仍要求 CompletedAt 有值以对齐教师端口径
            if (!r.CompletedAt.HasValue)
            {
                continue;
            }
            if (!totalByCourse.TryGetValue(r.CourseId, out var totalSet) || !totalSet.Contains(r.ExerciseId))
            {
                continue;
            }
            if (!completedByCourse.TryGetValue(r.CourseId, out var set))
            {
                set = new HashSet<Guid>();
                completedByCourse[r.CourseId] = set;
            }
            set.Add(r.ExerciseId);
        }

        foreach (var courseId in map.Keys.ToList())
        {
            var total = totalByCourse.TryGetValue(courseId, out var t) ? t.Count : 0;
            var completed = completedByCourse.TryGetValue(courseId, out var c) ? c.Count : 0;
            map[courseId] = (Math.Min(completed, total), total);
        }

        return map;
    }

    public async Task<LearningProgressDto> GetProgressAsync(Guid courseId)
    {
        var studentId = _currentUser.Id ?? throw new Volo.Abp.AbpException("User not found");

        var progressList = await _progressRepository.GetListAsync(
            x => x.StudentId == studentId && x.CourseId == courseId);

        var totalProgress = progressList.Count == 0
            ? 0
            : progressList.Sum(x => x.Progress) / progressList.Count;
        var totalTime = progressList.Sum(x => x.TimeSpent.TotalMinutes);
        var lastAccess = progressList.Count == 0
            ? DateTime.MinValue
            : progressList.Max(x => x.LastAccessAt);

        return new LearningProgressDto
        {
            CourseId = courseId,
            Progress = totalProgress,
            TimeSpent = TimeSpan.FromMinutes(totalTime),
            LastAccessAt = lastAccess
        };
    }

    public async Task<LearningProgressDto> RecordProgressAsync(RecordProgressInput input)
    {
        var studentId = _currentUser.Id ?? throw new Volo.Abp.AbpException("User not found");

        // 退课/未选课禁止上报学习进度：有退课情况肯定不能进入学习，直接拦截写操作
        var enrollment = await _studentCourseRepository.FirstOrDefaultAsync(
            x => x.StudentId == studentId && x.CourseId == input.CourseId && x.Status != StudentCourseStatus.Dropped);
        if (enrollment == null)
        {
            throw new Volo.Abp.UserFriendlyException("未选课，不能访问该课程学习页");
        }
        
        var progress = await _progressRepository.FirstOrDefaultAsync(
            x => x.StudentId == studentId 
                && x.CourseId == input.CourseId 
                && x.ResourceId == input.ResourceId);
        
        if (progress == null)
        {
            progress = new LearningProgress(
                GuidGenerator.Create(),
                studentId,
                input.CourseId);
            
            progress.ChapterId = input.ChapterId;
            progress.ResourceId = input.ResourceId;
            
            await _progressRepository.InsertAsync(progress);
        }
        
        progress.UpdateProgress(
            input.Progress, 
            input.LastPosition, 
            TimeSpan.FromMinutes(input.AdditionalMinutes));
        
        await _progressRepository.UpdateAsync(progress);
        
        var studentCourse = await _studentCourseRepository.FirstOrDefaultAsync(
            x => x.StudentId == studentId && x.CourseId == input.CourseId);
        
        if (studentCourse != null)
        {
            // 首次记录学习进度时，将状态从 Enrolled 转为 InProgress
            if (studentCourse.Status == StudentCourseStatus.Enrolled)
            {
                studentCourse.StartLearning();
            }

            var allProgress = await _progressRepository.GetListAsync(
                x => x.StudentId == studentId && x.CourseId == input.CourseId);

            if (allProgress.Count > 0)
            {
                studentCourse.UpdateProgress(allProgress.Sum(x => x.Progress) / allProgress.Count);
                await _studentCourseRepository.UpdateAsync(studentCourse);
            }
        }
        
        return ObjectMapper.Map<LearningProgress, LearningProgressDto>(progress);
    }

    public async Task<List<KnowledgeMasteryDto>> GetKnowledgeMasteryAsync(Guid courseId)
    {
        var studentId = _currentUser.Id ?? throw new Volo.Abp.AbpException("User not found");

        var masteryList = await _masteryRepository.GetListAsync(
            x => x.StudentId == studentId);

        // 关键修复：和 GetDashboardAsync 同样的问题——之前用 "Knowledge " + GUID 前 8 位
        // 拼成"知识点名称"，前端会显示 "Knowledge 7f3a1b2c" 这种无意义字串。
        // 改为按 KnowledgeResourceId 批量查 KnowledgeResource 表，用真实 Name。
        var resourceIds = masteryList
            .Where(m => m.KnowledgeResourceId != Guid.Empty)
            .Select(m => m.KnowledgeResourceId)
            .Distinct()
            .ToList();
        var resources = resourceIds.Count == 0
            ? new List<KnowledgeResource>()
            : await _knowledgeResourceRepository.GetListAsync(r => resourceIds.Contains(r.Id));
        var resourceMap = resources.ToDictionary(r => r.Id, r => r.Name ?? "未命名知识点");

        return masteryList.Select(m => new KnowledgeMasteryDto
        {
            Id = m.Id,
            KnowledgeResourceId = m.KnowledgeResourceId,
            KnowledgeResourceName = resourceMap.TryGetValue(m.KnowledgeResourceId, out var n) ? n : "未命名知识点",
            Level = m.Level,
            PracticeCount = m.PracticeCount,
            CorrectCount = m.CorrectCount,
            Accuracy = m.Accuracy,
            LastPracticeAt = m.LastPracticeAt
        }).ToList();
    }
}
