using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Enterprises.Dtos;
using KnowledgeHub.Permissions;
using KnowledgeHub.Schools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;

namespace KnowledgeHub.Schools;

/// <summary>
/// 学校有效期管理（平台侧，Host-only）。
///
/// <para>到期后：写操作被 <c>OrganizationValidityFilter</c> 拒绝，读保持放行（设计 7.3）。</para>
/// </summary>
[IgnoreAntiforgeryToken]
public class SchoolValidityAppService : KnowledgeHubAppService, ISchoolValidityAppService
{
    private readonly IRepository<School, Guid> _schoolRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IClock _clock;

    public SchoolValidityAppService(
        IRepository<School, Guid> schoolRepository,
        ITenantRepository tenantRepository,
        IAsyncQueryableExecuter asyncExecuter,
        IClock clock)
    {
        _schoolRepository = schoolRepository;
        _tenantRepository = tenantRepository;
        _asyncExecuter = asyncExecuter;
        _clock = clock;
    }

    [Authorize(KnowledgeHubPermissions.Schools.ManageValidity)]
    public async Task<List<SchoolValidityDto>> GetListAsync()
    {
        var tenants = await _tenantRepository.GetListAsync();
        var schools = await _schoolRepository.GetListAsync();
        var now = _clock.Now;

        return tenants
            .Select(t =>
            {
                var school = schools.FirstOrDefault(s => s.TenantId == t.Id);
                var isValid = school?.IsValidAt(now) ?? true;

                return new SchoolValidityDto
                {
                    TenantId = t.Id,
                    TenantName = t.Name,
                    SchoolName = school?.Name,
                    ValidFrom = school?.ValidFrom,
                    ValidTo = school?.ValidTo,
                    IsValid = isValid,
                    ReadOnlyAfterExpiry = school?.ReadOnlyAfterExpiry ?? true,
                    StatusName = school?.ValidTo == null
                        ? "长期有效"
                        : isValid ? "有效期内" : "已过期"
                };
            })
            .OrderBy(x => x.SchoolName ?? x.TenantName)
            .ToList();
    }

    [Authorize(KnowledgeHubPermissions.Schools.ManageValidity)]
    public async Task<SchoolValidityDto> SetValidityAsync(SetSchoolValidityDto input)
    {
        if (input.TenantId == Guid.Empty)
        {
            throw new UserFriendlyException("请选择学校。");
        }

        var tenant = await _tenantRepository.FindAsync(input.TenantId)
                     ?? throw new UserFriendlyException("租户不存在。");

        var school = await _schoolRepository.FirstOrDefaultAsync(x => x.TenantId == input.TenantId);
        if (school == null)
        {
            school = new School(GuidGenerator.Create(), input.TenantId, tenant.Name);
            await _schoolRepository.InsertAsync(school, autoSave: true);
        }

        school.SetValidity(input.ValidFrom, input.ValidTo, input.ReadOnlyAfterExpiry);
        await _schoolRepository.UpdateAsync(school, autoSave: true);

        var now = _clock.Now;
        return new SchoolValidityDto
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            SchoolName = school.Name,
            ValidFrom = school.ValidFrom,
            ValidTo = school.ValidTo,
            IsValid = school.IsValidAt(now),
            ReadOnlyAfterExpiry = school.ReadOnlyAfterExpiry,
            StatusName = school.ValidTo == null ? "长期有效" : school.IsValidAt(now) ? "有效期内" : "已过期"
        };
    }
}

public interface ISchoolValidityAppService
{
    Task<List<SchoolValidityDto>> GetListAsync();
    Task<SchoolValidityDto> SetValidityAsync(SetSchoolValidityDto input);
}
