using System;
using KnowledgeHub.ResourceLibraries;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 资源库成员（多校共建）——记录某所学校在某资源库中的共建身份。
///
/// <para>
/// 字段说明：
/// </para>
/// <list type="bullet">
/// <item><description><see cref="MemberTenantId"/>：成员学校租户 Id。</description></item>
/// <item><description><see cref="TenantId"/>（IMultiTenant）：库归属租户（主建学校），用于 ABP 租户过滤。</description></item>
/// </list>
///
/// <para>
/// 与 <see cref="KnowledgeHub.Alliance.AllianceMember"/> 的区别：联盟是「多校松散组织」，
/// 表达不出主建 / 合作的主次关系；本成员表明确承载「资源库由谁主建、谁参与共建」。
/// </para>
/// </summary>
public class ResourceLibraryMember : Entity<Guid>, IMultiTenant
{
    /// <summary>所属资源库 Id。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>成员学校租户 Id（主建学校或合作学校）。</summary>
    public Guid MemberTenantId { get; set; }

    /// <summary>成员学校名称（冗余快照，避免列表页 N+1 查询）。</summary>
    public string? MemberTenantName { get; set; }

    /// <summary>共建角色。</summary>
    public ResourceLibraryMemberRole Role { get; set; } = ResourceLibraryMemberRole.Partner;

    /// <summary>加入时间。</summary>
    public DateTime JoinedTime { get; set; }

    /// <summary>库归属租户（主建学校）。</summary>
    public Guid? TenantId { get; set; }

    public ResourceLibraryMember()
    {
    }

    public ResourceLibraryMember(
        Guid id,
        Guid libraryId,
        Guid memberTenantId,
        string? memberTenantName,
        ResourceLibraryMemberRole role,
        Guid? ownerTenantId = null)
        : base(id)
    {
        LibraryId = libraryId;
        MemberTenantId = memberTenantId;
        MemberTenantName = memberTenantName;
        Role = role;
        JoinedTime = DateTime.UtcNow;
        TenantId = ownerTenantId;
    }
}
