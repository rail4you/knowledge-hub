using System.Threading.Tasks;
using KnowledgeHub.ResourceLibraries.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 公开门户「教学资源库」目录。
///
/// <para>
/// 原型 <c>#courses</c> 页面已改为资源库列表：校内资源库（本校主建的私有 / 公开 / 共建库）
/// 与公共共享资源库（他校公开或共建、对本校开放的库）分两个分区展示。
/// 匿名可访问（门户页无登录要求），但校内分区只返回当前租户的库。
/// </para>
/// </summary>
public interface ITeachingLibraryCatalogAppService : IApplicationService
{
    /// <summary>分页获取教学资源库目录。</summary>
    Task<PagedResultDto<TeachingLibraryCardDto>> GetListAsync(TeachingLibraryCatalogRequestDto input);
}
