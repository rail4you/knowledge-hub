using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Data;
using Volo.Abp.Users;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 多校共建场景下的内容协作权限判定（课程 / 章节 / 习题 / 资源）。
///
/// <para>规则：</para>
/// <list type="bullet">
/// <item><description><b>创建者本人</b>可以改自己创建的内容。</description></item>
/// <item><description><b>本校院校管理员</b>可以管理本校租户下的内容。</description></item>
/// <item><description>
/// <b>共建资源库「主建单位」的院校管理员</b>可以管理整个库的课程 / 章节 / 习题 / 资源
/// （即使内容归属关联学校）。
/// </description></item>
/// <item><description>其他学校的教师只能改自己创建的内容，以及关联自己租户的资源。</description></item>
/// </list>
/// </summary>
public interface IContentCollaborationPolicy
{
    /// <summary>当前用户是否院校管理员（SchoolAdmin / admin）。</summary>
    bool IsSchoolAdmin();

    /// <summary>
    /// 是否可以编辑 / 删除该内容。
    /// </summary>
    /// <param name="creatorId">内容创建者（可为空：老数据）</param>
    /// <param name="contentTenantId">内容归属租户（学校）</param>
    /// <param name="libraryId">内容所属教学资源库（共建判定用；可为空）</param>
    Task<bool> CanEditContentAsync(Guid? creatorId, Guid? contentTenantId, Guid? libraryId);
}

public class ContentCollaborationPolicy : IContentCollaborationPolicy, ITransientDependency
{
    private readonly IRepository<ResourceLibrary, Guid> _libraryRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public ContentCollaborationPolicy(
        IRepository<ResourceLibrary, Guid> libraryRepository,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter)
    {
        _libraryRepository = libraryRepository;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
    }

    public bool IsSchoolAdmin()
    {
        var roles = _currentUser.Roles;
        return roles != null && (roles.Contains("SchoolAdmin") || roles.Contains("admin"));
    }

    public async Task<bool> CanEditContentAsync(Guid? creatorId, Guid? contentTenantId, Guid? libraryId)
    {
        var userId = _currentUser.Id;
        var tenantId = _currentTenant.Id;

        // 1) 创建者本人
        if (userId.HasValue && creatorId.HasValue && creatorId.Value == userId.Value)
        {
            return true;
        }

        if (!IsSchoolAdmin())
        {
            return false;
        }

        // 2) 本校院校管理员：本校租户下的内容
        if (tenantId.HasValue && contentTenantId == tenantId)
        {
            return true;
        }

        // 3) 共建库主建单位的院校管理员：可管理整个库的内容
        if (libraryId.HasValue && libraryId.Value != Guid.Empty && tenantId.HasValue)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var library = await _libraryRepository.FindAsync(libraryId.Value);
                if (library != null &&
                    library.Visibility == ResourceLibraryVisibility.CoBuilt &&
                    library.OwnerTenantId == tenantId.Value)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
