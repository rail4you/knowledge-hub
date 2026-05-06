using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.MicroMajors;

public class MicroMajorCourse : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid MicroMajorId { get; set; }
    public Guid CourseId { get; set; }
    public int SortOrder { get; set; }
    public bool IsCore { get; set; } = true;

    public MicroMajorCourse()
    {
    }

    public MicroMajorCourse(Guid id, Guid microMajorId, Guid courseId, int sortOrder)
        : base(id)
    {
        MicroMajorId = microMajorId;
        CourseId = courseId;
        SortOrder = sortOrder;
    }
}
