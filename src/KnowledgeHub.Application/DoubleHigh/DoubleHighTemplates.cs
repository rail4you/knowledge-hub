using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeHub.DoubleHigh.Dtos;
using KnowledgeHub.DoubleHigh.Enums;

namespace KnowledgeHub.DoubleHigh;

/// <summary>
/// 评估模板目录：新建评估项目时先选模板，指标、权重、评分规则、必备佐证一并预置，
/// 教师可在「项目配置」里继续调整。
/// </summary>
public static class DoubleHighTemplates
{
    public const string CourseTemplateKey = "course";
    public const string MajorTemplateKey = "major";
    public const string TeamTemplateKey = "team";

    /// <summary>无模板（不套模板，所有指标一开始为空，由老师自己添加）。</summary>
    public const string CustomTemplateKey = "custom";

    /// <summary>无模板的显示名称。</summary>
    public const string NoTemplateName = "无模板（自行添加指标）";

    private static readonly List<DoubleHighTemplateDto> Catalog = new()
    {
        new DoubleHighTemplateDto
        {
            Key = CourseTemplateKey,
            Name = "课程建设评估模板",
            ScopeLabel = "课程",
            Description = "面向单门课程的建设成效评估，含选课、完成率、资源、课程标准与思政等指标。",
            Indicators = new List<DoubleHighTemplateIndicatorDto>
            {
                new()
                {
                    Name = "课程选课人次",
                    IndicatorType = DoubleHighIndicatorType.Auto,
                    Unit = "人次",
                    DataSourceType = DoubleHighDataSourceType.CourseEnrollmentCount,
                    DataSourceName = "学生学习",
                    Weight = 0.2m,
                    ScoringRule = DoubleHighScoringRule.None,
                    Basis = "校本过程数据"
                },
                new()
                {
                    Name = "课程学习完成率",
                    IndicatorType = DoubleHighIndicatorType.Auto,
                    Unit = "%",
                    DataSourceType = DoubleHighDataSourceType.CourseCompletionRate,
                    DataSourceName = "学生学习",
                    Weight = 0.2m,
                    ScoringRule = DoubleHighScoringRule.Proportional,
                    TargetValue = 100m,
                    Basis = "校本过程数据"
                },
                new()
                {
                    Name = "课程教学资源数",
                    IndicatorType = DoubleHighIndicatorType.Auto,
                    Unit = "条",
                    DataSourceType = DoubleHighDataSourceType.ResourceCount,
                    DataSourceName = "教学资源库",
                    Weight = 0.15m,
                    ScoringRule = DoubleHighScoringRule.None,
                    Basis = "国家基本框架观测点19：数字教材建设（间接关联）"
                },
                new()
                {
                    Name = "课程标准与教学大纲",
                    IndicatorType = DoubleHighIndicatorType.EvidenceOnly,
                    Weight = 0.15m,
                    ScoringRule = DoubleHighScoringRule.None,
                    RequiredEvidences = new List<string> { "课程标准", "教学大纲" },
                    Basis = "国家基本框架观测点11：课程标准符合人才培养方案"
                },
                new()
                {
                    Name = "课程思政实施情况",
                    IndicatorType = DoubleHighIndicatorType.Text,
                    Weight = 0.15m,
                    ScoringRule = DoubleHighScoringRule.None,
                    RequiredEvidences = new List<string> { "课程思政教学案例" },
                    Basis = "国家基本框架观测点14"
                },
                new()
                {
                    Name = "教学改革与创新说明",
                    IndicatorType = DoubleHighIndicatorType.Text,
                    Weight = 0.15m,
                    ScoringRule = DoubleHighScoringRule.None,
                    Basis = "校本"
                }
            }
        },
        new DoubleHighTemplateDto
        {
            Key = MajorTemplateKey,
            Name = "专业建设评估模板",
            ScopeLabel = "专业",
            Description = "面向专业的建设成效评估，含开课、微专业、就业率、双师型占比等指标。",
            Indicators = new List<DoubleHighTemplateIndicatorDto>
            {
                new()
                {
                    Name = "专业课程开课数",
                    IndicatorType = DoubleHighIndicatorType.Auto,
                    Unit = "门",
                    DataSourceType = DoubleHighDataSourceType.CourseCount,
                    DataSourceName = "课程中心",
                    Weight = 0.12m,
                    ScoringRule = DoubleHighScoringRule.None,
                    Basis = "校本过程数据"
                },
                new()
                {
                    Name = "微专业数量",
                    IndicatorType = DoubleHighIndicatorType.Auto,
                    Unit = "个",
                    DataSourceType = DoubleHighDataSourceType.MicroMajorCount,
                    DataSourceName = "微专业",
                    Weight = 0.08m,
                    ScoringRule = DoubleHighScoringRule.None,
                    Basis = "校本过程数据"
                },
                new()
                {
                    Name = "教学资源库数量",
                    IndicatorType = DoubleHighIndicatorType.Auto,
                    Unit = "个",
                    DataSourceType = DoubleHighDataSourceType.ResourceCount,
                    DataSourceName = "教学资源库",
                    Weight = 0.08m,
                    ScoringRule = DoubleHighScoringRule.None,
                    Basis = "校本过程数据"
                },
                new()
                {
                    Name = "学生选课人次",
                    IndicatorType = DoubleHighIndicatorType.Auto,
                    Unit = "人次",
                    DataSourceType = DoubleHighDataSourceType.CourseEnrollmentCount,
                    DataSourceName = "学生学习",
                    Weight = 0.12m,
                    ScoringRule = DoubleHighScoringRule.None,
                    Basis = "校本过程数据"
                },
                new()
                {
                    Name = "就业率",
                    IndicatorType = DoubleHighIndicatorType.Auto,
                    Unit = "%",
                    DataSourceType = DoubleHighDataSourceType.EmploymentRate,
                    DataSourceName = "就业招聘",
                    Weight = 0.18m,
                    ScoringRule = DoubleHighScoringRule.Proportional,
                    TargetValue = 100m,
                    RequiredEvidences = new List<string> { "就业质量年度报告" },
                    Basis = "国家基本框架观测点9（无全国统一数值）"
                },
                new()
                {
                    Name = "人才培养方案修订情况",
                    IndicatorType = DoubleHighIndicatorType.Text,
                    Weight = 0.14m,
                    ScoringRule = DoubleHighScoringRule.None,
                    RequiredEvidences = new List<string> { "现行人才培养方案", "专业调研报告", "专家论证会纪要" },
                    Basis = "教职成〔2019〕13号：调研、论证、审核"
                },
                new()
                {
                    Name = "“双师型”教师占比",
                    IndicatorType = DoubleHighIndicatorType.Number,
                    Unit = "%",
                    DataSourceType = DoubleHighDataSourceType.Manual,
                    DataSourceName = "人事系统（手工填报）",
                    Weight = 0.18m,
                    ScoringRule = DoubleHighScoringRule.Threshold,
                    TargetValue = 50m,
                    RequiredEvidences = new List<string> { "“双师型”认定文件" },
                    Basis = "国家基本框架观测点22：≥50%"
                },
                new()
                {
                    Name = "校外实训基地数量",
                    IndicatorType = DoubleHighIndicatorType.Number,
                    Unit = "个",
                    DataSourceType = DoubleHighDataSourceType.Manual,
                    DataSourceName = "手工填报",
                    Weight = 0.1m,
                    ScoringRule = DoubleHighScoringRule.None,
                    RequiredEvidences = new List<string> { "校外实训基地协议" },
                    Basis = "国家基本框架观测点26"
                }
            }
        },
        new DoubleHighTemplateDto
        {
            Key = TeamTemplateKey,
            Name = "教学团队建设评估模板",
            ScopeLabel = "团队",
            Description = "面向教学团队的建设成效评估，含团队规模、成果说明与获奖荣誉。",
            Indicators = new List<DoubleHighTemplateIndicatorDto>
            {
                new()
                {
                    Name = "团队成员数",
                    IndicatorType = DoubleHighIndicatorType.Number,
                    Unit = "人",
                    DataSourceType = DoubleHighDataSourceType.Manual,
                    DataSourceName = "手工填报",
                    Weight = 0.3m,
                    ScoringRule = DoubleHighScoringRule.None,
                    Basis = "校本"
                },
                new()
                {
                    Name = "团队建设成果说明",
                    IndicatorType = DoubleHighIndicatorType.Text,
                    Weight = 0.4m,
                    ScoringRule = DoubleHighScoringRule.None,
                    RequiredEvidences = new List<string> { "成果证明材料" },
                    Basis = "校本"
                },
                new()
                {
                    Name = "获奖与荣誉",
                    IndicatorType = DoubleHighIndicatorType.EvidenceOnly,
                    Weight = 0.3m,
                    ScoringRule = DoubleHighScoringRule.None,
                    RequiredEvidences = new List<string> { "获奖证书" },
                    Basis = "校本"
                }
            }
        }
    };

    /// <summary>模板目录（返回副本，避免调用方误改静态数据）。含一个「无模板」选项，指标为空。</summary>
    public static List<DoubleHighTemplateDto> GetAll()
    {
        var result = Catalog
            .Select(t => new DoubleHighTemplateDto
            {
                Key = t.Key,
                Name = t.Name,
                ScopeLabel = t.ScopeLabel,
                Description = t.Description,
                Indicators = t.Indicators
                    .Select(i => new DoubleHighTemplateIndicatorDto
                    {
                        Name = i.Name,
                        IndicatorType = i.IndicatorType,
                        Unit = i.Unit,
                        DataSourceType = i.DataSourceType,
                        DataSourceName = i.DataSourceName,
                        Weight = i.Weight,
                        ScoringRule = i.ScoringRule,
                        TargetValue = i.TargetValue,
                        RequiredEvidences = new List<string>(i.RequiredEvidences),
                        Basis = i.Basis
                    })
                    .ToList()
            })
            .ToList();

        result.Add(new DoubleHighTemplateDto
        {
            Key = CustomTemplateKey,
            Name = NoTemplateName,
            ScopeLabel = "自定义",
            Description = "不预置任何指标，所有指标一开始为空，由老师按实际情况自行添加。",
            Indicators = new List<DoubleHighTemplateIndicatorDto>()
        });

        return result;
    }

    public static DoubleHighTemplateDto? Find(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return GetAll().FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetName(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) ||
            string.Equals(key, CustomTemplateKey, StringComparison.OrdinalIgnoreCase))
        {
            return NoTemplateName;
        }

        return Find(key)?.Name ?? NoTemplateName;
    }
}
