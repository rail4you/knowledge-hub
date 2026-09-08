using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Branding;

/// <summary>
/// 站点品牌设置：标题 / 副标题 / 页脚文本 / Logo。
/// 全局唯一（存 AbpSettings 全局），读取公开匿名，写入仅 host 超管。
/// </summary>
public interface IBrandingAppService : IApplicationService
{
    /// <summary>获取当前品牌配置（公开，首页/学生端匿名可调）</summary>
    Task<BrandingDto> GetAsync();

    /// <summary>更新品牌配置（仅 host 全局管理员）</summary>
    Task<BrandingDto> UpdateAsync(UpdateBrandingDto input);
}
