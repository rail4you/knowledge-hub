using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.Exams.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.Application.AI.Tasks;

/// <summary>
/// Hangfire 执行的 AI 生成任务：按任务类型分发到对应生成逻辑，
/// 边执行边刷新 AiGenerationTask 进度/状态，最终写入结果 JSON。
/// </summary>
public class AiGenerationJob : ITransientDependency
{
    private readonly IRepository<AiGenerationTask, Guid> _taskRepository;
    private readonly IRepository<CourseLessonPlan, Guid> _courseLessonPlanRepository;
    private readonly IRepository<CourseCase, Guid> _courseCaseRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly LessonPlanAppService _lessonPlanService;
    private readonly CaseAnalysisAppService _caseAnalysisService;
    private readonly CareerGuidanceAppService _careerGuidanceService;
    private readonly ExerciseAiGenerator _exerciseAiGenerator;
    private readonly IAiUsageTracker _usageTracker;
    private readonly IAiMediaGenerator _mediaGenerator;
    private readonly IMediaStorageService _mediaStorage;
    private readonly AiMediaResourceRegistrar _mediaResourceRegistrar;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private readonly ILogger<AiGenerationJob> _logger;

    public AiGenerationJob(
        IRepository<AiGenerationTask, Guid> taskRepository,
        IRepository<CourseLessonPlan, Guid> courseLessonPlanRepository,
        IRepository<CourseCase, Guid> courseCaseRepository,
        IGuidGenerator guidGenerator,
        IUnitOfWorkManager unitOfWorkManager,
        ICurrentTenant currentTenant,
        LessonPlanAppService lessonPlanService,
        CaseAnalysisAppService caseAnalysisService,
        CareerGuidanceAppService careerGuidanceService,
        ExerciseAiGenerator exerciseAiGenerator,
        IAiUsageTracker usageTracker,
        IAiMediaGenerator mediaGenerator,
        IMediaStorageService mediaStorage,
        AiMediaResourceRegistrar mediaResourceRegistrar,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILogger<AiGenerationJob> logger)
    {
        _taskRepository = taskRepository;
        _courseLessonPlanRepository = courseLessonPlanRepository;
        _courseCaseRepository = courseCaseRepository;
        _guidGenerator = guidGenerator;
        _unitOfWorkManager = unitOfWorkManager;
        _currentTenant = currentTenant;
        _lessonPlanService = lessonPlanService;
        _caseAnalysisService = caseAnalysisService;
        _careerGuidanceService = careerGuidanceService;
        _exerciseAiGenerator = exerciseAiGenerator;
        _usageTracker = usageTracker;
        _mediaGenerator = mediaGenerator;
        _mediaStorage = mediaStorage;
        _mediaResourceRegistrar = mediaResourceRegistrar;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid taskId, Guid? tenantId)
    {
        using (_currentTenant.Change(tenantId))
        {
            var task = await GetTaskAsync(taskId);
            if (task == null)
            {
                _logger.LogWarning("AiGenerationJob: task {TaskId} not found", taskId);
                return;
            }

            if (task.Status is AiTaskStatus.Cancelled or AiTaskStatus.Completed)
            {
                _logger.LogInformation("AiGenerationJob: task {TaskId} already {Status}, skip", taskId, task.Status);
                return;
            }

            await UpdateTaskAsync(taskId, t =>
            {
                t.Status = AiTaskStatus.Running;
                t.StartedAt ??= DateTime.UtcNow;
                t.Progress = 1;
                t.ProgressMessage = "开始生成…";
                t.ErrorMessage = null;
            });

            using var cts = new CancellationTokenSource();
            var monitor = MonitorCancellationAsync(taskId, cts);

            try
            {
                var resultJson = await GenerateAsync(task, cts.Token);

                // 生成结束（可能被取消），停掉监控
                cts.Cancel();
                await SafeAwaitAsync(monitor);

                if (await IsCancelledAsync(taskId))
                {
                    await UpdateTaskAsync(taskId, t =>
                    {
                        t.Status = AiTaskStatus.Cancelled;
                        t.ProgressMessage = "已取消";
                        t.CompletedAt = DateTime.UtcNow;
                    });
                    return;
                }

                await UpdateTaskAsync(taskId, t =>
                {
                    t.Status = AiTaskStatus.Completed;
                    t.ResultJson = resultJson;
                    t.Progress = 100;
                    t.ProgressMessage = "已完成";
                    t.CompletedAt = DateTime.UtcNow;
                    t.IsRead = false;
                    t.ErrorMessage = null;
                });
            }
            catch (Exception ex)
            {
                cts.Cancel();
                await SafeAwaitAsync(monitor);

                if (await IsCancelledAsync(taskId))
                {
                    await UpdateTaskAsync(taskId, t =>
                    {
                        t.Status = AiTaskStatus.Cancelled;
                        t.ProgressMessage = "已取消";
                        t.CompletedAt = DateTime.UtcNow;
                    });
                    return;
                }

                _logger.LogError(ex, "AiGenerationJob: task {TaskId} failed", taskId);
                await UpdateTaskAsync(taskId, t =>
                {
                    t.Status = AiTaskStatus.Failed;
                    t.ErrorMessage = Truncate(ex.Message, 2000);
                    t.ProgressMessage = "生成失败";
                    t.CompletedAt = DateTime.UtcNow;
                    t.IsRead = false;
                });
            }
        }
    }

    private async Task<string> GenerateAsync(AiGenerationTask task, CancellationToken cancellationToken)
    {
        // Hangfire 不经过 ABP 的 HTTP/UoW 管线，显式开启一个 UoW，
        // 保证生成服务内部的仓储可用；进度更新使用 requiresNew 独立提交。
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        // 用量记录：inputJson 既是 token 估算来源（多章节等多轮调用按总量估，IsEstimated 标记）
        var group = ToFeatureGroup(task.TaskType);
        var (model, fixedCost) = ResolveUsage(task.TaskType);
        var usageId = await _usageTracker.StartAsync(
            group, "BackgroundTask", model, task.InputJson,
            userId: task.CreatorUserId, tenantId: task.TenantId);
        try
        {
            var result = await GenerateCoreAsync(task, cancellationToken);
            await _usageTracker.CompleteAsync(usageId, result, true, fixedCost: fixedCost);
            await uow.CompleteAsync();
            return result;
        }
        catch (Exception ex)
        {
            await _usageTracker.CompleteAsync(usageId, null, false, ex.Message, fixedCost: fixedCost);
            throw;
        }
    }

    /// <summary>媒体生成按张/按次计费，使用万相模型与固定费用；文本任务按 token 估算。</summary>
    private (string Model, decimal? FixedCost) ResolveUsage(AiTaskType taskType) => taskType switch
    {
        AiTaskType.ImageGeneration => (_mediaGenerator.ImageModel, AiMediaPricing.ImageCost(_mediaGenerator.ImageModel)),
        AiTaskType.VideoGeneration => (_mediaGenerator.VideoModel, AiMediaPricing.VideoCost(_mediaGenerator.VideoModel, 5)),
        _ => (_configuration["Qwen:Model"] ?? "qwen-flash", (decimal?)null),
    };

    private static string ToFeatureGroup(AiTaskType taskType) => taskType switch
    {
        AiTaskType.LessonPlanSingle => AiFeatureGroups.LessonPlan,
        AiTaskType.LessonPlanMulti => AiFeatureGroups.LessonPlan,
        AiTaskType.LessonPlanCourseMulti => AiFeatureGroups.LessonPlan,
        AiTaskType.CaseAnalysis => AiFeatureGroups.CaseAnalysis,
        AiTaskType.CareerGuidance => AiFeatureGroups.CareerGuidance,
        AiTaskType.ExerciseGenerate => AiFeatureGroups.ExerciseGenerate,
        AiTaskType.ImageGeneration => AiFeatureGroups.ImageGeneration,
        AiTaskType.VideoGeneration => AiFeatureGroups.VideoGeneration,
        _ => AiFeatureGroups.Chat,
    };

    private async Task<string> GenerateCoreAsync(AiGenerationTask task, CancellationToken cancellationToken)
    {
        switch (task.TaskType)
        {
            case AiTaskType.LessonPlanSingle:
                return await GenerateLessonPlanSingleAsync(task, cancellationToken);
            case AiTaskType.LessonPlanMulti:
                return await GenerateLessonPlanMultiAsync(task, cancellationToken);
            case AiTaskType.LessonPlanCourseMulti:
                return await GenerateCourseLessonPlanMultiAsync(task, cancellationToken);
            case AiTaskType.CaseAnalysis:
                return await GenerateCaseAnalysisAsync(task, cancellationToken);
            case AiTaskType.CareerGuidance:
                return await GenerateCareerGuidanceAsync(task, cancellationToken);
            case AiTaskType.ExerciseGenerate:
                return await GenerateExerciseAsync(task, cancellationToken);
            case AiTaskType.ImageGeneration:
                return await GenerateImageAsync(task, cancellationToken);
            case AiTaskType.VideoGeneration:
                return await GenerateVideoAsync(task, cancellationToken);
            default:
                throw new UserFriendlyException($"不支持的任务类型：{task.TaskType}");
        }
    }

    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private async Task<string> GenerateImageAsync(AiGenerationTask task, CancellationToken cancellationToken)
    {
        var input = Deserialize<ImageGenerationInputDto>(task.InputJson);
        await UpdateProgressAsync(task.Id, 10, "正在生成图片…");

        var submitted = await _mediaGenerator.SubmitImageAsync(input.Prompt, input.Size, input.NegativePrompt);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var done = await _mediaGenerator.WaitForCompletionAsync(
            submitted.TaskId, TimeSpan.FromMinutes(5), cancellationToken,
            (_, _) => ReportMediaProgressAsync(task.Id, sw, expectedSeconds: 45, message: "正在生成图片…"));

        if (!string.Equals(done.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException(done.Error ?? "图片生成失败");
        }
        if (string.IsNullOrWhiteSpace(done.ImageUrl))
        {
            throw new UserFriendlyException("图片生成结果为空");
        }

        await UpdateProgressAsync(task.Id, 90, "正在保存图片…");
        var imageUrl = await _mediaStorage.PersistAsync(done.ImageUrl!, ".png", "images");

        // 课程上下文：登记为课程资源（待审核），审核通过后学生端才可见
        await UpdateProgressAsync(task.Id, 95, "正在登记到课程资源…");
        var resourceId = await _mediaResourceRegistrar.RegisterAsync(
            input.CourseId,
            KnowledgeHub.Resources.Enums.ResourceType.Image,
            imageUrl,
            "png",
            input.ResourceName,
            input.Prompt,
            task.Id.ToString());

        // 同时保留通义万相原始临时 URL：本地持久化地址对 DashScope 不可达（如 localhost），
        // 图生视频必须使用原始 URL 作为首帧。该 URL 约 24 小时内有效，图→视频在同一次会话完成。
        return JsonSerializer.Serialize(new { imageUrl, dashScopeUrl = done.ImageUrl, resourceId }, CamelCaseJson);
    }

    private async Task<string> GenerateVideoAsync(AiGenerationTask task, CancellationToken cancellationToken)
    {
        var input = Deserialize<VideoGenerationInputDto>(task.InputJson);
        await UpdateProgressAsync(task.Id, 10, "正在检测首帧图片内容…");

        // 内容预检：图片 / 提示词疑似违规时在提交前拦截并给出中文提示，
        // 避免提交后因平台内容安全拦截（Input data may contain inappropriate content）白等一轮。
        await _mediaGenerator.CheckVideoInputAsync(input.ImageUrl, input.Prompt);

        await UpdateProgressAsync(task.Id, 10, "正在生成视频…");

        // 本地持久化 / 上传的首帧图片转 base64 data URL，公网 URL 原样提交
        var resolvedImage = await _mediaGenerator.ResolveImageUrlForI2vAsync(input.ImageUrl);
        var submitted = await _mediaGenerator.SubmitVideoAsync(resolvedImage, input.Prompt, input.Duration);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var done = await _mediaGenerator.WaitForCompletionAsync(
            submitted.TaskId, TimeSpan.FromMinutes(10), cancellationToken,
            (_, _) => ReportMediaProgressAsync(task.Id, sw, expectedSeconds: 150, message: "正在生成视频…"));

        if (!string.Equals(done.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException(done.Error ?? "视频生成失败");
        }
        if (string.IsNullOrWhiteSpace(done.VideoUrl))
        {
            throw new UserFriendlyException("视频生成结果为空");
        }

        await UpdateProgressAsync(task.Id, 90, "正在保存视频…");
        var videoUrl = await _mediaStorage.PersistAsync(done.VideoUrl!, ".mp4", "videos");

        // 课程上下文：登记为课程资源（待审核），审核通过后学生端才可见
        await UpdateProgressAsync(task.Id, 95, "正在登记到课程资源…");
        var resourceId = await _mediaResourceRegistrar.RegisterAsync(
            input.CourseId,
            KnowledgeHub.Resources.Enums.ResourceType.Video,
            videoUrl,
            "mp4",
            input.ResourceName,
            input.Prompt,
            task.Id.ToString());

        return JsonSerializer.Serialize(new { videoUrl, imageUrl = input.ImageUrl, resourceId }, CamelCaseJson);
    }

    /// <summary>
    /// 等待期间的平滑进度：按已耗时至预期窗口映射到 10..84%，避免进度条长时间静止。
    /// </summary>
    private Task ReportMediaProgressAsync(Guid taskId, System.Diagnostics.Stopwatch sw, double expectedSeconds, string message)
    {
        var pct = 10 + (int)(Math.Min(sw.Elapsed.TotalSeconds, expectedSeconds) / expectedSeconds * 75);
        return UpdateProgressAsync(taskId, Math.Clamp(pct, 10, 84), message);
    }

    private async Task<string> GenerateLessonPlanSingleAsync(AiGenerationTask task, CancellationToken cancellationToken)
    {
        var input = Deserialize<LessonPlanGenerationInputDto>(task.InputJson);
        var sb = new StringBuilder();
        var lastUpdate = DateTime.UtcNow;

        await _lessonPlanService.GenerateStreamingAsync(input, async chunk =>
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                sb.Append(chunk.Content);
                if ((DateTime.UtcNow - lastUpdate).TotalSeconds >= 3)
                {
                    lastUpdate = DateTime.UtcNow;
                    await UpdateProgressAsync(task.Id, null, "正在生成教案…");
                }
            }
        });

        var text = sb.ToString();
        ThrowIfErrorPayload(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new UserFriendlyException("AI 未返回任何内容，请重试");
        }
        // 严格对应输入参数：学科 / 授课对象 / 课时 以用户输入为准（AI 常锚定 schema 示例的 45 分钟），
        // 并把教学环节时长缩放到与课时一致。
        var result = _lessonPlanService.NormalizeSingleLessonPlan(text, input.Subject, input.Grade, input.Duration);
        await TryPersistCourseLessonPlanAsync(task, input, result);
        return result;
    }

    /// <summary>
    /// 课程教案生成完成后自动落库到「教案管理」，并打上 AI 生成来源标记。
    /// 仅当输入带 CourseId 时执行；同一任务重复消费时按 AiTaskId 去重。
    /// </summary>
    private async Task TryPersistCourseLessonPlanAsync(
        AiGenerationTask task,
        LessonPlanGenerationInputDto input,
        string resultJson)
    {
        if (!input.CourseId.HasValue || string.IsNullOrWhiteSpace(resultJson))
        {
            return;
        }

        var existing = await _courseLessonPlanRepository.FindAsync(x => x.AiTaskId == task.Id);
        if (existing != null)
        {
            return;
        }

        var plan = Deserialize<LessonPlanDto>(resultJson);
        var title = !string.IsNullOrWhiteSpace(plan?.Title) ? plan!.Title : task.Title;
        if (title.Length > 256)
        {
            title = title[..256];
        }

        var entity = new CourseLessonPlan(_guidGenerator.Create(), input.CourseId.Value, title)
        {
            TenantId = task.TenantId,
            ChapterId = input.ChapterId,
            ChapterTitle = string.IsNullOrWhiteSpace(input.ChapterTitle) ? input.Topic : input.ChapterTitle,
            Description = "AI 基于课程章节生成",
            Source = CourseLessonPlanSource.Ai,
            ContentJson = resultJson,
            AiTaskId = task.Id
        };

        await _courseLessonPlanRepository.InsertAsync(entity, autoSave: true);
    }

    private async Task<string> GenerateLessonPlanMultiAsync(AiGenerationTask task, CancellationToken cancellationToken)
    {
        var input = Deserialize<MultiChapterLessonPlanGenerationInputDto>(task.InputJson);
        string? resultJson = null;
        string? errorMessage = null;

        await _lessonPlanService.GenerateMultiChapterStreamingAsync(input, async evt =>
        {
            if (evt.IsError && !string.IsNullOrWhiteSpace(evt.Message))
            {
                errorMessage = evt.Message;
            }
            if (evt.Progress > 0 || !string.IsNullOrWhiteSpace(evt.Message))
            {
                await UpdateProgressAsync(task.Id, evt.Progress > 0 ? evt.Progress : (int?)null, evt.Message);
            }
            if (!string.IsNullOrWhiteSpace(evt.ResultJson))
            {
                resultJson = evt.ResultJson;
            }
        }, cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException();
        }
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new UserFriendlyException(errorMessage);
        }
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            throw new UserFriendlyException("生成结果为空，请重试");
        }
        return resultJson!;
    }

    /// <summary>
    /// 课程多章节教案：按教师勾选并确认的章节逐章生成，每章生成完立即落库到「教案管理」
    /// （带章节归属 + AI 生成来源标记）。
    /// 进度文案形如「正在生成第 2/5 章：xxx」，前端据此展示逐章进度（与多章节整体教案一致）。
    /// </summary>
    private async Task<string> GenerateCourseLessonPlanMultiAsync(AiGenerationTask task, CancellationToken cancellationToken)
    {
        var input = Deserialize<CourseMultiChapterLessonPlanInputDto>(task.InputJson);

        var chapters = (input.Chapters ?? new List<CourseChapterLessonPlanItemDto>())
            .Where(c => !string.IsNullOrWhiteSpace(c.Title))
            .OrderBy(c => c.Order)
            .ToList();

        if (chapters.Count == 0)
        {
            throw new UserFriendlyException("请至少选择一个章节后再生成教案");
        }

        for (var i = 0; i < chapters.Count; i++)
        {
            chapters[i].Order = i + 1;
        }

        var total = chapters.Count;
        var results = new List<object>();

        for (var i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chapter = chapters[i];
            var index = i + 1;
            var progress = (int)(i / (double)total * 95);

            await UpdateProgressAsync(task.Id, progress, $"正在生成第 {index}/{total} 章：{chapter.Title}");

            var planJson = await _lessonPlanService.GenerateCourseChapterPlanAsync(input, chapter, cancellationToken);

            await TryPersistCourseChapterPlanAsync(task, input, chapter, planJson);

            results.Add(new
            {
                order = chapter.Order,
                chapterId = chapter.ChapterId,
                chapterTitle = chapter.Title,
                contentJson = planJson
            });

            await UpdateProgressAsync(task.Id, (int)((i + 1) / (double)total * 95), $"第 {index}/{total} 章已完成：{chapter.Title}");
        }

        return JsonSerializer.Serialize(new
        {
            courseId = input.CourseId,
            courseTitle = input.CourseTitle,
            chapterCount = total,
            chapters = results
        }, CamelCaseJson);
    }

    /// <summary>
    /// 课程教案（多章节）：逐章落库到「教案管理」，每章一条记录并带上章节归属。
    /// 同一任务 + 同一章节重复消费时按 AiTaskId + ChapterId 去重。
    /// </summary>
    private async Task TryPersistCourseChapterPlanAsync(
        AiGenerationTask task,
        CourseMultiChapterLessonPlanInputDto input,
        CourseChapterLessonPlanItemDto chapter,
        string resultJson)
    {
        if (input.CourseId == Guid.Empty || string.IsNullOrWhiteSpace(resultJson))
        {
            return;
        }

        var existing = await _courseLessonPlanRepository.FindAsync(x =>
            x.AiTaskId == task.Id && x.ChapterId == chapter.ChapterId);
        if (existing != null)
        {
            return;
        }

        var plan = Deserialize<LessonPlanDto>(resultJson);
        var title = !string.IsNullOrWhiteSpace(plan?.Title)
            ? plan!.Title
            : $"{input.CourseTitle} - {chapter.Title}";
        if (title.Length > 256)
        {
            title = title[..256];
        }

        var entity = new CourseLessonPlan(_guidGenerator.Create(), input.CourseId, title)
        {
            TenantId = task.TenantId,
            ChapterId = chapter.ChapterId,
            ChapterTitle = chapter.Title,
            Description = "AI 基于课程章节生成",
            Source = CourseLessonPlanSource.Ai,
            ContentJson = resultJson,
            AiTaskId = task.Id
        };

        await _courseLessonPlanRepository.InsertAsync(entity, autoSave: true);
    }

    private async Task<string> GenerateCaseAnalysisAsync(AiGenerationTask task, CancellationToken cancellationToken)
    {
        var input = Deserialize<CaseAnalysisGenerationInputDto>(task.InputJson);
        var sb = new StringBuilder();
        var lastUpdate = DateTime.UtcNow;

        await _caseAnalysisService.GenerateStreamingAsync(input, async chunk =>
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                sb.Append(chunk.Content);
                if ((DateTime.UtcNow - lastUpdate).TotalSeconds >= 3)
                {
                    lastUpdate = DateTime.UtcNow;
                    await UpdateProgressAsync(task.Id, null, "正在生成案例分析…");
                }
            }
        });

        var text = sb.ToString();
        ThrowIfErrorPayload(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new UserFriendlyException("AI 未返回任何内容，请重试");
        }

        await TryPersistCourseCaseAsync(task, input, text);
        return text;
    }

    /// <summary>
    /// 课程案例库生成完成后自动落库到「案例库」，并打上 AI 生成来源标记。
    /// 仅当输入带 CourseId 时执行；同一任务重复消费时按 AiTaskId 去重。
    /// </summary>
    private async Task TryPersistCourseCaseAsync(
        AiGenerationTask task,
        CaseAnalysisGenerationInputDto input,
        string resultJson)
    {
        if (!input.CourseId.HasValue || input.CourseId.Value == Guid.Empty || string.IsNullOrWhiteSpace(resultJson))
        {
            return;
        }

        var existing = await _courseCaseRepository.FindAsync(x => x.AiTaskId == task.Id);
        if (existing != null)
        {
            return;
        }

        var content = CourseCaseAppService.ParseContent(resultJson, task.Title, null);
        var title = !string.IsNullOrWhiteSpace(content.Title) ? content.Title : (task.Title ?? "AI 生成案例");
        if (title.Length > 256)
        {
            title = title[..256];
        }

        var entity = new CourseCase(_guidGenerator.Create(), input.CourseId.Value, title)
        {
            TenantId = task.TenantId,
            ChapterId = input.ChapterId,
            ChapterTitle = string.IsNullOrWhiteSpace(input.ChapterTitle) ? null : input.ChapterTitle,
            Summary = content.Summary,
            Industry = Truncate(content.Background?.Industry, 128),
            Timeframe = Truncate(content.Background?.Timeframe, 64),
            ContentJson = resultJson,
            Source = CourseCaseSource.Ai,
            AiTaskId = task.Id,
            CreatorName = "AI 生成"
        };

        await _courseCaseRepository.InsertAsync(entity, autoSave: true);
    }

    private async Task<string> GenerateCareerGuidanceAsync(AiGenerationTask task, CancellationToken cancellationToken)
    {
        var input = Deserialize<CareerGuidanceGenerationInputDto>(task.InputJson);
        var sb = new StringBuilder();
        var lastUpdate = DateTime.UtcNow;

        await _careerGuidanceService.GenerateStreamingAsync(input, async chunk =>
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                sb.Append(chunk.Content);
                if ((DateTime.UtcNow - lastUpdate).TotalSeconds >= 3)
                {
                    lastUpdate = DateTime.UtcNow;
                    await UpdateProgressAsync(task.Id, null, "正在生成职业规划…");
                }
            }
        });

        var text = sb.ToString();
        ThrowIfErrorPayload(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new UserFriendlyException("AI 未返回任何内容，请重试");
        }
        return text;
    }

    private async Task<string> GenerateExerciseAsync(AiGenerationTask task, CancellationToken cancellationToken)
    {
        await UpdateProgressAsync(task.Id, 20, "正在生成习题…");
        var input = Deserialize<GenerateExerciseInput>(task.InputJson);
        var result = await _exerciseAiGenerator.GenerateAsync(input);
        await UpdateProgressAsync(task.Id, 90, "正在保存习题…");

        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    // ==================== helpers ====================

    private async Task<AiGenerationTask?> GetTaskAsync(Guid taskId)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        var task = await _taskRepository.FindAsync(taskId);
        await uow.CompleteAsync();
        return task;
    }

    private async Task<bool> IsCancelledAsync(Guid taskId)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        var task = await _taskRepository.FindAsync(taskId);
        var cancelled = task?.Status == AiTaskStatus.Cancelled;
        await uow.CompleteAsync();
        return cancelled;
    }

    private async Task UpdateTaskAsync(Guid taskId, Action<AiGenerationTask> mutate)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        var task = await _taskRepository.FindAsync(taskId);
        if (task == null)
        {
            await uow.CompleteAsync();
            return;
        }
        mutate(task);
        await _taskRepository.UpdateAsync(task);
        await uow.CompleteAsync();
    }

    private async Task UpdateProgressAsync(Guid taskId, int? progress, string? message)
    {
        await UpdateTaskAsync(taskId, t =>
        {
            if (progress.HasValue)
            {
                t.Progress = Math.Clamp(progress.Value, 0, 99);
            }
            if (!string.IsNullOrWhiteSpace(message))
            {
                t.ProgressMessage = Truncate(message, 500);
            }
        });
    }

    private async Task MonitorCancellationAsync(Guid taskId, CancellationTokenSource cts)
    {
        try
        {
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
                if (await IsCancelledAsync(taskId))
                {
                    cts.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected on completion/cancel
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiGenerationJob: cancellation monitor error for task {TaskId}", taskId);
        }
    }

    private static async Task SafeAwaitAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // ignore
        }
    }

    private static T Deserialize<T>(string? json) where T : class, new()
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new UserFriendlyException("任务输入参数为空");
        }
        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new UserFriendlyException("任务输入参数解析失败");
        }
        catch (JsonException ex)
        {
            throw new UserFriendlyException($"任务输入参数解析失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 三个流式生成服务在资源缺失/摘要缺失时会把错误包成 {"error":"..."} 作为正文返回，
    /// 这里识别并转成失败。
    /// </summary>
    private static void ThrowIfErrorPayload(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("```"))
        {
            var nl = t.IndexOf('\n');
            if (nl >= 0) t = t[(nl + 1)..];
            if (t.EndsWith("```")) t = t[..^3].TrimEnd();
        }
        if (!t.StartsWith("{")) return;
        try
        {
            using var doc = JsonDocument.Parse(t);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var err))
            {
                throw new UserFriendlyException(err.GetString() ?? "AI 生成失败");
            }
        }
        catch (JsonException)
        {
            // 不是合法 JSON，交给前端解析阶段处理
        }
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= max ? value : value[..max];
    }
}
