using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.AI;

/// <summary>
/// AI 服务调用记录（用量审计 + 费用估算 + 配额统计共用）。
/// 每次调用 Qwen（对话 / 生成任务 / 视频理解 / 摘要 / 习题分析 / 智能体）记一条，
/// token 均为估算值（按文本长度折算，IsEstimated 标记），费用按单价表折算。
/// </summary>
public class AiUsageRecord : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>调用人（后台任务按任务发起人归属）。</summary>
    public Guid UserId { get; set; }

    /// <summary>用户名快照。</summary>
    public string? UserName { get; set; }

    /// <summary>调用时角色快照（逗号分隔）。</summary>
    public string? Roles { get; set; }

    /// <summary>
    /// 功能分组（配额维度）：
    /// CareerGuidance / LessonPlan / CaseAnalysis / ExerciseGenerate / Chat / Video / Summary。
    /// </summary>
    public string FeatureGroup { get; set; } = string.Empty;

    /// <summary>具体功能点，如 SubmitTask / ChatMessage / AgentReply / VideoAnalyze / DocSummary / AiAnalyze。</summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary>实际使用的模型，如 qwen-flash / qwen3-vl-flash。</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>状态：0=进行中，10=成功，40=失败。</summary>
    public byte Status { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    /// <summary>true=按文本长度估算，false=模型返回的精确用量。</summary>
    public bool IsEstimated { get; set; } = true;

    /// <summary>估算费用（元，6 位小数）。</summary>
    public decimal EstimatedCost { get; set; }

    public string? ErrorMessage { get; set; }

    protected AiUsageRecord()
    {
    }

    public AiUsageRecord(Guid id, Guid userId, string featureGroup, string feature, string model)
        : base(id)
    {
        UserId = userId;
        FeatureGroup = featureGroup;
        Feature = feature;
        Model = model;
    }
}
