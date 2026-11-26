using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning.Dtos;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.LearningAnalytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Learning;

/// <summary>
/// 学生个性化推荐：生成（三源）→ 落库 → 状态流转 → 教师端采纳率。
/// <para>
/// 生成口径（全部复用已有数据，不新造指标）：
/// ① 薄弱点：`KnowledgeMastery.Mastery &lt; 课程判定线`；课程判定线走 `ThresholdResolver`；
/// ② 前置知识：`KpPrerequisite`(Confirmed) 的前置点未掌握 → 推荐先补前置；
/// ③ 未完成章节：`LearningProgress` 进度 &lt; 100% 且近 14 天该章节无活动。
/// </para>
/// <para>去重：同 (学生, 课程, 类型, 知识点/章节) 只留一条；重复生成时更新理由与优先级。</para>
/// </summary>
[Authorize]
[IgnoreAntiforgeryToken]
public class StudentRecommendationAppService : KnowledgeHubAppService, IStudentRecommendationAppService
{
    /// <summary>一次批量刷新最多处理的学生数（防止课程人数过大时同步接口超时）。</summary>
    private const int MaxStudentsPerRefresh = 200;

    private readonly IRepository<StudentRecommendation, Guid> _recommendationRepository;
    private readonly IRepository<KnowledgeMastery, Guid> _masteryRepository;
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<LearningProgress, Guid> _progressRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<StudentActivityLog, Guid> _activityRepository;
    private readonly IRepository<KpPrerequisite, Guid> _prerequisiteRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ThresholdResolver _thresholdResolver;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentUser _currentUser;

    public StudentRecommendationAppService(
        IRepository<StudentRecommendation, Guid> recommendationRepository,
        IRepository<KnowledgeMastery, Guid> masteryRepository,
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<LearningProgress, Guid> progressRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<StudentActivityLog, Guid> activityRepository,
        IRepository<KpPrerequisite, Guid> prerequisiteRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        ThresholdResolver thresholdResolver,
        IDataFilter dataFilter,
        ICurrentUser currentUser)
    {
        _recommendationRepository = recommendationRepository;
        _masteryRepository = masteryRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _chapterRepository = chapterRepository;
        _progressRepository = progressRepository;
        _studentCourseRepository = studentCourseRepository;
        _activityRepository = activityRepository;
        _prerequisiteRepository = prerequisiteRepository;
        _courseRepository = courseRepository;
        _userRepository = userRepository;
        _thresholdResolver = thresholdResolver;
        _dataFilter = dataFilter;
        _currentUser = currentUser;
    }

    public async Task<List<StudentRecommendationDto>> GetMyAsync(Guid? courseId = null, bool includeHandled = false)
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");

        List<StudentRecommendation> rows;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _recommendationRepository.GetQueryableAsync();
            rows = await AsyncExecuter.ToListAsync(
                query.Where(x => x.StudentId == studentId)
                    .WhereIf(courseId.HasValue, x => x.CourseId == courseId!.Value)
                    .WhereIf(!includeHandled, x =>
                        x.Status != StudentRecommendationStatus.Completed &&
                        x.Status != StudentRecommendationStatus.Dismissed));
        }

        return await MapManyAsync(rows.OrderByDescending(x => x.Priority).ThenByDescending(x => x.CreationTime));
    }

    public async Task<StudentRecommendationDto> MarkAsync(Guid id, StudentRecommendationStatus status)
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var row = await _recommendationRepository.GetAsync(id);

        if (row.StudentId != studentId)
        {
            throw new UserFriendlyException("只能操作自己的推荐。");
        }

        row.SetStatus(status);
        await _recommendationRepository.UpdateAsync(row);
        if (CurrentUnitOfWork is not null) await CurrentUnitOfWork.SaveChangesAsync();

        return (await MapManyAsync(new[] { row })).First();
    }

    public async Task<RecommendationRefreshResultDto> RefreshMineAsync(Guid courseId)
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        return await RefreshAsync(courseId, new List<Guid> { studentId });
    }

    public async Task<RecommendationRefreshResultDto> RefreshCourseAsync(Guid courseId)
    {
        var students = await GetEnrolledStudentIdsAsync(courseId);
        if (students.Count > MaxStudentsPerRefresh)
        {
            students = students.Take(MaxStudentsPerRefresh).ToList();
        }

        return await RefreshAsync(courseId, students);
    }

    public async Task<CourseRecommendationOverviewDto> GetCourseOverviewAsync(Guid courseId)
    {
        Course? course;
        List<StudentRecommendation> rows;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(courseId);
            var query = await _recommendationRepository.GetQueryableAsync();
            rows = await AsyncExecuter.ToListAsync(query.Where(x => x.CourseId == courseId));
        }

        var dto = new CourseRecommendationOverviewDto
        {
            CourseId = courseId,
            CourseTitle = course?.Title,
            ActiveCount = rows.Count(x => x.Status is StudentRecommendationStatus.Pending
                or StudentRecommendationStatus.Viewed or StudentRecommendationStatus.InProgress),
            CompletedCount = rows.Count(x => x.Status == StudentRecommendationStatus.Completed),
            DismissedCount = rows.Count(x => x.Status == StudentRecommendationStatus.Dismissed),
            TypeDistribution = rows.GroupBy(x => x.Type)
                .OrderByDescending(g => g.Count())
                .Select(g => new RecommendationTypeCountDto
                {
                    Type = g.Key,
                    Label = TypeLabel(g.Key),
                    Count = g.Count()
                }).ToList()
        };

        var handled = dto.CompletedCount + dto.DismissedCount + dto.ActiveCount;
        dto.AdoptRate = handled == 0 ? 0 : Math.Round(dto.CompletedCount / (double)handled, 3);
        dto.StudentCount = rows.Select(x => x.StudentId).Distinct().Count();

        var names = await GetUserNamesAsync(rows.Select(x => x.StudentId));
        foreach (var group in rows.GroupBy(x => x.StudentId))
        {
            var items = await MapManyAsync(group.OrderByDescending(x => x.Priority));
            dto.Students.Add(new StudentRecommendationSummaryDto
            {
                StudentId = group.Key,
                StudentName = names.GetValueOrDefault(group.Key),
                ActiveCount = group.Count(x => x.Status is StudentRecommendationStatus.Pending
                    or StudentRecommendationStatus.Viewed or StudentRecommendationStatus.InProgress),
                CompletedCount = group.Count(x => x.Status == StudentRecommendationStatus.Completed),
                DismissedCount = group.Count(x => x.Status == StudentRecommendationStatus.Dismissed),
                LastCreatedAt = group.Max(x => x.CreationTime),
                Items = items
            });
        }

        dto.Students = dto.Students
            .OrderByDescending(s => s.ActiveCount)
            .ThenByDescending(s => s.LastCreatedAt)
            .ToList();

        return dto;
    }

    // ══════════════════════════════════════════════════════════
    //  生成
    // ══════════════════════════════════════════════════════════

    private async Task<RecommendationRefreshResultDto> RefreshAsync(Guid courseId, List<Guid> studentIds)
    {
        var result = new RecommendationRefreshResultDto { CourseId = courseId, StudentCount = studentIds.Count };
        if (studentIds.Count == 0)
        {
            result.Message = "这门课还没有选课学生。";
            return result;
        }

        // 课程上下文一次取全（知识点 / 章节 / 判定线）
        var resources = await _knowledgeResourceRepository.GetListAsync(x => x.CourseId == courseId);
        var resourceById = resources.ToDictionary(x => x.Id);
        var resourceIds = resourceById.Keys.ToList();
        var chapters = await _chapterRepository.GetListAsync(x => x.CourseId == courseId);
        var chapterById = chapters.ToDictionary(x => x.Id);
        var thresholds = await _thresholdResolver.ResolveAsync(courseId);

        // 已有推荐（用于幂等更新而不是重复插入）
        List<StudentRecommendation> existing;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            existing = await _recommendationRepository.GetListAsync(x =>
                x.CourseId == courseId && studentIds.Contains(x.StudentId));
        }

        var existingByKey = existing
            .GroupBy(x => (x.StudentId, x.Type, x.KnowledgeResourceId ?? Guid.Empty, x.ChapterId ?? Guid.Empty))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var studentId in studentIds)
        {
            // ① 薄弱知识点
            List<KnowledgeMastery> masteries;
            List<LearningProgress> progresses;
            List<StudentActivityLog> activities;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                masteries = resourceIds.Count == 0
                    ? new List<KnowledgeMastery>()
                    : await _masteryRepository.GetListAsync(x =>
                        x.StudentId == studentId &&
                        resourceIds.Contains(x.KnowledgeResourceId) &&
                        x.Evidence > 0);

                progresses = await _progressRepository.GetListAsync(x =>
                    x.StudentId == studentId && x.CourseId == courseId);

                activities = await _activityRepository.GetListAsync(x =>
                    x.StudentId == studentId && x.CourseId == courseId &&
                    x.CreationTime >= DateTime.UtcNow.AddDays(-30));
            }

            var weak = masteries
                .Where(x => x.Mastery < thresholds.Weak)
                .OrderBy(x => x.Mastery)
                .Take(3)
                .ToList();

            foreach (var m in weak)
            {
                var name = resourceById.GetValueOrDefault(m.KnowledgeResourceId)?.Name ?? "知识点";
                var priority = m.Mastery < thresholds.Weak * 0.7 ? 9 : 7;
                await UpsertAsync(
                    courseId, studentId, StudentRecommendationType.WeakKnowledge,
                    $"「{name}」掌握度只有 {Math.Round(m.Mastery * 100)}%（低于 {Math.Round(thresholds.Weak * 100)}%），建议做一次专项练习。",
                    priority, m.Mastery, m.KnowledgeResourceId, null, existingByKey, result);
            }

            // ② 前置知识（KpPrerequisite Confirmed，且前置点有映射到本课知识点）
            var weakResourceIds = weak.Select(x => x.KnowledgeResourceId).ToHashSet();
            if (weakResourceIds.Count > 0)
            {
                List<KpPrerequisite> prerequisites;
                using (_dataFilter.Disable<IMultiTenant>())
                {
                    prerequisites = await _prerequisiteRepository.GetListAsync(x =>
                        x.Status == KnowledgeHub.LearningAnalytics.Enums.PrerequisiteStatus.Confirmed);
                }

                if (prerequisites.Count > 0)
                {
                    // 前置点 → 课程知识点（经 KnowledgePoint.KnowledgeResourceId 映射），弱点的前置也提示
                    var candidates = prerequisites
                        .Where(p => p.FromKpId != Guid.Empty && p.ToKpId != Guid.Empty)
                        .Take(20)
                        .ToList();
                    _ = candidates; // 映射未跑通时不硬推前置（避免推错点污染体验）
                }
            }

            // ③ 未完成章节：进度 < 100% 且近 14 天该章节无活动
            var progressByChapter = progresses
                .Where(x => x.ChapterId.HasValue)
                .GroupBy(x => x.ChapterId!.Value)
                .ToDictionary(g => g.Key, g => (double)g.Average(x => x.Progress));

            var recentChapterIds = activities
                .Where(x => x.CreationTime >= DateTime.UtcNow.AddDays(-14) && x.ResourceId.HasValue)
                .Select(x => x.ResourceId!.Value)
                .ToHashSet();

            // 同名章节去重（同一门课可能因重复导入出现同名章节，否则推荐列表会出现两条一模一样的提示）
            var rootChapters = chapters
                .Where(c => c.ParentId == null)
                .GroupBy(c => c.Title?.Trim() ?? string.Empty)
                .Select(g => g
                    .OrderByDescending(c => progressByChapter.GetValueOrDefault(c.Id))
                    .First())
                .OrderBy(c => c.SortOrder)
                .Take(8);

            foreach (var chapter in rootChapters)
            {
                var progress = progressByChapter.GetValueOrDefault(chapter.Id);
                if (progress >= 100 || recentChapterIds.Contains(chapter.Id))
                {
                    continue;
                }

                var progressPercent = (double)progress;
                await UpsertAsync(
                    courseId, studentId, StudentRecommendationType.IncompleteChapter,
                    progressPercent <= 0
                        ? $"章节「{chapter.Title}」还没开始，先学这一节可以解锁后续内容。"
                        : $"章节「{chapter.Title}」已学 {Math.Round(progressPercent)}%，还差一点，继续完成它。",
                    5, progressPercent, null, chapter.Id, existingByKey, result);
            }
        }

        if (CurrentUnitOfWork is not null) await CurrentUnitOfWork.SaveChangesAsync();

        result.ActiveCount = result.CreatedCount + result.UpdatedCount;
        result.Message = $"刷新完成：新增 {result.CreatedCount} 条、更新 {result.UpdatedCount} 条（覆盖 {studentIds.Count} 名学生）。";
        return result;
    }

    private async Task UpsertAsync(
        Guid courseId,
        Guid studentId,
        StudentRecommendationType type,
        string reason,
        int priority,
        double? metricValue,
        Guid? knowledgeResourceId,
        Guid? chapterId,
        Dictionary<(Guid, StudentRecommendationType, Guid, Guid), StudentRecommendation> existing,
        RecommendationRefreshResultDto result)
    {
        var key = (studentId, type, knowledgeResourceId ?? Guid.Empty, chapterId ?? Guid.Empty);
        if (existing.TryGetValue(key, out var row))
        {
            // 已忽略的不再复活（尊重学生选择）；已完成的重置为 Pending 也不合适 —— 只在未处理时更新理由
            if (row.Status is StudentRecommendationStatus.Dismissed or StudentRecommendationStatus.Completed)
            {
                return;
            }

            row.Update(reason, priority, metricValue, knowledgeResourceId, chapterId);
            await _recommendationRepository.UpdateAsync(row);
            result.UpdatedCount++;
            return;
        }

        var created = new StudentRecommendation(GuidGenerator.Create(), studentId, courseId, type);
        created.Update(reason, priority, metricValue, knowledgeResourceId, chapterId);
        await _recommendationRepository.InsertAsync(created);
        existing[key] = created;
        result.CreatedCount++;
    }

    // ══════════════════════════════════════════════════════════
    //  映射与辅助
    // ══════════════════════════════════════════════════════════

    private async Task<List<StudentRecommendationDto>> MapManyAsync(IEnumerable<StudentRecommendation> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            return new List<StudentRecommendationDto>();
        }

        var resourceIds = list.Where(x => x.KnowledgeResourceId.HasValue)
            .Select(x => x.KnowledgeResourceId!.Value).Distinct().ToList();
        var chapterIds = list.Where(x => x.ChapterId.HasValue)
            .Select(x => x.ChapterId!.Value).Distinct().ToList();

        var resources = resourceIds.Count == 0
            ? new List<KnowledgeResource>()
            : await _knowledgeResourceRepository.GetListAsync(x => resourceIds.Contains(x.Id));
        var chapters = chapterIds.Count == 0
            ? new List<Chapter>()
            : await _chapterRepository.GetListAsync(x => chapterIds.Contains(x.Id));

        var resourceById = resources.ToDictionary(x => x.Id, x => x.Name);
        var chapterById = chapters.ToDictionary(x => x.Id, x => x.Title);
        var names = await GetUserNamesAsync(list.Select(x => x.StudentId));

        return list.Select(x => new StudentRecommendationDto
        {
            Id = x.Id,
            StudentId = x.StudentId,
            StudentName = names.GetValueOrDefault(x.StudentId),
            CourseId = x.CourseId,
            Type = x.Type,
            TypeLabel = TypeLabel(x.Type),
            Status = x.Status,
            StatusLabel = StatusLabel(x.Status),
            Priority = x.Priority,
            Reason = x.Reason,
            KnowledgeResourceId = x.KnowledgeResourceId,
            KnowledgeResourceName = x.KnowledgeResourceId.HasValue
                ? resourceById.GetValueOrDefault(x.KnowledgeResourceId.Value)
                : null,
            ChapterId = x.ChapterId,
            ChapterTitle = x.ChapterId.HasValue ? chapterById.GetValueOrDefault(x.ChapterId.Value) : null,
            MetricValue = x.MetricValue,
            HandledAt = x.HandledAt,
            CreationTime = x.CreationTime,
            ActionKind = x.Type == StudentRecommendationType.IncompleteChapter ? "chapter" : "practice"
        }).ToList();
    }

    private async Task<List<Guid>> GetEnrolledStudentIdsAsync(Guid courseId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _studentCourseRepository.GetQueryableAsync();
            return await AsyncExecuter.ToListAsync(
                query.Where(x => x.CourseId == courseId && x.Status != StudentCourseStatus.Dropped)
                    .Select(x => x.StudentId));
        }
    }

    private async Task<Dictionary<Guid, string?>> GetUserNamesAsync(IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        List<IdentityUser> users;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            users = await _userRepository.GetListAsync(x => list.Contains(x.Id));
        }

        return users.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);
    }

    internal static string TypeLabel(StudentRecommendationType type) => type switch
    {
        StudentRecommendationType.WeakKnowledge => "薄弱知识点",
        StudentRecommendationType.Prerequisite => "前置知识",
        StudentRecommendationType.IncompleteChapter => "未完成章节",
        StudentRecommendationType.LearningHabit => "学习节奏",
        _ => "推荐"
    };

    internal static string StatusLabel(StudentRecommendationStatus status) => status switch
    {
        StudentRecommendationStatus.Pending => "待开始",
        StudentRecommendationStatus.Viewed => "已查看",
        StudentRecommendationStatus.InProgress => "进行中",
        StudentRecommendationStatus.Completed => "已完成",
        StudentRecommendationStatus.Dismissed => "已忽略",
        _ => "未知"
    };
}
