using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

/// <summary>
/// 岗位能力图谱（对齐 kg-edu 的 <c>job_competency_graph</c>）。
/// <para>
/// 结构：岗位 → 核心工作任务（<see cref="JobCoreTask"/>）→ 任务所需能力（<see cref="JobTaskCompetency"/>
/// 指向 <c>Competencies.Competency</c>）→ 能力关联的课程知识点 → 学生掌握度。
/// </para>
/// <para>
/// 与 kg-edu 的差异：**不再单独建 <c>JobTaskAbility</c> 表**，而是复用能力体系里已有的能力项 ——
/// 否则「岗位能力」和「毕业要求指标点」会是两套互不相干的能力，达成度算不出来。
/// </para>
/// </summary>
public class JobCompetencyGraph : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>岗位名称（如"数据分析师"）。</summary>
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>所属专业（可空 = 全校/跨专业通用）。</summary>
    public Guid? MajorId { get; set; }

    /// <summary>可选：关联到具体的招聘岗位（JobPosting），用于招聘页展示该岗位的能力要求。</summary>
    public Guid? JobPostingId { get; set; }

    /// <summary>版本号：岗位能力模型会逐年修订，保留历史版本便于对照。</summary>
    public int Version { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    private JobCompetencyGraph() { }

    public JobCompetencyGraph(Guid id, string title) : base(id)
    {
        Title = title;
    }
}
