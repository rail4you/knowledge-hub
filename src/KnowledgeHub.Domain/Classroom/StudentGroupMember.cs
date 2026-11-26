using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>小组成员（(GroupId, StudentId) 唯一，同一学生在同一任务里只能属于一个组）。</summary>
public class StudentGroupMember : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid GroupId { get; set; }

    public Guid StudentId { get; set; }

    public string? StudentName { get; set; }

    private StudentGroupMember() { }

    public StudentGroupMember(Guid id, Guid groupId, Guid studentId) : base(id)
    {
        GroupId = groupId;
        StudentId = studentId;
    }
}
