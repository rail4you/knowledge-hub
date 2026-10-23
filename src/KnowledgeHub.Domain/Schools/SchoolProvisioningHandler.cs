using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Schools;

/// <summary>
/// 租户 → 学校档案自动开卡。
///
/// <para>
/// 需求「租户对应的学校是独立实体」：每新建一个租户（院校），自动建立一份学校档案，
/// 初始名称取租户名，后续由学校管理员在【学校设置】页维护徽标 / 简介等信息。
/// </para>
///
/// <para>
/// 通过订阅 ABP 的 <see cref="EntityCreatedEventData{TEntity}"/> 实现，
/// 覆盖所有创建租户的入口（管理端新建、ABP TenantAppService 等）。
/// </para>
/// </summary>
public class SchoolProvisioningHandler : ILocalEventHandler<EntityCreatedEventData<Tenant>>, ITransientDependency
{
    private readonly ISchoolRepository _schoolRepository;
    private readonly IGuidGenerator _guidGenerator;

    public SchoolProvisioningHandler(ISchoolRepository schoolRepository, IGuidGenerator guidGenerator)
    {
        _schoolRepository = schoolRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task HandleEventAsync(EntityCreatedEventData<Tenant> eventData)
    {
        var tenant = eventData.Entity;

        // 已有档案则不重复创建
        if (await _schoolRepository.ExistsForTenantAsync(tenant.Id))
        {
            return;
        }

        var school = new School(_guidGenerator.Create(), tenant.Id, tenant.Name);
        await _schoolRepository.InsertAsync(school, autoSave: true);
    }
}
