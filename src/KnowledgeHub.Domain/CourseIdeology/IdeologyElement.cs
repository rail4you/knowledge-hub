using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 思政元素（育人要点），如「科技自立自强」「家国情怀」「法治意识」。
///
/// <para>元素体系**按租户可配置**（不同学校可对照自己的《课程思政建设指导纲要》维护一套），
/// 默认种子 6 个常用元素。案例通过多对多关联到元素，图谱与覆盖分析都基于这套元素统计。</para>
/// </summary>
public class IdeologyElement : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>元素名最大长度。</summary>
    public const int MaxNameLength = 64;

    public Guid? TenantId { get; set; }

    /// <summary>元素名称（租户内唯一）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>分类（如：理想信念 / 价值取向 / 职业素养），可空。</summary>
    public string? Category { get; set; }

    /// <summary>元素说明（展示给老师的解释，可空）。</summary>
    public string? Description { get; set; }

    /// <summary>排序（小的在前）。</summary>
    public int SortOrder { get; set; }

    /// <summary>是否启用（停用后不再出现在选择器里，历史案例保留关联）。</summary>
    public bool IsEnabled { get; set; } = true;

    protected IdeologyElement()
    {
    }

    public IdeologyElement(Guid id, string name, Guid? tenantId) : base(id)
    {
        Name = name;
        TenantId = tenantId;
    }
}
