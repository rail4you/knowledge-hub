using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Studios.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Studios;

/// <summary>
/// 名师工作室。
///
/// <para>资料上传 → 领衔人审核 → 发布；资料文件复用资源中心（<c>Resource</c>）。</para>
/// </summary>
public interface IStudioAppService : IApplicationService
{
    /// <summary>我可见的工作室（我参与 + 本校公开的工作室）。</summary>
    Task<List<StudioListItemDto>> GetListAsync();

    Task<StudioDetailDto> GetAsync(Guid id);

    Task<StudioDetailDto> CreateAsync(CreateStudioDto input);

    Task<StudioDetailDto> UpdateAsync(Guid id, UpdateStudioDto input);

    Task DeleteAsync(Guid id);

    /// <summary>可加入的教师（同租户 Teacher，含是否已在工作室）。</summary>
    Task<List<StudioMemberDto>> GetCandidateTeachersAsync(Guid id);

    Task<List<StudioMemberDto>> AddMemberAsync(Guid id, StudioMemberInputDto input);

    Task RemoveMemberAsync(Guid id, Guid userId);

    /// <summary>成员上传（挂）资料到工作室。</summary>
    Task<StudioMaterialDto> CreateMaterialAsync(Guid id, CreateStudioMaterialDto input);

    /// <summary>重新提交（草稿 / 已驳回）。</summary>
    Task<StudioMaterialDto> SubmitMaterialAsync(Guid id, Guid materialId);

    /// <summary>领衔人审核：通过则发布。</summary>
    Task<StudioMaterialDto> ReviewMaterialAsync(Guid id, Guid materialId, ReviewStudioMaterialDto input);

    /// <summary>删除资料（上传者本人或领衔人）。</summary>
    Task DeleteMaterialAsync(Guid id, Guid materialId);
}
