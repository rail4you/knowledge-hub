using Localization.Resources.AbpUi;
using KnowledgeHub.Localization;
using Volo.Abp.Account;
using Volo.Abp.SettingManagement;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.HttpApi;
using Volo.Abp.Localization;
using Volo.Abp.TenantManagement;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Microsoft.Extensions.DependencyInjection;
using KnowledgeHub.LiveWs;

namespace KnowledgeHub;

 [DependsOn(
    typeof(KnowledgeHubApplicationContractsModule),
    typeof(AbpPermissionManagementHttpApiModule),
    typeof(AbpSettingManagementHttpApiModule),
    typeof(AbpAccountHttpApiModule),
    typeof(AbpIdentityHttpApiModule),
    typeof(AbpTenantManagementHttpApiModule),
    typeof(AbpTenantManagementDomainModule),
    typeof(AbpFeatureManagementHttpApiModule),
    typeof(AbpAspNetCoreMvcModule)
    )]
public class KnowledgeHubHttpApiModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(KnowledgeHubApplicationModule).Assembly);
        });

        // 关闭 ABP 的「自动防伪校验」。
        //
        // 背景：本平台是纯 JWT Bearer API（前端走 OAuth password flow，Authorization 头带 token），
        // 不存在 Cookie 认证，CSRF 防护在这里没有意义；但 ABP 的
        // AbpAutoValidateAntiforgeryTokenAuthorizationFilter 规则是：
        //   ① 请求带认证 Cookie → 校验
        //   ② 请求没带 XSRF-TOKEN Cookie → 跳过校验
        //   ③ 否则 → 校验
        // 浏览器里残留的 XSRF-TOKEN cookie 绑定的是「上一个登录用户」，
        // 换账号后发 POST/PUT/DELETE 会命中
        //   "The provided antiforgery token was meant for a different claims-based user than the current user."
        // → 400（表现为「创建失败」等莫名其妙的前端报错）。
        // 项目里其它手工控制器已普遍使用 [IgnoreAntiforgeryToken]，这里统一关掉自动校验，避免逐个漏改。
        Configure<AbpAntiForgeryOptions>(options =>
        {
            options.AutoValidate = false;
        });

        ConfigureLocalization();

        context.Services.AddSingleton<RecruitmentLiveWebSocketHandler>();
    }

    private void ConfigureLocalization()
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<KnowledgeHubResource>()
                .AddBaseTypes(
                    typeof(AbpUiResource)
                );
        });
    }
}
