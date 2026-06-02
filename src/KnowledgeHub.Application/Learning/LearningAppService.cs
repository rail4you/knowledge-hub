using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning.Dtos;
using KnowledgeHub.Learning.Enums;
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
    private readonly ICurrentUser _currentUser;

    public LearningAppService(
        IRepository<StudentCourse> studentCourseRepository,
        IRepository<LearningProgress> progressRepository,
        IRepository<KnowledgeMastery> masteryRepository,
        ICourseRepository courseRepository,
        ICurrentUser currentUser)
    {
        _studentCourseRepository = studentCourseRepository;
        _progressRepository = progressRepository;
        _masteryRepository = masteryRepository;
        _courseRepository = courseRepository;
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
        
        dashboard.RecentLearning = studentCourses
            .OrderByDescending(x => x.LastModificationTime)
            .Take(5)
            .Select(sc => new RecentLearningDto
            {
                CourseId = sc.CourseId,
                CourseName = "Course " + sc.CourseId.ToString()[..8],
                Progress = sc.Progress,
                LastAccessAt = sc.LastModificationTime ?? DateTime.UtcNow
            })
            .ToList();
        
        return dashboard;
    }

    public async Task<List<StudentCourseDto>> GetMyCoursesAsync()
    {
        var studentId = _currentUser.Id ?? throw new Volo.Abp.AbpException("User not found");
        
        var studentCourses = await _studentCourseRepository.GetListAsync(x => x.StudentId == studentId);
        
        var result = new List<StudentCourseDto>();
        
        foreach (var sc in studentCourses)
        {
            var course = await _courseRepository.FindAsync(sc.CourseId);
            if (course != null)
            {
                result.Add(new StudentCourseDto
                {
                    Id = sc.Id,
                    CourseId = sc.CourseId,
                    CourseTitle = course.Title,
                    CourseCoverImageUrl = course.CoverImageUrl,
                    Major = course.Major,
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
        
        return masteryList.Select(m => new KnowledgeMasteryDto
        {
            Id = m.Id,
            KnowledgeResourceId = m.KnowledgeResourceId,
            KnowledgeResourceName = "Knowledge " + m.KnowledgeResourceId.ToString()[..8],
            Level = m.Level,
            PracticeCount = m.PracticeCount,
            CorrectCount = m.CorrectCount,
            Accuracy = m.Accuracy,
            LastPracticeAt = m.LastPracticeAt
        }).ToList();
    }
}
