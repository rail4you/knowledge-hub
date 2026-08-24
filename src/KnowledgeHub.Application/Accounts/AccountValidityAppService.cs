using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace KnowledgeHub.Accounts;

/// <summary>
/// 账号有效期管理服务。
/// 仅「全局管理员（host）」可调用：跨院校为租户管理员/教师账号配置有效期、
/// 到期自动熔断编辑/管理权限、续期恢复权限。
/// </summary>
[Authorize]
public class AccountValidityAppService : KnowledgeHubAppService, IAccountValidityAppService
{
    private const int ExpiringDays = 7;

    private readonly IRepository<AccountValidity, Guid> _validityRepository;
    private readonly IRepository<Volo.Abp.Identity.IdentityUser, Guid> _userRepository;
    private readonly IRepository<Volo.Abp.Identity.IdentityRole, Guid> _roleRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAccountValidityPermissionHandler _permissionHandler;

    public AccountValidityAppService(
        IRepository<AccountValidity, Guid> validityRepository,
        IRepository<Volo.Abp.Identity.IdentityUser, Guid> userRepository,
        IRepository<Volo.Abp.Identity.IdentityRole, Guid> roleRepository,
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        IAccountValidityPermissionHandler permissionHandler)
    {
        _validityRepository = validityRepository;
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _permissionHandler = permissionHandler;
    }

    public async Task<PagedResultDto<AccountValidityDto>> GetListAsync(GetAccountValidityListInput input)
    {
        await EnsureHostAsync();

        using (DataFilter.Disable<IMultiTenant>())
        {
            var users = await _userRepository.GetListAsync(includeDetails: true);
            var roles = await _roleRepository.GetListAsync(includeDetails: false);
            var roleNameById = roles.ToDictionary(r => r.Id, r => r.Name);

            var validities = await _validityRepository.GetListAsync();
            var validityByUser = validities.ToDictionary(v => v.UserId);

            var tenants = await _tenantRepository.GetListAsync(includeDetails: false);
            var tenantNameById = tenants.ToDictionary(t => t.Id, t => t.Name);

            var now = Clock.Now;

            var items = new List<AccountValidityDto>();
            foreach (var user in users)
            {
                var primaryRole = GetPrimaryControlledRole(user, roleNameById);
                if (primaryRole == null)
                {
                    continue;
                }

                validityByUser.TryGetValue(user.Id, out var validity);

                var dto = BuildDto(user, primaryRole, validity, tenantNameById, now);
                if (!MatchesFilter(dto, input))
                {
                    continue;
                }

                items.Add(dto);
            }

            var totalCount = items.Count;

            // 排序
            IEnumerable<AccountValidityDto> ordered = input.Sorting switch
            {
                "userName" => items.OrderBy(x => x.UserName),
                "validUntil" or "validUntil desc" => items.OrderByDescending(x => x.ValidUntil),
                "validUntil asc" => items.OrderBy(x => x.ValidUntil),
                _ => items.OrderByDescending(x => x.LastModifiedTime ?? DateTime.MinValue)
            };

            var page = ordered
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToList();

            return new PagedResultDto<AccountValidityDto>(totalCount, page);
        }
    }

    public async Task<AccountValidityDto> GetAsync(Guid userId)
    {
        await EnsureHostAsync();

        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(userId, includeDetails: true);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {userId}");
            }

            var roles = await _roleRepository.GetListAsync(includeDetails: false);
            var roleNameById = roles.ToDictionary(r => r.Id, r => r.Name);
            var primaryRole = GetPrimaryControlledRole(user, roleNameById);
            if (primaryRole == null)
            {
                throw new UserFriendlyException($"账号 {user.UserName} 不处于有效期管控范围（仅租户管理员/教师可管控）。");
            }

            var validity = await _validityRepository.FindAsync(x => x.UserId == userId);
            var tenants = await _tenantRepository.GetListAsync(includeDetails: false);
            var tenantNameById = tenants.ToDictionary(t => t.Id, t => t.Name);

            return BuildDto(user, primaryRole, validity, tenantNameById, Clock.Now);
        }
    }

    [UnitOfWork]
    public async Task<AccountValidityDto> SetAsync(SetAccountValidityInput input)
    {
        await EnsureHostAsync();

        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(input.UserId, includeDetails: true);
            if (user == null)
            {
                throw new UserFriendlyException($"用户不存在: {input.UserId}");
            }

            var roles = await _roleRepository.GetListAsync(includeDetails: false);
            var roleNameById = roles.ToDictionary(r => r.Id, r => r.Name);
            var primaryRole = GetPrimaryControlledRole(user, roleNameById);
            if (primaryRole == null)
            {
                throw new UserFriendlyException($"账号 {user.UserName} 不处于有效期管控范围（仅租户管理员/教师可管控）。");
            }

            var now = Clock.Now;
            var validUntil = ResolveValidUntil(input.Permanent, input.ValidUntil, input.Days, now);

            var validity = await _validityRepository.FindAsync(x => x.UserId == user.Id);
            var isNew = validity == null;
            if (isNew)
            {
                validity = new AccountValidity(
                    GuidGenerator.Create(),
                    user.Id,
                    user.TenantId,
                    user.UserName ?? string.Empty,
                    user.Name ?? user.UserName ?? string.Empty,
                    primaryRole,
                    validUntil);
            }
            else
            {
                // 先撤销历史熔断（若有），避免继续叠加在旧状态上
                await RestoreIfExpiredAsync(validity, now);

                validity.TenantId = user.TenantId;
                validity.UserName = user.UserName ?? string.Empty;
                validity.DisplayName = user.Name ?? user.UserName ?? string.Empty;
                validity.RoleName = primaryRole;
                validity.UpdateValidUntil(validUntil);
            }

            // 若新截止时间已过或等于当前时间 → 立即熔断
            if (validUntil.HasValue && validUntil.Value <= now)
            {
                var revoked = await _permissionHandler.RevokeEditPermissionsAsync(
                    user.Id, user.TenantId, primaryRole);
                validity.MarkExpired(SerializeList(revoked), now);
            }
            else
            {
                validity.MarkRestored(now);
            }

            if (isNew)
            {
                await _validityRepository.InsertAsync(validity, autoSave: true);
            }
            else
            {
                await _validityRepository.UpdateAsync(validity, autoSave: true);
            }

            var tenants = await _tenantRepository.GetListAsync(includeDetails: false);
            var tenantNameById = tenants.ToDictionary(t => t.Id, t => t.Name);
            return BuildDto(user, primaryRole, validity, tenantNameById, now);
        }
    }

    [UnitOfWork]
    public async Task SetBatchAsync(SetAccountValidityBatchInput input)
    {
        await EnsureHostAsync();

        if (input.UserIds == null || input.UserIds.Count == 0)
        {
            throw new UserFriendlyException("请至少选择一位用户。");
        }

        var now = Clock.Now;
        var validUntil = ResolveValidUntil(input.Permanent, input.ValidUntil, input.Days, now);

        using (DataFilter.Disable<IMultiTenant>())
        {
            var roles = await _roleRepository.GetListAsync(includeDetails: false);
            var roleNameById = roles.ToDictionary(r => r.Id, r => r.Name);
            var tenants = await _tenantRepository.GetListAsync(includeDetails: false);
            var tenantNameById = tenants.ToDictionary(t => t.Id, t => t.Name);

            foreach (var userId in input.UserIds)
            {
                try
                {
                    var user = await _userRepository.FindAsync(userId, includeDetails: true);
                    if (user == null)
                    {
                        continue;
                    }

                    var primaryRole = GetPrimaryControlledRole(user, roleNameById);
                    if (primaryRole == null)
                    {
                        continue;
                    }

                    var validity = await _validityRepository.FindAsync(x => x.UserId == user.Id);
                    var isNew = validity == null;
                    if (isNew)
                    {
                        validity = new AccountValidity(
                            GuidGenerator.Create(),
                            user.Id,
                            user.TenantId,
                            user.UserName ?? string.Empty,
                            user.Name ?? user.UserName ?? string.Empty,
                            primaryRole,
                            validUntil);
                    }
                    else
                    {
                        await RestoreIfExpiredAsync(validity, now);
                        validity.TenantId = user.TenantId;
                        validity.UserName = user.UserName ?? string.Empty;
                        validity.DisplayName = user.Name ?? user.UserName ?? string.Empty;
                        validity.RoleName = primaryRole;
                        validity.UpdateValidUntil(validUntil);
                    }

                    if (validUntil.HasValue && validUntil.Value <= now)
                    {
                        var revoked = await _permissionHandler.RevokeEditPermissionsAsync(
                            user.Id, user.TenantId, primaryRole);
                        validity.MarkExpired(SerializeList(revoked), now);
                    }
                    else
                    {
                        validity.MarkRestored(now);
                    }

                    if (isNew)
                    {
                        await _validityRepository.InsertAsync(validity, autoSave: true);
                    }
                    else
                    {
                        await _validityRepository.UpdateAsync(validity, autoSave: true);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "[AccountValidity] 批量设置有效期失败 UserId={UserId}，跳过。", userId);
                }
            }
        }
    }

    /// <summary>若处于已到期状态，先恢复被熔断的权限并清空记录。</summary>
    private async Task RestoreIfExpiredAsync(AccountValidity validity, DateTime now)
    {
        if (validity.Status != AccountValidityStatus.Expired)
        {
            return;
        }

        var revoked = DeserializeList(validity.RevokedPermissionsJson);
        if (revoked.Count > 0)
        {
            await _permissionHandler.RestorePermissionsAsync(
                validity.UserId, validity.TenantId, revoked);
        }

        validity.MarkRestored(now);
    }

    private static DateTime? ResolveValidUntil(bool permanent, DateTime? validUntil, int? days, DateTime now)
    {
        if (permanent)
        {
            return null;
        }

        if (days.HasValue && days.Value > 0)
        {
            return now.AddDays(days.Value);
        }

        if (validUntil.HasValue)
        {
            return validUntil.Value;
        }

        throw new UserFriendlyException("请设置有效截止日期或选择有效天数/永久有效。");
    }

    /// <summary>取受控角色：优先 SchoolAdmin，其次 Teacher；无则返回 null。</summary>
    private static string? GetPrimaryControlledRole(
        Volo.Abp.Identity.IdentityUser user,
        Dictionary<Guid, string> roleNameById)
    {
        var roleNames = (user.Roles ?? new List<IdentityUserRole>())
            .Select(r => roleNameById.GetValueOrDefault(r.RoleId))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        if (roleNames.Contains("SchoolAdmin"))
        {
            return "SchoolAdmin";
        }

        return roleNames.Contains("Teacher") ? "Teacher" : null;
    }

    private AccountValidityDto BuildDto(
        Volo.Abp.Identity.IdentityUser user,
        string primaryRole,
        AccountValidity? validity,
        Dictionary<Guid, string> tenantNameById,
        DateTime now)
    {
        var validUntil = validity?.ValidUntil;
        var isExpired = validity != null && validity.Status == AccountValidityStatus.Expired;
        var remainingDays = validUntil.HasValue ? (int)Math.Ceiling((validUntil.Value - now).TotalDays) : (int?)null;

        bool isExpiringSoon;
        if (isExpired)
        {
            isExpiringSoon = true;
        }
        else if (validUntil.HasValue)
        {
            isExpiringSoon = validUntil.Value > now && validUntil.Value <= now.AddDays(ExpiringDays);
        }
        else
        {
            isExpiringSoon = false;
        }

        return new AccountValidityDto
        {
            Id = validity?.Id ?? Guid.Empty,
            UserId = user.Id,
            TenantId = user.TenantId,
            TenantName = user.TenantId.HasValue && tenantNameById.TryGetValue(user.TenantId.Value, out var tn) ? tn : null,
            UserName = user.UserName ?? string.Empty,
            DisplayName = user.Name ?? user.UserName,
            RoleName = primaryRole,
            ValidUntil = validUntil,
            Status = validity?.Status,
            IsExpired = isExpired,
            IsExpiringSoon = isExpiringSoon,
            RemainingDays = remainingDays,
            UserIsActive = user.IsActive,
            LastModifiedTime = GetLastModifiedTime(validity),
        };
    }

    private static DateTime? GetLastModifiedTime(AccountValidity? validity)
    {
        if (validity == null)
        {
            return null;
        }

        return validity.LastModificationTime ?? validity.CreationTime;
    }

    private bool MatchesFilter(AccountValidityDto dto, GetAccountValidityListInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.Trim();
            if (!(dto.UserName?.Contains(f, StringComparison.OrdinalIgnoreCase) == true
                  || dto.DisplayName?.Contains(f, StringComparison.OrdinalIgnoreCase) == true))
            {
                return false;
            }
        }

        if (input.TenantId.HasValue && dto.TenantId != input.TenantId.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(input.RoleName) && dto.RoleName != input.RoleName)
        {
            return false;
        }

        if (input.Status.HasValue)
        {
            if (dto.Status != input.Status)
            {
                return false;
            }
        }

        if (input.ExpiringSoon == true && !dto.IsExpiringSoon)
        {
            return false;
        }

        return true;
    }

    private async Task EnsureHostAsync()
    {
        if (_currentTenant.Id.HasValue)
        {
            throw new UserFriendlyException("仅全局管理员（平台运营方）可进行账号有效期配置。");
        }
    }

    private static string? SerializeList(IReadOnlyList<string> items)
    {
        return items == null || items.Count == 0 ? null : JsonSerializer.Serialize(items);
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
