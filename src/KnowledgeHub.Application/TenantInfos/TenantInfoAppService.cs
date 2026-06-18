using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Majors;
using KnowledgeHub.TenantInfos.Dtos;
using KnowledgeHub.TenantInfos.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
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

    public TenantInfoAppService(
        ITenantInfoRepository tenantInfoRepository,
        ITenantRepository tenantRepository,
        IMajorRepository majorRepository,
        IRepository<Course, Guid> courseRepository)
    {
        _tenantInfoRepository = tenantInfoRepository;
        _tenantRepository = tenantRepository;
        _majorRepository = majorRepository;
        _courseRepository = courseRepository;
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

    [Authorize]
    public async Task<TenantInfoDto> SaveCurrentAsync(CreateUpdateTenantInfoDto input)
    {
        var tenantId = CurrentTenant.Id;
        if (tenantId == null)
        {
            throw new UserFriendlyException("未找到租户信息，无法保存。");
        }
        return await SaveTenantInfoInternalAsync(tenantId.Value, input);
    }

    [Authorize]
    public async Task<TenantInfoDto> SaveByTenantIdAsync(Guid tenantId, CreateUpdateTenantInfoDto input)
    {
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

        var nodes = new List<TenantGraphNodeDto> { centerNode };
        var relations = new List<TenantGraphRelationDto>();

        foreach (var major in tenantMajors)
        {
            var majorNode = new TenantGraphNodeDto
            {
                Id = $"major_{major.Id}",
                Name = major.Name,
                NodeType = "major",
                Description = major.Description,
                ChildrenCount = await CountCoursesByMajorAsync(major.Id)
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

        var courseQuery = await _courseRepository.GetQueryableAsync();
        foreach (var major in tenantMajors)
        {
            var courses = courseQuery.Where(c => c.MajorId == major.Id).ToList();
            foreach (var course in courses)
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
                    SourceId = $"major_{major.Id}",
                    TargetId = courseNode.Id,
                    RelationType = "contains",
                    Label = "包含"
                });
            }
        }

        return new TenantKnowledgeGraphDto
        {
            CenterNode = centerNode,
            Majors = tenantMajors.Select(x => new TenantGraphNodeDto
            {
                Id = $"major_{x.Id}",
                Name = x.Name,
                NodeType = "major",
                Description = x.Description
            }).ToList(),
            AllNodes = nodes,
            Relations = relations
        };
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

    private async Task<int> CountCoursesByMajorAsync(Guid majorId)
    {
        var query = await _courseRepository.GetQueryableAsync();
        return query.Count(c => c.MajorId == majorId);
    }
}
