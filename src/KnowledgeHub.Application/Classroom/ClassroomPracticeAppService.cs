using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Classroom.Dtos;
using KnowledgeHub.Classroom.Enums;
using KnowledgeHub.Exams;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Classroom;

/// <summary>
/// 课堂练习：教师选范围 → **快照抽题** → 短链下发 → 客观题即时判定 → 教师实时统计。
/// <para>
/// 与智学分析的个人专项练习（<c>PracticeAppService</c>，按薄弱知识点动态抽题）刻意分开：
/// 这里是全班同一套题，题面在场次创建时固定，重复刷新不会换题。
/// 判分复用 <see cref="ExerciseGrader"/>，与平时练习同一套规则。
/// </para>
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize]
public class ClassroomPracticeAppService : KnowledgeHubAppService, IClassroomPracticeAppService
{
    private readonly IRepository<PracticeSession, Guid> _sessionRepository;
    private readonly IRepository<PracticeSessionItem, Guid> _itemRepository;
    private readonly IRepository<PracticeAttempt, Guid> _attemptRepository;
    private readonly IRepository<PracticeAttemptAnswer, Guid> _answerRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<ChapterExercise, Guid> _chapterExerciseRepository;
    private readonly ClassroomSupport _support;
    private readonly CourseMasteryRecalculationService _masteryRecalculation;
    private readonly IStudentActivityLogger _activityLogger;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentUser _currentUser;

    public ClassroomPracticeAppService(
        IRepository<PracticeSession, Guid> sessionRepository,
        IRepository<PracticeSessionItem, Guid> itemRepository,
        IRepository<PracticeAttempt, Guid> attemptRepository,
        IRepository<PracticeAttemptAnswer, Guid> answerRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<ChapterExercise, Guid> chapterExerciseRepository,
        ClassroomSupport support,
        CourseMasteryRecalculationService masteryRecalculation,
        IStudentActivityLogger activityLogger,
        IDataFilter dataFilter,
        ICurrentUser currentUser)
    {
        _sessionRepository = sessionRepository;
        _itemRepository = itemRepository;
        _attemptRepository = attemptRepository;
        _answerRepository = answerRepository;
        _exerciseRepository = exerciseRepository;
        _chapterExerciseRepository = chapterExerciseRepository;
        _support = support;
        _masteryRecalculation = masteryRecalculation;
        _activityLogger = activityLogger;
        _dataFilter = dataFilter;
        _currentUser = currentUser;
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Practice)]
    public async Task<PagedResultDto<PracticeSessionDto>> GetListAsync(Guid? courseId, int skipCount, int maxResultCount)
    {
        List<PracticeSession> items;
        int total;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _sessionRepository.GetQueryableAsync();
            query = query.WhereIf(courseId.HasValue, x => x.CourseId == courseId!.Value);
            total = await AsyncExecuter.CountAsync(query);
            items = await AsyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.StartedAt).Skip(skipCount).Take(maxResultCount));
        }

        var dtos = new List<PracticeSessionDto>(items.Count);
        foreach (var session in items)
        {
            dtos.Add(await MapWithCountsAsync(session));
        }

        return new PagedResultDto<PracticeSessionDto>(total, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Practice)]
    public async Task<PracticeSessionDto> GetAsync(Guid id)
    {
        var session = await _sessionRepository.GetAsync(id);
        return await MapWithCountsAsync(session);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Practice)]
    public async Task<PracticeSessionDto> CreateAsync(CreatePracticeSessionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写练习标题。");
        }

        var course = await _support.GetCourseAsync(input.CourseId);
        var questionCount = input.QuestionCount <= 0 ? 5 : Math.Min(input.QuestionCount, 50);

        var pool = await LoadExercisePoolAsync(input.CourseId, input.ChapterId, input.KnowledgeResourceIds);
        if (pool.Count == 0)
        {
            throw new UserFriendlyException("该范围内没有可用习题，请先在「习题管理」里添加题目。");
        }

        // 快照抽题：随机打乱后取前 N 道，写入 Items 固定下来
        var picked = pool
            .OrderBy(_ => Guid.NewGuid())
            .Take(Math.Min(questionCount, pool.Count))
            .ToList();

        var me = _support.CurrentUserIdOrThrow();
        var session = new PracticeSession(
            GuidGenerator.Create(),
            input.CourseId,
            input.Title.Trim(),
            ClassroomSupport.NewToken(),
            me)
        {
            ChapterId = input.ChapterId,
            KnowledgeResourceIds = input.KnowledgeResourceIds?.Any() == true
                ? System.Text.Json.JsonSerializer.Serialize(input.KnowledgeResourceIds)
                : null,
            QuestionCount = picked.Count,
            StartedAt = DateTime.UtcNow,
            TenantId = course.TenantId
        };

        await _sessionRepository.InsertAsync(session);

        for (var i = 0; i < picked.Count; i++)
        {
            await _itemRepository.InsertAsync(new PracticeSessionItem(
                GuidGenerator.Create(), session.Id, picked[i].Id, i));
        }

        return await MapWithCountsAsync(session);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Practice)]
    public async Task<PracticeSessionDto> CloseAsync(Guid id)
    {
        var session = await _sessionRepository.GetAsync(id);
        session.Close();
        await _sessionRepository.UpdateAsync(session);
        return await MapWithCountsAsync(session);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Practice)]
    public async Task DeleteAsync(Guid id)
    {
        await DeleteAttemptsAsync(id);

        List<PracticeSessionItem> items;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _itemRepository.GetQueryableAsync();
            items = await AsyncExecuter.ToListAsync(query.Where(x => x.SessionId == id));
        }

        foreach (var item in items)
        {
            await _itemRepository.DeleteAsync(item);
        }

        await _sessionRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Practice)]
    public async Task<PracticeStatsDto> GetStatsAsync(Guid id)
    {
        var session = await _sessionRepository.GetAsync(id);
        var exercises = await LoadSessionExercisesAsync(id);
        var exerciseMap = exercises.ToDictionary(x => x.Id);
        var enrolled = await _support.GetEnrolledStudentIdsAsync(session.CourseId);

        List<PracticeAttempt> attempts;
        List<PracticeAttemptAnswer> answers;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var attemptQuery = await _attemptRepository.GetQueryableAsync();
            attempts = await AsyncExecuter.ToListAsync(attemptQuery.Where(x => x.SessionId == id));

            var attemptIds = attempts.Select(x => x.Id).ToList();
            var answerQuery = await _answerRepository.GetQueryableAsync();
            answers = attemptIds.Count == 0
                ? new List<PracticeAttemptAnswer>()
                : await AsyncExecuter.ToListAsync(answerQuery.Where(x => attemptIds.Contains(x.AttemptId)));
        }

        var names = await _support.GetUserNamesAsync(attempts.Select(x => x.StudentId).Concat(enrolled));

        var stats = new PracticeStatsDto
        {
            SessionId = id,
            Title = session.Title,
            SubmittedCount = attempts.Count,
            ExpectedCount = enrolled.Count
        };
        stats.SubmitRate = stats.ExpectedCount > 0 ? (double)stats.SubmittedCount / stats.ExpectedCount : 0;

        // 逐题正确率
        foreach (var exercise in exercises)
        {
            var itemAnswers = answers.Where(a => a.ExerciseId == exercise.Id).ToList();
            var graded = itemAnswers.Where(a => a.IsCorrect.HasValue).ToList();
            stats.Items.Add(new PracticeItemStatsDto
            {
                ExerciseId = exercise.Id,
                Title = exercise.Title,
                AnsweredCount = itemAnswers.Count,
                CorrectCount = graded.Count(x => x.IsCorrect == true),
                CorrectRate = graded.Count == 0 ? 0 : (double)graded.Count(x => x.IsCorrect == true) / graded.Count
            });
        }

        foreach (var attempt in attempts.OrderByDescending(x => x.SubmittedAt))
        {
            names.TryGetValue(attempt.StudentId, out var pair);
            stats.Submitters.Add(new PracticeSubmitterDto
            {
                StudentId = attempt.StudentId,
                StudentName = attempt.StudentName ?? pair.Name ?? pair.UserName,
                SubmittedAt = attempt.SubmittedAt,
                CorrectCount = attempt.CorrectCount,
                TotalCount = attempt.TotalCount
            });
        }

        var submittedIds = attempts.Select(x => x.StudentId).ToHashSet();
        foreach (var studentId in enrolled.Where(x => !submittedIds.Contains(x)))
        {
            names.TryGetValue(studentId, out var pair);
            stats.NotSubmitted.Add(new CheckInAbsentDto
            {
                StudentId = studentId,
                StudentName = pair.Name ?? pair.UserName,
                StudentUserName = pair.UserName
            });
        }

        // 全班平均正确率（只算已判定的客观题）
        var allGraded = answers.Where(a => a.IsCorrect.HasValue).ToList();
        stats.AverageAccuracy = allGraded.Count == 0
            ? 0
            : (double)allGraded.Count(x => x.IsCorrect == true) / allGraded.Count;

        return stats;
    }

    // ── 学生侧 ────────────────────────────────────────────────────

    [Authorize(KnowledgeHubPermissions.Classroom.Participate)]
    public async Task<List<PracticeSessionDto>> GetMySessionsAsync([FromQuery] Guid? courseId = null)
    {
        var me = _support.CurrentUserIdOrThrow();

        List<PracticeSession> sessions;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var sessionQuery = await _sessionRepository.GetQueryableAsync();
            sessions = await AsyncExecuter.ToListAsync(
                sessionQuery
                    .WhereIf(courseId.HasValue, x => x.CourseId == courseId!.Value)
                    .OrderByDescending(x => x.StartedAt)
                    .Take(100));
        }

        // 只保留「我确实选修了该课程」的场次（按课程逐个核对选课关系，跨租户共享课程也成立）
        var result = new List<PracticeSessionDto>();
        var checkedCourses = new Dictionary<Guid, bool>();
        foreach (var session in sessions)
        {
            if (!checkedCourses.TryGetValue(session.CourseId, out var isEnrolled))
            {
                var studentIds = await _support.GetEnrolledStudentIdsAsync(session.CourseId);
                isEnrolled = studentIds.Contains(me);
                checkedCourses[session.CourseId] = isEnrolled;
            }

            if (isEnrolled)
            {
                result.Add(await MapWithCountsAsync(session));
            }
        }

        return result;
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Participate)]
    public async Task<PracticePaperDto> GetPaperAsync(string token)
    {
        var session = await GetSessionByTokenAsync(token);
        await _support.EnsureEnrolledAsync(session.CourseId);

        var exercises = await LoadSessionExercisesAsync(session.Id);
        var paper = new PracticePaperDto
        {
            SessionId = session.Id,
            Title = session.Title,
            Status = session.Status,
            CourseId = session.CourseId,
            CourseTitle = await _support.GetCourseTitleAsync(session.CourseId)
        };

        for (var i = 0; i < exercises.Count; i++)
        {
            var exercise = exercises[i];
            paper.Items.Add(new PracticePaperItemDto
            {
                ExerciseId = exercise.Id,
                SortOrder = i,
                Title = exercise.Title,
                QuestionContent = exercise.QuestionContent,
                Type = (int)exercise.Type,
                Options = exercise.Options
            });
        }

        var me = _support.CurrentUserIdOrThrow();
        PracticeAttempt? attempt = null;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _attemptRepository.GetQueryableAsync();
            attempt = await AsyncExecuter.FirstOrDefaultAsync(
                query.Where(x => x.SessionId == session.Id && x.StudentId == me));
        }

        if (attempt != null)
        {
            paper.MyAttempt = await BuildAttemptResultAsync(attempt, exercises);
        }

        return paper;
    }

    [Authorize(KnowledgeHubPermissions.Classroom.Participate)]
    public async Task<PracticeAttemptResultDto> SubmitAsync(SubmitPracticeInput input)
    {
        var session = await GetSessionByTokenAsync(input.Token);
        await _support.EnsureEnrolledAsync(session.CourseId);

        if (!session.IsOpen)
        {
            throw new UserFriendlyException("本次课堂练习已结束，不能再提交。");
        }

        var exercises = await LoadSessionExercisesAsync(session.Id);
        var exerciseMap = exercises.ToDictionary(x => x.Id);
        var me = _support.CurrentUserIdOrThrow();
        var displayName = await _support.GetDisplayNameAsync(me);

        PracticeAttempt? attempt;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _attemptRepository.GetQueryableAsync();
            attempt = await AsyncExecuter.FirstOrDefaultAsync(
                query.Where(x => x.SessionId == session.Id && x.StudentId == me));
        }

        var isNew = attempt == null;
        if (attempt == null)
        {
            attempt = new PracticeAttempt(GuidGenerator.Create(), session.Id, me)
            {
                TenantId = session.TenantId
            };
            attempt.StudentName = displayName;
        }

        // 覆盖式：先删旧答案再写新答案（同一学生重复提交按最后一次计）
        List<PracticeAttemptAnswer> oldAnswers;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var answerQuery = await _answerRepository.GetQueryableAsync();
            oldAnswers = await AsyncExecuter.ToListAsync(answerQuery.Where(x => x.AttemptId == attempt.Id));
        }

        foreach (var old in oldAnswers)
        {
            await _answerRepository.DeleteAsync(old);
        }

        var correct = 0;
        var objectiveTotal = 0;

        foreach (var submitted in input.Answers)
        {
            if (!exerciseMap.TryGetValue(submitted.ExerciseId, out var exercise))
            {
                continue; // 不在本场次的题直接忽略（防越权作答别的题）
            }

            var isCorrect = ExerciseGrader.Grade(exercise.Type, exercise.Answer, submitted.Answer);
            if (isCorrect.HasValue)
            {
                objectiveTotal++;
                if (isCorrect.Value)
                {
                    correct++;
                }
            }

            await _answerRepository.InsertAsync(new PracticeAttemptAnswer(
                GuidGenerator.Create(), attempt.Id, submitted.ExerciseId)
            {
                Answer = submitted.Answer,
                IsCorrect = isCorrect,
                TenantId = session.TenantId
            });
        }

        attempt.CorrectCount = correct;
        attempt.TotalCount = objectiveTotal;
        attempt.SubmittedAt = DateTime.UtcNow;
        attempt.StudentName ??= displayName;

        if (isNew)
        {
            await _attemptRepository.InsertAsync(attempt);
        }
        else
        {
            await _attemptRepository.UpdateAsync(attempt);
        }

        // 课堂练习也算一次学习证据：刷新知识点掌握度 + 记行为日志
        try
        {
            await _masteryRecalculation.RecalculateAsync(me, session.CourseId);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "课堂练习提交后掌握度重算失败 sessionId={SessionId}", session.Id);
        }

        await _activityLogger.LogAsync(
            me, StudentActivityAction.Practice, session.CourseId, "practice", session.Id,
            new { correct, total = objectiveTotal });

        return await BuildAttemptResultAsync(attempt, exercises);
    }

    // ── 内部 ──────────────────────────────────────────────────────

    private async Task<PracticeSession> GetSessionByTokenAsync(string token)
    {
        PracticeSession? session;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _sessionRepository.GetQueryableAsync();
            session = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Token == token));
        }

        if (session == null)
        {
            throw new UserFriendlyException("练习链接无效或已失效。");
        }

        return session;
    }

    /// <summary>按场次取固定题目集合（按 Items 排序）。</summary>
    private async Task<List<Exercise>> LoadSessionExercisesAsync(Guid sessionId)
    {
        List<PracticeSessionItem> items;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _itemRepository.GetQueryableAsync();
            items = await AsyncExecuter.ToListAsync(
                query.Where(x => x.SessionId == sessionId).OrderBy(x => x.SortOrder));
        }

        if (items.Count == 0)
        {
            return new List<Exercise>();
        }

        var exerciseIds = items.Select(x => x.ExerciseId).ToList();
        List<Exercise> exercises;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _exerciseRepository.GetQueryableAsync();
            exercises = await AsyncExecuter.ToListAsync(query.Where(x => exerciseIds.Contains(x.Id)));
        }

        var map = exercises.ToDictionary(x => x.Id);
        return items.Where(i => map.ContainsKey(i.ExerciseId)).Select(i => map[i.ExerciseId]).ToList();
    }

    /// <summary>
    /// 抽题池：课程内全部习题，可按章节（主章节或多对多关联命中）与知识点收窄。
    /// 与组卷逻辑保持同一口径：章节集合「任一命中即入选」。
    /// </summary>
    private async Task<List<Exercise>> LoadExercisePoolAsync(
        Guid courseId, Guid? chapterId, List<Guid>? knowledgeResourceIds)
    {
        List<Exercise> exercises;
        List<ChapterExercise> chapterLinks;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _exerciseRepository.GetQueryableAsync();
            exercises = await AsyncExecuter.ToListAsync(query.Where(x => x.CourseId == courseId));

            var linkQuery = await _chapterExerciseRepository.GetQueryableAsync();
            chapterLinks = await AsyncExecuter.ToListAsync(linkQuery);
        }

        var pool = exercises.AsEnumerable();

        if (chapterId.HasValue)
        {
            var linkedExerciseIds = chapterLinks
                .Where(x => x.ChapterId == chapterId.Value)
                .Select(x => x.ExerciseId)
                .ToHashSet();

            pool = pool.Where(x => x.ChapterId == chapterId.Value || linkedExerciseIds.Contains(x.Id));
        }

        if (knowledgeResourceIds?.Any() == true)
        {
            var krIds = knowledgeResourceIds.ToHashSet();
            pool = pool.Where(x => x.KnowledgeResourceId.HasValue && krIds.Contains(x.KnowledgeResourceId.Value));
        }

        // 主观题不适合即时统计，抽题池里排除（课堂练习定位是"当堂快速检测"）
        return pool.Where(x => ExerciseGrader.IsObjective(x.Type)).ToList();
    }

    private async Task<PracticeAttemptResultDto> BuildAttemptResultAsync(
        PracticeAttempt attempt, List<Exercise> exercises)
    {
        List<PracticeAttemptAnswer> answers;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _answerRepository.GetQueryableAsync();
            answers = await AsyncExecuter.ToListAsync(
                query.Where(x => x.AttemptId == attempt.Id));
        }

        var exerciseMap = exercises.ToDictionary(x => x.Id);
        var result = new PracticeAttemptResultDto
        {
            AttemptId = attempt.Id,
            SessionId = attempt.SessionId,
            SubmittedAt = attempt.SubmittedAt,
            CorrectCount = attempt.CorrectCount,
            TotalCount = attempt.TotalCount,
            Accuracy = attempt.TotalCount > 0 ? (double)attempt.CorrectCount / attempt.TotalCount : 0
        };

        foreach (var exercise in exercises)
        {
            var answer = answers.FirstOrDefault(a => a.ExerciseId == exercise.Id);
            result.Answers.Add(new PracticeAnswerResultDto
            {
                ExerciseId = exercise.Id,
                Title = exercise.Title,
                Answer = answer?.Answer,
                CorrectAnswer = exercise.Answer,
                IsCorrect = answer?.IsCorrect,
                AnswerExplanation = exercise.AnswerExplanation
            });
        }

        return result;
    }

    private async Task<PracticeSessionDto> MapWithCountsAsync(PracticeSession session)
    {
        var enrolled = await _support.GetEnrolledStudentIdsAsync(session.CourseId);

        int submitted;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _attemptRepository.GetQueryableAsync();
            submitted = await AsyncExecuter.CountAsync(query.Where(x => x.SessionId == session.Id));
        }

        return new PracticeSessionDto
        {
            Id = session.Id,
            CourseId = session.CourseId,
            CourseTitle = await _support.GetCourseTitleAsync(session.CourseId),
            Title = session.Title,
            Token = session.Token,
            Status = session.Status,
            ChapterId = session.ChapterId,
            KnowledgeResourceIds = session.KnowledgeResourceIds,
            QuestionCount = session.QuestionCount,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            SubmittedCount = submitted,
            ExpectedCount = enrolled.Count,
            SubmitRate = enrolled.Count > 0 ? (double)submitted / enrolled.Count : 0,
            CreationTime = session.CreationTime,
            CreatorId = session.CreatorId,
            LastModificationTime = session.LastModificationTime,
            LastModifierId = session.LastModifierId
        };
    }

    private async Task DeleteAttemptsAsync(Guid sessionId)
    {
        List<PracticeAttempt> attempts;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _attemptRepository.GetQueryableAsync();
            attempts = await AsyncExecuter.ToListAsync(query.Where(x => x.SessionId == sessionId));
        }

        foreach (var attempt in attempts)
        {
            List<PracticeAttemptAnswer> answers;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var query = await _answerRepository.GetQueryableAsync();
                answers = await AsyncExecuter.ToListAsync(query.Where(x => x.AttemptId == attempt.Id));
            }

            foreach (var answer in answers)
            {
                await _answerRepository.DeleteAsync(answer);
            }

            await _attemptRepository.DeleteAsync(attempt);
        }
    }
}
