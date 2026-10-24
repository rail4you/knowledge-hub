using System;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.MultiTenancy;
using KnowledgeHub.Schools;
using KnowledgeHub.TenantInfos;
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
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly ILogger<SchoolProvisioningEnsureHostedService> _logger;

    public SchoolProvisioningEnsureHostedService(
        ITenantRepository tenantRepository,
        ISchoolRepository schoolRepository,
        ITenantInfoRepository tenantInfoRepository,
        ILogger<SchoolProvisioningEnsureHostedService> logger)
    {
        _tenantRepository = tenantRepository;
        _schoolRepository = schoolRepository;
        _tenantInfoRepository = tenantInfoRepository;
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
            var renamed = 0;

            foreach (var tenant in tenants)
            {
                var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenant.Id, cancellationToken);
                var existing = await _schoolRepository.FindByTenantIdAsync(tenant.Id, cancellationToken);

                if (existing == null)
                {
                    // 机构名优先用专业库的中文展示名，没有才退回租户名（slug）
                    // ——需求「机构名称要用中文」，避免建出来就是 qidi / guozhou 这类英文名。
                    var displayName = tenantInfo?.Name ?? tenant.Name;

                    var school = new School(Guid.NewGuid(), tenant.Id, displayName);
                    await _schoolRepository.InsertAsync(school, autoSave: true, cancellationToken);
                    created++;
                    continue;
                }

                // 存量自愈：档案名还停留在租户 slug（qidi / guozhou），而专业库已配置中文名时，
                // 用中文名覆盖 —— 让「机构名称要用中文」在存量租户上也生效。
                if (!string.IsNullOrWhiteSpace(tenantInfo?.Name)
                    && string.Equals(existing.Name, tenant.Name, StringComparison.Ordinal)
                    && !string.Equals(existing.Name, tenantInfo.Name, StringComparison.Ordinal))
                {
                    existing.SetName(tenantInfo.Name);
                    await _schoolRepository.UpdateAsync(existing, autoSave: true, cancellationToken);
                    renamed++;
                }
            }

            if (renamed > 0)
            {
                _logger.LogInformation(
                    "[SchoolProvisioningEnsure] 已把 {Count} 个学校档案的英文租户名改为中文展示名。", renamed);
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
