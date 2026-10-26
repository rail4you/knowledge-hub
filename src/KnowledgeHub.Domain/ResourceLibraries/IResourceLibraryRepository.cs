using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.ResourceLibraries;

public interface IResourceLibraryRepository : IRepository<ResourceLibrary, Guid>
{
    /// <summary>获取指定资源库的成员列表（跨租户：成员学校可能不是库归属租户）。</summary>
    Task<List<ResourceLibraryMember>> GetMembersAsync(Guid libraryId, CancellationToken cancellationToken = default);

    /// <summary>获取某学校参与共建的全部资源库（含作为主建学校与作为合作学校的）。</summary>
    Task<List<ResourceLibrary>> GetByMemberTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>判断某学校是否为指定资源库的成员。</summary>
    Task<bool> IsMemberAsync(Guid libraryId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取指定资源库分配到的教师列表（跨租户：教师可能来自关联学校）。
    /// </summary>
    Task<List<ResourceLibraryTeacher>> GetTeachersAsync(Guid libraryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取某个教师被分配到的全部资源库 Id（跨租户：共建库可能归属别的学校）。
    /// </summary>
    Task<List<Guid>> GetLibraryIdsByTeacherAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>判断教师是否被分配到指定资源库。</summary>
    Task<bool> IsTeacherAssignedAsync(Guid libraryId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取某学校可见的候选资源库：主建的 + 参与共建的（跨租户查询）。
    /// 「可见」还需结合可见性（私有 / 公开 / 共建）与教师分配，由上层判定。
    /// </summary>
    Task<List<ResourceLibrary>> GetByTenantOrMemberAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 跨租户取库，<b>并预加载共建成员</b>。
    ///
    /// <para>
    /// 必须预加载：ABP 未启用 EF 懒加载代理，直接访问实体的 <c>Members</c> 集合
    /// 会得到空集合 —— 共建库「本租户是否为关联学校」的判定会被静默判成 false。
    /// </para>
    /// </summary>
    Task<ResourceLibrary?> FindWithMembersAsync(Guid libraryId, CancellationToken cancellationToken = default);
}
