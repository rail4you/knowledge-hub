using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库可见性 / 可写性判定。
///
/// <para>
/// 这是「租户 = 学校」下教学资源库模型的核心规则实现，管理端与教师端两个
/// AppService 共用同一套判定，避免两侧逻辑漂移。
/// </para>
///
/// <para>
/// 判定规则（<see cref="ResourceLibraryVisibility"/>）：
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>私有</b>：仅归属学校（主学校）的<b>被分配教师</b>可见可写；
/// 本校院校管理员始终可见可写。
/// </description></item>
/// <item><description>
/// <b>公开</b>：归属学校（主学校）的<b>全体教师</b>可见可写；
/// 不含关联学校。
/// </description></item>
/// <item><description>
/// <b>院校共建</b>：<b>主学校 + 全部关联学校</b>的教师可见可写，
/// 可互相看到对方共享进来的内容。
/// </description></item>
/// </list>
///
/// <para>
/// 院校管理员（角色 <c>SchoolAdmin</c>，以及 host 的 <c>admin</c>）对
/// 本租户名下（含作为关联方参与）的全部库拥有管理权，不受可见性限制。
/// </para>
/// </summary>
public interface IResourceLibraryAccessService
{
    /// <summary>取当前用户可见的教学资源库（含主建的 + 参与共建的）。</summary>
    Task<List<ResourceLibrary>> GetVisibleLibrariesAsync(
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin);

    /// <summary>取库并校验可见性；不可见时抛业务异常。</summary>
    Task<ResourceLibrary> GetLibraryAsync(
        Guid libraryId,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin);

    /// <summary>取库并校验可写性（可向库内共享内容）；不可写时抛业务异常。</summary>
    Task<ResourceLibrary> GetContributableLibraryAsync(
        Guid libraryId,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin);

    /// <summary>判断当前用户是否能看到该库。</summary>
    Task<bool> CanViewAsync(
        ResourceLibrary library,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin);

    /// <summary>判断当前用户是否可以向该库共享内容。</summary>
    Task<bool> CanContributeAsync(
        ResourceLibrary library,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin);

    /// <summary>判断教师是否被分配到该库。</summary>
    Task<bool> IsAssignedTeacherAsync(Guid libraryId, Guid userId);

    /// <summary>取可见性中文说明。</summary>
    string GetVisibilityName(ResourceLibraryVisibility visibility);
}

public class ResourceLibraryAccessService : IResourceLibraryAccessService, ITransientDependency
{
    private readonly IResourceLibraryRepository _libraryRepository;

    public ResourceLibraryAccessService(IResourceLibraryRepository libraryRepository)
    {
        _libraryRepository = libraryRepository;
    }

    public async Task<List<ResourceLibrary>> GetVisibleLibrariesAsync(
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        // 候选集必须跨租户查询：共建库的归属租户可能是别的学校
        var candidates = await _libraryRepository.GetByTenantOrMemberAsync(tenantId);

        if (isTenantAdmin)
        {
            return candidates;
        }

        var visible = new List<ResourceLibrary>();
        foreach (var library in candidates)
        {
            if (await CanViewCoreAsync(library, tenantId, userId))
            {
                visible.Add(library);
            }
        }

        return visible;
    }

    public async Task<ResourceLibrary> GetLibraryAsync(
        Guid libraryId,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        var library = await FindLibraryAsync(libraryId);
        if (!await CanViewAsync(library, tenantId, userId, isTenantAdmin))
        {
            throw new UserFriendlyException("教学资源库不存在或无权访问。");
        }

        return library;
    }

    public async Task<ResourceLibrary> GetContributableLibraryAsync(
        Guid libraryId,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        var library = await FindLibraryAsync(libraryId);
        if (!await CanContributeAsync(library, tenantId, userId, isTenantAdmin))
        {
            throw new UserFriendlyException("无权向该教学资源库共享内容。");
        }

        return library;
    }

    public async Task<bool> CanViewAsync(
        ResourceLibrary library,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        if (isTenantAdmin)
        {
            // 院校管理员对本租户名下（含作为关联方参与）的库一律可见
            return library.OwnerTenantId == tenantId ||
                   library.Members.Any(m => m.MemberTenantId == tenantId);
        }

        return await CanViewCoreAsync(library, tenantId, userId);
    }

    public async Task<bool> CanContributeAsync(
        ResourceLibrary library,
        Guid tenantId,
        Guid? userId,
        bool isTenantAdmin)
    {
        var isOwner = library.OwnerTenantId == tenantId;
        var isPartner = library.Members.Any(m => m.MemberTenantId == tenantId);

        if (isTenantAdmin)
        {
            return isOwner || isPartner;
        }

        switch (library.Visibility)
        {
            case ResourceLibraryVisibility.Private:
                // 私有库：仅归属学校的被分配教师可写
                return isOwner && userId.HasValue &&
                       await IsAssignedTeacherAsync(library.Id, userId.Value);

            case ResourceLibraryVisibility.Public:
                // 公开库：本校全体教师可写
                return isOwner;

            case ResourceLibraryVisibility.CoBuilt:
                // 共建库：主学校 + 关联学校全体教师可写
                return isOwner || isPartner;

            default:
                return false;
        }
    }

    public async Task<bool> IsAssignedTeacherAsync(Guid libraryId, Guid userId)
    {
        return await _libraryRepository.IsTeacherAssignedAsync(libraryId, userId);
    }

    public string GetVisibilityName(ResourceLibraryVisibility visibility)
    {
        return visibility switch
        {
            ResourceLibraryVisibility.Private => "私有",
            ResourceLibraryVisibility.Public => "公开",
            ResourceLibraryVisibility.CoBuilt => "院校共建",
            _ => "未知"
        };
    }

    /// <summary>
    /// 非管理员的可见性核心判定。
    /// </summary>
    private async Task<bool> CanViewCoreAsync(ResourceLibrary library, Guid tenantId, Guid? userId)
    {
        var isOwner = library.OwnerTenantId == tenantId;
        var isPartner = library.Members.Any(m => m.MemberTenantId == tenantId);

        switch (library.Visibility)
        {
            case ResourceLibraryVisibility.Private:
                // 私有库：仅归属学校的被分配教师可见
                if (!isOwner)
                {
                    return false;
                }

                return userId.HasValue && await IsAssignedTeacherAsync(library.Id, userId.Value);

            case ResourceLibraryVisibility.Public:
                // 公开库：本校全体教师可见
                return isOwner;

            case ResourceLibraryVisibility.CoBuilt:
                // 共建库：主学校 + 关联学校全体教师可见
                return isOwner || isPartner;

            default:
                return false;
        }
    }

    /// <summary>跨租户取库（库的归属租户可能是别校），并预加载共建成员。</summary>
    private async Task<ResourceLibrary> FindLibraryAsync(Guid libraryId)
    {
        var library = await _libraryRepository.FindWithMembersAsync(libraryId);
        if (library == null)
        {
            throw new UserFriendlyException("教学资源库不存在。");
        }

        return library;
    }
}
