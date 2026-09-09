using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using KnowledgeHub.TenantInfos.Dtos;
using KnowledgeHub.TenantInfos.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.TenantInfos;

[IgnoreAntiforgeryToken]
public class TenantInfoAppService : KnowledgeHubAppService, ITenantInfoAppService
{
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IMajorRepository _majorRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IDataFilter _dataFilter;

    public TenantInfoAppService(
        ITenantInfoRepository tenantInfoRepository,
        ITenantRepository tenantRepository,
        IMajorRepository majorRepository,
        IRepository<Course, Guid> courseRepository,
        IDataFilter dataFilter)
    {
        _tenantInfoRepository = tenantInfoRepository;
        _tenantRepository = tenantRepository;
        _majorRepository = majorRepository;
        _courseRepository = courseRepository;
        _dataFilter = dataFilter;
    }

    [Authorize(KnowledgeHubPermissions.TenantInfo.Default)]
    public async Task<List<TenantInfoListItemDto>> GetListAsync()
    {
        // host 全局管理员：返回所有租户；租户级管理员（SchoolAdmin 等持有该权限）：
        // 仅返回自己所在租户的单条数据，用于“资源库管理”页管理本租户展示配置。
        if (CurrentTenant.Id != null)
        {
            var ownTenantId = CurrentTenant.Id.Value;
            var ownTenant = await _tenantRepository.FindAsync(ownTenantId);
            if (ownTenant == null)
            {
                return new List<TenantInfoListItemDto>();
            }

            var ownInfo = await _tenantInfoRepository.FindByTenantIdAsync(ownTenantId);
            return new List<TenantInfoListItemDto>
            {
                new TenantInfoListItemDto
                {
                    TenantId = ownTenant.Id,
                    TenantName = ownTenant.Name,
                    HasInfo = ownInfo != null,
                    Type = ownInfo?.Type ?? TenantType.Professional,
                    Name = ownInfo?.Name ?? ownTenant.Name,
                    Description = ownInfo?.Description,
                    CoverImageCount = CountJsonItems(ownInfo?.CoverImages),
                    SpecialProjectCount = CountJsonItems(ownInfo?.SpecialProjects),
                    MajorCount = await CountMajorsAsync(ownTenantId),
                    CourseCount = await CountCoursesAsync(ownTenantId),
                }
            };
        }

        var tenants = await _tenantRepository.GetListAsync();
        var result = new List<TenantInfoListItemDto>();

        // 关闭多租户过滤，让全局管理员在 host 上下文也能统查所有租户的关联数据
        using (_dataFilter.Disable<IMultiTenant>())
        {
            foreach (var tenant in tenants.OrderBy(t => t.CreationTime))
            {
                var info = await _tenantInfoRepository.FindByTenantIdAsync(tenant.Id);
                var majorCount = await CountMajorsAsync(tenant.Id);
                var courseCount = await CountCoursesAsync(tenant.Id);

                result.Add(new TenantInfoListItemDto
                {
                    TenantId = tenant.Id,
                    TenantName = tenant.Name,
                    HasInfo = info != null,
                    Type = info?.Type ?? TenantType.Professional,
                    Name = info?.Name ?? tenant.Name,
                    Description = info?.Description,
                    CoverImageCount = CountJsonItems(info?.CoverImages),
                    SpecialProjectCount = CountJsonItems(info?.SpecialProjects),
                    MajorCount = majorCount,
                    CourseCount = courseCount,
                });
            }
        }

        return result;
    }

    [AllowAnonymous]
    public async Task<TenantInfoDto> GetCurrentAsync()
    {
        var tenantId = CurrentTenant.Id;
        if (tenantId == null)
        {
            // 如果不在租户上下文中，使用第一个租户
            var tenants = await _tenantRepository.GetListAsync();
            var first = tenants.FirstOrDefault();
            if (first == null)
                throw new UserFriendlyException("系统中没有租户。");
            tenantId = first.Id;
        }
        return await GetByTenantIdAsync(tenantId.Value);
    }

    [AllowAnonymous]
    public async Task<TenantInfoDto> GetByTenantIdAsync(Guid tenantId)
    {
        var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenantId);
        if (tenantInfo == null)
        {
            var tenant = await _tenantRepository.FindAsync(tenantId);
            if (tenant == null)
            {
                throw new UserFriendlyException("租户不存在。");
            }

            tenantInfo = new TenantInfo(
                GuidGenerator.Create(),
                tenantId,
                tenant.Name);
            await _tenantInfoRepository.InsertAsync(tenantInfo, autoSave: true);
        }

        var majorCount = await CountMajorsAsync(tenantId);
        var courseCount = await CountCoursesAsync(tenantId);

        return MapToDto(tenantInfo, majorCount, courseCount);
    }

    [Authorize(KnowledgeHubPermissions.TenantInfo.Edit)]
    public async Task<TenantInfoDto> SaveCurrentAsync(CreateUpdateTenantInfoDto input)
    {
        var tenantId = CurrentTenant.Id;
        if (tenantId == null)
        {
            // 如果没有租户上下文，回退到第一个租户（与 GetCurrentAsync 一致）
            var tenants = await _tenantRepository.GetListAsync();
            var first = tenants.FirstOrDefault();
            if (first == null)
                throw new UserFriendlyException("系统中没有租户，无法保存。");
            tenantId = first.Id;
        }
        return await SaveTenantInfoInternalAsync(tenantId.Value, input);
    }

    [Authorize(KnowledgeHubPermissions.TenantInfo.Edit)]
    public async Task<TenantInfoDto> SaveByTenantIdAsync(Guid tenantId, CreateUpdateTenantInfoDto input)
    {
        // host 全局管理员可修改任意租户；租户级管理员（SchoolAdmin）仅可修改自己所在租户，
        // 用于“资源库管理”页管理本租户展示配置。
        if (CurrentTenant.Id != null && CurrentTenant.Id.Value != tenantId)
        {
            throw new AbpAuthorizationException("仅可管理自己所在租户的资源库信息。");
        }
        return await SaveTenantInfoInternalAsync(tenantId, input);
    }

    private async Task<TenantInfoDto> SaveTenantInfoInternalAsync(Guid tenantId, CreateUpdateTenantInfoDto input)
    {
        var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenantId);
        if (tenantInfo == null)
        {
            var tenant = await _tenantRepository.FindAsync(tenantId);
            tenantInfo = new TenantInfo(GuidGenerator.Create(), tenantId, input.Name.Trim(), input.Type);
            await _tenantInfoRepository.InsertAsync(tenantInfo, autoSave: false);
        }

        tenantInfo.SetName(input.Name);
        tenantInfo.Type = input.Type;
        tenantInfo.SetDescription(input.Description);

        tenantInfo.SetCoverImages(input.CoverImageList.Count > 0
            ? JsonSerializer.Serialize(input.CoverImageList)
            : null);

        tenantInfo.SetTalentTrainingPlan(input.TalentTrainingPlan);
        tenantInfo.SetProfessionalTeachingStandards(input.ProfessionalTeachingStandards);

        tenantInfo.SetSpecialProjects(input.SpecialProjectList.Count > 0
            ? JsonSerializer.Serialize(input.SpecialProjectList)
            : null);

        await _tenantInfoRepository.UpdateAsync(tenantInfo, autoSave: true);

        var majorCount = await CountMajorsAsync(tenantId);
        var courseCount = await CountCoursesAsync(tenantId);

        return MapToDto(tenantInfo, majorCount, courseCount);
    }

    [AllowAnonymous]
    public async Task<TenantKnowledgeGraphDto> GetKnowledgeGraphAsync(Guid tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
        var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenantId);
        var tenantName = tenantInfo?.Name ?? "资源库";

        var centerNode = new TenantGraphNodeDto
        {
            Id = $"tenant_{tenantId}",
            Name = tenantName,
            NodeType = "tenant",
            Description = tenantInfo?.Description
        };

        var query = await _majorRepository.GetQueryableAsync();
        var tenantMajors = query.Where(x => x.TenantId == tenantId).ToList();

        // 一次性加载该租户所有课程，按 MajorId 分组统计
        var courseQuery = await _courseRepository.GetQueryableAsync();
        var allCourses = courseQuery.Where(c => c.TenantId == tenantId && c.MajorId.HasValue).ToList();
        var courseCountByMajor = allCourses
            .GroupBy(c => c.MajorId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var nodes = new List<TenantGraphNodeDto> { centerNode };
        var relations = new List<TenantGraphRelationDto>();

        foreach (var major in tenantMajors)
        {
            courseCountByMajor.TryGetValue(major.Id, out var courseCount);
            var majorNode = new TenantGraphNodeDto
            {
                Id = $"major_{major.Id}",
                Name = major.Name,
                NodeType = "major",
                Description = major.Description,
                ChildrenCount = courseCount
            };
            nodes.Add(majorNode);

            relations.Add(new TenantGraphRelationDto
            {
                SourceId = centerNode.Id,
                TargetId = majorNode.Id,
                RelationType = "contains",
                Label = "包含"
            });
        }

        // 添加课程节点
        foreach (var course in allCourses)
        {
            var courseNode = new TenantGraphNodeDto
            {
                Id = $"course_{course.Id}",
                Name = course.Title,
                NodeType = "course",
                Description = course.Description
            };
            nodes.Add(courseNode);

            relations.Add(new TenantGraphRelationDto
            {
                SourceId = $"major_{course.MajorId}",
                TargetId = courseNode.Id,
                RelationType = "contains",
                Label = "包含"
            });
        }

        return new TenantKnowledgeGraphDto
        {
            CenterNode = centerNode,
            Majors = tenantMajors.Select(x => new TenantGraphNodeDto
            {
                Id = $"major_{x.Id}",
                Name = x.Name,
                NodeType = "major",
                Description = x.Description,
                ChildrenCount = courseCountByMajor.TryGetValue(x.Id, out var c) ? c : 0
            }).ToList(),
            AllNodes = nodes,
            Relations = relations
        };
        } // end using _dataFilter.Disable<IMultiTenant>()
    }

    [AllowAnonymous]
    public async Task<TenantKnowledgeGraphDto> GetCurrentKnowledgeGraphAsync()
    {
        var tenantId = CurrentTenant.Id;
        if (tenantId == null)
        {
            var tenants = await _tenantRepository.GetListAsync();
            var first = tenants.FirstOrDefault();
            if (first == null)
                return new TenantKnowledgeGraphDto();
            tenantId = first.Id;
        }
        return await GetKnowledgeGraphAsync(tenantId.Value);
    }

    // --- helpers ---

    private static TenantInfoDto MapToDto(TenantInfo entity, int majorCount, int courseCount)
    {
        var coverImageList = new List<string>();
        if (!string.IsNullOrWhiteSpace(entity.CoverImages))
        {
            try { coverImageList = JsonSerializer.Deserialize<List<string>>(entity.CoverImages) ?? new(); }
            catch { }
        }

        var specialProjectList = new List<SpecialProjectItem>();
        if (!string.IsNullOrWhiteSpace(entity.SpecialProjects))
        {
            try { specialProjectList = JsonSerializer.Deserialize<List<SpecialProjectItem>>(entity.SpecialProjects) ?? new(); }
            catch { }
        }

        return new TenantInfoDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Type = entity.Type,
            Name = entity.Name,
            Description = entity.Description,
            CoverImageList = coverImageList,
            TalentTrainingPlan = entity.TalentTrainingPlan,
            ProfessionalTeachingStandards = entity.ProfessionalTeachingStandards,
            SpecialProjectList = specialProjectList,
            MajorCount = majorCount,
            CourseCount = courseCount,
            CreationTime = entity.CreationTime,
            CreatorId = entity.CreatorId,
            LastModificationTime = entity.LastModificationTime,
            LastModifierId = entity.LastModifierId,
        };
    }

    private async Task<int> CountMajorsAsync(Guid tenantId)
    {
        var query = await _majorRepository.GetQueryableAsync();
        return query.Count(x => x.TenantId == tenantId);
    }

    private async Task<int> CountCoursesAsync(Guid tenantId)
    {
        var query = await _courseRepository.GetQueryableAsync();
        return query.Count(c => c.TenantId == tenantId);
    }

    private static int CountJsonItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try
        {
            var list = JsonSerializer.Deserialize<List<object>>(json);
            return list?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}
