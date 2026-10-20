using System;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>分析报告（学生 / 班级 / 教学）。数字由代码算，文字由 LLM 或规则生成。</summary>
public class AnalysisReport : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public AnalysisScope Scope { get; set; }

    public Guid? StudentId { get; set; }

    public Guid CourseId { get; set; }

    public Guid? PaperBatchId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>结论摘要（LLM 或规则生成）</summary>
    public string? Summary { get; set; }

    /// <summary>结构化事实 JSON（可复现，缓存键的一部分）</summary>
    public string? FactsJson { get; set; }

    /// <summary>完整内容 JSON（薄弱点、建议、学习计划等）</summary>
    public string? ContentJson { get; set; }

    /// <summary>事实 JSON + 提示词版本的哈希，用于避免重复调用模型</summary>
    public string? FactsHash { get; set; }

    /// <summary>是否由 LLM 生成（false = 规则生成）</summary>
    public bool GeneratedByLlm { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PushedAt { get; set; }

    private AnalysisReport() { }

    public AnalysisReport(Guid id, AnalysisScope scope, Guid courseId) : base(id)
    {
        Scope = scope;
        CourseId = courseId;
    }
}
