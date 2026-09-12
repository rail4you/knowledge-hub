using System;
using System.Collections.Generic;

namespace KnowledgeHub.Application.AI.Dtos;

/// <summary>AI 使用管理：当前 Key 与模型状态。</summary>
public class AiManagementStatusDto
{
    /// <summary>Key 脱敏展示（如 ****abcd），未配置为空。</summary>
    public string MaskedApiKey { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
    /// <summary>当前文本模型（配置值或默认值）。</summary>
    public string TextModel { get; set; } = string.Empty;
    /// <summary>当前视频理解模型。</summary>
    public string VisionModel { get; set; } = string.Empty;
    public double VideoFps { get; set; }
    public List<AiModelPriceDto> Pricing { get; set; } = new();
}

/// <summary>模型单价（元/百万 tokens）。</summary>
public class AiModelPriceDto
{
    public string Model { get; set; } = string.Empty;
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
}

public class UpdateAiApiKeyDto
{
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>用量记录行。</summary>
public class AiUsageRecordDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public string? Roles { get; set; }
    public string FeatureGroup { get; set; } = string.Empty;
    public string FeatureGroupName { get; set; } = string.Empty;
    public string Feature { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    /// <summary>0=进行中，10=成功，40=失败。</summary>
    public byte Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public bool IsEstimated { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreationTime { get; set; }
}

public class GetAiUsageRecordsInput
{
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? FeatureGroup { get; set; }
    public byte? Status { get; set; }
    /// <summary>用户名 / 功能点关键字。</summary>
    public string? Filter { get; set; }
    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; } = 20;
}

/// <summary>用量汇总（按筛选条件）。</summary>
public class AiUsageSummaryDto
{
    public long TotalCount { get; set; }
    public long SuccessCount { get; set; }
    public long FailedCount { get; set; }
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public decimal TotalEstimatedCost { get; set; }
}

/// <summary>配额配置：角色 → 分组 → 每日次数（null=不限）。</summary>
public class AiQuotasDto
{
    public Dictionary<string, Dictionary<string, int?>> Quotas { get; set; } = new();
}
