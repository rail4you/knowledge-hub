using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.UnifiedAuth;

public class UnifiedSignInOutcome
{
    public bool Succeeded { get; init; }
    public bool IsNotAllowed { get; init; }
    public string? Reason { get; init; }
    public IdentityUser? User { get; init; }

    public static UnifiedSignInOutcome Success(IdentityUser user) => new() { Succeeded = true, User = user };
    public static UnifiedSignInOutcome Failed(string? reason) => new() { Reason = reason };
    public static UnifiedSignInOutcome Denied(string? reason) => new() { IsNotAllowed = true, Reason = reason };
}

public interface IUnifiedAuthService
{
    bool Enabled { get; }

    Task<UnifiedSignInOutcome> VerifyAndProvisionAsync(
        Guid tenantId, string userName, string password, CancellationToken cancellationToken = default);

    Task RegisterLocalUserAsync(
        Guid? tenantId, string userName, string? password, string? phone, string? email,
        string? localRole, string? externalId = null, CancellationToken cancellationToken = default);

    Task SyncPasswordAsync(
        Guid tenantId, string userName, string currentPassword, string newPassword,
        CancellationToken cancellationToken = default);

    Task<UnifiedLookupTenantResponse?> LookupTenantAsync(string name, CancellationToken cancellationToken = default);

    Task RegisterTenantAsync(Guid tenantId, string name, CancellationToken cancellationToken = default);
}

/// <summary>
/// 登录校验与本地账号联动：
/// 1. 密码由账户服务校验（旧哈希自动懒迁移）；
/// 2. 本地 AbpUsers 作为应用侧档案保留（角色/权限/业务外键不变）；
/// 3. 统一账户已有、本应用没有的人员，首次登录时 JIT 建号并回填映射。
/// </summary>
public class UnifiedAuthService : ITransientDependency, IUnifiedAuthService
{
    /// <summary>这些原因属于“凭据错误”，其余视为业务拒绝（到期/停用等）。</summary>
    private static readonly string[] CredentialErrors =
    {
        "invalid_credentials", "account_locked", "tenant_not_found", "invalid_response"
    };

    private readonly UnifiedAuthClient _client;
    private readonly IOptions<UnifiedAuthOptions> _options;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<UnifiedAuthService> _logger;

    public UnifiedAuthService(
        UnifiedAuthClient client,
        IOptions<UnifiedAuthOptions> options,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        ILogger<UnifiedAuthService> logger)
    {
        _client = client;
        _options = options;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public bool Enabled => _options.Value.Enabled;

    public async Task<UnifiedSignInOutcome> VerifyAndProvisionAsync(
        Guid tenantId, string userName, string password, CancellationToken cancellationToken = default)
    {
        UnifiedVerifyResponse response;
        try
        {
            response = await _client.VerifyAsync(tenantId, userName, password, cancellationToken);
        }
        catch (Exception ex) when (ex is UnifiedAuthException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "统一账户服务不可用，租户={TenantId} 用户={UserName}", tenantId, userName);
            if (_options.Value.FailOpen)
            {
                return UnifiedSignInOutcome.Failed("unified_unavailable");
            }
            return UnifiedSignInOutcome.Failed("unified_unavailable");
        }

        if (!response.Allowed)
        {
            var reason = response.Reason ?? "invalid_credentials";
            if (CredentialErrors.Contains(reason))
            {
                return UnifiedSignInOutcome.Failed(reason);
            }
            _logger.LogInformation("统一账户拒绝登录: tenant={TenantId} user={UserName} reason={Reason}",
                tenantId, userName, reason);
            return UnifiedSignInOutcome.Denied(reason);
        }

        var user = await ResolveLocalUserAsync(tenantId, userName, response, cancellationToken);
        if (user == null && _options.Value.ProvisionOnLogin && response.Person != null)
        {
            user = await ProvisionLocalUserAsync(tenantId, response, cancellationToken);
        }

        return user == null
            ? UnifiedSignInOutcome.Failed("local_user_unavailable")
            : UnifiedSignInOutcome.Success(user);
    }

    private async Task<IdentityUser?> ResolveLocalUserAsync(
        Guid tenantId, string userName, UnifiedVerifyResponse response, CancellationToken cancellationToken)
    {
        using (_currentTenant.Change(tenantId))
        {
            if (Guid.TryParse(response.ExternalId, out var externalGuid))
            {
                var byId = await _userManager.FindByIdAsync(externalGuid.ToString());
                if (byId != null && byId.TenantId == tenantId)
                {
                    return byId;
                }
            }

            foreach (var candidate in new[] { response.ExternalLogin, userName, response.Membership?.MemberNo })
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }
                var user = await _userManager.FindByNameAsync(candidate.Trim());
                if (user != null && user.TenantId == tenantId)
                {
                    return user;
                }
            }
        }
        return null;
    }

    private async Task<IdentityUser?> ProvisionLocalUserAsync(
        Guid tenantId, UnifiedVerifyResponse response, CancellationToken cancellationToken)
    {
        using (_currentTenant.Change(tenantId))
        {
            var person = response.Person!;
            var requested = FirstNonEmpty(
                response.ExternalLogin,
                response.Membership?.MemberNo,
                person.PhoneE164,
                "u" + person.Id.ToString("N")[..12]);
            var userName = await MakeUniqueUserNameAsync(requested!, tenantId);

            string email = $"u{person.Id:N}@unified.local";
            if (!string.IsNullOrWhiteSpace(person.Email))
            {
                var existingByEmail = await _userManager.FindByEmailAsync(person.Email!);
                if (existingByEmail == null)
                {
                    email = person.Email!;
                }
            }

            var user = new IdentityUser(Guid.NewGuid(), userName, email, tenantId)
            {
                Name = string.IsNullOrWhiteSpace(person.Name) ? userName : person.Name
            };
            if (!string.IsNullOrWhiteSpace(person.PhoneE164))
            {
                user.SetPhoneNumber(person.PhoneE164, true);
            }
            user.SetIsActive(true);

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("JIT 建号失败: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
                return null;
            }

            var roleName = _options.Value.ResolveRole(response.Membership?.Role);
            var roleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning("JIT 建号后赋角色 {Role} 失败: {Errors}", roleName,
                    string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }

            if (response.MustChangePassword)
            {
                user.SetShouldChangePasswordOnNextLogin(true);
                await _userManager.UpdateAsync(user);
            }

            try
            {
                await _client.LinkIdentityAsync(tenantId, person.Id, user.Id.ToString(), user.UserName!,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "回填应用账号映射失败: tenant={TenantId} user={UserName}", tenantId, user.UserName);
            }

            _logger.LogInformation("JIT 建号完成: tenant={TenantId} user={UserName} person={PersonId}",
                tenantId, user.UserName, person.Id);
            return user;
        }
    }

    private async Task<string> MakeUniqueUserNameAsync(string requested, Guid tenantId)
    {
        var sanitized = new string(requested.Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' or '@' ? ch : '_')
            .ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "u" + Guid.NewGuid().ToString("N")[..10];
        }

        var candidate = sanitized;
        for (var i = 0; i < 20; i++)
        {
            var existing = await _userManager.FindByNameAsync(candidate);
            if (existing == null)
            {
                return candidate;
            }
            candidate = sanitized + "_" + Guid.NewGuid().ToString("N")[..6];
        }
        return sanitized + "_" + Guid.NewGuid().ToString("N")[..10];
    }

    public async Task RegisterLocalUserAsync(
        Guid? tenantId, string userName, string? password, string? phone, string? email,
        string? localRole, string? externalId = null, CancellationToken cancellationToken = default)
    {
        if (!Enabled || tenantId == null || string.IsNullOrWhiteSpace(userName))
        {
            return;
        }

        try
        {
            var request = new CreateUnifiedMemberRequest
            {
                AppTenantId = tenantId.Value.ToString(),
                MemberNo = userName.Trim(),
                Name = userName.Trim(),
                Phone = phone,
                Email = email,
                Role = _options.Value.ResolveUnifiedRole(localRole),
                Password = password,
                ExternalLogin = userName.Trim(),
                ExternalId = externalId
            };
            var result = await _client.CreateMemberAsync(request, cancellationToken);
            if (result.Person != null)
            {
                await _client.LinkIdentityAsync(tenantId.Value, result.Person.Id,
                    externalId ?? userName, userName, cancellationToken);
            }
            if (!string.IsNullOrWhiteSpace(result.InitialPassword))
            {
                _logger.LogWarning("统一账户为 {UserName} 生成了初始密码（本应用已提供密码时不应出现）", userName);
            }
        }
        catch (Exception ex)
        {
            // 登录仍可用本地密码回退；同步失败记录后由管理员补齐。
            _logger.LogError(ex, "向统一账户登记本地用户失败: tenant={TenantId} user={UserName}", tenantId, userName);
        }
    }

    public async Task SyncPasswordAsync(
        Guid tenantId, string userName, string currentPassword, string newPassword,
        CancellationToken cancellationToken = default)
    {
        await _client.ChangePasswordAsync(tenantId, userName, currentPassword, newPassword, cancellationToken);
    }

    public async Task<UnifiedLookupTenantResponse?> LookupTenantAsync(
        string name, CancellationToken cancellationToken = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        try
        {
            return await _client.LookupTenantAsync(name, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查询统一账户租户失败: {Name}", name);
            return null;
        }
    }

    public async Task RegisterTenantAsync(Guid tenantId, string name, CancellationToken cancellationToken = default)
    {
        if (!Enabled)
        {
            return;
        }
        try
        {
            await _client.RegisterTenantAsync(tenantId.ToString(), name, cancellationToken);
        }
        catch (Exception ex)
        {
            // 本地租户已创建成功；登记失败记录后由管理后台补齐，不阻断业务建校。
            _logger.LogError(ex, "向统一账户登记租户失败: {TenantId} {Name}", tenantId, name);
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }
}
