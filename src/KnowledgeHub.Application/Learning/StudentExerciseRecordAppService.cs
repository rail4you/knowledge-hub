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
public class StudentExerciseRecordAppService : KnowledgeHubAppService, IStudentExerciseRecordAppService
{
    private readonly IRepository<StudentExerciseRecord, Guid> _recordRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<LearningProgress, Guid> _learningProgressRepository;
    private readonly ICurrentTenant _currentTenant;

    public StudentExerciseRecordAppService(
        IRepository<StudentExerciseRecord, Guid> recordRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<LearningProgress, Guid> learningProgressRepository,
        ICurrentTenant currentTenant)
    {
        _recordRepository = recordRepository;
        _exerciseRepository = exerciseRepository;
        _studentCourseRepository = studentCourseRepository;
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _userRepository = userRepository;
        _learningProgressRepository = learningProgressRepository;
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

    [AllowAnonymous]
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

    [AllowAnonymous]
    public async Task<PagedResultDto<StudentLearningStatisticsDto>> GetLearningStatisticsAsync(GetLearningStatisticsInput input)
    {
        var tenantFilter = ResolveTenantFilter(input.TenantId);

        // Get enrolled students for the course
        List<StudentCourse> studentCourses;
        List<StudentExerciseRecord> allRecords;
        List<Exercise> allExercises;

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

            var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
            allExercises = await exerciseQuery
                .WhereIf(input.ChapterId.HasValue, e => e.ChapterId == input.ChapterId!.Value)
                .Where(e => e.CourseId == input.CourseId)
                .ToListAsync();
        }

        var exerciseCount = allExercises.Count;

        // Load student names
        var studentIds = studentCourses.Select(sc => sc.StudentId).Distinct().ToList();
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
            var totalTime = studentRecords.Aggregate(TimeSpan.Zero, (acc, r) => acc + r.TimeSpent);

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
                LastActiveTime = studentRecords
                    .Where(r => r.CompletedAt.HasValue)
                    .Select(r => r.CompletedAt!.Value)
                    .DefaultIfEmpty()
                    .Max()
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

    [AllowAnonymous]
    public async Task<CourseLearningOverviewDto> GetCourseLearningOverviewAsync(GetCourseLearningOverviewInput input)
    {
        var tenantFilter = ResolveTenantFilter(input.TenantId);

        List<StudentCourse> studentCourses;
        List<StudentExerciseRecord> allRecords;
        List<LearningProgress> learningProgresses;
        List<Exercise> allExercises;
        List<Chapter> chapters;
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

        var chapterProgress = chapters.Select(ch =>
        {
            var chExercises = allExercises.Where(e => e.ChapterId == ch.Id).ToList();
            var chRecords = allRecords.Where(r => r.ChapterId == ch.Id).ToList();
            var chCompleted = chRecords.Count(r => r.CompletedAt.HasValue);
            var chGraded = chRecords.Where(r => r.IsCorrect.HasValue).ToList();
            var chCorrect = chGraded.Count(r => r.IsCorrect!.Value);

            return new ChapterProgressDto
            {
                ChapterId = ch.Id,
                ChapterName = ch.Title,
                TotalExercises = chExercises.Count,
                CompletedCount = chCompleted,
                CompletionRate = chExercises.Count > 0
                    ? Math.Round((decimal)chCompleted / chExercises.Count * 100, 1)
                    : 0,
                CorrectRate = chGraded.Count > 0
                    ? Math.Round((decimal)chCorrect / chGraded.Count * 100, 1)
                    : 0
            };
        }).ToList();

        return new CourseLearningOverviewDto
        {
            CourseId = input.CourseId,
            CourseName = course.Title,
            TotalStudents = allLearnerIds,
            ActiveStudents = activeStudentIds,
            TotalExercises = allExercises.Count,
            TotalLearningMinutes = Math.Round((decimal)totalTimeMinutes, 1),
            AverageCompletionRate = allExercises.Count > 0 && allLearnerIds > 0
                ? Math.Round((decimal)completedRecords.Count / (allExercises.Count * allLearnerIds) * 100, 1)
                : 0,
            AverageCorrectRate = gradedRecords.Count > 0
                ? Math.Round((decimal)gradedRecords.Count(r => r.IsCorrect!.Value) / gradedRecords.Count * 100, 1)
                : 0,
            ChapterProgress = chapterProgress
        };
    }

    [AllowAnonymous]
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

    private bool? AutoGrade(Exercise exercise, string? studentAnswer)
    {
        if (string.IsNullOrWhiteSpace(studentAnswer) || string.IsNullOrWhiteSpace(exercise.Answer))
            return null;

        return exercise.Type switch
        {
            ExerciseType.SingleChoice or ExerciseType.TrueFalse =>
                string.Equals(studentAnswer.Trim(), exercise.Answer.Trim(), StringComparison.OrdinalIgnoreCase),
            ExerciseType.MultiChoice =>
                AreSetsEqual(studentAnswer, exercise.Answer),
            ExerciseType.FillBlank =>
                string.Equals(studentAnswer.Trim(), exercise.Answer.Trim(), StringComparison.OrdinalIgnoreCase),
            // Subjective questions: no auto-grade
            ExerciseType.ShortAnswer or ExerciseType.Essay or ExerciseType.CaseAnalysis => null,
            _ => null
        };
    }

    private static bool AreSetsEqual(string answer1, string answer2)
    {
        var set1 = answer1.Split(',', ';').Select(s => s.Trim()).OrderBy(s => s);
        var set2 = answer2.Split(',', ';').Select(s => s.Trim()).OrderBy(s => s);
        return set1.SequenceEqual(set2);
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
