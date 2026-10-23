using System;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.MultiTenancy;
using KnowledgeHub.Schools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Schools;

/// <summary>
/// 启动期学校档案回填 HostedService。
///
/// <para>
/// 问题背景：<c>SchoolProvisioningHandler</c> 只在<b>新建</b>租户时触发，
/// 升级前已存在的租户不会自动获得学校档案 → 首页拿不到校徽 / 校名，学校简介页 404。
/// </para>
///
/// <para>
/// 解决：API 每次启动时遍历所有租户，为缺失学校档案的租户补建一份（名称取租户名）。
/// 幂等：已存在档案的租户直接跳过。
/// </para>
///
/// <para>
/// 失败容忍：单个租户失败不阻塞其它租户，也不让 API 启动失败。
/// </para>
/// </summary>
public class SchoolProvisioningEnsureHostedService : IHostedService, ITransientDependency
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ISchoolRepository _schoolRepository;
    private readonly ILogger<SchoolProvisioningEnsureHostedService> _logger;

    public SchoolProvisioningEnsureHostedService(
        ITenantRepository tenantRepository,
        ISchoolRepository schoolRepository,
        ILogger<SchoolProvisioningEnsureHostedService> logger)
    {
        _tenantRepository = tenantRepository;
        _schoolRepository = schoolRepository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!MultiTenancyConsts.IsEnabled)
        {
            return;
        }

        try
        {
            var tenants = await _tenantRepository.GetListAsync(cancellationToken: cancellationToken);
            var created = 0;

            foreach (var tenant in tenants)
            {
                if (await _schoolRepository.ExistsForTenantAsync(tenant.Id, cancellationToken))
                {
                    continue;
                }

                var school = new School(Guid.NewGuid(), tenant.Id, tenant.Name);
                await _schoolRepository.InsertAsync(school, autoSave: true, cancellationToken);
                created++;
            }

            if (created > 0)
            {
                _logger.LogInformation(
                    "[SchoolProvisioningEnsure] 已为 {Count} 个存量租户补建学校档案。", created);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SchoolProvisioningEnsure] 启动期学校档案回填失败，应用继续启动。");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
