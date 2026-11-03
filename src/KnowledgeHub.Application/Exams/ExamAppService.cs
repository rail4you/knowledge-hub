using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams.Dtos;
using KnowledgeHub.Exams.Enums;
using KnowledgeHub.Learning;
using KnowledgeHub.LearningAnalytics;
using KnowledgeHub.LearningAnalytics.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Exams;

/// <summary>
/// 考试系统：教师端组卷（按题型 / 每题分 / 总分）+ 判卷，学生端答题。
/// 判分口径：
/// 1. 客观题（单选 / 多选 / 判断 / 填空）按标准答案自动判分（提交即判）；
/// 2. 主观题（问答 / 论述 / 案例 / 名词解释 / 计算 / 编程）支持「AI 辅助判分」与人工判卷，
///    人工判卷结果为准，AI 判分仅作为建议初值（IsAiGraded = true 可追溯）。
/// </summary>
[Authorize]
[IgnoreAntiforgeryToken]
public class ExamAppService : KnowledgeHubAppService, IExamAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private const string RulesProperty = "GenerateRules";

    private readonly IRepository<Exam, Guid> _examRepository;
    private readonly IRepository<ExamExercise, Guid> _examExerciseRepository;
    private readonly IRepository<StudentExam, Guid> _studentExamRepository;
    private readonly IRepository<StudentAnswer, Guid> _studentAnswerRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<ChapterExercise, Guid> _chapterExerciseRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    /// <summary>智学分析：考试作答写入统一作答记录，供掌握度 / 推荐使用。</summary>
    private readonly IRepository<AnswerRecord, Guid> _answerRecordRepository;
    private readonly MasteryRecalculationService _masteryService;
    private readonly ExerciseAppService _exerciseAppService;

    public ExamAppService(
        IRepository<Exam, Guid> examRepository,
        IRepository<ExamExercise, Guid> examExerciseRepository,
        IRepository<StudentExam, Guid> studentExamRepository,
        IRepository<StudentAnswer, Guid> studentAnswerRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<ChapterExercise, Guid> chapterExerciseRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<AnswerRecord, Guid> answerRecordRepository,
        MasteryRecalculationService masteryService,
        ExerciseAppService exerciseAppService)
    {
        _examRepository = examRepository;
        _examExerciseRepository = examExerciseRepository;
        _studentExamRepository = studentExamRepository;
        _studentAnswerRepository = studentAnswerRepository;
        _exerciseRepository = exerciseRepository;
        _chapterExerciseRepository = chapterExerciseRepository;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _userRepository = userRepository;
        _answerRecordRepository = answerRecordRepository;
        _masteryService = masteryService;
        _exerciseAppService = exerciseAppService;
    }

    // ══════════════════════════════════════════════════════════
    //  教师端：试卷管理
    // ══════════════════════════════════════════════════════════

    public async Task<PagedResultDto<ExamListItemDto>> GetListAsync(ExamListQueryDto input)
    {
        var query = await _examRepository.GetQueryableAsync();

        if (input.CourseId.HasValue)
        {
            query = query.Where(x => x.CourseId == input.CourseId.Value);
        }
        if (input.ChapterId.HasValue)
        {
            query = query.Where(x => x.ChapterId == input.ChapterId.Value);
        }
        if (input.Type.HasValue)
        {
            query = query.Where(x => x.Type == input.Type.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var kw = input.Filter.Trim();
            query = query.Where(x => x.Title.Contains(kw) || (x.Description != null && x.Description.Contains(kw)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var exams = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var dtos = await MapToListItemAsync(exams);
        return new PagedResultDto<ExamListItemDto>(totalCount, dtos);
    }

    public async Task<ExamDetailDto> GetAsync(Guid id)
    {
        var exam = await _examRepository.GetAsync(id);
        return await BuildDetailAsync(exam);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ExamDetailDto> CreateAsync(CreateExamDto input)
    {
        var exam = await CreateExamEntityAsync(input);

        var items = input.Exercises
            .Where(x => x.ExerciseId != Guid.Empty)
            .Select((x, index) => (x, index))
            .Select(t => new { t.x.ExerciseId, Score = Math.Max(1, t.x.Score), SortOrder = t.x.SortOrder > 0 ? t.x.SortOrder : t.index })
            .ToList();

        if (items.Count == 0)
        {
            throw new UserFriendlyException("试卷至少要包含一道题");
        }

        await EnsureExercisesBelongToCourseAsync(input.CourseId, items.Select(x => x.ExerciseId));

        foreach (var item in items)
        {
            await _examExerciseRepository.InsertAsync(new ExamExercise(
                GuidGenerator.Create(), exam.Id, item.ExerciseId, item.Score, item.SortOrder));
        }

        exam.TotalScore = items.Sum(x => x.Score);
        exam.PassingScore = NormalizePassingScore(input.PassingScore, exam.TotalScore);
        await _examRepository.UpdateAsync(exam);

        // 先落盘再回读：EF 查询不会返回尚未 SaveChanges 的新增行，否则响应里 questions 为空
        await SaveChangesAsync();
        return await BuildDetailAsync(exam);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ExamDetailDto> GenerateAsync(GenerateExamInput input)
    {
        if (input.CourseId == Guid.Empty)
        {
            throw new UserFriendlyException("请先选择课程");
        }

        var exam = await CreateExamEntityAsync(new CreateExamDto
        {
            CourseId = input.CourseId,
            ChapterId = input.ChapterId,
            Title = input.Title,
            Description = input.Description,
            Type = input.Type,
            DurationMinutes = input.DurationMinutes,
            PassingScore = input.PassingScore,
            StartTime = input.StartTime,
            EndTime = input.EndTime
        });

        var pool = await LoadExercisePoolAsync(input.CourseId, input.ChapterId);
        if (pool.Count == 0)
        {
            await _examRepository.DeleteAsync(exam);
            throw new UserFriendlyException("该课程（或章节）下还没有习题，请先在「习题管理」中准备题库");
        }

        var picked = new List<(Exercise Exercise, int Score)>();

        if (input.Rules.Any(x => x.Count > 0))
        {
            // 精细模式：按题型逐个抽题
            var random = new Random(Guid.NewGuid().GetHashCode());
            foreach (var rule in input.Rules.Where(x => x.Count > 0))
            {
                var candidates = pool.Where(x => x.Type == rule.Type).ToList();
                if (candidates.Count < rule.Count)
                {
                    await _examRepository.DeleteAsync(exam);
                    throw new UserFriendlyException(
                        $"「{TypeName(rule.Type)}」题量不足：需要 {rule.Count} 道，题库里只有 {candidates.Count} 道");
                }

                foreach (var exercise in candidates.OrderBy(_ => random.Next()).Take(rule.Count))
                {
                    var score = rule.ScorePerItem > 0
                        ? rule.ScorePerItem
                        : exercise.Score > 0 ? exercise.Score : 1;
                    picked.Add((exercise, score));
                }
            }
        }
        else if (input.TargetTotalScore.HasValue && input.TargetTotalScore.Value > 0)
        {
            // 总分模式：按目标总分自动配平（题型规则只作为范围约束）
            picked = PickByTargetScore(pool, input.TargetTotalScore.Value, input.Rules);
        }
        else
        {
            await _examRepository.DeleteAsync(exam);
            throw new UserFriendlyException("请设置组卷规则（题型 + 数量 + 每题分），或指定目标总分");
        }

        if (picked.Count == 0)
        {
            await _examRepository.DeleteAsync(exam);
            throw new UserFriendlyException("按当前规则没有抽到题目，请调整组卷规则");
        }

        var ordered = input.Shuffle
            ? picked.OrderBy(_ => Guid.NewGuid()).ToList()
            : picked;

        var sortOrder = 0;
        foreach (var (exercise, score) in ordered)
        {
            await _examExerciseRepository.InsertAsync(new ExamExercise(
                GuidGenerator.Create(), exam.Id, exercise.Id, Math.Max(1, score), sortOrder++));
        }

        exam.TotalScore = ordered.Sum(x => Math.Max(1, x.Score));
        exam.PassingScore = NormalizePassingScore(input.PassingScore, exam.TotalScore);
        exam.SetProperty(RulesProperty, JsonSerializer.Serialize(input.Rules, JsonOptions));
        await _examRepository.UpdateAsync(exam);

        await SaveChangesAsync();
        return await BuildDetailAsync(exam);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<ExamDetailDto> UpdateAsync(Guid id, CreateExamDto input)
    {
        var exam = await _examRepository.GetAsync(id);

        if (!string.IsNullOrWhiteSpace(input.Title))
        {
            exam.Title = input.Title.Trim();
        }
        exam.ChapterId = input.ChapterId;
        exam.Description = input.Description;
        exam.Type = input.Type;
        exam.DurationMinutes = input.DurationMinutes > 0 ? input.DurationMinutes : 60;
        exam.StartTime = input.StartTime;
        exam.EndTime = input.EndTime;

        // 题目变更：整表覆盖（简单可靠，试卷题量有限）
        var existingItems = await _examExerciseRepository.GetListAsync(x => x.ExamId == id);
        if (existingItems.Count > 0)
        {
            await _examExerciseRepository.DeleteManyAsync(existingItems);
        }

        var items = input.Exercises
            .Where(x => x.ExerciseId != Guid.Empty)
            .Select((x, index) => (x, index))
            .Select(t => new { t.x.ExerciseId, Score = Math.Max(1, t.x.Score), SortOrder = t.x.SortOrder > 0 ? t.x.SortOrder : t.index })
            .ToList();

        if (items.Count > 0)
        {
            await EnsureExercisesBelongToCourseAsync(exam.CourseId, items.Select(x => x.ExerciseId));
            foreach (var item in items)
            {
                await _examExerciseRepository.InsertAsync(new ExamExercise(
                    GuidGenerator.Create(), exam.Id, item.ExerciseId, item.Score, item.SortOrder));
            }
        }

        exam.TotalScore = items.Count > 0 ? items.Sum(x => x.Score) : exam.TotalScore;
        exam.PassingScore = NormalizePassingScore(input.PassingScore, exam.TotalScore);
        await _examRepository.UpdateAsync(exam);

        await SaveChangesAsync();
        return await BuildDetailAsync(exam);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        var hasSubmission = await _studentExamRepository.AnyAsync(x => x.ExamId == id);
        if (hasSubmission)
        {
            throw new UserFriendlyException("该试卷已有学生作答记录，不能删除（可修改试卷或等待评卷结束）");
        }

        var items = await _examExerciseRepository.GetListAsync(x => x.ExamId == id);
        if (items.Count > 0)
        {
            await _examExerciseRepository.DeleteManyAsync(items);
        }
        await _examRepository.DeleteAsync(id);
    }

    // ══════════════════════════════════════════════════════════
    //  教师端：判卷
    // ══════════════════════════════════════════════════════════

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<PagedResultDto<ExamSubmissionItemDto>> GetSubmissionsAsync(ExamSubmissionQueryDto input)
    {
        var query = await _studentExamRepository.GetQueryableAsync();

        if (input.ExamId.HasValue)
        {
            query = query.Where(x => x.ExamId == input.ExamId.Value);
        }
        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }
        if (input.CourseId.HasValue)
        {
            var examIds = await AsyncExecuter.ToListAsync(
                (await _examRepository.GetQueryableAsync())
                    .Where(x => x.CourseId == input.CourseId.Value)
                    .Select(x => x.Id));
            query = query.Where(x => examIds.Contains(x.ExamId));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var submissions = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.SubmittedAt ?? x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var dtos = await BuildSubmissionItemsAsync(submissions);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var kw = input.Filter.Trim();
            dtos = dtos.Where(x => (x.StudentName ?? string.Empty).Contains(kw) || x.ExamTitle.Contains(kw)).ToList();
        }

        return new PagedResultDto<ExamSubmissionItemDto>(totalCount, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<StudentExamResultDto> GetSubmissionAsync(Guid studentExamId)
    {
        var result = await BuildResultAsync(studentExamId, includeStandardAnswer: true);
        return result;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<StudentExamResultDto> GradeAsync(Guid studentExamId, GradeSubmissionInput input)
    {
        var studentExam = await _studentExamRepository.GetAsync(studentExamId);
        var exam = await _examRepository.GetAsync(studentExam.ExamId);
        var items = await GetExamItemsAsync(exam.Id);
        var answers = await _studentAnswerRepository.GetListAsync(x => x.StudentExamId == studentExamId);

        foreach (var answerInput in input.Answers)
        {
            var item = items.FirstOrDefault(x => x.ExerciseId == answerInput.ExerciseId);
            if (item == null) continue;

            var answer = answers.FirstOrDefault(x => x.ExerciseId == answerInput.ExerciseId);
            if (answer == null)
            {
                answer = new StudentAnswer(GuidGenerator.Create(), studentExamId, answerInput.ExerciseId)
                {
                    TenantId = CurrentTenant.Id
                };
                await _studentAnswerRepository.InsertAsync(answer);
            }

            var score = answerInput.Score ?? answer.Score;
            if (score.HasValue)
            {
                score = Math.Clamp(score.Value, 0, item.Score);
            }
            answer.SetGrade(score, answerInput.Feedback ?? answer.Feedback, answer.IsCorrect, isAiGraded: false);
            await _studentAnswerRepository.UpdateAsync(answer);
        }

        await SaveChangesAsync();
        await ApplyTotalAsync(studentExam, exam, items, answers);
        await SaveChangesAsync();
        await SyncAnswerRecordsByExamItemsAsync(studentExam, exam, items);
        return await BuildResultAsync(studentExamId, includeStandardAnswer: true);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<StudentExamResultDto> AutoGradeAsync(Guid studentExamId)
    {
        var studentExam = await _studentExamRepository.GetAsync(studentExamId);
        var exam = await _examRepository.GetAsync(studentExam.ExamId);
        var items = await GetExamItemsAsync(exam.Id);
        var exercises = await GetExerciseMapAsync(items.Select(x => x.ExerciseId));
        var answers = await _studentAnswerRepository.GetListAsync(x => x.StudentExamId == studentExamId);

        foreach (var answer in answers)
        {
            var item = items.FirstOrDefault(x => x.ExerciseId == answer.ExerciseId);
            if (item == null || !exercises.TryGetValue(answer.ExerciseId, out var exercise)) continue;

            if (IsObjective(exercise.Type))
            {
                var correct = IsObjectiveCorrect(exercise.Type, answer.Content, exercise.Answer);
                answer.SetGrade(correct ? item.Score : 0, correct ? "客观题自动判分：正确" : "客观题自动判分：错误", correct, isAiGraded: false);
            }
            else
            {
                // 主观题：AI 辅助判分（失败不影响整体，保留待人工判卷）
                try
                {
                    var graded = await _exerciseAppService.GradeEssayAsync(new GradeEssayInput
                    {
                        StudentExamId = studentExamId,
                        ExerciseId = exercise.Id,
                        StandardAnswer = exercise.Answer ?? string.Empty,
                        MaxScore = item.Score
                    });
                    var score = Math.Clamp(graded.Score, 0, item.Score);
                    answer.SetGrade(score, graded.Feedback, graded.IsCorrect, isAiGraded: true);
                }
                catch (Exception)
                {
                    answer.SetGrade(null, "AI 判分失败，请人工判卷", null, isAiGraded: false);
                }
            }

            await _studentAnswerRepository.UpdateAsync(answer);
        }

        await SaveChangesAsync();
        await ApplyTotalAsync(studentExam, exam, items, answers);
        await SaveChangesAsync();
        await SyncAnswerRecordsByExamItemsAsync(studentExam, exam, items);
        return await BuildResultAsync(studentExamId, includeStandardAnswer: true);
    }

    // ══════════════════════════════════════════════════════════
    //  学生端
    // ══════════════════════════════════════════════════════════

    public async Task<List<StudentExamListItemDto>> GetMyExamsAsync(Guid courseId)
    {
        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录");

        var exams = await AsyncExecuter.ToListAsync(
            (await _examRepository.GetQueryableAsync())
                .Where(x => x.CourseId == courseId)
                .OrderBy(x => x.CreationTime));

        if (exams.Count == 0)
        {
            return new List<StudentExamListItemDto>();
        }

        var examIds = exams.Select(x => x.Id).ToList();
        var mine = await AsyncExecuter.ToListAsync(
            (await _studentExamRepository.GetQueryableAsync())
                .Where(x => x.StudentId == userId && examIds.Contains(x.ExamId)));

        var counts = await GetExerciseCountMapAsync(examIds);

        return exams.Select(exam =>
        {
            var record = mine.Where(x => x.ExamId == exam.Id)
                .OrderByDescending(x => x.CreationTime)
                .FirstOrDefault();
            return new StudentExamListItemDto
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                Description = exam.Description,
                Type = exam.Type,
                DurationMinutes = exam.DurationMinutes,
                TotalScore = exam.TotalScore,
                PassingScore = exam.PassingScore,
                ExerciseCount = counts.GetValueOrDefault(exam.Id),
                StartTime = exam.StartTime,
                EndTime = exam.EndTime,
                StudentExamId = record?.Id,
                Status = record?.Status ?? ExamStatus.NotStarted,
                MyScore = record?.TotalScore,
                IsPassed = record?.IsPassed,
                SubmittedAt = record?.SubmittedAt
            };
        }).ToList();
    }

    public async Task<StudentExamPaperDto> StartAsync(Guid examId)
    {
        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录");
        var exam = await _examRepository.GetAsync(examId);
        await EnsureCanTakeExamAsync(exam, userId);

        var studentExam = await AsyncExecuter.FirstOrDefaultAsync(
            (await _studentExamRepository.GetQueryableAsync())
                .Where(x => x.ExamId == examId && x.StudentId == userId)
                .OrderByDescending(x => x.CreationTime));

        if (studentExam != null && studentExam.Status is ExamStatus.Submitted or ExamStatus.Graded)
        {
            throw new UserFriendlyException("该试卷已提交，不能重复作答");
        }

        if (studentExam == null)
        {
            studentExam = new StudentExam(GuidGenerator.Create(), userId, examId)
            {
                TenantId = CurrentTenant.Id
            };
            studentExam.Start();
            await _studentExamRepository.InsertAsync(studentExam);
        }
        else if (studentExam.Status == ExamStatus.NotStarted)
        {
            studentExam.Start();
            await _studentExamRepository.UpdateAsync(studentExam);
        }

        var items = await GetExamItemsAsync(examId);
        var exercises = await GetExerciseMapAsync(items.Select(x => x.ExerciseId));
        var answers = await _studentAnswerRepository.GetListAsync(x => x.StudentExamId == studentExam.Id);

        return new StudentExamPaperDto
        {
            StudentExamId = studentExam.Id,
            ExamId = exam.Id,
            ExamTitle = exam.Title,
            Description = exam.Description,
            DurationMinutes = exam.DurationMinutes,
            TotalScore = exam.TotalScore,
            PassingScore = exam.PassingScore,
            Status = studentExam.Status,
            StartedAt = studentExam.StartedAt,
            SubmittedAt = studentExam.SubmittedAt,
            Questions = items.Select(item =>
            {
                exercises.TryGetValue(item.ExerciseId, out var exercise);
                return new ExamPaperQuestionDto
                {
                    ExerciseId = item.ExerciseId,
                    SortOrder = item.SortOrder,
                    Score = item.Score,
                    Title = exercise?.Title ?? string.Empty,
                    QuestionContent = exercise?.QuestionContent ?? string.Empty,
                    Type = exercise?.Type ?? ExerciseType.ShortAnswer,
                    Options = exercise?.Options,
                    Difficulty = exercise?.Difficulty ?? 1,
                    MyAnswer = answers.FirstOrDefault(a => a.ExerciseId == item.ExerciseId)?.Content
                };
            }).ToList()
        };
    }

    public async Task<StudentExamResultDto> SubmitAsync(Guid studentExamId, SubmitExamInput input)
    {
        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录");
        var studentExam = await _studentExamRepository.GetAsync(studentExamId);

        if (studentExam.StudentId != userId)
        {
            throw new UserFriendlyException("只能提交自己的试卷");
        }
        if (studentExam.Status is ExamStatus.Submitted or ExamStatus.Graded)
        {
            throw new UserFriendlyException("该试卷已提交，不能重复提交");
        }

        var exam = await _examRepository.GetAsync(studentExam.ExamId);
        var items = await GetExamItemsAsync(exam.Id);
        var exercises = await GetExerciseMapAsync(items.Select(x => x.ExerciseId));
        var existing = await _studentAnswerRepository.GetListAsync(x => x.StudentExamId == studentExamId);

        foreach (var item in items)
        {
            var submitted = input.Answers.FirstOrDefault(x => x.ExerciseId == item.ExerciseId);
            var answer = existing.FirstOrDefault(x => x.ExerciseId == item.ExerciseId);
            if (answer == null)
            {
                answer = new StudentAnswer(GuidGenerator.Create(), studentExamId, item.ExerciseId)
                {
                    TenantId = CurrentTenant.Id
                };
                answer.SetAnswer(submitted?.Content);
                await _studentAnswerRepository.InsertAsync(answer);
            }
            else
            {
                answer.SetAnswer(submitted?.Content);
                await _studentAnswerRepository.UpdateAsync(answer);
            }

            // 客观题提交即判分，学生立刻能看到客观题得分；主观题待教师/AI 判卷
            if (submitted != null && exercises.TryGetValue(item.ExerciseId, out var exercise) && IsObjective(exercise.Type))
            {
                var correct = IsObjectiveCorrect(exercise.Type, submitted.Content, exercise.Answer);
                answer.SetGrade(correct ? item.Score : 0, correct ? "客观题自动判分：正确" : "客观题自动判分：错误", correct, isAiGraded: false);
                await _studentAnswerRepository.UpdateAsync(answer);
            }
        }

        studentExam.Submit();
        await _studentExamRepository.UpdateAsync(studentExam);

        await SaveChangesAsync();
        // 客观题已判分：同步进智学分析的统一作答记录，让考试结果影响掌握度与推荐
        await SyncAnswerRecordsAsync(studentExam, exam, items, exercises);
        return await BuildResultAsync(studentExamId, includeStandardAnswer: true);
    }

    public async Task<StudentExamResultDto> GetMyResultAsync(Guid studentExamId)
    {
        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录");
        var studentExam = await _studentExamRepository.GetAsync(studentExamId);
        if (studentExam.StudentId != userId)
        {
            throw new UserFriendlyException("只能查看自己的答题结果");
        }

        // 未评卷前不下发标准答案，避免提前泄露
        return await BuildResultAsync(studentExamId, includeStandardAnswer: studentExam.Status == ExamStatus.Graded);
    }

    // ══════════════════════════════════════════════════════════
    //  内部工具
    // ══════════════════════════════════════════════════════════

    private async Task<Exam> CreateExamEntityAsync(CreateExamDto input)
    {
        if (input.CourseId == Guid.Empty)
        {
            throw new UserFriendlyException("请先选择课程");
        }
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写试卷标题");
        }

        var course = await FindCourseAsync(input.CourseId);
        if (course == null)
        {
            throw new UserFriendlyException("课程不存在");
        }

        var exam = new Exam(GuidGenerator.Create(), input.CourseId, input.Title.Trim())
        {
            TenantId = CurrentTenant.Id,
            ChapterId = input.ChapterId,
            Description = input.Description,
            Type = input.Type,
            DurationMinutes = input.DurationMinutes > 0 ? input.DurationMinutes : 60,
            StartTime = input.StartTime,
            EndTime = input.EndTime
        };

        await _examRepository.InsertAsync(exam);
        return exam;
    }

    private async Task<Course?> FindCourseAsync(Guid courseId)
    {
        if (CurrentTenant.Id.HasValue)
        {
            return await _courseRepository.FindAsync(courseId);
        }
        using (DataFilter.Disable<IMultiTenant>())
        {
            return await _courseRepository.FindAsync(courseId);
        }
    }

    private async Task<List<Exercise>> LoadExercisePoolAsync(Guid courseId, Guid? chapterId)
    {
        // 习题按 CourseId 归属，课程已锁定租户，因此这里可以安全地忽略租户过滤器：
        // 否则宿主管理员（CurrentTenant 为空）打开租户课程时会抽不到任何题目。
        using var filterScope = DataFilter.Disable<IMultiTenant>();
        var query = await _exerciseRepository.GetQueryableAsync();
        query = query.Where(x => x.CourseId == courseId);

        if (chapterId.HasValue)
        {
            // 章节维度：主章节（Exercise.ChapterId）或章节多对多关联（ChapterExercise）命中都算
            var chapterExerciseQuery = await _chapterExerciseRepository.GetQueryableAsync();
            var linkedIds = await AsyncExecuter.ToListAsync(
                chapterExerciseQuery.Where(x => x.ChapterId == chapterId.Value).Select(x => x.ExerciseId));
            query = query.Where(x => x.ChapterId == chapterId.Value || linkedIds.Contains(x.Id));
        }

        return await AsyncExecuter.ToListAsync(query);
    }

    /// <summary>
    /// 总分模式抽题：按分值从大到小贪心填充，最后不足时用一题的分值补齐，
    /// 保证「总分正好等于目标值」（考试总分很少出现零头，这个口径最直观）。
    /// </summary>
    private static List<(Exercise Exercise, int Score)> PickByTargetScore(
        List<Exercise> pool,
        int target,
        List<ExamTypeRuleDto> rules)
    {
        var allowedTypes = rules.Where(x => x.Count > 0).Select(x => x.Type).ToHashSet();
        var candidates = pool
            .Where(x => allowedTypes.Count == 0 || allowedTypes.Contains(x.Type))
            .ToList();

        var shuffled = candidates.OrderBy(_ => Guid.NewGuid()).ToList();
        var picked = new List<(Exercise, int)>();
        var remaining = target;

        foreach (var exercise in shuffled)
        {
            if (remaining <= 0) break;
            var score = exercise.Score > 0 ? exercise.Score : 1;
            if (score <= remaining)
            {
                picked.Add((exercise, score));
                remaining -= score;
            }
            else if (picked.Count == 0)
            {
                // 第一题就超过目标分：直接按目标分赋分
                picked.Add((exercise, remaining));
                remaining = 0;
            }
        }

        if (remaining > 0 && picked.Count > 0)
        {
            // 剩余分并给最后一题，保证总分精确等于目标
            var last = picked[^1];
            picked[^1] = (last.Item1, last.Item2 + remaining);
        }

        return picked.Select(x => (x.Item1, x.Item2)).ToList();
    }

    private async Task EnsureExercisesBelongToCourseAsync(Guid courseId, IEnumerable<Guid> exerciseIds)
    {
        var ids = exerciseIds.Distinct().ToList();
        using var filterScope = DataFilter.Disable<IMultiTenant>();
        var query = await _exerciseRepository.GetQueryableAsync();
        var found = await AsyncExecuter.ToListAsync(
            query.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.CourseId }));

        if (found.Count != ids.Count)
        {
            throw new UserFriendlyException("部分习题不存在");
        }
        if (found.Any(x => x.CourseId != courseId))
        {
            throw new UserFriendlyException("只有本课程的习题才能加入试卷");
        }
    }

    private static int NormalizePassingScore(int passingScore, int totalScore)
    {
        if (totalScore <= 0) return 0;
        var value = passingScore <= 0 ? 60 : passingScore;
        // <=100 视为百分比（60 → 60%），>100 视为绝对分值
        var absolute = value <= 100 ? (int)Math.Ceiling(totalScore * value / 100.0) : value;
        return Math.Clamp(absolute, 0, totalScore);
    }

    private async Task<List<ExamListItemDto>> MapToListItemAsync(List<Exam> exams)
    {
        if (exams.Count == 0) return new List<ExamListItemDto>();

        var examIds = exams.Select(x => x.Id).ToList();

        var exerciseCounts = await GetExerciseCountMapAsync(examIds);

        var submissions = await AsyncExecuter.ToListAsync(
            (await _studentExamRepository.GetQueryableAsync()).Where(x => examIds.Contains(x.ExamId)));

        var courseIds = exams.Select(x => x.CourseId).Distinct().ToList();
        var courseNames = await GetCourseNameMapAsync(courseIds);

        return exams.Select(exam =>
        {
            var mine = submissions.Where(x => x.ExamId == exam.Id).ToList();
            return new ExamListItemDto
            {
                Id = exam.Id,
                CourseId = exam.CourseId,
                CourseName = courseNames.GetValueOrDefault(exam.CourseId),
                ChapterId = exam.ChapterId,
                Title = exam.Title,
                Description = exam.Description,
                Type = exam.Type,
                DurationMinutes = exam.DurationMinutes,
                TotalScore = exam.TotalScore,
                PassingScore = exam.PassingScore,
                StartTime = exam.StartTime,
                EndTime = exam.EndTime,
                ExerciseCount = exerciseCounts.GetValueOrDefault(exam.Id),
                SubmissionCount = mine.Count(x => x.Status is ExamStatus.Submitted or ExamStatus.Graded),
                PendingGradeCount = mine.Count(x => x.Status == ExamStatus.Submitted),
                GradedCount = mine.Count(x => x.Status == ExamStatus.Graded),
                CreationTime = exam.CreationTime,
                CreatorId = exam.CreatorId,
                LastModificationTime = exam.LastModificationTime,
                LastModifierId = exam.LastModifierId
            };
        }).ToList();
    }

    private async Task<ExamDetailDto> BuildDetailAsync(Exam exam)
    {
        var items = await GetExamItemsAsync(exam.Id);
        var exercises = await GetExerciseMapAsync(items.Select(x => x.ExerciseId));
        var courseNames = await GetCourseNameMapAsync(new List<Guid> { exam.CourseId });

        var dto = new ExamDetailDto
        {
            Id = exam.Id,
            CourseId = exam.CourseId,
            CourseName = courseNames.GetValueOrDefault(exam.CourseId),
            ChapterId = exam.ChapterId,
            Title = exam.Title,
            Description = exam.Description,
            Type = exam.Type,
            DurationMinutes = exam.DurationMinutes,
            TotalScore = exam.TotalScore,
            PassingScore = exam.PassingScore,
            StartTime = exam.StartTime,
            EndTime = exam.EndTime,
            ExerciseCount = items.Count,
            CreationTime = exam.CreationTime,
            CreatorId = exam.CreatorId,
            LastModificationTime = exam.LastModificationTime,
            LastModifierId = exam.LastModifierId,
            Questions = items.Select(item =>
            {
                exercises.TryGetValue(item.ExerciseId, out var exercise);
                return new ExamQuestionDto
                {
                    ExerciseId = item.ExerciseId,
                    SortOrder = item.SortOrder,
                    Score = item.Score,
                    Title = exercise?.Title ?? string.Empty,
                    QuestionContent = exercise?.QuestionContent ?? string.Empty,
                    Type = exercise?.Type ?? ExerciseType.ShortAnswer,
                    Options = exercise?.Options,
                    Answer = exercise?.Answer ?? string.Empty,
                    QuestionAnalysis = exercise?.QuestionAnalysis,
                    Difficulty = exercise?.Difficulty ?? 1
                };
            }).ToList()
        };

        var rulesJson = exam.GetProperty<string>(RulesProperty);
        if (!string.IsNullOrWhiteSpace(rulesJson))
        {
            try
            {
                dto.Rules = JsonSerializer.Deserialize<List<ExamTypeRuleDto>>(rulesJson, JsonOptions);
            }
            catch (JsonException)
            {
                // 规则只是回显，解析失败忽略
            }
        }

        return dto;
    }

    /// <summary>强制把当前 UoW 的变更落盘（用于「写入后立即回读」的场景）。</summary>
    private async Task SaveChangesAsync()
    {
        if (CurrentUnitOfWork != null)
        {
            await CurrentUnitOfWork.SaveChangesAsync();
        }
    }

    private async Task<List<ExamExercise>> GetExamItemsAsync(Guid examId)
    {
        var items = await AsyncExecuter.ToListAsync(
            (await _examExerciseRepository.GetQueryableAsync())
                .Where(x => x.ExamId == examId)
                .OrderBy(x => x.SortOrder));
        return items;
    }

    private async Task<Dictionary<Guid, Exercise>> GetExerciseMapAsync(IEnumerable<Guid> exerciseIds)
    {
        var ids = exerciseIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, Exercise>();

        using var filterScope = DataFilter.Disable<IMultiTenant>();
        var list = await AsyncExecuter.ToListAsync(
            (await _exerciseRepository.GetQueryableAsync()).Where(x => ids.Contains(x.Id)));
        return list.ToDictionary(x => x.Id);
    }

    private async Task<Dictionary<Guid, int>> GetExerciseCountMapAsync(List<Guid> examIds)
    {
        var rows = await AsyncExecuter.ToListAsync(
            (await _examExerciseRepository.GetQueryableAsync())
                .Where(x => examIds.Contains(x.ExamId))
                .Select(x => x.ExamId));

        return rows.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<Dictionary<Guid, string>> GetCourseNameMapAsync(List<Guid> courseIds)
    {
        if (courseIds.Count == 0) return new Dictionary<Guid, string>();

        using (DataFilter.Disable<IMultiTenant>())
        {
            var rows = await AsyncExecuter.ToListAsync(
                (await _courseRepository.GetQueryableAsync())
                    .Where(x => courseIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Title }));
            return rows.ToDictionary(x => x.Id, x => x.Title ?? string.Empty);
        }
    }

    private async Task<List<ExamSubmissionItemDto>> BuildSubmissionItemsAsync(List<StudentExam> submissions)
    {
        if (submissions.Count == 0) return new List<ExamSubmissionItemDto>();

        var examIds = submissions.Select(x => x.ExamId).Distinct().ToList();
        var exams = await AsyncExecuter.ToListAsync(
            (await _examRepository.GetQueryableAsync()).Where(x => examIds.Contains(x.Id)));
        var examMap = exams.ToDictionary(x => x.Id);

        var userIds = submissions.Select(x => x.StudentId).Distinct().ToList();
        var userNames = new Dictionary<Guid, string>();
        using (DataFilter.Disable<IMultiTenant>())
        {
            var users = await AsyncExecuter.ToListAsync(
                (await _userRepository.GetQueryableAsync())
                    .Where(x => userIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Name, x.UserName }));
            foreach (var user in users)
            {
                userNames[user.Id] = user.Name ?? user.UserName ?? string.Empty;
            }
        }

        var studentExamIds = submissions.Select(x => x.Id).ToList();
        var answers = await AsyncExecuter.ToListAsync(
            (await _studentAnswerRepository.GetQueryableAsync())
                .Where(x => studentExamIds.Contains(x.StudentExamId)));

        return submissions.Select(sub =>
        {
            examMap.TryGetValue(sub.ExamId, out var exam);
            return new ExamSubmissionItemDto
            {
                StudentExamId = sub.Id,
                ExamId = sub.ExamId,
                ExamTitle = exam?.Title ?? string.Empty,
                StudentId = sub.StudentId,
                StudentName = userNames.GetValueOrDefault(sub.StudentId),
                Status = sub.Status,
                TotalScore = sub.TotalScore,
                MaxScore = exam?.TotalScore ?? 0,
                SubmittedAt = sub.SubmittedAt,
                GradedAt = sub.GradedAt,
                SubjectiveCount = answers.Count(x => x.StudentExamId == sub.Id)
            };
        }).OrderByDescending(x => x.SubmittedAt ?? DateTime.MinValue).ToList();
    }

    private async Task<StudentExamResultDto> BuildResultAsync(Guid studentExamId, bool includeStandardAnswer)
    {
        var studentExam = await _studentExamRepository.GetAsync(studentExamId);
        var exam = await _examRepository.GetAsync(studentExam.ExamId);
        var items = await GetExamItemsAsync(exam.Id);
        var exercises = await GetExerciseMapAsync(items.Select(x => x.ExerciseId));
        var answers = await _studentAnswerRepository.GetListAsync(x => x.StudentExamId == studentExamId);

        var studentName = string.Empty;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await AsyncExecuter.FirstOrDefaultAsync(
                (await _userRepository.GetQueryableAsync()).Where(x => x.Id == studentExam.StudentId));
            studentName = user?.Name ?? user?.UserName ?? string.Empty;
        }

        return new StudentExamResultDto
        {
            StudentExamId = studentExam.Id,
            ExamId = exam.Id,
            ExamTitle = exam.Title,
            StudentId = studentExam.StudentId,
            StudentName = studentName,
            Status = studentExam.Status,
            TotalScore = studentExam.TotalScore,
            MaxScore = exam.TotalScore,
            IsPassed = studentExam.IsPassed,
            StartedAt = studentExam.StartedAt,
            SubmittedAt = studentExam.SubmittedAt,
            GradedAt = studentExam.GradedAt,
            Answers = items.Select(item =>
            {
                exercises.TryGetValue(item.ExerciseId, out var exercise);
                var answer = answers.FirstOrDefault(x => x.ExerciseId == item.ExerciseId);
                return new StudentAnswerDetailDto
                {
                    ExerciseId = item.ExerciseId,
                    SortOrder = item.SortOrder,
                    Title = exercise?.Title ?? string.Empty,
                    QuestionContent = exercise?.QuestionContent ?? string.Empty,
                    Type = exercise?.Type ?? ExerciseType.ShortAnswer,
                    Options = exercise?.Options,
                    MaxScore = item.Score,
                    Content = answer?.Content,
                    StandardAnswer = includeStandardAnswer ? exercise?.Answer : null,
                    QuestionAnalysis = includeStandardAnswer ? exercise?.QuestionAnalysis : null,
                    Score = answer?.Score,
                    Feedback = answer?.Feedback,
                    IsCorrect = answer?.IsCorrect,
                    IsAiGraded = answer?.IsAiGraded ?? false
                };
            }).ToList()
        };
    }

    /// <summary>汇总总分、判定是否及格并置为已评卷。</summary>
    private async Task ApplyTotalAsync(
        StudentExam studentExam,
        Exam exam,
        List<ExamExercise> items,
        List<StudentAnswer> answers)
    {
        var total = answers.Sum(x => x.Score ?? 0);
        var isPassed = total >= (exam.PassingScore > 0 ? exam.PassingScore : 60);

        studentExam.SetGrade(total, isPassed);
        await _studentExamRepository.UpdateAsync(studentExam);

        _ = items;
    }

    private async Task EnsureCanTakeExamAsync(Exam exam, Guid userId)
    {
        if (exam.StartTime.HasValue && Clock.Now < exam.StartTime.Value)
        {
            throw new UserFriendlyException($"考试尚未开始（{exam.StartTime.Value:yyyy-MM-dd HH:mm} 开考）");
        }
        if (exam.EndTime.HasValue && Clock.Now > exam.EndTime.Value)
        {
            throw new UserFriendlyException("考试已结束");
        }

        // 教师（具备课程编辑权限）可直接预览试卷；学生必须已选课
        if (await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Courses.Edit))
        {
            return;
        }

        var enrolled = await _studentCourseRepository.AnyAsync(
            x => x.StudentId == userId && x.CourseId == exam.CourseId);
        if (!enrolled)
        {
            throw new UserFriendlyException("请先选修该课程后再参加考试");
        }
    }

    // ── 智学分析联动：考试作答写入统一作答记录 ──────────────

    private async Task SyncAnswerRecordsByExamItemsAsync(
        StudentExam studentExam,
        Exam exam,
        List<ExamExercise> items)
    {
        var exercises = await GetExerciseMapAsync(items.Select(x => x.ExerciseId));
        await SyncAnswerRecordsAsync(studentExam, exam, items, exercises);
    }

    /// <summary>
    /// 把「已判分」的考试作答同步进 <see cref="AnswerRecord"/>：
    /// 先按 <c>StudentExamId</c> 整体删除再重建，避免重复判卷造成重复观测；
    /// 随后重算该学生该课程的知识点掌握度，考试结果因此进入薄弱点 / 推荐口径。
    /// </summary>
    private async Task SyncAnswerRecordsAsync(
        StudentExam studentExam,
        Exam exam,
        List<ExamExercise> items,
        Dictionary<Guid, Exercise> exercises)
    {
        // StudentExamId 全局唯一，跨租户读取是安全的（宿主管理员代判卷时必须能读到租户内的记录），
        // 否则按租户过滤会导致「旧记录删不掉、学生答案读不到」，重复判卷会重复计分。
        List<AnswerRecord> existing;
        List<StudentAnswer> answers;
        using (DataFilter.Disable<IMultiTenant>())
        {
            existing = await _answerRecordRepository.GetListAsync(x => x.StudentExamId == studentExam.Id);
            answers = await _studentAnswerRepository.GetListAsync(x => x.StudentExamId == studentExam.Id);
        }

        if (existing.Count > 0)
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                await _answerRecordRepository.DeleteManyAsync(existing, autoSave: true);
            }
        }
        var answeredAt = studentExam.SubmittedAt ?? studentExam.GradedAt ?? Clock.Now;

        // 归属租户以「课程」为准：宿主管理员代学生预览/提交时 StudentExam.TenantId 为空，
        // 但课程、题库、知识点都在课程所属租户内，写错租户会导致掌握度永远读不到数据。
        var course = await FindCourseAsync(exam.CourseId);
        var ownerTenantId = course?.TenantId ?? studentExam.TenantId;

        var inserted = 0;
        foreach (var item in items)
        {
            var answer = answers.FirstOrDefault(x => x.ExerciseId == item.ExerciseId);
            if (answer?.Score == null)
            {
                continue; // 未判分的题目不计入掌握度，避免把「还没批」当成「答错」
            }

            exercises.TryGetValue(item.ExerciseId, out var exercise);
            await _answerRecordRepository.InsertAsync(new AnswerRecord(
                GuidGenerator.Create(),
                studentExam.StudentId,
                exam.CourseId,
                item.ExerciseId)
            {
                TenantId = ownerTenantId,
                ChapterId = exercise?.ChapterId,
                Score = answer.Score.Value,
                FullScore = item.Score > 0 ? item.Score : 1,
                Source = AnswerSource.Exam,
                AnsweredAt = answeredAt,
                StudentExamId = studentExam.Id
            }, autoSave: false);
            inserted++;
        }

        if (inserted == 0)
        {
            return;
        }

        await SaveChangesAsync();

        try
        {
            // 切换到学生所在租户再重算：题库 / 知识点 / 掌握度都是租户内数据，
            // 宿主管理员代判卷时如果不切换，会读到空的课程上下文（掌握度全 0、证据为空）。
            using (CurrentTenant.Change(ownerTenantId))
            {
                await _masteryService.RecalculateAsync(studentExam.StudentId, exam.CourseId);
            }
        }
        catch (Exception ex)
        {
            // 掌握度重算失败不应回滚考试提交/判卷结果
            Logger.LogWarning(ex, "考试作答写入后重算掌握度失败：StudentExamId={StudentExamId}", studentExam.Id);
        }
    }

    // ── 客观题判分 ──────────────────────────────────────────

    public static bool IsObjective(ExerciseType type)
        => type is ExerciseType.SingleChoice or ExerciseType.MultiChoice
            or ExerciseType.TrueFalse or ExerciseType.FillBlank;

    /// <summary>
    /// 客观题判定：单选忽略大小写与标点；多选按选项集合比较；判断统一为 true/false；
    /// 填空按去空格 + 忽略大小写比较，支持「答案1|答案2」多备选。
    /// </summary>
    public static bool IsObjectiveCorrect(ExerciseType type, string? studentAnswer, string? standardAnswer)
    {
        var student = NormalizeText(studentAnswer);
        if (string.IsNullOrEmpty(student))
        {
            return false;
        }

        switch (type)
        {
            case ExerciseType.SingleChoice:
            {
                var target = ExtractLetters(standardAnswer);
                var mine = ExtractLetters(student);
                return target.Count > 0 && mine.Count > 0 && mine[0] == target[0];
            }
            case ExerciseType.MultiChoice:
            {
                var target = ExtractLetters(standardAnswer).OrderBy(x => x).ToList();
                var mine = ExtractLetters(student).OrderBy(x => x).ToList();
                return target.Count > 0 && target.SequenceEqual(mine);
            }
            case ExerciseType.TrueFalse:
            {
                var target = NormalizeBoolean(standardAnswer);
                var mine = NormalizeBoolean(student);
                return target.HasValue && mine.HasValue && target == mine;
            }
            case ExerciseType.FillBlank:
            {
                var candidates = (standardAnswer ?? string.Empty)
                    .Split(new[] { '|', '；', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(NormalizeForCompare)
                    .Where(x => x.Length > 0)
                    .ToList();
                var mine = NormalizeForCompare(student);
                return candidates.Count > 0 && candidates.Any(c => c == mine);
            }
            default:
                return false;
        }
    }

    private static string NormalizeText(string? value)
        => (value ?? string.Empty).Trim();

    /// <summary>从答案里提取选项字母（A/B/C…），兼容「A」「A,B」「AB」「A、B」等写法。</summary>
    private static List<string> ExtractLetters(string? answer)
    {
        var text = NormalizeText(answer).ToUpperInvariant();
        var letters = Regex.Matches(text, "[A-Z]")
            .Select(m => m.Value)
            .Where(x => x.Length == 1 && x[0] >= 'A' && x[0] <= 'Z')
            .Distinct()
            .ToList();
        return letters;
    }

    private static bool? NormalizeBoolean(string? answer)
    {
        var text = NormalizeForCompare(answer);
        if (text.Length == 0) return null;

        if (text is "true" or "t" or "1" or "正确" or "对" or "是" or "√" or "yes" or "y")
        {
            return true;
        }
        if (text is "false" or "f" or "0" or "错误" or "错" or "否" or "×" or "x" or "no" or "n")
        {
            return false;
        }
        return null;
    }

    private static string NormalizeForCompare(string? value)
        => Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), @"\s+", string.Empty);

    private static string TypeName(ExerciseType type) => type switch
    {
        ExerciseType.SingleChoice => "单选题",
        ExerciseType.MultiChoice => "多选题",
        ExerciseType.TrueFalse => "判断题",
        ExerciseType.FillBlank => "填空题",
        ExerciseType.ShortAnswer => "问答题",
        ExerciseType.Essay => "论述题",
        ExerciseType.CaseAnalysis => "案例分析",
        ExerciseType.TermExplanation => "名词解释",
        ExerciseType.Calculation => "计算题",
        ExerciseType.Programming => "编程题",
        _ => "题目"
    };
}
