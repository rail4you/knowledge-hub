using System;
using System.Threading.Tasks;
using KnowledgeHub.Majors;
using KnowledgeHub.ResourceLibraries;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 资源库归属校验器。
///
/// <para>
/// 需求「资源库里对应的课程，资源，习题等也有专业维度的限制」：
/// 把内容挂进资源库时，内容所属专业必须落在该库的<b>主建专业</b>上；
/// 且只能挂到本校（主建学校）拥有的资源库。
/// </para>
/// </summary>
public interface IResourceLibraryAttachmentValidator
{
    /// <summary>
    /// 校验内容是否允许挂到目标资源库；返回校验通过的资源库 Id（未指定库时返回 null）。
    /// </summary>
    /// <param name="libraryId">目标资源库 Id；为空表示不归属任何库。</param>
    /// <param name="majorId">内容的专业 Id（主专业）。</param>
    /// <param name="currentTenantId">当前租户（学校）Id。</param>
    Task<Guid?> ValidateAsync(Guid? libraryId, Guid? majorId, Guid currentTenantId);
}

public class ResourceLibraryAttachmentValidator : IResourceLibraryAttachmentValidator, ITransientDependency
{
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IMajorRepository _majorRepository;

    public ResourceLibraryAttachmentValidator(
        IResourceLibraryRepository libraryRepository,
        IMajorRepository majorRepository)
    {
        _libraryRepository = libraryRepository;
        _majorRepository = majorRepository;
    }

    public async Task<Guid?> ValidateAsync(Guid? libraryId, Guid? majorId, Guid currentTenantId)
    {
        if (!libraryId.HasValue || libraryId.Value == Guid.Empty)
        {
            return null;
        }

        var library = await _libraryRepository.FindAsync(libraryId.Value);
        if (library == null)
        {
            throw new UserFriendlyException("资源库不存在。");
        }

        if (library.OwnerTenantId != currentTenantId)
        {
            throw new UserFriendlyException("只能把内容挂到本校主建的资源库。");
        }

        if (library.PrimaryMajorId.HasValue)
        {
            if (!majorId.HasValue)
            {
                throw new UserFriendlyException(
                    $"资源库「{library.Name}」限定专业为「{await ResolveMajorNameAsync(library.PrimaryMajorId.Value)}」，请先为内容设置专业。");
            }

            if (majorId.Value != library.PrimaryMajorId.Value)
            {
                throw new UserFriendlyException(
                    $"内容专业与资源库「{library.Name}」的主建专业不一致。");
            }
        }

        return library.Id;
    }

    private async Task<string> ResolveMajorNameAsync(Guid majorId)
    {
        var major = await _majorRepository.FindAsync(majorId);
        return major?.Name ?? "该库的主建专业";
    }
}
