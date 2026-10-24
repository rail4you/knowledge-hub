using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Majors;
using KnowledgeHub.Permissions;
using KnowledgeHub.ResourceLibraries.Dtos;
using KnowledgeHub.Resources;
using KnowledgeHub.Schools;
using KnowledgeHub.TenantInfos;
using KnowledgeHub.Schools.Dtos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 资源库 AppService（多校共建）。
///
/// <para>
/// 数据模型：库是<b>主建学校</b>的租户级实体，合作学校通过 <c>ResourceLibraryMember</c> 加入；
/// 库内资源/课程归属主建学校（不破坏租户隔离），合作学校通过成员关系获得可见性。
/// </para>
///
/// <para>
/// 专业维度：库通过 <c>PrimaryMajorId</c> 绑定主建专业；把课程/资源挂进库时校验其专业与库一致。
/// </para>
/// </summary>
public class ResourceLibraryAppService : KnowledgeHubAppService, IResourceLibraryAppService
{
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IRepository<ResourceLibraryMember, Guid> _memberRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IMajorRepository _majorRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly ISchoolRepository _schoolRepository;
    private readonly ITenantInfoRepository _tenantInfoRepository;
    private readonly IDataFilter _dataFilter;

    public ResourceLibraryAppService(
        IResourceLibraryRepository libraryRepository,
        IRepository<ResourceLibraryMember, Guid> memberRepository,
        ITenantRepository tenantRepository,
        IMajorRepository majorRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<Course, Guid> courseRepository,
        ISchoolRepository schoolRepository,
        ITenantInfoRepository tenantInfoRepository,
        IDataFilter dataFilter)
    {
        _libraryRepository = libraryRepository;
        _memberRepository = memberRepository;
        _tenantRepository = tenantRepository;
        _majorRepository = majorRepository;
        _resourceRepository = resourceRepository;
        _courseRepository = courseRepository;
        _schoolRepository = schoolRepository;
        _tenantInfoRepository = tenantInfoRepository;
        _dataFilter = dataFilter;
    }

    /// <summary>获取当前租户可见的资源库：主建的 + 参与共建的。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public virtual async Task<List<ResourceLibraryDto>> GetListAsync(ResourceLibraryListRequestDto input)
    {
        var currentTenantId = CurrentTenant.Id;
        if (currentTenantId == null)
        {
            return new List<ResourceLibraryDto>();
        }

        var tenantId = currentTenantId.Value;
        var libraries = await GetVisibleLibrariesAsync(tenantId);

        if (input?.PrimaryMajorId.HasValue == true)
        {
            libraries = libraries.Where(x => x.PrimaryMajorId == input.PrimaryMajorId.Value).ToList();
        }

        if (input?.OnlyJoined == true)
        {
            libraries = libraries.Where(x => x.Members.Any(m => m.MemberTenantId == tenantId)).ToList();
        }

        var result = new List<ResourceLibraryDto>();
        foreach (var library in libraries
                     .OrderBy(x => x.SortOrder)
                     .ThenByDescending(x => x.CreationTime))
        {
            result.Add(await ToDtoAsync(library, tenantId));
        }

        return result;
    }

    /// <summary>获取资源库详情（主建学校或合作学校均可查看）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public virtual async Task<ResourceLibraryDto> GetAsync(Guid id)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await GetVisibleLibrariesAsync(currentTenantId);
        var target = library.FirstOrDefault(x => x.Id == id)
                     ?? throw new UserFriendlyException("资源库不存在或无权访问。");

        return await ToDtoAsync(target, currentTenantId);
    }

    /// <summary>创建资源库，当前租户自动成为主建学校。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Create)]
    public virtual async Task<ResourceLibraryDto> CreateAsync(CreateResourceLibraryDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new UserFriendlyException("请填写资源库名称。");
        }

        if (input.PrimaryMajorId.HasValue)
        {
            await EnsureMajorBelongsToTenantAsync(input.PrimaryMajorId.Value, currentTenantId);
        }

        var library = new ResourceLibrary(GuidGenerator.Create(), currentTenantId, input.Name.Trim())
        {
            Description = input.Description,
            CoverUrl = input.CoverUrl,
            PrimaryMajorId = input.PrimaryMajorId,
            IsPublic = input.IsPublic ?? true,
            SortOrder = input.SortOrder ?? 0,
            Status = ResourceLibraryStatus.Draft
        };

        await _libraryRepository.InsertAsync(library, autoSave: true);

        // 主建学校自身作为 Owner 成员入表，保证「成员」语义统一
        var tenant = await _tenantRepository.FindAsync(currentTenantId);
        library.Members.Add(new ResourceLibraryMember(
            GuidGenerator.Create(),
            library.Id,
            currentTenantId,
            tenant?.Name,
            ResourceLibraryMemberRole.Owner,
            currentTenantId));

        await _libraryRepository.UpdateAsync(library, autoSave: true);

        return await ToDtoAsync(library, currentTenantId);
    }

    /// <summary>更新资源库（仅主建学校）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Edit)]
    public virtual async Task<ResourceLibraryDto> UpdateAsync(Guid id, UpdateResourceLibraryDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await GetOwnedLibraryAsync(id, currentTenantId);

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
            await EnsureMajorBelongsToTenantAsync(input.PrimaryMajorId.Value, currentTenantId);
            library.SetPrimaryMajorId(input.PrimaryMajorId.Value);
        }

        if (input.IsPublic.HasValue)
        {
            library.SetIsPublic(input.IsPublic.Value);
        }

        if (input.SortOrder.HasValue)
        {
            library.SetSortOrder(input.SortOrder.Value);
        }

        if (input.Status.HasValue)
        {
            library.SetStatus(input.Status.Value);
        }

        await _libraryRepository.UpdateAsync(library, autoSave: true);
        return await ToDtoAsync(library, currentTenantId);
    }

    /// <summary>删除资源库（仅主建学校）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await GetOwnedLibraryAsync(id, currentTenantId);

        // 库内资源/课程解除归属，避免出现指向已删库的悬空引用
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var resources = await AsyncExecuter.ToListAsync(
                (await _resourceRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));
            foreach (var resource in resources)
            {
                resource.LibraryId = null;
            }

            var courses = await AsyncExecuter.ToListAsync(
                (await _courseRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));
            foreach (var course in courses)
            {
                course.LibraryId = null;
            }

            if (resources.Count > 0)
            {
                await _resourceRepository.UpdateManyAsync(resources, autoSave: true);
            }

            if (courses.Count > 0)
            {
                await _courseRepository.UpdateManyAsync(courses, autoSave: true);
            }
        }

        await _libraryRepository.DeleteAsync(library, autoSave: true);
    }

    /// <summary>添加合作学校（仅主建学校）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageMembers)]
    public virtual async Task<ResourceLibraryMemberDto> AddMemberAsync(Guid id, AddResourceLibraryMemberDto input)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await GetOwnedLibraryAsync(id, currentTenantId);

        if (input.MemberTenantId == Guid.Empty)
        {
            throw new UserFriendlyException("请选择合作学校。");
        }

        if (input.MemberTenantId == currentTenantId)
        {
            throw new UserFriendlyException("主建学校无需重复添加。");
        }

        var targetTenant = await _tenantRepository.FindAsync(input.MemberTenantId);
        if (targetTenant == null)
        {
            throw new UserFriendlyException("合作学校不存在。");
        }

        var members = await _libraryRepository.GetMembersAsync(library.Id);
        if (members.Any(x => x.MemberTenantId == input.MemberTenantId))
        {
            throw new UserFriendlyException("该学校已在共建名单中。");
        }

        var member = new ResourceLibraryMember(
            GuidGenerator.Create(),
            library.Id,
            input.MemberTenantId,
            await ResolveTenantDisplayNameAsync(input.MemberTenantId, targetTenant.Name),
            ResourceLibraryMemberRole.Partner,
            currentTenantId);

        await _memberRepository.InsertAsync(member, autoSave: true);

        return new ResourceLibraryMemberDto
        {
            Id = member.Id,
            MemberTenantId = member.MemberTenantId,
            MemberTenantName = member.MemberTenantName,
            Role = member.Role,
            JoinedTime = member.JoinedTime
        };
    }

    /// <summary>移除合作学校（仅主建学校，不能移除主建学校自身）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageMembers)]
    public virtual async Task RemoveMemberAsync(Guid id, Guid memberTenantId)
    {
        var currentTenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var library = await GetOwnedLibraryAsync(id, currentTenantId);

        var members = await _libraryRepository.GetMembersAsync(library.Id);
        var member = members.FirstOrDefault(x => x.MemberTenantId == memberTenantId);
        if (member == null)
        {
            throw new UserFriendlyException("该学校不在共建名单中。");
        }

        if (member.Role == ResourceLibraryMemberRole.Owner)
        {
            throw new UserFriendlyException("不能移除主建学校。");
        }

        await _memberRepository.DeleteAsync(member, autoSave: true);
    }

    /// <summary>把课程/资源挂进资源库（专业维度校验）。</summary>
    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public virtual async Task ValidateAttachAsync(Guid libraryId, Guid? majorId)
    {
        var library = await _libraryRepository.GetAsync(libraryId);
        if (!library.PrimaryMajorId.HasValue)
        {
            return;
        }

        if (!majorId.HasValue)
        {
            throw new UserFriendlyException($"资源库「{library.Name}」限定专业，请先设置内容所属专业。");
        }

        if (majorId.Value != library.PrimaryMajorId.Value)
        {
            throw new UserFriendlyException($"内容专业与资源库「{library.Name}」的主建专业不一致。");
        }
    }

    private async Task<List<ResourceLibrary>> GetVisibleLibrariesAsync(Guid tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _libraryRepository.GetQueryableAsync();
            var libraries = await AsyncExecuter.ToListAsync(
                query.Where(x => x.OwnerTenantId == tenantId ||
                                 x.Members.Any(m => m.MemberTenantId == tenantId)));

            return libraries;
        }
    }

    private async Task<ResourceLibrary> GetOwnedLibraryAsync(Guid id, Guid currentTenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var library = await _libraryRepository.GetAsync(id);
            if (library.OwnerTenantId != currentTenantId)
            {
                throw new UserFriendlyException("只有主建学校可以操作该资源库。");
            }

            return library;
        }
    }

    private async Task EnsureMajorBelongsToTenantAsync(Guid majorId, Guid tenantId)
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

    /// <summary>
    /// 租户 → 中文机构名（学校档案名 → 专业库展示名 → 租户名）。
    /// 需求「机构名称要用中文」：共建名单 / 主建方不能显示 qidi 这类租户 slug。
    /// </summary>
    private async Task<string> ResolveTenantDisplayNameAsync(Guid tenantId, string? fallback)
    {
        var school = await _schoolRepository.FindByTenantIdAsync(tenantId);
        if (school != null && !string.IsNullOrWhiteSpace(school.Name)
            && !string.Equals(school.Name, fallback, StringComparison.Ordinal))
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

    /// <summary>批量解析成员显示名（历史记录里存的是租户 slug）。</summary>
    private async Task<List<ResourceLibraryMemberDto>> ResolveMemberNamesAsync(List<ResourceLibraryMember> members)
    {
        var result = new List<ResourceLibraryMemberDto>();
        foreach (var m in members)
        {
            result.Add(new ResourceLibraryMemberDto
            {
                Id = m.Id,
                MemberTenantId = m.MemberTenantId,
                MemberTenantName = await ResolveTenantDisplayNameAsync(m.MemberTenantId, m.MemberTenantName),
                Role = m.Role,
                JoinedTime = m.JoinedTime
            });
        }

        return result;
    }

    private async Task<ResourceLibraryDto> ToDtoAsync(ResourceLibrary library, Guid currentTenantId)
    {
        var members = await _libraryRepository.GetMembersAsync(library.Id);
        var ownerTenant = await _tenantRepository.FindAsync(library.OwnerTenantId);

        string? majorName = null;
        if (library.PrimaryMajorId.HasValue)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var major = await _majorRepository.FindAsync(library.PrimaryMajorId.Value);
                majorName = major?.Name;
            }
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var resourceCount = await AsyncExecuter.CountAsync(
                (await _resourceRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));
            var courseCount = await AsyncExecuter.CountAsync(
                (await _courseRepository.GetQueryableAsync()).Where(x => x.LibraryId == library.Id));

            var currentMember = members.FirstOrDefault(x => x.MemberTenantId == currentTenantId);

            return new ResourceLibraryDto
            {
                Id = library.Id,
                TenantId = library.TenantId,
                Name = library.Name,
                Description = library.Description,
                CoverUrl = library.CoverUrl,
                PrimaryMajorId = library.PrimaryMajorId,
                PrimaryMajorName = majorName,
                OwnerTenantId = library.OwnerTenantId,
                OwnerTenantName = await ResolveTenantDisplayNameAsync(library.OwnerTenantId, ownerTenant?.Name),
                Status = library.Status,
                IsPublic = library.IsPublic,
                SortOrder = library.SortOrder,
                ResourceCount = resourceCount,
                CourseCount = courseCount,
                MemberCount = members.Count,
                CurrentRole = currentMember != null ? (int)currentMember.Role : -1,
                IsOwner = library.OwnerTenantId == currentTenantId,
                Members = await ResolveMemberNamesAsync(members)
            };
        }
    }
}
