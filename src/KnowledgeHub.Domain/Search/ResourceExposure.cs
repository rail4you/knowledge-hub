using System;
using KnowledgeHub.Resources;
using Volo.Abp.Domain.Entities;

namespace KnowledgeHub.Domain.Search;

public class ResourceExposure : Entity<Guid>
{
    public Guid ResourceId { get; set; }
    public int TimesInResults { get; set; }
    public int TimesClicked { get; set; }
    public DateTime LastSeen { get; set; }
    public Guid? TenantId { get; set; }

    public virtual Resource? Resource { get; set; }
}
