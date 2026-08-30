using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources.Enums;
using KnowledgeHub.TenantInfos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Resources;

/// <summary>
/// 资源共享 AppService：把源租户的资源共享给一个或多个目标租户。
/// - 一个资源可以共享给多个目标租户（多条 ResourceShare）。
/// - 目标租户通过 GetSharedToMeAsync 看到这些资源。
/// - 共享记录按源租户隔离（TenantId = SourceTenantId），查询时禁用租户过滤器按 TargetTenantId 匹配。
/// </summary>
[Authorize(KnowledgeHubPermissions.Resources.Default)]
public class ResourceShareAppService : ApplicationService, IResourceShareAppService
{
    private readonly IResourceShareRepository _shareRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IDataFilter _dataFilter;

    public ResourceShareAppService(
        IResourceShareRepository shareRepository,
        IRepository<Resource, Guid> resourceRepository,
        ITenantRepository tenantRepository,
        ITenantInfoRepository tenantInfoRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IDataFilter dataFilter)
    {
        _shareRepository = shareRepository;
        _resourceRepository = resourceRepository;
        _tenantRepository = tenantRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _userRepository = userRepository;
        _dataFilter = dataFilter;
    }

    /// <summary>
    /// 把资源共享给一个或多个目标租户。
    /// 限制：只能共享已通过审核的资源（SchoolApproved 或 LeagueApproved）。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.Resources.Share)]
    public virtual async Task<List<ResourceShareDto>> ShareAsync(CreateResourceShareDto input)
    {
        if (input.TargetTenantIds == null || input.TargetTenantIds.Count == 0)
        {
            throw new UserFriendlyException("请至少选择一个目标租户");
        }

        var resource = await _resourceRepository.GetAsync(input.ResourceId);
        if (resource.TenantId != CurrentTenant.Id)
        {
            throw new UserFriendlyException("只能共享本租户的资源");
        }
        if (resource.Status != ResourceStatus.SchoolApproved &&
            resource.Status != ResourceStatus.LeagueApproved)
        {
            throw new UserFriendlyException("只能共享已通过审核的资源");
        }

        // 过滤掉自己、已共享、已失效的租户
        var sourceTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前租户未知");
        var validTargetIds = input.TargetTenantIds
            .Where(t => t != Guid.Empty && t != sourceTenantId)
            .Distinct()
            .ToList();
        if (validTargetIds.Count == 0)
        {
            throw new UserFriendlyException("目标租户不能为空或本租户");
        }

        // 校验所有目标租户存在
        var tenants = await _tenantRepository.GetListAsync();
        var existingTenantIds = tenants.Select(t => t.Id).ToHashSet();
        var invalid = validTargetIds.Where(t => !existingTenantIds.Contains(t)).ToList();
        if (invalid.Count > 0)
        {
            throw new UserFriendlyException($"目标租户无效：{string.Join(", ", invalid)}");
        }

        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录");
        var results = new List<ResourceShareDto>();

        foreach (var targetTenantId in validTargetIds)
        {
            if (await _shareRepository.ExistsAsync(input.ResourceId, targetTenantId))
            {
                continue;
            }
            var share = new ResourceShare(
                GuidGenerator.Create(),
                input.ResourceId,
                sourceTenantId,
                targetTenantId,
                userId,
                input.Note);
            await _shareRepository.InsertAsync(share);
            results.Add(new ResourceShareDto
            {
                Id = share.Id,
                ResourceId = share.ResourceId,
                SourceTenantId = share.SourceTenantId,
                TargetTenantId = share.TargetTenantId,
                SharedByUserId = share.SharedByUserId,
                SharedAt = share.SharedAt,
                Note = share.Note
            });
        }

        // 补全租户名/用户名
        await FillShareDtoMetaAsync(results, sourceTenantId);
        return results;
    }

    [Authorize(KnowledgeHubPermissions.Resources.ManageShare)]
    public virtual async Task UnshareAsync(Guid resourceId, Guid targetTenantId)
    {
        var sourceTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前租户未知");
        var shares = await _shareRepository.GetByResourceAsync(resourceId);
        var share = shares.FirstOrDefault(s => s.TargetTenantId == targetTenantId);
        if (share == null)
        {
            throw new UserFriendlyException("共享记录不存在");
        }
        await _shareRepository.DeleteAsync(share);
    }

    public virtual async Task<List<ResourceShareDto>> GetSharesAsync(Guid resourceId)
    {
        var sourceTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前租户未知");
        // 验证资源属于本租户
        var resource = await _resourceRepository.GetAsync(resourceId);
        if (resource.TenantId != sourceTenantId)
        {
            throw new UserFriendlyException("只能查看本租户资源的共享情况");
        }

        var shares = await _shareRepository.GetByResourceAsync(resourceId);
        var dtos = shares.Select(s => new ResourceShareDto
        {
            Id = s.Id,
            ResourceId = s.ResourceId,
            ResourceName = resource.Name,
            SourceTenantId = s.SourceTenantId,
            TargetTenantId = s.TargetTenantId,
            SharedByUserId = s.SharedByUserId,
            SharedAt = s.SharedAt,
            Note = s.Note
        }).ToList();
        await FillShareDtoMetaAsync(dtos, sourceTenantId);
        return dtos;
    }

    /// <summary>
    /// 共享给我的资源列表（"共享给我的" Tab 数据源）。
    /// 跨租户查询：禁用租户过滤器，按 TargetTenantId 匹配。
    /// </summary>
    [AllowAnonymous]
    public virtual async Task<PagedResultDto<SharedResourceDto>> GetSharedToMeAsync(SharedResourceListQueryDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前租户未知");

        // 跨租户查询 ResourceShare
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var shareQuery = await _shareRepository.GetQueryableAsync();
            shareQuery = shareQuery.Where(s => s.TargetTenantId == currentTenantId);

            var shares = await AsyncExecuter.ToListAsync(shareQuery);
            if (shares.Count == 0)
            {
                return new PagedResultDto<SharedResourceDto>(0, new List<SharedResourceDto>());
            }

            var resourceIds = shares.Select(s => s.ResourceId).Distinct().ToList();
            var resourceQuery = await _resourceRepository.GetQueryableAsync();
            resourceQuery = resourceQuery.Where(r => resourceIds.Contains(r.Id) &&
                                                    (r.Status == ResourceStatus.SchoolApproved ||
                                                     r.Status == ResourceStatus.LeagueApproved));

            // 应用筛选
            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                var f = input.Filter.ToLower();
                resourceQuery = resourceQuery.Where(r =>
                    (r.Name != null && r.Name.ToLower().Contains(f)) ||
                    (r.Description != null && r.Description.ToLower().Contains(f)));
            }
            if (input.ResourceType.HasValue)
            {
                resourceQuery = resourceQuery.Where(r => r.ResourceType == input.ResourceType.Value);
            }
            if (input.CategoryId.HasValue)
            {
                resourceQuery = resourceQuery.Where(r => r.CategoryId == input.CategoryId.Value);
            }
            if (input.MajorId.HasValue)
            {
                resourceQuery = resourceQuery.Where(r => r.MajorId == input.MajorId.Value);
            }

            var totalCount = await AsyncExecuter.CountAsync(resourceQuery);
            var pagedResources = await AsyncExecuter.ToListAsync(
                resourceQuery
                    .OrderByDescending(r => r.CreationTime)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount));

            // 按 ResourceId 把 share 信息合并回去（取最新的一条共享记录）
            var shareByResource = shares
                .GroupBy(s => s.ResourceId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SharedAt).First());

            var dtos = pagedResources.Select(r => new SharedResourceDto
            {
                Id = r.Id,
                ShareId = shareByResource.TryGetValue(r.Id, out var s) ? s.Id : Guid.Empty,
                Name = r.Name,
                Description = r.Description,
                ResourceType = r.ResourceType,
                CategoryId = r.CategoryId,
                MajorId = r.MajorId,
                FileExtension = r.FileExtension,
                Status = r.Status,
                Summary = r.Summary,
                CollectionCount = r.CollectionCount,
                DownloadCount = r.DownloadCount,
                ViewCount = r.ViewCount,
                SourceTenantId = r.TenantId ?? Guid.Empty,
                SharedAt = shareByResource.TryGetValue(r.Id, out var ss) ? ss.SharedAt : default,
                SharedByUserId = shareByResource.TryGetValue(r.Id, out var sss) ? sss.SharedByUserId : Guid.Empty
            }).ToList();

            await FillSharedResourceDtoMetaAsync(dtos);
            return new PagedResultDto<SharedResourceDto>(totalCount, dtos);
        }
    }

    /// <summary>
    /// 我共享出去的资源列表（"我共享的" Tab 数据源）。
    /// </summary>
    public virtual async Task<PagedResultDto<SharedResourceDto>> GetSharedByMeAsync(SharedResourceListQueryDto input)
    {
        var sourceTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前租户未知");

        // 查询本租户所有共享出去的记录
        var shareQuery = await _shareRepository.GetQueryableAsync();
        var shares = await AsyncExecuter.ToListAsync(shareQuery);
        if (shares.Count == 0)
        {
            return new PagedResultDto<SharedResourceDto>(0, new List<SharedResourceDto>());
        }

        var resourceIds = shares.Select(s => s.ResourceId).Distinct().ToList();
        var resourceQuery = await _resourceRepository.GetQueryableAsync();
        resourceQuery = resourceQuery.Where(r => resourceIds.Contains(r.Id));

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.ToLower();
            resourceQuery = resourceQuery.Where(r =>
                (r.Name != null && r.Name.ToLower().Contains(f)) ||
                (r.Description != null && r.Description.ToLower().Contains(f)));
        }
        if (input.ResourceType.HasValue)
        {
            resourceQuery = resourceQuery.Where(r => r.ResourceType == input.ResourceType.Value);
        }
        if (input.CategoryId.HasValue)
        {
            resourceQuery = resourceQuery.Where(r => r.CategoryId == input.CategoryId.Value);
        }
        if (input.MajorId.HasValue)
        {
            resourceQuery = resourceQuery.Where(r => r.MajorId == input.MajorId.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(resourceQuery);
        var pagedResources = await AsyncExecuter.ToListAsync(
            resourceQuery
                .OrderByDescending(r => r.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        // 收集所有目标租户（用于统计/展示）
        var allTargets = shares
            .GroupBy(s => s.ResourceId)
            .ToDictionary(g => g.Key, g => g.Select(s => s.TargetTenantId).ToList());

        var dtos = pagedResources.Select(r => new SharedResourceDto
        {
            Id = r.Id,
            ShareId = allTargets.TryGetValue(r.Id, out var ts) ? Guid.Empty : Guid.Empty,
            Name = r.Name,
            Description = r.Description,
            ResourceType = r.ResourceType,
            CategoryId = r.CategoryId,
            MajorId = r.MajorId,
            FileExtension = r.FileExtension,
            Status = r.Status,
            Summary = r.Summary,
            CollectionCount = r.CollectionCount,
            DownloadCount = r.DownloadCount,
            ViewCount = r.ViewCount,
            SourceTenantId = r.TenantId ?? sourceTenantId,
            SharedAt = shares.Where(s => s.ResourceId == r.Id).Max(s => s.SharedAt)
        }).ToList();

        await FillSharedResourceDtoMetaAsync(dtos);
        return new PagedResultDto<SharedResourceDto>(totalCount, dtos);
    }

    private async Task FillShareDtoMetaAsync(List<ResourceShareDto> dtos, Guid sourceTenantId)
    {
        if (dtos.Count == 0) return;
        var tenantIds = dtos.Select(d => d.TargetTenantId).Append(sourceTenantId).Distinct().ToList();
        var tenantInfos = new Dictionary<Guid, string>();
        foreach (var tid in tenantIds)
        {
            var info = await _tenantInfoRepository.FindByTenantIdAsync(tid);
            tenantInfos[tid] = info?.Name ?? (await _tenantRepository.FindAsync(tid))?.Name ?? tid.ToString();
        }
        var userIds = dtos.Select(d => d.SharedByUserId).Distinct().ToList();
        var users = await AsyncExecuter.ToListAsync(
            (await _userRepository.GetQueryableAsync()).Where(u => userIds.Contains(u.Id)));
        var userMap = users.ToDictionary(u => u.Id, u => ResolveUserDisplayName(u));

        foreach (var dto in dtos)
        {
            if (tenantInfos.TryGetValue(dto.SourceTenantId, out var sn)) dto.SourceTenantName = sn;
            if (tenantInfos.TryGetValue(dto.TargetTenantId, out var tn)) dto.TargetTenantName = tn;
            if (userMap.TryGetValue(dto.SharedByUserId, out var un)) dto.SharedByUserName = un;
        }
    }

    private async Task FillSharedResourceDtoMetaAsync(List<SharedResourceDto> dtos)
    {
        if (dtos.Count == 0) return;
        var tenantIds = dtos.Select(d => d.SourceTenantId).Where(t => t != Guid.Empty).Distinct().ToList();
        var tenantInfos = new Dictionary<Guid, string>();
        foreach (var tid in tenantIds)
        {
            var info = await _tenantInfoRepository.FindByTenantIdAsync(tid);
            tenantInfos[tid] = info?.Name ?? (await _tenantRepository.FindAsync(tid))?.Name ?? tid.ToString();
        }
        var userIds = dtos.Select(d => d.SharedByUserId).Where(u => u != Guid.Empty).Distinct().ToList();
        var users = new Dictionary<Guid, string>();
        if (userIds.Count > 0)
        {
            var userList = await AsyncExecuter.ToListAsync(
                (await _userRepository.GetQueryableAsync()).Where(u => userIds.Contains(u.Id)));
            foreach (var u in userList) users[u.Id] = ResolveUserDisplayName(u);
        }
        foreach (var dto in dtos)
        {
            if (tenantInfos.TryGetValue(dto.SourceTenantId, out var tn)) dto.SourceTenantName = tn;
            if (users.TryGetValue(dto.SharedByUserId, out var un)) dto.SharedByUserName = un;
        }
    }

    private static string ResolveUserDisplayName(IdentityUser user)
    {
        var displayName = string.Join(" ", new[] { user.Name, user.Surname }
            .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
        if (!string.IsNullOrWhiteSpace(user.UserName)) return user.UserName;
        return user.Id.ToString();
    }
}
