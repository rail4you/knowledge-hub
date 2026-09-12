using KnowledgeHub.Resources;
using KnowledgeHub.EntityFrameworkCore;
using KnowledgeHub.Application.Search;
using KnowledgeHub.Application.Search.LiteParse;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Resources.Conversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Account;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub;

 [DependsOn(
    typeof(KnowledgeHubDomainModule),
    typeof(KnowledgeHubApplicationContractsModule),
    typeof(KnowledgeHubEntityFrameworkCoreModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpAccountApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule)
    )]
public class KnowledgeHubApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        context.Services.Configure<OfficeConversionOptions>(
            configuration.GetSection("OfficeConversion"));

        context.Services.AddSingleton<IDocumentExtractionService, LiteParseDocumentExtractionService>();
        context.Services.AddSingleton<ILiteParseExtractionService, LiteParseDocumentExtractionService>();
        context.Services.AddSingleton<Practicums.PracticumChatConnectionManager>();
        context.Services.AddTransient<TeachingAgents.TeachingAgentContextBuilder>();
        // 必须 Singleton：转换服务内部的并发闸门 + in-flight 去重要全局共享，
        // 若为 Transient 则每个请求各持有一份状态，并发限制形同虚设，会打爆服务器。
        context.Services.AddSingleton<IOfficeConversionService, GotenbergConversionService>();
        // 动态并发管理器（每类服务一个可在线调整的闸门）
        context.Services.AddSingleton<ConversionConcurrencyManager>();
        // PPTX 大媒体预压缩（GIF/大图 ffmpeg 压小后再喂 LibreOffice）
        context.Services.AddSingleton<PptxImagePreprocessor>();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // Qwen 动态 Key 解析：静态 QwenClient 经此 scope 工厂读取数据库配置，
        // 管理页换 Key 后（缓存 5 分钟或主动清理）新调用即生效，无需重启。
        QwenClient.ScopeFactory = context.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
    }
}
