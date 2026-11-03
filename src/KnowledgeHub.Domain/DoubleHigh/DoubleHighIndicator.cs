using System;
using KnowledgeHub.DoubleHigh.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

public class DoubleHighIndicator : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ParentId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string IndicatorCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Unit { get; set; }
    public DoubleHighDataSourceType DataSourceType { get; set; } = DoubleHighDataSourceType.Manual;
    public decimal? TargetValue { get; set; }
    public decimal Weight { get; set; }
    public int SortOrder { get; set; }

    /// <summary>
    /// 填报类型：系统取数 / 填写数值 / 填写说明 / 仅佐证。
    /// 决定教师填报页面对该指标的操作方式。
    /// </summary>
    public DoubleHighIndicatorType IndicatorType { get; set; } = DoubleHighIndicatorType.Number;

    /// <summary>评分规则：比例制 / 达标制 / 直接计分 / 不计分。</summary>
    public DoubleHighScoringRule ScoringRule { get; set; } = DoubleHighScoringRule.Proportional;

    /// <summary>
    /// 必备佐证清单（JSON 数组字符串，如 ["课程标准","教学大纲"]）。
    /// 每一项都必须至少上传一个文件，该指标才算填报完成。
    /// </summary>
    public string? RequiredEvidences { get; set; }

    /// <summary>设置依据（对应国家基本框架观测点 / 校本说明等）。</summary>
    public string? Basis { get; set; }

    /// <summary>数据来源的文字描述（如「学生学习」「人事系统（手工填报）」）。</summary>
    public string? DataSourceName { get; set; }

    public DoubleHighIndicator()
    {
    }

    public DoubleHighIndicator(Guid id, Guid projectId, string indicatorCode, string name)
        : base(id)
    {
        ProjectId = projectId;
        IndicatorCode = indicatorCode;
        Name = name;
    }
}
