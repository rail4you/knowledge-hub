using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Studios;

/// <summary>
/// 名师工作室。
///
/// <para>采购要求：「平台管理员可创建/修改/启停工作室并指定或更换领衔人；
/// 领衔人负责加入申请、成员角色与权限、资源审核和展示管理」。</para>
///
/// <para>资料本身复用资源中心的 <c>Resource</c> + 文件/预览链路，
/// 工作室只维护「归属 + 准入状态」（见 <see cref="StudioMaterial"/>）。</para>
/// </summary>
public class Studio : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? CoverUrl { get; set; }

    /// <summary>领衔人（负责审核与展示管理）。</summary>
    public Guid? LeaderUserId { get; set; }

    public string? LeaderName { get; set; }

    public StudioStatus Status { get; set; } = StudioStatus.Active;

    public int SortOrder { get; set; }

    public ICollection<StudioMember> Members { get; set; } = new List<StudioMember>();

    private Studio()
    {
    }

    public Studio(Guid id, Guid? tenantId, string name)
        : base(id)
    {
        TenantId = tenantId;
        Name = name?.Trim() ?? string.Empty;
    }

    public void SetInfo(string? description, string? coverUrl)
    {
        Description = description;
        CoverUrl = coverUrl;
    }

    public void SetLeader(Guid? userId, string? userName)
    {
        LeaderUserId = userId;
        LeaderName = userName;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new UserFriendlyException("请填写工作室名称。");
        }

        Name = name.Trim();
    }

    public void SetStatus(StudioStatus status) => Status = status;
}
