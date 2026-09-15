using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.Exams.Enums;
using KnowledgeHub.Learning.Dtos;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Learning;

[IgnoreAntiforgeryToken]
[Authorize]
public class StudentExerciseRecordAppService : KnowledgeHubAppService, IStudentExerciseRecordAppService
{
    private readonly IRepository<StudentExerciseRecord, Guid> _recordRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<LearningProgress, Guid> _learningProgressRepository;
    private readonly IRepository<ChapterExercise, Guid> _chapterExerciseRepository;
    private readonly ICurrentTenant _currentTenant;

    public StudentExerciseRecordAppService(
        IRepository<StudentExerciseRecord, Guid> recordRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<LearningProgress, Guid> learningProgressRepository,
        IRepository<ChapterExercise, Guid> chapterExerciseRepository,
        ICurrentTenant currentTenant)
    {
        _recordRepository = recordRepository;
        _exerciseRepository = exerciseRepository;
        _studentCourseRepository = studentCourseRepository;
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _userRepository = userRepository;
        _learningProgressRepository = learningProgressRepository;
        _chapterExerciseRepository = chapterExerciseRepository;
        _currentTenant = currentTenant;
    }

    private Guid? ResolveTenantFilter(Guid? inputTenantId)
    {
        if (_currentTenant.Id.HasValue)
            return _currentTenant.Id;
        return inputTenantId;
    }

    #region Student APIs

    public async Task<StudentExerciseRecordDto> SaveOrUpdateRecordAsync(SaveExerciseRecordInput input)
    {
        var studentId = CurrentUser.GetId();

        // 退课/未选课禁止提交习题：有退课情况肯定不能进入学习，直接拦截写操作
        StudentCourse? enrollment;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var scQuery = await _studentCourseRepository.GetQueryableAsync();
            enrollment = await scQuery.FirstOrDefaultAsync(
                x => x.StudentId == studentId && x.CourseId == input.CourseId && x.Status != StudentCourseStatus.Dropped);
        }
        if (enrollment == null)
        {
            throw new UserFriendlyException("未选课，不能访问该课程学习页");
        }

        // Find existing record
        StudentExerciseRecord? record;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _recordRepository.GetQueryableAsync();
            record = await query.FirstOrDefaultAsync(
                x => x.StudentId == studentId && x.ExerciseId == input.ExerciseId);
        }

        // Get exercise for auto-grading
        Exercise? exercise;
        using (DataFilter.Disable<IMultiTenant>())
        {
            exercise = await _exerciseRepository.FirstOrDefaultAsync(x => x.Id == input.ExerciseId);
        }

        bool? isCorrect = null;
        if (exercise != null)
        {
            isCorrect = AutoGrade(exercise, input.StudentAnswer);
        }

        if (record == null)
        {
            record = new StudentExerciseRecord(GuidGenerator.Create(), studentId, input.CourseId, input.ExerciseId)
            {
                ChapterId = input.ChapterId
            };
            record.SetAnswer(input.StudentAnswer, isCorrect);
            record.AddTimeSpent(TimeSpan.FromTicks(input.TimeSpentTicks));
            await _recordRepository.InsertAsync(record);
        }
        else
        {
            record.SetAnswer(input.StudentAnswer, isCorrect);
            record.AddTimeSpent(TimeSpan.FromTicks(input.TimeSpentTicks));
            if (input.ChapterId.HasValue)
                record.ChapterId = input.ChapterId;
            await _recordRepository.UpdateAsync(record);
        }

        return await MapToDtoAsync(record);
    }

    public async Task<PagedResultDto<StudentExerciseRecordDto>> GetRecordsByCourseAsync(GetStudentExerciseRecordsInput input)
    {
        if (CurrentUser.Id == null)
        {
            return new PagedResultDto<StudentExerciseRecordDto>(0, new List<StudentExerciseRecordDto>());
        }

        var studentId = CurrentUser.GetId();

        List<StudentExerciseRecord> items;
        long totalCount;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _recordRepository.GetQueryableAsync();
            query = query
                .Where(x => x.StudentId == studentId && x.CourseId == input.CourseId)
                .WhereIf(input.ChapterId.HasValue, x => x.ChapterId == input.ChapterId);

            totalCount = await query.LongCountAsync();
            items = await query
                .OrderByDescending(x => x.CreationTime)
                .PageBy(input.SkipCount, input.MaxResultCount)
                .ToListAsync();
        }

        var dtos = await MapToDtoListAsync(items);
        return new PagedResultDto<StudentExerciseRecordDto>(totalCount, dtos);
    }

    public async Task<PagedResultDto<StudentExerciseRecordDto>> GetRecordsByChapterAsync(GetStudentExerciseRecordsInput input)
    {
        input.ChapterId ??= default;
        return await GetRecordsByCourseAsync(input);
    }

    public async Task<PagedResultDto<StudentExerciseRecordDto>> GetMyRecentRecordsAsync(GetMyRecentRecordsInput input)
    {
        if (CurrentUser.Id == null)
        {
            return new PagedResultDto<StudentExerciseRecordDto>(0, new List<StudentExerciseRecordDto>());
        }

        var studentId = CurrentUser.GetId();

        List<StudentExerciseRecord> items;
        long totalCount;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _recordRepository.GetQueryableAsync();
            query = query.Where(x => x.StudentId == studentId);

            if (input.CourseId.HasValue)
            {
                query = query.Where(x => x.CourseId == input.CourseId.Value);
            }

            if (input.IsCorrect.HasValue)
            {
                if (input.IsCorrect.Value == 1)
                {
                    query = query.Where(x => x.IsCorrect == true);
                }
                else if (input.IsCorrect.Value == 0)
                {
                    query = query.Where(x => x.IsCorrect == false);
                }
            }

            totalCount = await query.LongCountAsync();
            items = await query
                .OrderByDescending(x => x.CreationTime)
                .PageBy(input.SkipCount, input.MaxResultCount)
                .ToListAsync();
        }

        var dtos = await MapToDtoListAsync(items);
        return new PagedResultDto<StudentExerciseRecordDto>(totalCount, dtos);
    }

    public async Task MarkAnswerViewedAsync(MarkAnswerViewedInput input)
    {
        var studentId = CurrentUser.GetId();

        StudentExerciseRecord record;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _recordRepository.GetQueryableAsync();
            record = await query.FirstOrDefaultAsync(
                x => x.StudentId == studentId && x.ExerciseId == input.ExerciseId && x.CourseId == input.CourseId)
                ?? throw new UserFriendlyException("未找到作答记录");
        }

        record.MarkAnswerViewed();
        await _recordRepository.UpdateAsync(record);
    }

    public async Task SubmitSelfAssessmentAsync(SubmitSelfAssessmentInput input)
    {
        var studentId = CurrentUser.GetId();

        StudentExerciseRecord record;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _recordRepository.GetQueryableAsync();
            record = await query.FirstOrDefaultAsync(
                x => x.StudentId == studentId && x.ExerciseId == input.ExerciseId && x.CourseId == input.CourseId)
                ?? throw new UserFriendlyException("未找到作答记录");
        }

        record.SetSelfAssessment(input.Assessment);
        await _recordRepository.UpdateAsync(record);
    }

    public async Task<ListResultDto<ChapterProgressDto>> GetChapterProgressAsync(Guid courseId)
    {
        var studentId = CurrentUser.GetId();

        List<StudentExerciseRecord> records;
        List<Exercise> exercises;
        List<Chapter> chapters;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var recordQuery = await _recordRepository.GetQueryableAsync();
            records = await recordQuery
                .Where(x => x.StudentId == studentId && x.CourseId == courseId)
                .ToListAsync();

            var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
            exercises = await exerciseQuery.Where(x => x.CourseId == courseId).ToListAsync();

            var chapterQuery = await _chapterRepository.GetQueryableAsync();
            chapters = await chapterQuery.Where(x => x.CourseId == courseId).ToListAsync();
        }

        var result = chapters.Select(ch =>
        {
            var chapterExercises = exercises.Where(e => e.ChapterId == ch.Id).ToList();
            var chapterRecords = records.Where(r => r.ChapterId == ch.Id).ToList();
            var completedCount = chapterRecords.Count(r => r.CompletedAt.HasValue);
            var gradedRecords = chapterRecords.Where(r => r.IsCorrect.HasValue).ToList();
            var correctCount = gradedRecords.Count(r => r.IsCorrect!.Value);

            return new ChapterProgressDto
            {
                ChapterId = ch.Id,
                ChapterName = ch.Title,
                TotalExercises = chapterExercises.Count,
                CompletedCount = completedCount,
                CompletionRate = chapterExercises.Count > 0
                    ? Math.Round((decimal)completedCount / chapterExercises.Count * 100, 1)
                    : 0,
                CorrectRate = gradedRecords.Count > 0
                    ? Math.Round((decimal)correctCount / gradedRecords.Count * 100, 1)
                    : 0
            };
        }).ToList();

        return new ListResultDto<ChapterProgressDto>(result);
    }

    #endregion

    #region Teacher APIs

    [Authorize(KnowledgeHubPermissions.Learning.ViewStatistics)]
    public async Task<PagedResultDto<StudentExerciseRecordDto>> GetStudentRecordsAsync(GetStudentExerciseRecordsInput input, Guid studentId)
    {
        List<StudentExerciseRecord> items;
        long totalCount;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _recordRepository.GetQueryableAsync();
            query = query
                .Where(x => x.StudentId == studentId && x.CourseId == input.CourseId)
                .WhereIf(input.ChapterId.HasValue, x => x.ChapterId == input.ChapterId);

            totalCount = await query.LongCountAsync();
            items = await query
                .OrderByDescending(x => x.CreationTime)
                .PageBy(input.SkipCount, input.MaxResultCount)
                .ToListAsync();
        }

        var dtos = await MapToDtoListAsync(items);
        return new PagedResultDto<StudentExerciseRecordDto>(totalCount, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Learning.ViewStatistics)]
    public async Task<PagedResultDto<StudentLearningStatisticsDto>> GetLearningStatisticsAsync(GetLearningStatisticsInput input)
    {
        var tenantFilter = ResolveTenantFilter(input.TenantId);

        // Get enrolled students for the course
        List<StudentCourse> studentCourses;
        List<StudentExerciseRecord> allRecords;
        List<LearningProgress> learningProgresses;
        List<Exercise> allExercises;
        List<ChapterExercise> chapterExerciseLinks;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var scQuery = await _studentCourseRepository.GetQueryableAsync();
            studentCourses = await scQuery
                .Where(sc => sc.CourseId == input.CourseId)
                .WhereIf(tenantFilter.HasValue, sc => sc.TenantId == tenantFilter!.Value)
                .ToListAsync();

            var recordQuery = await _recordRepository.GetQueryableAsync();
            allRecords = await recordQuery
                .Where(r => r.CourseId == input.CourseId)
                .WhereIf(tenantFilter.HasValue, r => r.TenantId == tenantFilter!.Value)
                .WhereIf(input.StartTime.HasValue, r => r.CompletedAt >= input.StartTime!.Value)
                .WhereIf(input.EndTime.HasValue, r => r.CompletedAt <= input.EndTime!.Value)
                .ToListAsync();

            var progressQuery = await _learningProgressRepository.GetQueryableAsync();
            learningProgresses = await progressQuery
                .Where(p => p.CourseId == input.CourseId)
                .WhereIf(tenantFilter.HasValue, p => p.TenantId == tenantFilter!.Value)
                .WhereIf(input.ChapterId.HasValue, p => p.ChapterId == input.ChapterId!.Value)
                .WhereIf(input.StartTime.HasValue, p => p.LastAccessAt >= input.StartTime!.Value)
                .WhereIf(input.EndTime.HasValue, p => p.LastAccessAt <= input.EndTime!.Value)
                .ToListAsync();

            var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
            allExercises = await exerciseQuery
                .Where(e => e.CourseId == input.CourseId)
                .ToListAsync();

            var chapterQuery = await _chapterRepository.GetQueryableAsync();
            var courseChapterIds = await chapterQuery
                .Where(c => c.CourseId == input.CourseId)
                .Select(c => c.Id)
                .ToListAsync();

            var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();
            chapterExerciseLinks = await ceQuery
                .Where(ce => courseChapterIds.Contains(ce.ChapterId))
                .ToListAsync();
        }

        // 总题数口径：只统计已关联到章节的题目（主章节 Exercise.ChapterId + 复用关联表 ChapterExercise 去重）。
        // 未关联章节的题目（题库备选题）不计入分母，否则完成率会被拉低。
        var exerciseChapterMap = BuildExerciseChapterMap(allExercises, chapterExerciseLinks);
        int exerciseCount;
        if (input.ChapterId.HasValue)
        {
            var chapterId = input.ChapterId.Value;
            exerciseCount = exerciseChapterMap.Count(kv => kv.Value.Contains(chapterId));
        }
        else
        {
            exerciseCount = exerciseChapterMap.Count;
        }

        // 统计口径与 GetCourseLearningOverviewAsync 对齐：学习人数 = 选课 ∪ 做题 ∪ 视频/资源进度 去重，
        // 这样顶部「学习时长(分钟)」与下方每人的时长之和能对应上。
        var allLearnerIds = studentCourses.Select(sc => sc.StudentId)
            .Concat(allRecords.Select(r => r.StudentId))
            .Concat(learningProgresses.Select(p => p.StudentId))
            .Distinct()
            .ToList();
        var studentIds = allLearnerIds.Count > 0 ? allLearnerIds : studentCourses.Select(sc => sc.StudentId).Distinct().ToList();

        // 聚合视频/资源时长与最后活跃时间，按学生分组
        var progressTimeMap = learningProgresses
            .GroupBy(p => p.StudentId)
            .ToDictionary(g => g.Key, g => g.Aggregate(TimeSpan.Zero, (acc, p) => acc + p.TimeSpent));
        var progressLastAccessMap = learningProgresses
            .GroupBy(p => p.StudentId)
            .ToDictionary(g => g.Key, g => g.Max(p => p.LastAccessAt));
        // 选课时间兜底：从未做题、也无视频进度的学生，用选课时间作为最后活跃，保证列表不再空白
        var enrollTimeMap = studentCourses
            .GroupBy(sc => sc.StudentId)
            .ToDictionary(g => g.Key, g => g.Min(sc => sc.EnrolledAt));

        // Load student names
        Dictionary<Guid, string> studentMap;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var userQuery = await _userRepository.GetQueryableAsync();
            studentMap = (await userQuery.Where(u => studentIds.Contains(u.Id)).ToListAsync())
                .ToDictionary(u => u.Id, u => ResolveStudentName(u));
        }

        var statistics = studentIds.Select(studentId =>
        {
            var studentRecords = allRecords.Where(r => r.StudentId == studentId).ToList();
            var completedCount = studentRecords.Count(r => r.CompletedAt.HasValue);
            var gradedRecords = studentRecords.Where(r => r.IsCorrect.HasValue).ToList();
            var correctCount = gradedRecords.Count(r => r.IsCorrect!.Value);
            var exerciseTime = studentRecords.Aggregate(TimeSpan.Zero, (acc, r) => acc + r.TimeSpent);
            var videoTime = progressTimeMap.TryGetValue(studentId, out var pt) ? pt : TimeSpan.Zero;
            var totalTime = exerciseTime + videoTime;

            // 作答时间兜底：CompletedAt 为空的老数据用记录创建时间，保证有作答就有活跃时间
            var lastExerciseTime = studentRecords
                .Select(r => r.CompletedAt ?? r.CreationTime)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();
            var lastProgressTime = progressLastAccessMap.TryGetValue(studentId, out var lat) ? lat : DateTime.MinValue;
            var lastEnrollTime = enrollTimeMap.TryGetValue(studentId, out var et) ? et : DateTime.MinValue;
            var lastActive = new[] { lastExerciseTime, lastProgressTime, lastEnrollTime }.Max();
            DateTime? lastActiveTime = lastActive == DateTime.MinValue ? null : lastActive;

            return new StudentLearningStatisticsDto
            {
                StudentId = studentId,
                // 关键修复：原回退 "" 在用户被删除/软删除时会让导出和列表的「学生姓名」列空白。
                // 与 ResolveStudentName 保持一致：缺失时回退到 `学员#<短ID>`，让老师至少能看出"有这个人"。
                StudentName = studentMap.GetValueOrDefault(studentId, $"学员#{studentId.ToString()[..8]}"),
                CompletedCount = completedCount,
                TotalCount = exerciseCount,
                CompletionRate = exerciseCount > 0
                    ? Math.Round((decimal)completedCount / exerciseCount * 100, 1)
                    : 0,
                CorrectRate = gradedRecords.Count > 0
                    ? Math.Round((decimal)correctCount / gradedRecords.Count * 100, 1)
                    : 0,
                TotalTimeSpent = totalTime,
                LastActiveTime = lastActiveTime
            };
        }).ToList();

        // Apply sorting
        statistics = ApplySorting(statistics, input.Sorting).ToList();

        var totalCount = statistics.Count;
        var paged = statistics
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<StudentLearningStatisticsDto>(totalCount, paged);
    }

    [Authorize(KnowledgeHubPermissions.Learning.ViewStatistics)]
    public async Task<CourseLearningOverviewDto> GetCourseLearningOverviewAsync(GetCourseLearningOverviewInput input)
    {
        var tenantFilter = ResolveTenantFilter(input.TenantId);

        List<StudentCourse> studentCourses;
        List<StudentExerciseRecord> allRecords;
        List<LearningProgress> learningProgresses;
        List<Exercise> allExercises;
        List<Chapter> chapters;
        List<ChapterExercise> chapterExerciseLinks;
        Course course;

        using (DataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.GetAsync(input.CourseId);

            var scQuery = await _studentCourseRepository.GetQueryableAsync();
            studentCourses = await scQuery
                .Where(sc => sc.CourseId == input.CourseId)
                .WhereIf(tenantFilter.HasValue, sc => sc.TenantId == tenantFilter!.Value)
                .ToListAsync();

            var recordQuery = await _recordRepository.GetQueryableAsync();
            allRecords = await recordQuery
                .Where(r => r.CourseId == input.CourseId)
                .WhereIf(tenantFilter.HasValue, r => r.TenantId == tenantFilter!.Value)
                .ToListAsync();

            // P1-9：拉取视频/资源学习进度，覆盖"看完视频但没做题"的场景。
            // 之前的实现只看 StudentExerciseRecord，导致教师课程统计的"学习人数"对纯看视频的学生为 0。
            var progressQuery = await _learningProgressRepository.GetQueryableAsync();
            learningProgresses = await progressQuery
                .Where(p => p.CourseId == input.CourseId)
                .WhereIf(tenantFilter.HasValue, p => p.TenantId == tenantFilter!.Value)
                .ToListAsync();

            var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
            allExercises = await exerciseQuery.Where(e => e.CourseId == input.CourseId).ToListAsync();

            var chapterQuery = await _chapterRepository.GetQueryableAsync();
            chapters = await chapterQuery.Where(c => c.CourseId == input.CourseId).ToListAsync();

            var chapterIds = chapters.Select(c => c.Id).ToList();
            var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();
            chapterExerciseLinks = await ceQuery
                .Where(ce => chapterIds.Contains(ce.ChapterId))
                .ToListAsync();
        }

        // P1-9：学习人数 = 选课学生 ∪ 有学习记录的学生 ∪ 有视频进度的学生（去重）。
        // 这样无论学生是选了课、做题、还是看完视频，都能被计入"学习人数"。
        var allLearnerIds = studentCourses.Select(sc => sc.StudentId)
            .Concat(allRecords.Select(r => r.StudentId))
            .Concat(learningProgresses.Select(p => p.StudentId))
            .Distinct()
            .Count();

        // 活跃学生：有任意学习行为（做题或看视频）的学生
        var activeStudentIds = allRecords
            .Where(r => r.CompletedAt.HasValue)
            .Select(r => r.StudentId)
            .Concat(learningProgresses.Select(p => p.StudentId))
            .Distinct()
            .Count();

        // P1-9：累计学习时长（分钟）— 把视频观看时长也计入。
        var totalTimeMinutes = learningProgresses.Sum(p => p.TimeSpent.TotalMinutes)
            + allRecords.Sum(r => r.TimeSpent.TotalMinutes);

        var gradedRecords = allRecords.Where(r => r.IsCorrect.HasValue).ToList();
        var completedRecords = allRecords.Where(r => r.CompletedAt.HasValue).ToList();

        // 总题数口径与 GetLearningStatisticsAsync 对齐：只统计已关联到章节的题目
        // （主章节 Exercise.ChapterId + 复用关联表 ChapterExercise 去重），未关联章节的备选题不计入。
        var exerciseChapterMap = BuildExerciseChapterMap(allExercises, chapterExerciseLinks);
        var associatedExerciseCount = exerciseChapterMap.Count;

        // 章节 -> 题目 ID 集合（去重，只收敛到本课程的章节）
        var chapterExerciseIdsMap = chapters.ToDictionary(c => c.Id, _ => new HashSet<Guid>());
        foreach (var kv in exerciseChapterMap)
        {
            foreach (var chId in kv.Value)
            {
                if (chapterExerciseIdsMap.TryGetValue(chId, out var set)) set.Add(kv.Key);
            }
        }

        bool RecordInChapter(StudentExerciseRecord r, Guid chapterId) =>
            r.ChapterId == chapterId ||
            (exerciseChapterMap.TryGetValue(r.ExerciseId, out var chs) && chs.Contains(chapterId));

        var chapterProgress = chapters.Select(ch =>
        {
            var chTotal = chapterExerciseIdsMap[ch.Id].Count;
            var chRecords = allRecords.Where(r => RecordInChapter(r, ch.Id)).ToList();
            var chCompleted = chRecords.Count(r => r.CompletedAt.HasValue);
            var chGraded = chRecords.Where(r => r.IsCorrect.HasValue).ToList();
            var chCorrect = chGraded.Count(r => r.IsCorrect!.Value);

            return new ChapterProgressDto
            {
                ChapterId = ch.Id,
                ChapterName = ch.Title,
                TotalExercises = chTotal,
                CompletedCount = chCompleted,
                CompletionRate = chTotal > 0
                    ? Math.Round((decimal)chCompleted / chTotal * 100, 1)
                    : 0,
                CorrectRate = chGraded.Count > 0
                    ? Math.Round((decimal)chCorrect / chGraded.Count * 100, 1)
                    : 0,
                ParticipantCount = chRecords
                    .Where(r => r.CompletedAt.HasValue)
                    .Select(r => r.StudentId)
                    .Distinct()
                    .Count()
            };
        })
        // 只显示有习题关联的章节：未挂任何题目的章节不展示，避免空行干扰
        .Where(x => x.TotalExercises > 0)
        .ToList();

        return new CourseLearningOverviewDto
        {
            CourseId = input.CourseId,
            CourseName = course.Title,
            TotalStudents = allLearnerIds,
            ActiveStudents = activeStudentIds,
            TotalExercises = associatedExerciseCount,
            TotalLearningMinutes = (double)Math.Round((decimal)totalTimeMinutes, 1),
            AverageCompletionRate = associatedExerciseCount > 0 && allLearnerIds > 0
                ? Math.Round((decimal)completedRecords.Count / (associatedExerciseCount * allLearnerIds) * 100, 1)
                : 0,
            AverageCorrectRate = gradedRecords.Count > 0
                ? Math.Round((decimal)gradedRecords.Count(r => r.IsCorrect!.Value) / gradedRecords.Count * 100, 1)
                : 0,
            ChapterProgress = chapterProgress
        };
    }

    [Authorize(KnowledgeHubPermissions.Learning.ViewStatistics)]
    public async Task<StudentLearningDetailDto> GetStudentLearningDetailAsync(GetStudentLearningDetailInput input)
    {
        var tenantFilter = ResolveTenantFilter(null);

        // 课程 + 章节 + 习题 + 关联表 + 该生提交记录 + 视频/资源进度，一次取全在内存聚合
        Course? course;
        List<Chapter> chapters;
        List<Exercise> allExercises;
        List<ChapterExercise> chapterExerciseLinks;
        List<StudentExerciseRecord> studentRecords;
        List<LearningProgress> learningProgresses;

        using (DataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FirstOrDefaultAsync(c => c.Id == input.CourseId);

            var recordQuery = await _recordRepository.GetQueryableAsync();
            studentRecords = await recordQuery
                .Where(r => r.StudentId == input.StudentId && r.CourseId == input.CourseId)
                .WhereIf(tenantFilter.HasValue, r => r.TenantId == tenantFilter!.Value)
                .ToListAsync();

            var progressQuery = await _learningProgressRepository.GetQueryableAsync();
            learningProgresses = await progressQuery
                .Where(p => p.StudentId == input.StudentId && p.CourseId == input.CourseId)
                .WhereIf(tenantFilter.HasValue, p => p.TenantId == tenantFilter!.Value)
                .ToListAsync();

            var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
            allExercises = await exerciseQuery.Where(e => e.CourseId == input.CourseId).ToListAsync();

            var chapterQuery = await _chapterRepository.GetQueryableAsync();
            chapters = await chapterQuery.Where(c => c.CourseId == input.CourseId).ToListAsync();

            var chapterIds = chapters.Select(c => c.Id).ToList();
            var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();
            chapterExerciseLinks = await ceQuery
                .Where(ce => chapterIds.Contains(ce.ChapterId))
                .ToListAsync();
        }

        // 学生姓名与登录账号（与导出逻辑一致：缺失时回退到 学员#短ID）
        IdentityUser? user;
        using (DataFilter.Disable<IMultiTenant>())
        {
            user = await _userRepository.FirstOrDefaultAsync(u => u.Id == input.StudentId);
        }
        var studentName = user != null ? ResolveStudentName(user) : $"学员#{input.StudentId.ToString()[..8]}";
        var loginAccount = user?.UserName ?? user?.Email ?? string.Empty;

        // 题目 -> 所属章节映射（主章节 + 复用关联表去重），与列表统计口径完全一致
        var exerciseChapterMap = BuildExerciseChapterMap(allExercises, chapterExerciseLinks);
        // 章节 -> 题目 ID 集合
        var chapterExerciseIdsMap = chapters.ToDictionary(c => c.Id, _ => new HashSet<Guid>());
        foreach (var kv in exerciseChapterMap)
        {
            foreach (var chId in kv.Value)
            {
                if (chapterExerciseIdsMap.TryGetValue(chId, out var set)) set.Add(kv.Key);
            }
        }

        bool RecordInChapter(StudentExerciseRecord r, Guid chapterId) =>
            r.ChapterId == chapterId ||
            (exerciseChapterMap.TryGetValue(r.ExerciseId, out var chs) && chs.Contains(chapterId));

        var exerciseMap = allExercises.ToDictionary(e => e.Id, e => e.Title);
        var chapterMap = chapters.ToDictionary(c => c.Id, c => c.Title);

        StudentExerciseRecordDto ToRecordDto(StudentExerciseRecord r)
        {
            return new StudentExerciseRecordDto
            {
                Id = r.Id,
                StudentId = r.StudentId,
                StudentName = studentName,
                CourseId = r.CourseId,
                CourseName = course?.Title ?? string.Empty,
                ChapterId = r.ChapterId,
                ChapterName = r.ChapterId.HasValue ? chapterMap.GetValueOrDefault(r.ChapterId.Value, string.Empty) : null,
                ExerciseId = r.ExerciseId,
                ExerciseTitle = exerciseMap.GetValueOrDefault(r.ExerciseId, string.Empty),
                StudentAnswer = r.StudentAnswer,
                IsCorrect = r.IsCorrect,
                HasViewedAnswer = r.HasViewedAnswer,
                ViewedAt = r.ViewedAt,
                SelfAssessment = r.SelfAssessment,
                TimeSpent = r.TimeSpent,
                CompletedAt = r.CompletedAt,
                CreationTime = r.CreationTime,
                CreatorId = r.CreatorId,
                LastModificationTime = r.LastModificationTime,
                LastModifierId = r.LastModifierId,
            };
        }

        // 章节明细：只保留关联过题目或学生做过题的章节，避免空章节占位
        var chaptersDetail = chapters.Select(ch =>
        {
            var chTotal = chapterExerciseIdsMap[ch.Id].Count;
            var chRecords = studentRecords.Where(r => RecordInChapter(r, ch.Id)).ToList();
            var chCompleted = chRecords.Count(r => r.CompletedAt.HasValue);
            var chGraded = chRecords.Where(r => r.IsCorrect.HasValue).ToList();
            var chCorrect = chGraded.Count(r => r.IsCorrect!.Value);
            var chTime = chRecords.Aggregate(TimeSpan.Zero, (acc, r) => acc + r.TimeSpent);

            return new StudentChapterLearningDetailDto
            {
                ChapterId = ch.Id,
                ChapterName = ch.Title,
                TotalExercises = chTotal,
                CompletedCount = chCompleted,
                CorrectCount = chCorrect,
                CompletionRate = chTotal > 0
                    ? Math.Round((decimal)chCompleted / chTotal * 100, 1)
                    : 0,
                CorrectRate = chGraded.Count > 0
                    ? Math.Round((decimal)chCorrect / chGraded.Count * 100, 1)
                    : 0,
                TimeSpent = chTime,
                Records = chRecords
                    .OrderByDescending(r => r.CompletedAt ?? r.CreationTime)
                    .Select(ToRecordDto)
                    .ToList()
            };
        })
        .Where(x => x.TotalExercises > 0 || x.Records.Count > 0)
        .OrderBy(x => x.ChapterName)
        .ToList();

        // 汇总（与 GetLearningStatisticsAsync 口径一致）
        var completedCount = studentRecords.Count(r => r.CompletedAt.HasValue);
        var graded = studentRecords.Where(r => r.IsCorrect.HasValue).ToList();
        var correctCount = graded.Count(r => r.IsCorrect!.Value);
        var exerciseTime = studentRecords.Aggregate(TimeSpan.Zero, (acc, r) => acc + r.TimeSpent);
        var videoTime = learningProgresses.Aggregate(TimeSpan.Zero, (acc, p) => acc + p.TimeSpent);
        var totalTime = exerciseTime + videoTime;

        var lastExerciseTime = studentRecords
            .Select(r => r.CompletedAt ?? r.CreationTime)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
        var lastProgressTime = learningProgresses.Count > 0
            ? learningProgresses.Max(p => p.LastAccessAt)
            : DateTime.MinValue;
        var lastActive = new[] { lastExerciseTime, lastProgressTime }.Max();

        return new StudentLearningDetailDto
        {
            StudentId = input.StudentId,
            StudentName = studentName,
            LoginAccount = loginAccount,
            CompletedCount = completedCount,
            TotalCount = exerciseChapterMap.Count,
            CompletionRate = exerciseChapterMap.Count > 0
                ? Math.Round((decimal)completedCount / exerciseChapterMap.Count * 100, 1)
                : 0,
            CorrectRate = graded.Count > 0
                ? Math.Round((decimal)correctCount / graded.Count * 100, 1)
                : 0,
            TotalTimeSpent = totalTime,
            LastActiveTime = lastActive == DateTime.MinValue ? null : lastActive,
            Chapters = chaptersDetail
        };
    }

    [Authorize(KnowledgeHubPermissions.Learning.ViewStatistics)]
    public async Task<TenantCourseStatisticsDto> GetTenantCourseStatisticsAsync(GetTenantCourseStatisticsInput input)
    {
        var tenantFilter = ResolveTenantFilter(input.TenantId);

        List<Course> courses;
        List<StudentCourse> studentCourses;
        List<StudentExerciseRecord> allRecords;
        List<LearningProgress> learningProgresses;
        List<Exercise> allExercises;
        List<Chapter> chapters;
        List<ChapterExercise> chapterExerciseLinks;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var courseQuery = await _courseRepository.GetQueryableAsync();
            courses = await courseQuery
                .WhereIf(tenantFilter.HasValue, c => c.TenantId == tenantFilter!.Value)
                .ToListAsync();

            var courseIds = courses.Select(c => c.Id).ToList();
            if (courseIds.Count == 0) return new TenantCourseStatisticsDto();

            var scQuery = await _studentCourseRepository.GetQueryableAsync();
            studentCourses = await scQuery.Where(sc => courseIds.Contains(sc.CourseId)).ToListAsync();

            var recordQuery = await _recordRepository.GetQueryableAsync();
            allRecords = await recordQuery.Where(r => courseIds.Contains(r.CourseId)).ToListAsync();

            var progressQuery = await _learningProgressRepository.GetQueryableAsync();
            learningProgresses = await progressQuery.Where(p => courseIds.Contains(p.CourseId)).ToListAsync();

            var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
            allExercises = await exerciseQuery.Where(e => courseIds.Contains(e.CourseId)).ToListAsync();

            var chapterQuery = await _chapterRepository.GetQueryableAsync();
            chapters = await chapterQuery.Where(c => courseIds.Contains(c.CourseId)).ToListAsync();

            var chapterIds = chapters.Select(c => c.Id).ToList();
            var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();
            chapterExerciseLinks = await ceQuery.Where(ce => chapterIds.Contains(ce.ChapterId)).ToListAsync();
        }

        // 与单课程口径对齐：题目 -> 所属章节（主章节 + 复用关联表去重），未关联章节的不计入
        var exerciseChapterMap = BuildExerciseChapterMap(allExercises, chapterExerciseLinks);
        var exerciseCourseMap = allExercises.ToDictionary(e => e.Id, e => e.CourseId);

        var items = courses.Select(course =>
        {
            var courseId = course.Id;
            var scs = studentCourses.Where(sc => sc.CourseId == courseId).ToList();
            var recs = allRecords.Where(r => r.CourseId == courseId).ToList();
            var progs = learningProgresses.Where(p => p.CourseId == courseId).ToList();
            var courseChapters = chapters.Where(c => c.CourseId == courseId).ToList();

            var assocExerciseIds = exerciseChapterMap.Keys
                .Where(exId => exerciseCourseMap.TryGetValue(exId, out var ec) && ec == courseId)
                .ToHashSet();
            var linkedChapterCount = courseChapters.Count(ch =>
                assocExerciseIds.Any(exId =>
                    exerciseChapterMap.TryGetValue(exId, out var chs) && chs.Contains(ch.Id)));

            var learnerIds = scs.Select(sc => sc.StudentId)
                .Concat(recs.Select(r => r.StudentId))
                .Concat(progs.Select(p => p.StudentId))
                .Distinct()
                .ToList();
            var activeIds = recs.Where(r => r.CompletedAt.HasValue).Select(r => r.StudentId)
                .Concat(progs.Select(p => p.StudentId))
                .Distinct()
                .ToList();

            var completedCount = recs.Count(r => r.CompletedAt.HasValue);
            var graded = recs.Where(r => r.IsCorrect.HasValue).ToList();
            var correctCount = graded.Count(r => r.IsCorrect!.Value);

            var lastActive = recs.Select(r => r.CompletedAt ?? r.CreationTime)
                .Concat(progs.Select(p => p.LastAccessAt))
                .Concat(scs.Select(sc => sc.EnrolledAt))
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            return new CourseStatisticsItemDto
            {
                CourseId = courseId,
                CourseName = course.Title,
                TotalStudents = learnerIds.Count,
                ActiveStudents = activeIds.Count,
                TotalExercises = assocExerciseIds.Count,
                ChapterCount = linkedChapterCount,
                AverageCompletionRate = assocExerciseIds.Count > 0 && learnerIds.Count > 0
                    ? Math.Round((decimal)completedCount / (assocExerciseIds.Count * learnerIds.Count) * 100, 1)
                    : 0,
                AverageCorrectRate = graded.Count > 0
                    ? Math.Round((decimal)correctCount / graded.Count * 100, 1)
                    : 0,
                LastActiveTime = lastActive == DateTime.MinValue ? null : lastActive
            };
        })
        .OrderByDescending(x => x.TotalStudents)
        .ThenBy(x => x.CourseName)
        .ToList();

        var allLearnerIds = studentCourses.Select(sc => sc.StudentId)
            .Concat(allRecords.Select(r => r.StudentId))
            .Concat(learningProgresses.Select(p => p.StudentId))
            .Distinct()
            .ToList();
        var allActiveIds = allRecords.Where(r => r.CompletedAt.HasValue).Select(r => r.StudentId)
            .Concat(learningProgresses.Select(p => p.StudentId))
            .Distinct()
            .ToList();
        var totalCompleted = allRecords.Count(r => r.CompletedAt.HasValue);
        var totalGraded = allRecords.Where(r => r.IsCorrect.HasValue).ToList();
        // 总完成率分母：各课程（关联题数 × 学习人数）之和，与单课程口径对齐
        var denomSum = items.Sum(x => (long)x.TotalExercises * x.TotalStudents);

        return new TenantCourseStatisticsDto
        {
            TotalCourses = courses.Count,
            TotalStudents = allLearnerIds.Count,
            ActiveStudents = allActiveIds.Count,
            TotalExercises = exerciseChapterMap.Count,
            AverageCompletionRate = denomSum > 0
                ? Math.Round((decimal)totalCompleted / denomSum * 100, 1)
                : 0,
            AverageCorrectRate = totalGraded.Count > 0
                ? Math.Round((decimal)totalGraded.Count(r => r.IsCorrect!.Value) / totalGraded.Count * 100, 1)
                : 0,
            Courses = items
        };
    }

    [Authorize(KnowledgeHubPermissions.Learning.ExportData)]
    public async Task<IRemoteStreamContent> ExportTenantCourseStatisticsAsync(GetTenantCourseStatisticsInput input)
    {
        var result = await GetTenantCourseStatisticsAsync(input);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("课程汇总");

        worksheet.Cell(1, 1).Value = "课程";
        worksheet.Cell(1, 2).Value = "学习人数";
        worksheet.Cell(1, 3).Value = "活跃学生";
        worksheet.Cell(1, 4).Value = "总习题";
        worksheet.Cell(1, 5).Value = "有题章节";
        worksheet.Cell(1, 6).Value = "平均完成率(%)";
        worksheet.Cell(1, 7).Value = "平均正确率(%)";
        worksheet.Cell(1, 8).Value = "最后活跃时间";

        var headerRange = worksheet.Range(1, 1, 1, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        for (int i = 0; i < result.Courses.Count; i++)
        {
            var item = result.Courses[i];
            var row = i + 2;
            worksheet.Cell(row, 1).Value = item.CourseName;
            worksheet.Cell(row, 2).Value = item.TotalStudents;
            worksheet.Cell(row, 3).Value = item.ActiveStudents;
            worksheet.Cell(row, 4).Value = item.TotalExercises;
            worksheet.Cell(row, 5).Value = item.ChapterCount;
            worksheet.Cell(row, 6).Value = item.AverageCompletionRate;
            worksheet.Cell(row, 7).Value = item.AverageCorrectRate;
            worksheet.Cell(row, 8).Value = item.LastActiveTime?.ToString("yyyy-MM-dd HH:mm") ?? "";
        }

        // 总计行：跨课程数据不做简单相加（学习人数已跨课程去重），放在表尾便于核对
        var totalRow = result.Courses.Count + 2;
        worksheet.Cell(totalRow, 1).Value = "合计（去重后）";
        worksheet.Cell(totalRow, 2).Value = result.TotalStudents;
        worksheet.Cell(totalRow, 3).Value = result.ActiveStudents;
        worksheet.Cell(totalRow, 4).Value = result.TotalExercises;
        worksheet.Cell(totalRow, 6).Value = result.AverageCompletionRate;
        worksheet.Cell(totalRow, 7).Value = result.AverageCorrectRate;
        worksheet.Range(totalRow, 1, totalRow, 8).Style.Font.Bold = true;

        worksheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);

        return new RemoteStreamContent(stream, $"课程统计_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(KnowledgeHubPermissions.Learning.ExportData)]
    public async Task<IRemoteStreamContent> ExportLearningStatisticsAsync(GetLearningStatisticsInput input)
    {
        input.SkipCount = 0;
        input.MaxResultCount = int.MaxValue;

        var result = await GetLearningStatisticsAsync(input);

        // 关键修复 P1-17：批量预加载学生登录账号（UserName），
        // 避免在写每行 Excel 时 N+1 查 IdentityUser。空表时跳过。
        var studentIds = result.Items.Select(i => i.StudentId).Distinct().ToList();
        Dictionary<Guid, string> loginAccountMap = new();
        if (studentIds.Count > 0)
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                var userQuery = await _userRepository.GetQueryableAsync();
                loginAccountMap = (await userQuery.Where(u => studentIds.Contains(u.Id)).ToListAsync())
                    .ToDictionary(u => u.Id, u => u.UserName ?? u.Email ?? string.Empty);
            }
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("学习统计");

        // Headers
        worksheet.Cell(1, 1).Value = "学生姓名";
        // 关键修复 P1-17：在姓名右侧加一列"登录账号"，当 ResolveStudentName 落到 UserName/邮箱
        // 回退分支时，老师也能从登录账号反查学生（学生一般记得自己的登录账号）。
        worksheet.Cell(1, 2).Value = "登录账号";
        worksheet.Cell(1, 3).Value = "完成数";
        worksheet.Cell(1, 4).Value = "总题数";
        worksheet.Cell(1, 5).Value = "完成率(%)";
        worksheet.Cell(1, 6).Value = "正确率(%)";
        worksheet.Cell(1, 7).Value = "总用时";
        worksheet.Cell(1, 8).Value = "最后活跃时间";

        // Style header
        var headerRange = worksheet.Range(1, 1, 1, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Data
        for (int i = 0; i < result.Items.Count; i++)
        {
            var item = result.Items[i];
            var row = i + 2;
            worksheet.Cell(row, 1).Value = item.StudentName;
            // 关键修复：登录账号缺失（用户被删除）时不要留空，避免和正常用户混在一起时无法区分。
            // 复用 `学员#<短ID>` 与姓名列保持一致，老师一眼看出"该用户已注销"。
            worksheet.Cell(row, 2).Value = loginAccountMap.TryGetValue(item.StudentId, out var acct) && !string.IsNullOrEmpty(acct)
                ? acct
                : $"学员#{item.StudentId.ToString()[..8]}";
            worksheet.Cell(row, 3).Value = item.CompletedCount;
            worksheet.Cell(row, 4).Value = item.TotalCount;
            worksheet.Cell(row, 5).Value = item.CompletionRate;
            worksheet.Cell(row, 6).Value = item.CorrectRate;
            worksheet.Cell(row, 7).Value = item.TotalTimeSpent.ToString(@"hh\:mm\:ss");
            worksheet.Cell(row, 8).Value = item.LastActiveTime?.ToString("yyyy-MM-dd HH:mm") ?? "";
        }

        worksheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);

        return new RemoteStreamContent(stream, $"学习统计_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// 构建「题目 -&gt; 所属章节」映射（主章节 Exercise.ChapterId + 复用关联表 ChapterExercise 去重合并）。
    /// 未关联任何章节的题目（题库备选题）不会出现在映射中；关联表里指向已不在本课程题目集合中的脏数据会被忽略。
    /// </summary>
    private static Dictionary<Guid, HashSet<Guid>> BuildExerciseChapterMap(
        List<Exercise> exercises, List<ChapterExercise> links)
    {
        var exerciseIds = new HashSet<Guid>(exercises.Select(e => e.Id));
        var map = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var e in exercises)
        {
            if (!e.ChapterId.HasValue) continue;
            if (!map.TryGetValue(e.Id, out var set)) { set = new HashSet<Guid>(); map[e.Id] = set; }
            set.Add(e.ChapterId.Value);
        }
        foreach (var ce in links)
        {
            if (!exerciseIds.Contains(ce.ExerciseId)) continue;
            if (!map.TryGetValue(ce.ExerciseId, out var set)) { set = new HashSet<Guid>(); map[ce.ExerciseId] = set; }
            set.Add(ce.ChapterId);
        }
        return map;
    }

    private bool? AutoGrade(Exercise exercise, string? studentAnswer)
    {
        if (string.IsNullOrWhiteSpace(studentAnswer) || string.IsNullOrWhiteSpace(exercise.Answer))
            return null;

        return exercise.Type switch
        {
            ExerciseType.SingleChoice =>
                string.Equals(NormalizeChoiceToken(studentAnswer), NormalizeChoiceToken(exercise.Answer), StringComparison.OrdinalIgnoreCase),
            ExerciseType.TrueFalse =>
                GradeTrueFalse(studentAnswer, exercise.Answer),
            ExerciseType.MultiChoice =>
                AreSetsEqual(studentAnswer, exercise.Answer),
            ExerciseType.FillBlank =>
                string.Equals(studentAnswer.Trim(), exercise.Answer.Trim(), StringComparison.OrdinalIgnoreCase),
            // Subjective questions: no auto-grade
            ExerciseType.ShortAnswer or ExerciseType.Essay or ExerciseType.CaseAnalysis => null,
            _ => null
        };
    }

    private static bool GradeTrueFalse(string? studentAnswer, string? correctAnswer)
    {
        var s = NormalizeTrueFalse(studentAnswer);
        var c = NormalizeTrueFalse(correctAnswer);
        if (s.HasValue && c.HasValue) return s.Value == c.Value;
        // 有一边无法归一化时回退为忽略大小写比较，避免误判也避免误判为对
        return string.Equals(studentAnswer?.Trim(), correctAnswer?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 归一化判断题答案为 bool。兼容手动创建的 true/false 与 AI 生成的 对/错/正确/错误等写法。
    /// 无法识别时回退为 trim + OrdinalIgnoreCase 比较，避免“true vs True”这类大小写误判。
    /// </summary>
    private static bool? NormalizeTrueFalse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim().ToLowerInvariant();
        // 去掉首尾引号/句号等噪音
        t = t.Trim('\'', '"', '。', '.', '!', '！');
        return t switch
        {
            "true" or "t" or "1" or "yes" or "y" or "√" or "✓" or "对" or "正确" or "是" or "真" or "right" => true,
            "false" or "f" or "0" or "no" or "n" or "×" or "x" or "错" or "错误" or "否" or "假" or "wrong" => false,
            _ => null,
        };
    }

    /// <summary>
    /// 归一化单选选项 token：兼容数字序号（1→A）、带前缀（"A."/"A、"/"A)"）、小写等问题。
    /// </summary>
    private static string NormalizeChoiceToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var t = raw.Trim().ToUpperInvariant();
        // 取第一个有效 token（防止 "A,B" 误传到单选用整个字符串比较）
        var first = t.Split(new[] { ',', ';', '，', '；', '、', ' ', '\t', '|', '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? t;
        t = first.Trim();
        // 去掉 "A." / "A、" / "A)" / "A:" / "A-" 这类前缀残留
        if (t.Length >= 2 && t[0] >= 'A' && t[0] <= 'Z' && ".、)]:：:-".Contains(t[1]))
            t = t[0].ToString();
        // 数字序号转字母：1→A, 2→B ...；兼容历史数据 0→A
        if (int.TryParse(t, out var n))
        {
            if (n == 0) return "A";
            if (n >= 1 && n <= 26) return ((char)('A' + n - 1)).ToString();
            return t;
        }
        return t;
    }

    private static IEnumerable<string> SplitChoiceTokens(string answer)
    {
        return answer.Split(new[] { ',', ';', '，', '；', '、', ' ', '\t', '\n', '\r', '|', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => NormalizeChoiceToken(s))
            .Where(s => !string.IsNullOrEmpty(s));
    }

    private static bool AreSetsEqual(string answer1, string answer2)
    {
        // 兼容无分隔符连写（如历史数据 "ABC"）与逗号分隔（如 "A,B,C"）两种写法
        static List<string> ToSet(string a)
        {
            var tokens = SplitChoiceTokens(a).ToList();
            if (tokens.Count == 1 && tokens[0].Length > 1 && tokens[0].All(c => c >= 'A' && c <= 'Z'))
                return tokens[0].Select(c => c.ToString()).OrderBy(s => s).ToList();
            return tokens.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }
        var set1 = ToSet(answer1);
        var set2 = ToSet(answer2);
        return set1.SequenceEqual(set2, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<StudentExerciseRecordDto> MapToDtoAsync(StudentExerciseRecord record)
    {
        var dtos = await MapToDtoListAsync(new List<StudentExerciseRecord> { record });
        return dtos.First();
    }

    private async Task<List<StudentExerciseRecordDto>> MapToDtoListAsync(List<StudentExerciseRecord> records)
    {
        if (records.Count == 0) return new List<StudentExerciseRecordDto>();

        var exerciseIds = records.Select(r => r.ExerciseId).Distinct().ToList();
        var courseIds = records.Select(r => r.CourseId).Distinct().ToList();
        var chapterIds = records.Where(r => r.ChapterId.HasValue).Select(r => r.ChapterId!.Value).Distinct().ToList();
        var studentIds = records.Select(r => r.StudentId).Distinct().ToList();

        Dictionary<Guid, string> exerciseMap, courseMap, chapterMap, studentMap;

        using (DataFilter.Disable<IMultiTenant>())
        {
            var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
            exerciseMap = (await exerciseQuery.Where(e => exerciseIds.Contains(e.Id)).ToListAsync())
                .ToDictionary(e => e.Id, e => e.Title);

            var courseQuery = await _courseRepository.GetQueryableAsync();
            courseMap = (await courseQuery.Where(c => courseIds.Contains(c.Id)).ToListAsync())
                .ToDictionary(c => c.Id, c => c.Title);

            var chapterQuery = await _chapterRepository.GetQueryableAsync();
            chapterMap = chapterIds.Count > 0
                ? (await chapterQuery.Where(ch => chapterIds.Contains(ch.Id)).ToListAsync())
                    .ToDictionary(ch => ch.Id, ch => ch.Title)
                : new Dictionary<Guid, string>();

            var userQuery = await _userRepository.GetQueryableAsync();
            studentMap = (await userQuery.Where(u => studentIds.Contains(u.Id)).ToListAsync())
                .ToDictionary(u => u.Id, u => ResolveStudentName(u));
        }

        return records.Select(r => new StudentExerciseRecordDto
        {
            Id = r.Id,
            StudentId = r.StudentId,
            StudentName = studentMap.GetValueOrDefault(r.StudentId, ""),
            CourseId = r.CourseId,
            CourseName = courseMap.GetValueOrDefault(r.CourseId, ""),
            ChapterId = r.ChapterId,
            ChapterName = r.ChapterId.HasValue ? chapterMap.GetValueOrDefault(r.ChapterId.Value, "") : null,
            ExerciseId = r.ExerciseId,
            ExerciseTitle = exerciseMap.GetValueOrDefault(r.ExerciseId, ""),
            StudentAnswer = r.StudentAnswer,
            IsCorrect = r.IsCorrect,
            HasViewedAnswer = r.HasViewedAnswer,
            ViewedAt = r.ViewedAt,
            SelfAssessment = r.SelfAssessment,
            TimeSpent = r.TimeSpent,
            CompletedAt = r.CompletedAt,
            CreationTime = r.CreationTime,
            CreatorId = r.CreatorId,
            LastModificationTime = r.LastModificationTime,
            LastModifierId = r.LastModifierId,
        }).ToList();
    }

    private static IEnumerable<StudentLearningStatisticsDto> ApplySorting(
        List<StudentLearningStatisticsDto> items, string? sorting)
    {
        if (string.IsNullOrWhiteSpace(sorting))
            return items.OrderByDescending(x => x.LastActiveTime);

        var parts = sorting.Split(' ', 2);
        var field = parts[0];
        var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        return field switch
        {
            "studentName" => desc ? items.OrderByDescending(x => x.StudentName) : items.OrderBy(x => x.StudentName),
            "completionRate" => desc ? items.OrderByDescending(x => x.CompletionRate) : items.OrderBy(x => x.CompletionRate),
            "correctRate" => desc ? items.OrderByDescending(x => x.CorrectRate) : items.OrderBy(x => x.CorrectRate),
            "totalTimeSpent" => desc ? items.OrderByDescending(x => x.TotalTimeSpent) : items.OrderBy(x => x.TotalTimeSpent),
            _ => items.OrderByDescending(x => x.LastActiveTime)
        };
    }

    /// <summary>
    /// 关键修复 P1-17：原实现只用 `u.Name ?? u.UserName`，当 IdentityUser 的 Name
    /// 字段为空（管理员通过导入/接口创建用户时常未填写 Name，只填了 UserName 或
    /// 邮箱）时，导出的"学生姓名"列就是空字符串或邮箱号，老师看不出是谁。
    /// 改为按 Surname+Name（中文姓名常用形式）→ Name → UserName → 邮箱 → "学员#<短ID>"
    /// 的优先级组合出有意义的展示名。
    /// </summary>
    private static string ResolveStudentName(IdentityUser u)
    {
        // 优先显示 Name（真实姓名），没有则用 UserName（登录名）
        var name = u.Name?.Trim();
        if (!string.IsNullOrEmpty(name)) return name;
        if (!string.IsNullOrEmpty(u.UserName)) return u.UserName;
        if (!string.IsNullOrEmpty(u.Email)) return u.Email;
        return $"学员#{u.Id.ToString()[..8]}";
    }

    #endregion
}
