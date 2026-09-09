using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程—专业关联（一门课程可归属多个专业）。
/// 其中一条为“主专业”（IsPrimary），与 <see cref="Course.MajorId"/> 双写保持一致，
/// 历史查询/统计只认主专业；筛选/搜索认全量。无任何关联时视为“公共课”。
/// </summary>
public class CourseMajor : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid CourseId { get; set; }
    public Guid MajorId { get; set; }
    public bool IsPrimary { get; set; }

    public CourseMajor()
    {
    }

    public CourseMajor(Guid id, Guid courseId, Guid majorId, bool isPrimary = false)
        : base(id)
    {
        CourseId = courseId;
        MajorId = majorId;
        IsPrimary = isPrimary;
    }
}
