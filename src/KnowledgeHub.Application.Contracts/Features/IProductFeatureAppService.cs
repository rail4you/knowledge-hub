using System.Threading.Tasks;
using KnowledgeHub.Features.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Features;

/// <summary>
/// 产品能力开关查询（只读）。
///
/// <para>
/// 前端在启动时读取一次，用于决定「联盟协同 / 特殊教育」等增值菜单是否注册。
/// 取代了早期「安装授权 / 版本升级」的 Edition 接口。
/// </para>
/// </summary>
public interface IProductFeatureAppService : IApplicationService
{
    /// <summary>读取当前产品能力开关（匿名可访问：登录页也要能加载）。</summary>
    Task<ProductFeatureDto> GetAsync();
}
