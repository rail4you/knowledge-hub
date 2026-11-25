using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.CourseIdeology.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 思政元素体系（按租户可配置）。默认种子 6 个通用元素，学校可自行增删改。
/// </summary>
[Authorize(KnowledgeHubPermissions.CourseIdeology.Default)]
public class IdeologyElementAppService : KnowledgeHubAppService, IIdeologyElementAppService
{
    private readonly IRepository<IdeologyElement, Guid> _elementRepository;
    private readonly IRepository<IdeologyCaseElement, Guid> _caseElementRepository;

    public IdeologyElementAppService(
        IRepository<IdeologyElement, Guid> elementRepository,
        IRepository<IdeologyCaseElement, Guid> caseElementRepository)
    {
        _elementRepository = elementRepository;
        _caseElementRepository = caseElementRepository;
    }

    public async Task<List<IdeologyElementDto>> GetListAsync(bool onlyEnabled = false)
    {
        var query = await _elementRepository.GetQueryableAsync();
        if (onlyEnabled)
        {
            query = query.Where(x => x.IsEnabled);
        }

        var elements = await AsyncExecuter.ToListAsync(
            query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name));

        // 关联案例数：一次分组统计，避免 N+1。
        var links = await AsyncExecuter.ToListAsync((await _caseElementRepository.GetQueryableAsync()));
        var counts = links.GroupBy(x => x.ElementId).ToDictionary(g => g.Key, g => g.Count());

        return elements.Select(x => new IdeologyElementDto
        {
            Id = x.Id,
            Name = x.Name,
            Category = x.Category,
            Description = x.Description,
            SortOrder = x.SortOrder,
            IsEnabled = x.IsEnabled,
            CaseCount = counts.GetValueOrDefault(x.Id),
        }).ToList();
    }

    [Authorize(KnowledgeHubPermissions.CourseIdeology.ManageElement)]
    public async Task<IdeologyElementDto> CreateAsync(CreateUpdateIdeologyElementDto input)
    {
        await EnsureNameAvailableAsync(input.Name, null);

        var element = new IdeologyElement(GuidGenerator.Create(), input.Name.Trim(), CurrentTenant.Id)
        {
            Category = TrimToNull(input.Category),
            Description = TrimToNull(input.Description),
            SortOrder = input.SortOrder,
            IsEnabled = input.IsEnabled,
        };

        await _elementRepository.InsertAsync(element, autoSave: true);

        return new IdeologyElementDto
        {
            Id = element.Id,
            Name = element.Name,
            Category = element.Category,
            Description = element.Description,
            SortOrder = element.SortOrder,
            IsEnabled = element.IsEnabled,
        };
    }

    [Authorize(KnowledgeHubPermissions.CourseIdeology.ManageElement)]
    public async Task<IdeologyElementDto> UpdateAsync(Guid id, CreateUpdateIdeologyElementDto input)
    {
        var element = await _elementRepository.GetAsync(id);
        await EnsureNameAvailableAsync(input.Name, id);

        element.Name = input.Name.Trim();
        element.Category = TrimToNull(input.Category);
        element.Description = TrimToNull(input.Description);
        element.SortOrder = input.SortOrder;
        element.IsEnabled = input.IsEnabled;

        await _elementRepository.UpdateAsync(element, autoSave: true);

        return new IdeologyElementDto
        {
            Id = element.Id,
            Name = element.Name,
            Category = element.Category,
            Description = element.Description,
            SortOrder = element.SortOrder,
            IsEnabled = element.IsEnabled,
        };
    }

    [Authorize(KnowledgeHubPermissions.CourseIdeology.ManageElement)]
    public async Task DeleteAsync(Guid id)
    {
        // 先清关联，避免留下指向已删元素的死链（案例本体保留）。
        var links = await _caseElementRepository.GetListAsync(x => x.ElementId == id);
        if (links.Count > 0)
        {
            await _caseElementRepository.DeleteManyAsync(links, autoSave: true);
        }

        await _elementRepository.DeleteAsync(id);
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? excludeId)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new UserFriendlyException("思政元素名称不能为空。");
        }

        if (trimmed.Length > IdeologyElement.MaxNameLength)
        {
            throw new UserFriendlyException($"思政元素名称不能超过 {IdeologyElement.MaxNameLength} 个字符。");
        }

        var query = await _elementRepository.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(query.Where(x =>
            x.Name == trimmed && (excludeId == null || x.Id != excludeId.Value)));
        if (exists)
        {
            throw new UserFriendlyException($"思政元素「{trimmed}」已存在。");
        }
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
