using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Dtos;
using KnowledgeHub.LearningAnalytics.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 学生端专项练习：取推荐题集 → 作答（复用在线作答记录，自动判分）→ 回写掌握度。
/// 这是「推荐 → 练习 → 掌握度更新」闭环的最后一环。
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize]
public class PracticeAppService : KnowledgeHubAppService, IPracticeAppService
{
    private readonly RecommendationEngine _recommendation;
    private readonly MasteryRecalculationService _masteryService;
    private readonly ThresholdResolver _thresholdResolver;
    private readonly IStudentExerciseRecordAppService _recordService;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<ExerciseKnowledgePoint, Guid> _mapRepository;
    private readonly IRepository<KnowledgePoint, Guid> _kpRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    /// <summary>课程知识点树（直连抽题用）。</summary>
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;

    public PracticeAppService(
        RecommendationEngine recommendation,
        MasteryRecalculationService masteryService,
        ThresholdResolver thresholdResolver,
        IStudentExerciseRecordAppService recordService,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<ExerciseKnowledgePoint, Guid> mapRepository,
        IRepository<KnowledgePoint, Guid> kpRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository)
    {
        _recommendation = recommendation;
        _masteryService = masteryService;
        _thresholdResolver = thresholdResolver;
        _recordService = recordService;
        _exerciseRepository = exerciseRepository;
        _mapRepository = mapRepository;
        _kpRepository = kpRepository;
        _chapterRepository = chapterRepository;
        _courseRepository = courseRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
    }

    public async Task<PracticeSetDto> GetSetAsync(
        Guid courseId,
        Guid? knowledgePoint = null,
        int take = 6,
        Guid? knowledgeResourceId = null)
    {
        var studentId = CurrentUser.Id ?? throw new AbpAuthorizationException("未登录");
        var course = await _courseRepository.FindAsync(courseId);

        var result = new PracticeSetDto
        {
            CourseId = courseId,
            CourseName = course?.Title ?? string.Empty,
        };

        // ① 指定课程知识点（视频「练一练」/ 知识点页直达）：直接按 Exercise.KnowledgeResourceId 取题。
        //    这条路不依赖「智学分析 KP」映射，所以即使映射还没跑通也能精确练到该知识点。
        if (knowledgeResourceId.HasValue)
        {
            var node = await _knowledgeResourceRepository.FindAsync(knowledgeResourceId.Value);
            var direct = (await _exerciseRepository.GetListAsync(e =>
                    e.CourseId == courseId && e.KnowledgeResourceId == knowledgeResourceId.Value))
                .Take(Math.Clamp(take, 1, 20))
                .ToList();

            if (direct.Count == 0)
            {
                result.Message = $"「{node?.Name ?? "该知识点"}」下还没有习题，先在「习题管理」里把题目绑定到这个知识点。";
                return result;
            }

            var directIds = direct.Select(x => x.Id).ToList();
            var directMaps = await _mapRepository.GetListAsync(m => directIds.Contains(m.ExerciseId));
            var directKpIds = directMaps.Select(m => m.KnowledgePointId).Distinct().ToList();
            var directKpNames = directKpIds.Any()
                ? (await _kpRepository.GetListAsync(k => directKpIds.Contains(k.Id))).ToDictionary(k => k.Id, k => k.Name)
                : new Dictionary<Guid, string>();

            result.KnowledgePointName = node?.Name ?? "指定知识点";
            result.Reason = "按课程知识点直接抽题";
            result.Items = direct.Select(e => new PracticeExerciseDto
            {
                Id = e.Id,
                Title = e.Title,
                QuestionContent = e.QuestionContent,
                Type = e.Type,
                Options = e.Options,
                Score = e.Score,
                Difficulty = e.Difficulty,
                KnowledgePointNames = directMaps.Where(m => m.ExerciseId == e.Id)
                    .Select(m => directKpNames.GetValueOrDefault(m.KnowledgePointId))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => n!)
                    .Distinct()
                    .ToList(),
            }).ToList();

            return result;
        }

        // 推荐引擎按薄弱知识点分组给出候选（内容 + 协同 + 难度适配）
        var groups = await _recommendation.RecommendPracticeAsync(studentId, courseId, null, maxKnowledgePoints: 3);
        if (!groups.Any())
        {
            result.Message = "暂无可推荐的练习题：先完成课程练习或等待老师上传试卷后再来。";
            return result;
        }

        var group = knowledgePoint.HasValue
            ? groups.FirstOrDefault(g => g.KnowledgePointId == knowledgePoint.Value)
            : null;
        group ??= groups.First();

        var exerciseIds = group.ExerciseIds.Take(Math.Clamp(take, 1, 20)).ToList();
        if (!exerciseIds.Any())
        {
            result.Message = "该知识点暂无可用习题。";
            return result;
        }

        var exercises = (await _exerciseRepository.GetListAsync(e => exerciseIds.Contains(e.Id)))
            .OrderBy(e => exerciseIds.IndexOf(e.Id))
            .ToList();

        var chapterIds = exercises.Where(e => e.ChapterId.HasValue).Select(e => e.ChapterId!.Value).Distinct().ToList();
        var chapters = chapterIds.Any()
            ? (await _chapterRepository.GetListAsync(c => chapterIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Title)
            : new Dictionary<Guid, string>();

        var maps = await _mapRepository.GetListAsync(m => exerciseIds.Contains(m.ExerciseId));
        var kpIds = maps.Select(m => m.KnowledgePointId).Distinct().ToList();
        var kpNames = kpIds.Any()
            ? (await _kpRepository.GetListAsync(k => kpIds.Contains(k.Id))).ToDictionary(k => k.Id, k => k.Name)
            : new Dictionary<Guid, string>();

        result.KnowledgePointId = group.KnowledgePointId;
        result.KnowledgePointName = group.Title.Replace("专项练习", string.Empty);
        result.Reason = group.Reason;

        result.Items = exercises.Select(e => new PracticeExerciseDto
        {
            Id = e.Id,
            Title = e.Title,
            QuestionContent = e.QuestionContent,
            Type = e.Type,
            Options = e.Options,
            Score = e.Score,
            Difficulty = e.Difficulty,
            ChapterTitle = e.ChapterId.HasValue ? chapters.GetValueOrDefault(e.ChapterId.Value) : null,
            KnowledgePointNames = maps.Where(m => m.ExerciseId == e.Id)
                .Select(m => kpNames.GetValueOrDefault(m.KnowledgePointId))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct()
                .ToList(),
        }).ToList();

        return result;
    }

    public async Task<PracticeResultDto> SubmitAsync(SubmitPracticeInput input)
    {
        var studentId = CurrentUser.Id ?? throw new AbpAuthorizationException("未登录");
        if (input?.Answers == null || !input.Answers.Any())
        {
            throw new UserFriendlyException("没有需要提交的作答");
        }

        var answers = input.Answers.Take(50).ToList();
        var exerciseIds = answers.Select(a => a.ExerciseId).Distinct().ToList();

        var exercises = (await _exerciseRepository.GetListAsync(e => exerciseIds.Contains(e.Id)))
            .ToDictionary(e => e.Id);

        var result = new PracticeResultDto();
        var itemResults = new List<PracticeItemResultDto>();

        foreach (var answer in answers)
        {
            if (!exercises.TryGetValue(answer.ExerciseId, out var exercise))
            {
                continue;
            }

            // 复用在线作答服务：自动判分 + 同步选课完成度（未选课会抛业务异常）
            var record = await _recordService.SaveOrUpdateRecordAsync(new SaveExerciseRecordInput
            {
                CourseId = input.CourseId,
                ChapterId = exercise.ChapterId,
                ExerciseId = exercise.Id,
                StudentAnswer = answer.Answer,
                TimeSpentTicks = Math.Max(0, answer.TimeSpentTicks),
            });

            result.TotalCount++;
            if (record.IsCorrect == true) result.CorrectCount++;
            else if (record.IsCorrect == false) result.WrongCount++;

            itemResults.Add(new PracticeItemResultDto
            {
                ExerciseId = exercise.Id,
                Title = exercise.Title,
                StudentAnswer = record.StudentAnswer,
                CorrectAnswer = exercise.Answer,
                IsCorrect = record.IsCorrect,
                Analysis = exercise.QuestionAnalysis ?? exercise.AnswerExplanation,
            });
        }

        // 回写掌握度（在线作答 + 纸质作答统一重算）
        var snapshots = await _masteryService.RecalculateAsync(studentId, input.CourseId);
        var thresholds = await _thresholdResolver.ResolveAllAsync(input.CourseId);

        var maps = await _mapRepository.GetListAsync(m => exerciseIds.Contains(m.ExerciseId));
        var relatedKpIds = maps.Select(m => m.KnowledgePointId).Distinct().ToHashSet();

        result.KnowledgePoints = snapshots
            .Where(s => relatedKpIds.Contains(s.KnowledgePointId))
            .Select(s =>
            {
                var effective = thresholds.For(s.KnowledgePointId);
                return new KnowledgeMasteryItemDto
                {
                    KnowledgePointId = s.KnowledgePointId,
                    Name = s.Name,
                    ChapterTitle = s.ChapterTitle,
                    Mastery = Math.Round(s.Mastery, 4),
                    Evidence = Math.Round(s.Evidence, 2),
                    Level = (int)effective.Judge(s.Mastery),
                    TargetLine = effective.Ok,
                    HasCustomThreshold = thresholds.Overrides.ContainsKey(s.KnowledgePointId),
                };
            })
            .OrderBy(k => k.Mastery)
            .ToList();

        result.Items = itemResults;
        return result;
    }
}
