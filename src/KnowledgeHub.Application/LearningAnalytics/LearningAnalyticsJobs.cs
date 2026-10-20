using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning;
using KnowledgeHub.LearningAnalytics.Enums;
using KnowledgeHub.LearningAnalytics.Ml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>掌握度模型重训（可入队，供教师端按钮与夜间任务调用）。</summary>
public class MasteryModelTrainJob : ITransientDependency
{
    private readonly MasteryModelTrainer _trainer;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public MasteryModelTrainJob(
        MasteryModelTrainer trainer,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _trainer = trainer;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task ExecuteAsync(Guid? tenantId)
    {
        using (_currentTenant.Change(tenantId))
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            await _trainer.TrainAsync();
            await uow.CompleteAsync();
        }
    }
}

/// <summary>先修关系挖掘（可入队）。</summary>
public class PrerequisiteMiningJob : ITransientDependency
{
    private readonly PrerequisiteMiner _miner;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public PrerequisiteMiningJob(
        PrerequisiteMiner miner,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _miner = miner;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task ExecuteAsync(Guid? courseId, Guid? tenantId)
    {
        using (_currentTenant.Change(tenantId))
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            await _miner.MineAsync(courseId);
            await uow.CompleteAsync();
        }
    }
}

/// <summary>
/// 智学分析夜间任务（Hangfire RecurringJob，每天 03:00）：
/// 1) 逐租户重训掌握度矩阵分解模型；
/// 2) 挖掘先修知识点候选（Proposed，需老师确认）；
/// 3) 开课前衔接复习包推送（默认关闭：LearningAnalytics:PreCourseReviewPushEnabled）。
/// </summary>
public class LearningAnalyticsNightlyJob : ITransientDependency
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly MasteryModelTrainJob _trainJob;
    private readonly PrerequisiteMiningJob _miningJob;
    private readonly PreCourseReviewPushService _preCoursePush;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LearningAnalyticsNightlyJob> _logger;

    public LearningAnalyticsNightlyJob(
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        MasteryModelTrainJob trainJob,
        PrerequisiteMiningJob miningJob,
        PreCourseReviewPushService preCoursePush,
        IConfiguration configuration,
        ILogger<LearningAnalyticsNightlyJob> logger)
    {
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _trainJob = trainJob;
        _miningJob = miningJob;
        _preCoursePush = preCoursePush;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var trainEnabled = _configuration.GetValue("LearningAnalytics:NightlyTrainEnabled", true);
        var miningEnabled = _configuration.GetValue("LearningAnalytics:PrerequisiteMiningEnabled", true);
        var pushEnabled = _configuration.GetValue("LearningAnalytics:PreCourseReviewPushEnabled", false);

        // 宿主（host）上下文
        await RunForTenantAsync(null, trainEnabled, miningEnabled, pushEnabled);

        // 各租户
        var tenants = await _tenantRepository.GetListAsync();
        foreach (var tenant in tenants)
        {
            await RunForTenantAsync(tenant.Id, trainEnabled, miningEnabled, pushEnabled);
        }

        _logger.LogInformation("智学分析夜间任务完成：租户 {TenantCount} 个", tenants.Count);
    }

    private async Task RunForTenantAsync(Guid? tenantId, bool train, bool mining, bool push)
    {
        try
        {
            if (train)
            {
                await _trainJob.ExecuteAsync(tenantId);
            }

            if (mining)
            {
                await _miningJob.ExecuteAsync(null, tenantId);
            }

            if (push)
            {
                await _preCoursePush.PushAsync(tenantId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "智学分析夜间任务失败：tenant {TenantId}", tenantId);
        }
    }
}

/// <summary>
/// 衔接复习包推送：为「已配置前置课程关联 + 后续课程已选课」的学生生成/刷新复习包并推送一次消息
/// （同一学生 + 课程只推一次）。由夜间任务与教师端「立即推送」共用。
/// 注意：课程关联只表达前后置关系，不含排课时间，因此不做时间窗口判断。
/// </summary>
public class PreCourseReviewPushService : ITransientDependency
{
    private readonly IRepository<CourseLink, Guid> _courseLinkRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<StudentNotification, Guid> _notificationRepository;
    private readonly RecommendationEngine _recommendation;
    private readonly IConfiguration _configuration;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<PreCourseReviewPushService> _logger;

    public PreCourseReviewPushService(
        IRepository<CourseLink, Guid> courseLinkRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<StudentNotification, Guid> notificationRepository,
        RecommendationEngine recommendation,
        IConfiguration configuration,
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        ILogger<PreCourseReviewPushService> logger)
    {
        _courseLinkRepository = courseLinkRepository;
        _studentCourseRepository = studentCourseRepository;
        _notificationRepository = notificationRepository;
        _recommendation = recommendation;
        _configuration = configuration;
        _currentTenant = currentTenant;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public async Task<int> PushAsync(Guid? tenantId)
    {
        using (_currentTenant.Change(tenantId))
        {
            var links = await _courseLinkRepository.GetListAsync(l => l.Status == PrerequisiteStatus.Confirmed);
            if (!links.Any())
            {
                return 0;
            }

            var maxPerRun = _configuration.GetValue("LearningAnalytics:PreCourseReviewMaxPerRun", 200);
            var pushed = 0;

            foreach (var link in links)
            {
                // 只要配置了「前置 → 后续」关联，就为后续课程已选课的学生生成衔接复习包；
                // 同一学生 + 课程只推一次（见下方通知去重）。
                var enrollments = await _studentCourseRepository.GetListAsync(e => e.CourseId == link.SuccessorCourseId);
                foreach (var studentId in enrollments.Select(e => e.StudentId).Distinct())
                {
                    if (pushed >= maxPerRun)
                    {
                        _logger.LogInformation("开课前衔接推送达到单次上限 {Max}，本轮提前结束", maxPerRun);
                        return pushed;
                    }

                    var existing = await _notificationRepository.GetListAsync(n =>
                        n.StudentId == studentId &&
                        n.Type == AnalysisNotificationType.PreCourseReview &&
                        n.CourseId == link.SuccessorCourseId);
                    if (existing.Any())
                    {
                        continue;
                    }

                    var progress = await _recommendation.BuildReviewPackageAsync(
                        studentId, link.SuccessorCourseId, PathStrategy.Mixed, persist: true);
                    if (progress == null || progress.Total == 0)
                    {
                        continue;
                    }

                    var names = string.Join("、", progress.Items.Take(3).Select(i => i.Name));
                    var notification = new StudentNotification(
                        _guidGenerator.Create(),
                        studentId,
                        $"【课前提醒】{progress.UpcomingCourseTitle} 即将开课",
                        $"检测到你在前置课程中「{names}」等 {progress.Total} 个知识点仍需巩固，" +
                        "已为你准备衔接复习包，可在「智学分析 → 学习路径」中查看并完成。")
                    {
                        Type = AnalysisNotificationType.PreCourseReview,
                        Source = "系统",
                        CourseId = link.SuccessorCourseId,
                        RelatedId = progress.ReviewPackageId,
                    };
                    await _notificationRepository.InsertAsync(notification, autoSave: true);
                    pushed++;
                }
            }

            if (pushed > 0)
            {
                _logger.LogInformation("开课前衔接复习包已推送 {Count} 条", pushed);
            }

            return pushed;
        }
    }
}
