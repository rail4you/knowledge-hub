using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Schools;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace KnowledgeHub.Organizations;

/// <summary>
/// 单位有效期巡检（V2.0 P5 / V2-506）。
///
/// <para>
/// 每天跑一次：扫描学校有效期，输出「已过期 / 7 天内到期」清单日志，
/// 并作为「后台任务执行前复核」的样例入口 —— 真实业务任务在动手前应调用
/// <see cref="IOrganizationValidityChecker.EnsureWritableForTenantAsync"/>。
/// </para>
///
/// <para>
/// 这是<b>可观测</b>而非阻断：到期后的写操作由 HTTP 过滤器拦截，
/// 该任务只负责让运维提前看到风险（后续可接站内信/邮件）。
/// </para>
/// </summary>
public class OrganizationValidityMaintenanceJob : ITransientDependency
{
    private readonly IRepository<School, Guid> _schoolRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IClock _clock;
    private readonly ILogger<OrganizationValidityMaintenanceJob> _logger;

    public OrganizationValidityMaintenanceJob(
        IRepository<School, Guid> schoolRepository,
        ITenantRepository tenantRepository,
        IClock clock,
        ILogger<OrganizationValidityMaintenanceJob> logger)
    {
        _schoolRepository = schoolRepository;
        _tenantRepository = tenantRepository;
        _clock = clock;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var now = _clock.Now;
        var schools = await _schoolRepository.GetListAsync();
        var tenants = await _tenantRepository.GetListAsync();

        var nameMap = tenants.ToDictionary(x => x.Id, x => x.Name);
        var expiring = schools
            .Where(x => x.ValidTo.HasValue)
            .Select(x => new
            {
                x.Name,
                x.TenantId,
                x.ValidTo,
                IsValid = x.IsValidAt(now),
                DaysLeft = (x.ValidTo!.Value.Date.AddDays(1) - now.Date).Days
            })
            .ToList();

        var expired = expiring.Where(x => !x.IsValid).ToList();
        var soon = expiring.Where(x => x.IsValid && x.DaysLeft <= 7).ToList();

        if (expired.Count > 0)
        {
            _logger.LogWarning(
                "[OrgValidity] 已过期学校 {Count} 所：{Names}（这些学校的写操作已被拒绝）",
                expired.Count,
                string.Join("、", expired.Select(x => $"{x.Name}({(nameMap.ContainsKey(x.TenantId) ? nameMap[x.TenantId] : x.TenantId.ToString())})")));
        }

        if (soon.Count > 0)
        {
            _logger.LogWarning(
                "[OrgValidity] 7 天内到期学校 {Count} 所：{Names}",
                soon.Count,
                string.Join("、", soon.Select(x => $"{x.Name}(剩余 {x.DaysLeft} 天)")));
        }

        if (expired.Count == 0 && soon.Count == 0)
        {
            _logger.LogInformation("[OrgValidity] 巡检完成：共 {Total} 所，全部在有效期内或长期有效。", schools.Count);
        }
    }
}
