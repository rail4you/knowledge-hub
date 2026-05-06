using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.News.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.News;

public class NewsCategoryAppService : KnowledgeHubAppService, INewsCategoryAppService
{
    private readonly IRepository<NewsCategory, Guid> _categoryRepository;

    public NewsCategoryAppService(IRepository<NewsCategory, Guid> categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<NewsCategoryDto> GetAsync(Guid id)
    {
        var entity = await _categoryRepository.GetAsync(id);
        return MapToDto(entity);
    }

    public async Task<PagedResultDto<NewsCategoryDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var query = await _categoryRepository.GetQueryableAsync();
        var totalCount = query.Count();
        var items = query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<NewsCategoryDto>(totalCount, items.Select(MapToDto).ToList());
    }

    public async Task<List<NewsCategoryDto>> GetActiveListAsync()
    {
        var items = await _categoryRepository.GetListAsync(x => x.IsActive);
        return items.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(MapToDto).ToList();
    }

    public async Task<List<NewsCategoryDto>> GetTreeAsync()
    {
        var items = await _categoryRepository.GetListAsync(x => x.IsActive);
        var dtoMap = items
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(MapToDto)
            .ToDictionary(x => x.Id);

        var roots = new List<NewsCategoryDto>();
        foreach (var dto in dtoMap.Values)
        {
            if (dto.ParentId.HasValue && dtoMap.TryGetValue(dto.ParentId.Value, out var parent))
            {
                parent.Children.Add(dto);
            }
            else
            {
                roots.Add(dto);
            }
        }

        return roots;
    }

    [Authorize(KnowledgeHubPermissions.News.Create)]
    public async Task<NewsCategoryDto> CreateAsync(CreateUpdateNewsCategoryDto input)
    {
        await EnsureCodeUniqueAsync(input.Code, null);

        var entity = new NewsCategory(GuidGenerator.Create(), input.Name.Trim(), input.Code.Trim())
        {
            ParentId = input.ParentId,
            SortOrder = input.SortOrder,
            IsActive = input.IsActive,
            TenantId = CurrentTenant.Id
        };

        await _categoryRepository.InsertAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(KnowledgeHubPermissions.News.Edit)]
    public async Task<NewsCategoryDto> UpdateAsync(Guid id, CreateUpdateNewsCategoryDto input)
    {
        var entity = await _categoryRepository.GetAsync(id);
        await EnsureCodeUniqueAsync(input.Code, id);

        entity.ParentId = input.ParentId;
        entity.Name = input.Name.Trim();
        entity.Code = input.Code.Trim();
        entity.SortOrder = input.SortOrder;
        entity.IsActive = input.IsActive;

        await _categoryRepository.UpdateAsync(entity);
        return MapToDto(entity);
    }

    [Authorize(KnowledgeHubPermissions.News.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var hasChildren = await _categoryRepository.AnyAsync(x => x.ParentId == id);
        if (hasChildren)
        {
            throw new UserFriendlyException("请先删除子分类。");
        }

        await _categoryRepository.DeleteAsync(id);
    }

    private async Task EnsureCodeUniqueAsync(string code, Guid? currentId)
    {
        var exists = await _categoryRepository.AnyAsync(x => x.Code == code && x.Id != currentId);
        if (exists)
        {
            throw new UserFriendlyException("资讯分类编码已存在。");
        }
    }

    private static NewsCategoryDto MapToDto(NewsCategory entity)
    {
        return new NewsCategoryDto
        {
            Id = entity.Id,
            ParentId = entity.ParentId,
            Name = entity.Name,
            Code = entity.Code,
            SortOrder = entity.SortOrder,
            IsActive = entity.IsActive,
            CreationTime = entity.CreationTime,
            CreatorId = entity.CreatorId,
            LastModificationTime = entity.LastModificationTime,
            LastModifierId = entity.LastModifierId,
            IsDeleted = entity.IsDeleted,
            DeleterId = entity.DeleterId,
            DeletionTime = entity.DeletionTime
        };
    }
}
