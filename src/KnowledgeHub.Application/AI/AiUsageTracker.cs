using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// AI 调用用量追踪：每次 Qwen 调用记一条（开始记 input，结束补 output + 费用）。
/// token 均为估算值（文本长度折算），费用按单价表折算，仅供成本参考。
/// 自身绝不抛异常，不能影响主流程。
/// </summary>
public interface IAiUsageTracker
{
    Task<Guid> StartAsync(
        string featureGroup,
        string feature,
        string model,
        string? inputText,
        int? inputTokens = null,
        Guid? userId = null,
        Guid? tenantId = null,
        string? userName = null,
        string? roles = null);

    Task CompleteAsync(Guid recordId, string? outputText, bool success, string? error = null, int? outputTokens = null, int? inputTokens = null, bool exact = false);

    /// <summary>文本长度折算 token（中英混合约 1.5 字符/token）。</summary>
    static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return Math.Max(1, (int)Math.Ceiling(text.Length / 1.5));
    }

    /// <summary>估算费用（元）。单价单位：元/百万 tokens。</summary>
    static decimal EstimateCost(string? model, int inputTokens, int outputTokens)
    {
        var (pIn, pOut) = Pricing.GetValueOrDefault(model ?? "", Pricing["default"]);
        return Math.Round(inputTokens / 1000000m * pIn + outputTokens / 1000000m * pOut, 6);
    }

    /// <summary>模型单价表（元/百万 tokens：输入，输出）。改模型时同步这里。</summary>
    static readonly Dictionary<string, (decimal In, decimal Out)> Pricing = new()
    {
        ["qwen-flash"] = (0.15m, 1.5m),
        ["qwen3-vl-flash"] = (0.16m, 1.5m),
        ["qwen-plus"] = (0.8m, 2m),
        ["qwen3-vl-plus"] = (1m, 10m),
        ["text-embedding-v3"] = (0.5m, 0m),
        ["default"] = (0.8m, 2m),
    };
}

public class AiUsageTracker : IAiUsageTracker, ITransientDependency
{
    private readonly IRepository<AiUsageRecord, Guid> _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _uowManager;

    public AiUsageTracker(
        IRepository<AiUsageRecord, Guid> repository,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager uowManager)
    {
        _repository = repository;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _uowManager = uowManager;
    }

    public async Task<Guid> StartAsync(
        string featureGroup,
        string feature,
        string model,
        string? inputText,
        int? inputTokens = null,
        Guid? userId = null,
        Guid? tenantId = null,
        string? userName = null,
        string? roles = null)
    {
        try
        {
            // 独立事务：主流程回滚（任务失败）也不丢用量记录
            using var uow = _uowManager.Begin(requiresNew: true);
            var uid = userId ?? _currentUser.Id ?? Guid.Empty;
            var record = new AiUsageRecord(
                Guid.NewGuid(), uid, featureGroup, feature, model ?? "qwen-flash")
            {
                TenantId = tenantId ?? _currentTenant.Id,
                UserName = userName ?? _currentUser.UserName,
                Roles = roles ?? (_currentUser.Roles != null ? string.Join(",", _currentUser.Roles) : null),
                Status = AiUsageStatus.Running,
                InputTokens = inputTokens ?? IAiUsageTracker.EstimateTokens(inputText),
                // 起记时一律先标估算；Complete 时传入 exact=true 才转精确
                IsEstimated = true,
            };
            await _repository.InsertAsync(record, autoSave: true);
            await uow.CompleteAsync();
            return record.Id;
        }
        catch
        {
            return Guid.Empty;
        }
    }

    public async Task CompleteAsync(Guid recordId, string? outputText, bool success, string? error = null, int? outputTokens = null, int? inputTokens = null, bool exact = false)
    {
        if (recordId == Guid.Empty) return;
        try
        {
            using var uow = _uowManager.Begin(requiresNew: true);
            var record = await _repository.FindAsync(recordId);
            if (record == null) return;
            if (inputTokens.HasValue) record.InputTokens = inputTokens.Value;
            record.OutputTokens = outputTokens ?? IAiUsageTracker.EstimateTokens(outputText);
            // 只有调用方明确传入模型返回的精确用量时才标为非估算
            record.IsEstimated = !exact;
            record.Status = success ? AiUsageStatus.Completed : AiUsageStatus.Failed;
            record.ErrorMessage = string.IsNullOrWhiteSpace(error)
                ? null
                : (error.Length > 2000 ? error[..2000] : error);
            record.EstimatedCost = IAiUsageTracker.EstimateCost(record.Model, record.InputTokens, record.OutputTokens);
            await _repository.UpdateAsync(record, autoSave: true);
            await uow.CompleteAsync();
        }
        catch
        {
            // 用量记录失败静默忽略
        }
    }
}
