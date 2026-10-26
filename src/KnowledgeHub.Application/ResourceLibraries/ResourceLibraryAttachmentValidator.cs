using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.ResourceLibraries;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库归属校验器。
///
/// <para>
/// 需求「资源库里对应的课程，资源，习题等也有专业维度的限制」：
/// 库可以绑定一个<b>主建专业</b>作为业务标签（用于门户展示、筛选、专业建设统计），
/// 但**不再限制**库内内容的专业 —— 课程 / 实训可以是多专业的，资源也都可以进库，
/// 不再要求「内容专业必须与库主建专业一致」。
/// </para>
///
/// <para>
/// <b>挂载范围</b>（仍然生效）：
/// <list type="bullet">
/// <item><description>本校主建的库 —— 任何本校教师都可挂。</description></item>
/// <item><description>
/// 院校共建库 —— 关联学校的教师也可把<b>本校</b>的内容挂进去（内容仍归本校租户，
/// 通过 LibraryId 跨租户指向该库）。
/// </description></item>
/// </list>
/// </para>
/// </summary>
public interface IResourceLibraryAttachmentValidator
{
    /// <summary>
    /// 校验内容是否允许挂到目标教学资源库；返回校验通过的资源库 Id（未指定库时返回 null）。
    /// </summary>
    /// <param name="libraryId">目标教学资源库 Id；为空表示不归属任何库。</param>
    /// <param name="currentTenantId">当前租户（学校）Id。</param>
    Task<Guid?> ValidateAsync(Guid? libraryId, Guid currentTenantId);
}

public class ResourceLibraryAttachmentValidator : IResourceLibraryAttachmentValidator, ITransientDependency
{
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IDataFilter _dataFilter;

    public ResourceLibraryAttachmentValidator(
        IResourceLibraryRepository libraryRepository,
        IDataFilter dataFilter)
    {
        _libraryRepository = libraryRepository;
        _dataFilter = dataFilter;
    }

    public async Task<Guid?> ValidateAsync(Guid? libraryId, Guid currentTenantId)
    {
        if (!libraryId.HasValue || libraryId.Value == Guid.Empty)
        {
            return null;
        }

        // 共建库的归属租户可能是别校，查询必须跨租户
        using (_dataFilter.Disable<IMultiTenant>())
        {
            // 必须带成员集合：共建库的「参与校」判定依赖 library.Members
            // （FindAsync 不会 Include，导致参与校老师挂内容被误判为无权）
            var library = await _libraryRepository.FindWithMembersAsync(libraryId.Value);
            if (library == null)
            {
                throw new UserFriendlyException("教学资源库不存在。");
            }

            var isOwner = library.OwnerTenantId == currentTenantId;
            var isPartner = library.Members.Any(m => m.MemberTenantId == currentTenantId);

            if (!isOwner && !(library.Visibility == ResourceLibraryVisibility.CoBuilt && isPartner))
            {
                throw new UserFriendlyException("只能把内容挂到本校主建或参与共建的教学资源库。");
            }

            // 注：不再校验「内容专业 = 库主建专业」——
            // 课程 / 实训支持多专业，资源专业可缺省，一律允许入库。
            return library.Id;
        }
    }
}
