using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Majors;
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
/// 把内容挂进教学资源库时，内容所属专业必须落在该库的<b>主建专业</b>上。
/// </para>
///
/// <para>
/// <b>挂载范围</b>：
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
    /// <param name="majorId">内容的专业 Id（主专业）。</param>
    /// <param name="currentTenantId">当前租户（学校）Id。</param>
    Task<Guid?> ValidateAsync(Guid? libraryId, Guid? majorId, Guid currentTenantId);
}

public class ResourceLibraryAttachmentValidator : IResourceLibraryAttachmentValidator, ITransientDependency
{
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IMajorRepository _majorRepository;
    private readonly IDataFilter _dataFilter;

    public ResourceLibraryAttachmentValidator(
        IResourceLibraryRepository libraryRepository,
        IMajorRepository majorRepository,
        IDataFilter dataFilter)
    {
        _libraryRepository = libraryRepository;
        _majorRepository = majorRepository;
        _dataFilter = dataFilter;
    }

    public async Task<Guid?> ValidateAsync(Guid? libraryId, Guid? majorId, Guid currentTenantId)
    {
        if (!libraryId.HasValue || libraryId.Value == Guid.Empty)
        {
            return null;
        }

        // 共建库的归属租户可能是别校，查询必须跨租户
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var library = await _libraryRepository.FindAsync(libraryId.Value);
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

            if (library.PrimaryMajorId.HasValue)
            {
                if (!majorId.HasValue)
                {
                    throw new UserFriendlyException(
                        $"教学资源库「{library.Name}」限定专业为「{await ResolveMajorNameAsync(library.PrimaryMajorId.Value)}」，请先为内容设置专业。");
                }

                if (majorId.Value != library.PrimaryMajorId.Value)
                {
                    throw new UserFriendlyException(
                        $"内容专业与教学资源库「{library.Name}」的主建专业不一致。");
                }
            }

            return library.Id;
        }
    }

    private async Task<string> ResolveMajorNameAsync(Guid majorId)
    {
        var major = await _majorRepository.FindAsync(majorId);
        return major?.Name ?? "该库的主建专业";
    }
}
