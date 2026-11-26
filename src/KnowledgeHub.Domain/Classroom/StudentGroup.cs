using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>小组任务下的一个小组（组名 + 排序），成员见 <see cref="StudentGroupMember"/>。</summary>
public class StudentGroup : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid GroupTaskId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    private StudentGroup() { }

    public StudentGroup(Guid id, Guid groupTaskId, string name, int sortOrder) : base(id)
    {
        GroupTaskId = groupTaskId;
        Name = name;
        SortOrder = sortOrder;
    }
}
