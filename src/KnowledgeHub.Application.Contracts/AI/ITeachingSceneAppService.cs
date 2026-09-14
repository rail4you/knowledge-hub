using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using KnowledgeHub.Application.AI.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// 教学场景模板：系统内置模板（只读）+ 本租户自定义场景的增删改查。
/// </summary>
public interface ITeachingSceneAppService : IApplicationService
{
    Task<List<TeachingSceneDto>> GetListAsync(TeachingSceneCategory? category = null);

    Task<TeachingSceneDto> CreateAsync(CreateUpdateTeachingSceneDto input);

    Task<TeachingSceneDto> UpdateAsync(Guid id, CreateUpdateTeachingSceneDto input);

    Task DeleteAsync(Guid id);

    /// <summary>把系统内置模板复制为“我的场景”，之后可自由编辑。</summary>
    Task<TeachingSceneDto> CopyToMineAsync(Guid id);
}
