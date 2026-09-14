using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Application.AI;

[Authorize(KnowledgeHubPermissions.AI.Default)]
public class TeachingSceneAppService : KnowledgeHubAppService, ITeachingSceneAppService
{
    private readonly IRepository<TeachingScene, Guid> _repository;

    public TeachingSceneAppService(IRepository<TeachingScene, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<List<TeachingSceneDto>> GetListAsync(TeachingSceneCategory? category = null)
    {
        var tenantId = CurrentTenant.Id;
        var query = await _repository.GetQueryableAsync();

        // 系统内置模板对所有租户可见；自定义场景仅本租户可见。
        query = query.Where(x => x.IsSystem || x.TenantId == tenantId);
        if (category.HasValue)
        {
            query = query.Where(x => x.Category == category.Value);
        }

        var items = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.IsSystem ? 0 : 1)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.CreationTime));

        return items.Select(Map).ToList();
    }

    public async Task<TeachingSceneDto> CreateAsync(CreateUpdateTeachingSceneDto input)
    {
        var entity = new TeachingScene(
            GuidGenerator.Create(),
            input.Name.Trim(),
            input.Prompt.Trim(),
            input.Category)
        {
            TenantId = CurrentTenant.Id,
            SortOrder = input.SortOrder,
            IsSystem = false,
        };

        await _repository.InsertAsync(entity, autoSave: true);
        return Map(entity);
    }

    public async Task<TeachingSceneDto> UpdateAsync(Guid id, CreateUpdateTeachingSceneDto input)
    {
        var entity = await GetEditableAsync(id);
        entity.Name = input.Name.Trim();
        entity.Prompt = input.Prompt.Trim();
        entity.Category = input.Category;
        entity.SortOrder = input.SortOrder;

        await _repository.UpdateAsync(entity, autoSave: true);
        return Map(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetEditableAsync(id);
        await _repository.DeleteAsync(entity);
    }

    public async Task<TeachingSceneDto> CopyToMineAsync(Guid id)
    {
        var source = await _repository.GetAsync(id);
        if (!source.IsSystem && source.TenantId != CurrentTenant.Id)
        {
            throw new AbpAuthorizationException("无权复制该场景");
        }

        var copy = new TeachingScene(
            GuidGenerator.Create(),
            $"{source.Name}（副本）",
            source.Prompt,
            source.Category)
        {
            TenantId = CurrentTenant.Id,
            SortOrder = source.SortOrder,
            IsSystem = false,
        };

        await _repository.InsertAsync(copy, autoSave: true);
        return Map(copy);
    }

    private async Task<TeachingScene> GetEditableAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsSystem)
        {
            throw new UserFriendlyException("系统内置模板不可修改，请先“复制为我的场景”再编辑");
        }
        if (entity.TenantId != CurrentTenant.Id)
        {
            throw new AbpAuthorizationException("无权修改该场景");
        }
        return entity;
    }

    private static TeachingSceneDto Map(TeachingScene entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Prompt = entity.Prompt,
        Category = entity.Category,
        SortOrder = entity.SortOrder,
        IsSystem = entity.IsSystem,
    };
}
