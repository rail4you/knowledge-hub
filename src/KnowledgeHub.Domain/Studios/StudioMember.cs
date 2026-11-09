using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Studios;

/// <summary>工作室成员。</summary>
public class StudioMember : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudioId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public StudioMemberRole Role { get; set; } = StudioMemberRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    private StudioMember()
    {
    }

    public StudioMember(Guid id, Guid? tenantId, Guid studioId, Guid userId, string userName, StudioMemberRole role)
        : base(id)
    {
        TenantId = tenantId;
        StudioId = studioId;
        UserId = userId;
        UserName = userName;
        Role = role;
    }
}
