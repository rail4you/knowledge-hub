using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace KnowledgeHub.Application.Identity;

public class GetTenantUsersInput : GetIdentityUsersInput
{
    public Guid? TenantId { get; set; }

    /// <summary>
    /// 仅查询全局（TenantId == null）用户。host 管理员选择“全局”时使用。
    /// 为 true 时优先于 <see cref="TenantId"/>。
    /// </summary>
    public bool? OnlyHost { get; set; }
}
