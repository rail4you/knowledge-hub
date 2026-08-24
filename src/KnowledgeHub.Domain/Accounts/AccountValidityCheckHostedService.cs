using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace KnowledgeHub.Accounts;

/// <summary>
/// 账号有效期检测后台任务。
///
/// 周期性扫描到期账号，自动执行「差异化权限熔断」：
/// 对所有 Status=Active 且 ValidUntil 已过期的记录，按快照角色收回编辑/管理权限并标记为过期。
/// 全程无需人工干预。续期恢复由 AccountValidityAppService 在设置新的有效期时同步完成。
/// </summary>
public class AccountValidityCheckHostedService : BackgroundService, ITransientDependency
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountValidityCheckHostedService> _logger;

    public AccountValidityCheckHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AccountValidityCheckHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(CheckInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessExpiredAccountsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[AccountValidity] 到期账号扫描失败，继续下一轮。");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 应用关闭
        }
    }

    [UnitOfWork]
    protected virtual async Task ProcessExpiredAccountsAsync(CancellationToken cancellationToken)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<AccountValidity, Guid>>();
            var handler = scope.ServiceProvider.GetRequiredService<IAccountValidityPermissionHandler>();
            var clock = scope.ServiceProvider.GetRequiredService<Volo.Abp.Timing.IClock>();
            var now = clock.Now;

            // 只处理「当前有效」且已到期的记录
            var expired = (await repository.GetListAsync(cancellationToken: cancellationToken))
                .Where(x => x.Status == AccountValidityStatus.Active
                            && x.ValidUntil.HasValue
                            && x.ValidUntil.Value <= now)
                .ToList();

            if (expired.Count == 0)
            {
                return;
            }

            _logger.LogInformation("[AccountValidity] 发现 {Count} 个到期账号，开始自动熔断权限。", expired.Count);

            foreach (var item in expired)
            {
                try
                {
                    var revoked = await handler.RevokeEditPermissionsAsync(
                        item.UserId,
                        item.TenantId,
                        item.RoleName);

                    item.MarkExpired(revoked.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(revoked), now);
                    await repository.UpdateAsync(item, autoSave: true, cancellationToken: cancellationToken);

                    _logger.LogInformation(
                        "[AccountValidity] 账号 {UserName}({UserId}) 已到期，收回了 {Count} 项编辑/管理权限（角色 {RoleName}）。",
                        item.UserName, item.UserId, revoked.Count, item.RoleName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[AccountValidity] 账号 {UserName}({UserId}) 到期熔断失败，跳过。",
                        item.UserName, item.UserId);
                }
            }
        }
    }
}
