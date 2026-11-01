using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Majors;
using KnowledgeHub.Practicums;
using KnowledgeHub.ResourceLibraries.Dtos;
using KnowledgeHub.Resources;
using KnowledgeHub.Schools;
using KnowledgeHub.TenantInfos;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库「变更落地」服务。
///
/// <para>
/// 抽出这个服务的原因：V2.0 起建库 / 改库 / 删库要走审核，
/// 「审核通过后应用变更」与「审核开关关闭时直接应用」必须是<b>同一份代码</b>，
/// 否则两条路径会随时间长歪（典型症状：审核通过后少写了某个字段）。
/// </para>
///
/// <para>
/// 因此：
/// </para>
/// <list type="bullet">
/// <item><description><see cref="ApplyUpdateAsync"/> / <see cref="ApplyDeleteAsync"/>：直接修改（开关关闭时的旧行为）。</description></item>
/// <item><description><see cref="ApplyArchiveAsync"/>：审核通过后的归档（V2.0 删除语义，不再硬删）。</description></item>
/// </list>
/// </summary>
public interface IResourceLibraryChangeApplier
{
    /// <summary>应用修改（名称/简介/封面/专业/负责人/可见性/成员校/对外开关/排序/状态）。</summary>
    Task ApplyUpdateAsync(ResourceLibrary library, UpdateResourceLibraryDto input, Guid ownerTenantId);

    /// <summary>直接删除（旧行为：解绑内容 + 删除库行 + 清理教师分配）。仅审核开关关闭时使用。</summary>
    Task ApplyDeleteAsync(ResourceLibrary library, Guid ownerTenantId);

    /// <summary>归档（V2.0 删除语义）：解绑内容、保留成员与教师分配、库置为 Archived。</summary>
    Task ApplyArchiveAsync(ResourceLibrary library, Guid ownerTenantId);

    /// <summary>规范化关联学校 Id（过滤主学校 / 社会学校 / 重复 / 不存在）。</summary>
    Task<List<Guid>> NormalizePartnerIdsAsync(List<Guid>? partnerTenantIds, Guid currentTenantId);

    /// <summary>取库里现有的关联学校 Id。</summary>
    Task<List<Guid>> GetExistingPartnerIdsAsync(Guid libraryId, Guid currentTenantId);

    /// <summary>加一所关联学校。</summary>
    Task AddPartnerMemberAsync(ResourceLibrary library, Guid partnerTenantId, Guid currentTenantId);

    /// <summary>整体替换关联学校名单。</summary>
    Task ReplacePartnerMembersAsync(ResourceLibrary library, List<Guid> partnerTenantIds, Guid currentTenantId);

    /// <summary>清空关联学校。</summary>
    Task ClearPartnerMembersAsync(Guid libraryId, Guid currentTenantId);

    /// <summary>校验主建专业属于本校。</summary>
    Task EnsureMajorBelongsToTenantAsync(Guid majorId, Guid tenantId);

    /// <summary>解析学校展示名（学校档案名 → 专业库展示名 → 租户名 → Id）。</summary>
    Task<string> ResolveTenantDisplayNameAsync(Guid tenantId, string? fallback);
}

public class ResourceLibraryChangeApplier : IResourceLibraryChangeApplier, ITransientDependency
{
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IRepository<ResourceLibraryMember, Guid> _memberRepository;
    private readonly IRepository<ResourceLibraryTeacher, Guid> _teacherRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<PracticumProject, Guid> _practicumRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IMajorRepository _majorRepository;
    private readonly ISchoolRepository _schoolRepository;
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly IPublicSchoolProvider _publicSchoolProvider;
    private readonly IDataFilter _dataFilter;
    private readonly IGuidGenerator _guidGenerator;

    public ResourceLibraryChangeApplier(
        IResourceLibraryRepository libraryRepository,
        IRepository<ResourceLibraryMember, Guid> memberRepository,
        IRepository<ResourceLibraryTeacher, Guid> teacherRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<PracticumProject, Guid> practicumRepository,
        ITenantRepository tenantRepository,
        IMajorRepository majorRepository,
        ISchoolRepository schoolRepository,
        ITenantInfoRepository tenantInfoRepository,
        IPublicSchoolProvider publicSchoolProvider,
        IDataFilter dataFilter,
        IGuidGenerator guidGenerator)
    {
        _libraryRepository = libraryRepository;
        _memberRepository = memberRepository;
        _teacherRepository = teacherRepository;
        _resourceRepository = resourceRepository;
        _courseRepository = courseRepository;
        _practicumRepository = practicumRepository;
        _tenantRepository = tenantRepository;
        _majorRepository = majorRepository;
        _schoolRepository = schoolRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _publicSchoolProvider = publicSchoolProvider;
        _dataFilter = dataFilter;
        _guidGenerator = guidGenerator;
    }

    public async Task ApplyUpdateAsync(ResourceLibrary library, UpdateResourceLibraryDto input, Guid ownerTenantId)
    {
        if (input.Name != null)
        {
            library.SetName(input.Name);
        }

        if (input.Description != null)
        {
            library.SetDescription(input.Description);
        }

        if (input.CoverUrl != null)
        {
            library.SetCoverUrl(input.CoverUrl);
        }

        if (input.PrimaryMajorId.HasValue)
        {
            await EnsureMajorBelongsToTenantAsync(input.PrimaryMajorId.Value, ownerTenantId);
            library.SetPrimaryMajorId(input.PrimaryMajorId.Value);
        }

        if (input.Principal != null)
        {
            library.SetPrincipal(input.Principal);
        }

        if (input.IsPublic.HasValue)
        {
            library.SetIsPublic(input.IsPublic.Value);
        }

        if (input.OpenToExternalLearners.HasValue)
        {
            // 校级私有库按需求不对外开放自学，禁止开启
            if (input.OpenToExternalLearners.Value && library.Visibility == ResourceLibraryVisibility.Private)
            {
                throw new UserFriendlyException("校级私有教学资源库不对外开放自学，无需开启该选项。");
            }

            library.SetOpenToExternalLearners(input.OpenToExternalLearners.Value);
        }

        if (input.SortOrder.HasValue)
        {
            library.SetSortOrder(input.SortOrder.Value);
        }

        if (input.Status.HasValue)
        {
            library.SetStatus(input.Status.Value);
        }

        // 可见性变更：私有 / 公开 → 清空关联学校；院校共建 → 必须有关联学校
        if (input.Visibility.HasValue && input.Visibility.Value != library.Visibility)
        {
            var nextVisibility = input.Visibility.Value;

            if (nextVisibility == ResourceLibraryVisibility.CoBuilt)
            {
                var partners = input.PartnerTenantIds != null
                    ? await NormalizePartnerIdsAsync(input.PartnerTenantIds, ownerTenantId)
                    : await GetExistingPartnerIdsAsync(library.Id, ownerTenantId);

                if (partners.Count == 0)
                {
                    throw new UserFriendlyException("院校共建库必须至少设置一所关联学校。");
                }

                library.SetVisibility(nextVisibility);
                await ReplacePartnerMembersAsync(library, partners, ownerTenantId);
            }
            else
            {
                library.SetVisibility(nextVisibility);
                await ClearPartnerMembersAsync(library.Id, ownerTenantId);
            }

            // 形态变化时，「对外开放自学」跟随形态默认值（除非请求中显式指定）
            if (!input.OpenToExternalLearners.HasValue)
            {
                library.SetOpenToExternalLearners(nextVisibility.DefaultOpenToExternalLearners());
            }
        }
        else if (input.Visibility.HasValue)
        {
            library.SetVisibility(input.Visibility.Value);
        }
        else if (input.PartnerTenantIds != null)
        {
            // 未改可见性但传了关联学校列表：仅院校共建库允许维护
            if (library.Visibility != ResourceLibraryVisibility.CoBuilt)
            {
                throw new UserFriendlyException("只有院校共建库才能设置关联学校。");
            }

            var partners = await NormalizePartnerIdsAsync(input.PartnerTenantIds, ownerTenantId);
            await ReplacePartnerMembersAsync(library, partners, ownerTenantId);
        }

        await _libraryRepository.UpdateAsync(library, autoSave: true);
    }

    public async Task ApplyDeleteAsync(ResourceLibrary library, Guid ownerTenantId)
    {
        await DetachLibraryContentAsync(library.Id);

        await _libraryRepository.DeleteAsync(library, autoSave: true);
    }

    public async Task ApplyArchiveAsync(ResourceLibrary library, Guid ownerTenantId)
    {
        // 归档：内容解除归属（内容本身保留），成员校与教师分配保留以便追溯；
        // 与旧「硬删」的差别：库行不删，审核历史、贡献记录、学习记录都还能查到。
        await DetachLibraryContentAsync(library.Id);

        library.SetStatus(ResourceLibraryStatus.Archived);
        await _libraryRepository.UpdateAsync(library, autoSave: true);
    }

    /// <summary>把库内资源 / 课程 / 实训的归属清空，并清理教师分配（仅硬删路径调用）。</summary>
    private async Task DetachLibraryContentAsync(Guid libraryId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var resources = (await _resourceRepository.GetQueryableAsync())
                .Where(x => x.LibraryId == libraryId)
                .ToList();
            foreach (var resource in resources)
            {
                resource.LibraryId = null;
            }

            var courses = (await _courseRepository.GetQueryableAsync())
                .Where(x => x.LibraryId == libraryId)
                .ToList();
            foreach (var course in courses)
            {
                course.LibraryId = null;
            }

            var practicums = (await _practicumRepository.GetQueryableAsync())
                .Where(x => x.LibraryId == libraryId)
                .ToList();
            foreach (var practicum in practicums)
            {
                practicum.LibraryId = null;
            }

            if (resources.Count > 0)
            {
                await _resourceRepository.UpdateManyAsync(resources, autoSave: true);
            }

            if (courses.Count > 0)
            {
                await _courseRepository.UpdateManyAsync(courses, autoSave: true);
            }

            if (practicums.Count > 0)
            {
                await _practicumRepository.UpdateManyAsync(practicums, autoSave: true);
            }

            // 教师分配记录随库级联删除（EF 外键级联已配置，这里显式清理以兼容历史数据）
            var assignments = (await _teacherRepository.GetQueryableAsync())
                .Where(x => x.LibraryId == libraryId)
                .ToList();
            if (assignments.Count > 0)
            {
                await _teacherRepository.DeleteManyAsync(assignments, autoSave: true);
            }
        }
    }

    public async Task<List<Guid>> NormalizePartnerIdsAsync(List<Guid>? partnerTenantIds, Guid currentTenantId)
    {
        var result = new List<Guid>();
        if (partnerTenantIds == null || partnerTenantIds.Count == 0)
        {
            return result;
        }

        // 社会学校是自行注册社会学员的归属租户，不是建制学校，不能参与校际共建
        var publicSchoolTenantId = await _publicSchoolProvider.GetPublicSchoolTenantIdAsync();

        foreach (var tenantId in partnerTenantIds.Distinct())
        {
            if (tenantId == Guid.Empty || tenantId == currentTenantId)
            {
                continue;
            }

            if (tenantId == publicSchoolTenantId)
            {
                throw new UserFriendlyException("社会学校不是建制学校，不能作为关联学校。");
            }

            if (await _tenantRepository.FindAsync(tenantId) == null)
            {
                throw new UserFriendlyException($"关联学校不存在（{tenantId}）。");
            }

            result.Add(tenantId);
        }

        return result;
    }

    public async Task<List<Guid>> GetExistingPartnerIdsAsync(Guid libraryId, Guid currentTenantId)
    {
        var members = await _libraryRepository.GetMembersAsync(libraryId);
        return members
            .Where(m => m.Role == ResourceLibraryMemberRole.Partner && m.MemberTenantId != currentTenantId)
            .Select(m => m.MemberTenantId)
            .Distinct()
            .ToList();
    }

    public async Task AddPartnerMemberAsync(ResourceLibrary library, Guid partnerTenantId, Guid currentTenantId)
    {
        var tenant = await _tenantRepository.FindAsync(partnerTenantId);
        var member = new ResourceLibraryMember(
            _guidGenerator.Create(),
            library.Id,
            partnerTenantId,
            await ResolveTenantDisplayNameAsync(partnerTenantId, tenant?.Name),
            ResourceLibraryMemberRole.Partner,
            currentTenantId);

        await _memberRepository.InsertAsync(member, autoSave: true);
    }

    public async Task ReplacePartnerMembersAsync(ResourceLibrary library, List<Guid> partnerTenantIds, Guid currentTenantId)
    {
        var existing = await _libraryRepository.GetMembersAsync(library.Id);
        var existingPartners = existing
            .Where(m => m.Role == ResourceLibraryMemberRole.Partner)
            .ToList();

        var target = new HashSet<Guid>(partnerTenantIds);

        foreach (var member in existingPartners)
        {
            if (!target.Contains(member.MemberTenantId))
            {
                await _memberRepository.DeleteAsync(member, autoSave: true);
            }
        }

        var existingIds = existingPartners.Select(m => m.MemberTenantId).ToHashSet();
        foreach (var partnerTenantId in partnerTenantIds)
        {
            if (!existingIds.Contains(partnerTenantId))
            {
                await AddPartnerMemberAsync(library, partnerTenantId, currentTenantId);
            }
        }
    }

    public async Task ClearPartnerMembersAsync(Guid libraryId, Guid currentTenantId)
    {
        var members = await _libraryRepository.GetMembersAsync(libraryId);
        var partners = members
            .Where(m => m.Role == ResourceLibraryMemberRole.Partner && m.MemberTenantId != currentTenantId)
            .ToList();

        foreach (var member in partners)
        {
            await _memberRepository.DeleteAsync(member, autoSave: true);
        }
    }

    public async Task EnsureMajorBelongsToTenantAsync(Guid majorId, Guid tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var major = await _majorRepository.FindAsync(majorId);
            if (major == null)
            {
                throw new UserFriendlyException("主建专业不存在。");
            }

            if (major.TenantId != tenantId)
            {
                throw new UserFriendlyException("主建专业必须属于当前学校。");
            }
        }
    }

    public async Task<string> ResolveTenantDisplayNameAsync(Guid tenantId, string? fallback)
    {
        // 学校档案名优先：那是「这所学校」的权威中文机构名。
        // 注意不能拿 fallback 做等值排除 —— 历史成员记录里存的往往就是学校档案名，
        // 一旦等值就漏掉学校名、错误地回退到专业库展示名。
        var school = await _schoolRepository.FindByTenantIdAsync(tenantId);
        if (school != null && !string.IsNullOrWhiteSpace(school.Name))
        {
            return school.Name;
        }

        var tenantInfo = await _tenantInfoRepository.FindByTenantIdAsync(tenantId);
        if (!string.IsNullOrWhiteSpace(tenantInfo?.Name))
        {
            return tenantInfo.Name;
        }

        return fallback ?? tenantId.ToString();
    }
}
