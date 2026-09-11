using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Exams;
using KnowledgeHub.Exams.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
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
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly LessonPlanAppService _lessonPlanService;
    private readonly CaseAnalysisAppService _caseAnalysisService;
    private readonly CareerGuidanceAppService _careerGuidanceService;
    private readonly ExerciseAiGenerator _exerciseAiGenerator;
    private readonly ILogger<AiGenerationJob> _logger;

    public AiGenerationJob(
        IRepository<AiGenerationTask, Guid> taskRepository,
        IUnitOfWorkManager unitOfWorkManager,
        ICurrentTenant currentTenant,
        LessonPlanAppService lessonPlanService,
        CaseAnalysisAppService caseAnalysisService,
        CareerGuidanceAppService careerGuidanceService,
        ExerciseAiGenerator exerciseAiGenerator,
        ILogger<AiGenerationJob> logger)
    {
        _taskRepository = taskRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _currentTenant = currentTenant;
        _lessonPlanService = lessonPlanService;
        _caseAnalysisService = caseAnalysisService;
        _careerGuidanceService = careerGuidanceService;
        _exerciseAiGenerator = exerciseAiGenerator;
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
        var result = await GenerateCoreAsync(task, cancellationToken);
        await uow.CompleteAsync();
        return result;
    }

    private async Task<string> GenerateCoreAsync(AiGenerationTask task, CancellationToken cancellationToken)
    {
        switch (task.TaskType)
        {
            case AiTaskType.LessonPlanSingle:
                return await GenerateLessonPlanSingleAsync(task, cancellationToken);
            case AiTaskType.LessonPlanMulti:
                return await GenerateLessonPlanMultiAsync(task, cancellationToken);
            case AiTaskType.CaseAnalysis:
                return await GenerateCaseAnalysisAsync(task, cancellationToken);
            case AiTaskType.CareerGuidance:
                return await GenerateCareerGuidanceAsync(task, cancellationToken);
            case AiTaskType.ExerciseGenerate:
                return await GenerateExerciseAsync(task, cancellationToken);
            default:
                throw new UserFriendlyException($"不支持的任务类型：{task.TaskType}");
        }
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
        return text;
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
        return text;
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
