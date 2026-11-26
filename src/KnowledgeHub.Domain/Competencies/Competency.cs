using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Competencies;

/// <summary>
/// 能力项（毕业要求指标点 / 岗位能力 / 素养项）。
/// <para>
/// 用**自引用树**表达层级，而不是像 kg-edu 那样拆成 main_ability / sub_ability 两张表：
/// <see cref="Level"/> 0=主能力、1=子能力、2=指标点（可再往下加），
/// 一套结构同时满足「专业认证的毕业要求分解」和「岗位能力分解」两种用法。
/// </para>
/// <para>
/// 归属：<see cref="MajorId"/> 为空表示全校通用（通识/素养类），非空表示该专业的能力体系。
/// 课程不直接"拥有"能力，而是通过 <see cref="CourseCompetencySupport"/> 声明「本课支撑哪些能力、强度多少」，
/// 能力再通过 <see cref="CompetencyKnowledgeLink"/> 绑定到课程知识点，从而算出达成度。
/// </para>
/// </summary>
public class Competency : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>所属专业；空 = 全校通用。</summary>
    public Guid? MajorId { get; set; }

    /// <summary>上级能力；空 = 主能力。</summary>
    public Guid? ParentId { get; set; }

    /// <summary>层级：0 主能力 / 1 子能力 / 2 指标点。</summary>
    public int Level { get; set; }

    /// <summary>编码（如 2.3 / A-1），专业认证会直接引用。</summary>
    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;

    private Competency() { }

    public Competency(Guid id, string name) : base(id)
    {
        Name = name;
    }
}
