using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Application.AI.Tasks;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// AI 异步生成任务：创建后立即返回，后台 Hangfire 执行；
/// 提供列表/详情/结果/取消/重试/删除与当前用户完成通知。
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize]
public class AiGenerationTaskAppService : KnowledgeHubAppService, IAiGenerationTaskAppService
{
    private readonly IRepository<AiGenerationTask, Guid> _taskRepository;
    private readonly IAiTaskQueue _aiTaskQueue;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IIdentityUserRepository _userRepository;
    private readonly IAiQuotaService _quotaService;

    public AiGenerationTaskAppService(
        IRepository<AiGenerationTask, Guid> taskRepository,
        IAiTaskQueue aiTaskQueue,
        IUnitOfWorkManager unitOfWorkManager,
        IIdentityUserRepository userRepository,
        IAiQuotaService quotaService)
    {
        _taskRepository = taskRepository;
        _aiTaskQueue = aiTaskQueue;
        _unitOfWorkManager = unitOfWorkManager;
        _userRepository = userRepository;
        _quotaService = quotaService;
    }

    public async Task<AiGenerationTaskDto> CreateAsync(CreateAiGenerationTaskDto input)
    {
        await AuthorizationService.CheckAsync(RequiredPermission(input.TaskType));

        if (string.IsNullOrWhiteSpace(input.InputJson))
        {
            throw new UserFriendlyException("任务参数为空");
        }

        // 每日配额：超限直接中文提示，不再烧 token
        await _quotaService.CheckAsync(ToFeatureGroup(input.TaskType));

        var task = new AiGenerationTask(
            GuidGenerator.Create(),
            CurrentUser.Id ?? Guid.Empty,
            input.TaskType,
            Truncate(input.Title, 500) ?? "AI 生成任务")
        {
            TenantId = CurrentTenant.Id,
            ResourceId = input.ResourceId,
            ResourceName = Truncate(input.ResourceName, 500),
            InputJson = input.InputJson,
            Status = AiTaskStatus.Pending,
            Progress = 0,
            ProgressMessage = "排队中…"
        };

        await _taskRepository.InsertAsync(task);

        var taskId = task.Id;
        var tenantId = CurrentTenant.Id;
        var uow = _unitOfWorkManager.Current;
        if (uow != null)
        {
            // 等事务提交后再入队，避免 Hangfire 抢先执行却查不到任务行。
            uow.OnCompleted(async () => await _aiTaskQueue.EnqueueAsync(taskId, tenantId));
        }
        else
        {
            await _aiTaskQueue.EnqueueAsync(taskId, tenantId);
        }

        return MapToDto(task, includePayload: true);
    }

    public async Task<PagedResultDto<AiGenerationTaskDto>> GetListAsync(GetAiGenerationTaskListDto input)
    {
        var canManage = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.AI.ManageTasks);
        var query = await _taskRepository.GetQueryableAsync();

        if (!canManage || input.OnlyMine)
        {
            var me = CurrentUser.Id ?? Guid.Empty;
            query = query.Where(x => x.CreatorUserId == me);
        }

        if (input.TaskType.HasValue)
        {
            query = query.Where(x => x.TaskType == input.TaskType.Value);
        }
        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var kw = input.Filter.Trim();
            query = query.Where(x => x.Title.Contains(kw) || (x.ResourceName != null && x.ResourceName.Contains(kw)));
        }
        if (input.StartTime.HasValue)
        {
            query = query.Where(x => x.CreationTime >= input.StartTime.Value);
        }
        if (input.EndTime.HasValue)
        {
            query = query.Where(x => x.CreationTime <= input.EndTime.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var dtos = items.Select(x => MapToDto(x, includePayload: false)).ToList();
        await FillCreatorNamesAsync(dtos);

        return new PagedResultDto<AiGenerationTaskDto>(totalCount, dtos);
    }

    public async Task<AiGenerationTaskDto> GetAsync(Guid id)
    {
        var task = await GetAndAuthorizeAsync(id);
        var dto = MapToDto(task, includePayload: true);
        await FillCreatorNamesAsync(new List<AiGenerationTaskDto> { dto });
        return dto;
    }

    public async Task<string?> GetResultAsync(Guid id)
    {
        var task = await GetAndAuthorizeAsync(id);
        return task.ResultJson;
    }

    public async Task CancelAsync(Guid id)
    {
        var task = await GetAndAuthorizeAsync(id);
        if (task.Status is AiTaskStatus.Completed or AiTaskStatus.Failed or AiTaskStatus.Cancelled)
        {
            throw new UserFriendlyException("该任务已结束，无法取消");
        }

        task.Status = AiTaskStatus.Cancelled;
        task.ProgressMessage = "已取消";
        task.CompletedAt = DateTime.UtcNow;
        await _taskRepository.UpdateAsync(task);
    }

    public async Task RetryAsync(Guid id)
    {
        var task = await GetAndAuthorizeAsync(id);
        if (task.Status is AiTaskStatus.Pending or AiTaskStatus.Running)
        {
            throw new UserFriendlyException("任务进行中，请稍后再试");
        }

        task.Status = AiTaskStatus.Pending;
        task.Progress = 0;
        task.ProgressMessage = "排队中…";
        task.ResultJson = null;
        task.ErrorMessage = null;
        task.StartedAt = null;
        task.CompletedAt = null;
        task.IsRead = false;
        task.RetryCount += 1;
        await _taskRepository.UpdateAsync(task);

        var taskId = task.Id;
        var tenantId = task.TenantId;
        var uow = _unitOfWorkManager.Current;
        if (uow != null)
        {
            uow.OnCompleted(async () => await _aiTaskQueue.EnqueueAsync(taskId, tenantId));
        }
        else
        {
            await _aiTaskQueue.EnqueueAsync(taskId, tenantId);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetAndAuthorizeAsync(id);
        await _taskRepository.DeleteAsync(id);
    }

    public async Task<int> GetMyUnreadCountAsync()
    {
        var me = CurrentUser.Id ?? Guid.Empty;
        var query = await _taskRepository.GetQueryableAsync();
        return await AsyncExecuter.CountAsync(query.Where(x =>
            x.CreatorUserId == me && x.Status == AiTaskStatus.Completed && !x.IsRead));
    }

    public async Task<List<AiGenerationTaskDto>> GetMyCompletedAsync(bool unreadOnly = false)
    {
        var me = CurrentUser.Id ?? Guid.Empty;
        var query = await _taskRepository.GetQueryableAsync();
        query = query.Where(x => x.CreatorUserId == me && x.Status == AiTaskStatus.Completed);
        if (unreadOnly)
        {
            query = query.Where(x => !x.IsRead);
        }

        var items = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CompletedAt ?? x.CreationTime).Take(20));

        return items.Select(x => MapToDto(x, includePayload: false)).ToList();
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var task = await GetAndAuthorizeAsync(id);
        if (!task.IsRead)
        {
            task.IsRead = true;
            await _taskRepository.UpdateAsync(task);
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        var me = CurrentUser.Id ?? Guid.Empty;
        var query = await _taskRepository.GetQueryableAsync();
        var items = await AsyncExecuter.ToListAsync(query.Where(x =>
            x.CreatorUserId == me && x.Status == AiTaskStatus.Completed && !x.IsRead));
        if (items.Count == 0) return;

        foreach (var item in items)
        {
            item.IsRead = true;
            await _taskRepository.UpdateAsync(item);
        }
    }

    // ==================== helpers ====================

    private async Task<AiGenerationTask> GetAndAuthorizeAsync(Guid id)
    {
        var task = await _taskRepository.GetAsync(id);
        var canManage = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.AI.ManageTasks);
        if (!canManage && task.CreatorUserId != (CurrentUser.Id ?? Guid.Empty))
        {
            throw new AbpAuthorizationException("无权访问该任务");
        }
        return task;
    }

    private static string RequiredPermission(AiTaskType taskType) => taskType switch
    {
        AiTaskType.LessonPlanSingle => KnowledgeHubPermissions.AI.LessonPlan,
        AiTaskType.LessonPlanMulti => KnowledgeHubPermissions.AI.LessonPlan,
        AiTaskType.CaseAnalysis => KnowledgeHubPermissions.AI.CaseAnalysis,
        AiTaskType.CareerGuidance => KnowledgeHubPermissions.AI.CareerGuidance,
        AiTaskType.ExerciseGenerate => KnowledgeHubPermissions.AI.ExerciseGenerate,
        _ => throw new UserFriendlyException($"未知的任务类型：{taskType}")
    };

    private static string ToFeatureGroup(AiTaskType taskType) => taskType switch
    {
        AiTaskType.LessonPlanSingle => AiFeatureGroups.LessonPlan,
        AiTaskType.LessonPlanMulti => AiFeatureGroups.LessonPlan,
        AiTaskType.CaseAnalysis => AiFeatureGroups.CaseAnalysis,
        AiTaskType.CareerGuidance => AiFeatureGroups.CareerGuidance,
        AiTaskType.ExerciseGenerate => AiFeatureGroups.ExerciseGenerate,
        _ => AiFeatureGroups.Chat,
    };

    private async Task FillCreatorNamesAsync(List<AiGenerationTaskDto> items)
    {
        var ids = items.Select(x => x.CreatorUserId).Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return;

        foreach (var id in ids)
        {
            try
            {
                var user = await _userRepository.FindAsync(id);
                if (user == null) continue;
                foreach (var item in items.Where(x => x.CreatorUserId == id))
                {
                    item.CreatorUserName = user.UserName;
                }
            }
            catch
            {
                // 跨租户 / 用户已删除时忽略
            }
        }
    }

    private static AiGenerationTaskDto MapToDto(AiGenerationTask task, bool includePayload)
    {
        return new AiGenerationTaskDto
        {
            Id = task.Id,
            CreatorUserId = task.CreatorUserId,
            TaskType = task.TaskType,
            Status = task.Status,
            Title = task.Title,
            ResourceId = task.ResourceId,
            ResourceName = task.ResourceName,
            Progress = task.Progress,
            ProgressMessage = task.ProgressMessage,
            ErrorMessage = task.ErrorMessage,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
            RetryCount = task.RetryCount,
            IsRead = task.IsRead,
            CreationTime = task.CreationTime,
            InputJson = includePayload ? task.InputJson : null,
            ResultJson = includePayload ? task.ResultJson : null
        };
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
