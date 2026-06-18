using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning.Dtos;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Majors;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace KnowledgeHub.Learning;

[IgnoreAntiforgeryToken]
public class LearningAppService : ApplicationService, ILearningAppService
{
    private readonly IRepository<StudentCourse> _studentCourseRepository;
    private readonly IRepository<LearningProgress> _progressRepository;
    private readonly IRepository<KnowledgeMastery> _masteryRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IRepository<KnowledgeResource> _knowledgeResourceRepository;
    private readonly ICurrentUser _currentUser;

    public LearningAppService(
        IRepository<StudentCourse> studentCourseRepository,
        IRepository<LearningProgress> progressRepository,
        IRepository<KnowledgeMastery> masteryRepository,
        ICourseRepository courseRepository,
        IRepository<KnowledgeResource> knowledgeResourceRepository,
        ICurrentUser currentUser)
    {
        _studentCourseRepository = studentCourseRepository;
        _progressRepository = progressRepository;
        _masteryRepository = masteryRepository;
        _courseRepository = courseRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _currentUser = currentUser;
    }

    public async Task<LearningDashboardDto> GetDashboardAsync()
    {
        var studentId = _currentUser.Id ?? throw new Volo.Abp.AbpException("User not found");
        
        var studentCourses = await _studentCourseRepository.GetListAsync(x => x.StudentId == studentId);
        
        var dashboard = new LearningDashboardDto
        {
            TotalCourses = studentCourses.Count,
            CompletedCourses = studentCourses.Count(x => x.Status == StudentCourseStatus.Completed),
            InProgressCourses = studentCourses.Count(x => x.Status == StudentCourseStatus.InProgress),
            NotStartedCourses = studentCourses.Count(x => x.Status == StudentCourseStatus.Enrolled),
            TotalLearningTime = studentCourses.Sum(x => (decimal)x.Progress) / 100 * 60,
            AverageProgress = studentCourses.Count > 0 
                ? studentCourses.Average(x => x.Progress) 
                : 0
        };
        
        var last7Days = Enumerable.Range(0, 7)
            .Select(offset => DateTime.UtcNow.AddDays(-offset).ToString("yyyy-MM-dd"))
            .ToList();
        
        dashboard.DailyTimeLabels = last7Days;
        dashboard.DailyTimeValues = last7Days.Select(_ => 0m).ToList();
        
        dashboard.KnowledgeDimensions = new List<KnowledgeDimensionDto>
        {
            new() { Name = "基础知识" },
            new() { Name = "应用能力" },
            new() { Name = "创新能力" },
            new() { Name = "分析能力" },
            new() { Name = "综合能力" }
        };
        dashboard.MasteryValues = new List<decimal> { 60, 50, 40, 70, 55 };

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

        var studentCourses = await _studentCourseRepository.GetListAsync(x => x.StudentId == studentId);

        var result = new List<StudentCourseListItemDto>();

        var majorIds = new HashSet<Guid>();
        foreach (var sc in studentCourses)
        {
            var course = await _courseRepository.FindAsync(sc.CourseId);
            if (course?.MajorId is Guid mid)
            {
                majorIds.Add(mid);
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
            var course = await _courseRepository.FindAsync(sc.CourseId);
            if (course != null)
            {
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

        return result;
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
