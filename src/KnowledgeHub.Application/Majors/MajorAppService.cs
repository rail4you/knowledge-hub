using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Majors.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Majors;

[IgnoreAntiforgeryToken]
public class MajorAppService : KnowledgeHubAppService, IMajorAppService
{
    private readonly IMajorRepository _majorRepository;

    public MajorAppService(IMajorRepository majorRepository)
    {
        _majorRepository = majorRepository;
    }

    public async Task<MajorDto> GetAsync(Guid id)
    {
        var entity = await _majorRepository.GetAsync(id);
        return MapToDto(entity);
    }

    [AllowAnonymous]
    public async Task<PagedResultDto<MajorDto>> GetListAsync(PagedMajorRequestDto input)
    {
        var query = await _majorRepository.GetQueryableAsync();
        query = query
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x =>
                x.Name.Contains(input.Filter!) ||
                (x.Code != null && x.Code.Contains(input.Filter!)));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.Name)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<MajorDto>(
            totalCount,
            items.Select(MapToDto).ToList());
    }

    // Lookup 列表对所有登录用户开放：课程/资源/用户表单创建/编辑时都要拉这个下拉。
    [AllowAnonymous]
    public async Task<List<MajorLookupDto>> GetLookupListAsync()
    {
        var items = await _majorRepository.GetLookupListAsync();
        return items
            .Select(x => new MajorLookupDto
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code
            })
            .ToList();
    }

    [Authorize]
    public async Task<MajorDto> CreateAsync(CreateUpdateMajorDto input)
    {
        await EnsureUniqueAsync(input.Name, input.Code, null);

        var entity = new Major(
            GuidGenerator.Create(),
            input.Name.Trim(),
            input.Code?.Trim(),
            input.Description,
            input.TrainingObjectives)
        {
            TenantId = CurrentTenant.Id
        };

        await _majorRepository.InsertAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize]
    public async Task<MajorDto> UpdateAsync(Guid id, CreateUpdateMajorDto input)
    {
        var entity = await _majorRepository.GetAsync(id);
        await EnsureUniqueAsync(input.Name, input.Code, id);

        entity.SetName(input.Name.Trim());
        entity.SetCode(input.Code?.Trim());
        entity.SetDescription(input.Description);
        entity.SetTrainingObjectives(input.TrainingObjectives);

        await _majorRepository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize]
    public async Task DeleteAsync(Guid id)
    {
        await _majorRepository.DeleteAsync(id);
    }

    private async Task EnsureUniqueAsync(string name, string? code, Guid? excludeId)
    {
        var query = await _majorRepository.GetQueryableAsync();
        if (await query.AnyAsync(x => x.Id != excludeId && x.Name == name.Trim()))
        {
            throw new UserFriendlyException("专业名称已存在。");
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            var trimmed = code.Trim();
            if (await query.AnyAsync(x => x.Id != excludeId && x.Code == trimmed))
            {
                throw new UserFriendlyException("专业代号已存在。");
            }
        }
    }

    private static MajorDto MapToDto(Major entity)
    {
        return new MajorDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            TrainingObjectives = entity.TrainingObjectives,
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
