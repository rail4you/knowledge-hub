using System;
using System.Net.Http;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;
using OpenIddict.Server.AspNetCore;
using Polly;
using Polly.Extensions.Http;
using KnowledgeHub.Resources;
using KnowledgeHub.EntityFrameworkCore;
using KnowledgeHub.MultiTenancy;
using KnowledgeHub.HealthChecks;
using KnowledgeHub.Resources.FileStorage;
using KnowledgeHub.Resources.Conversion;
using KnowledgeHub.Resources.Media;
using KnowledgeHub.Application.Search;
using KnowledgeHub.Application.Search.LiteParse;
using KnowledgeHub.Application.Contracts.Search;
using Microsoft.OpenApi;
using Volo.Abp;
using Volo.Abp.Studio;
using ResourcesSearchService = KnowledgeHub.Resources.MeiliSearchService;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations;
using Volo.Abp.Autofac;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Microsoft.AspNetCore.Hosting;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Swashbuckle;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.Security.Claims;
using Volo.Abp.UI.Navigation;
using KnowledgeHub.Web;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Hangfire.Redis.StackExchange;
using KnowledgeHub.Application.AI.Tasks;
using KnowledgeHub.HangfireJobs;
using KnowledgeHub.Json;
using KnowledgeHub.TeachingAgents;
using Volo.Abp.Json;
using KnowledgeHub.LiveWs;
using KnowledgeHub.Practicums.WasmMirrors;

namespace KnowledgeHub;

[DependsOn(
    typeof(KnowledgeHubHttpApiModule),
    typeof(AbpStudioClientAspNetCoreModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMultiTenancyModule),
    typeof(KnowledgeHubApplicationModule),
    typeof(KnowledgeHubEntityFrameworkCoreModule),
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpBackgroundJobsModule)
    )]
public class KnowledgeHubHttpApiHostModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("KnowledgeHub");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        if (hostingEnvironment.IsDevelopment())
        {
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.AddEphemeralSigningKey();
                serverBuilder.AddEncryptionKey(new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes(256 / 8)));
                serverBuilder.DisableAccessTokenEncryption();
                ApplyIssuerPolicy(configuration, serverBuilder);
            });
        }
        else if (TryGetProductionSigningCertificate(
                     configuration,
                     out var certificatePath,
                     out var certificatePassPhrase))
        {
            // 生产：用稳定的 pfx 证书签发/加密 token，容器重启后已签发的
            // access_token / refresh_token 仍然有效，避免浏览器带着旧 token 访问
            // 课程等页面时 401/404、必须手动刷新多次才能恢复。
            // 证书文件随镜像发布（见 csproj 的 Content Include），路径走
            // AuthServer:CertificatePath（默认 openiddict.pfx，生产用独立的
            // openiddict.production.pfx），密码走 AuthServer:CertificatePassPhrase
            //（生产通过 JWT_CERTIFICATE_PASSWORD 注入）。
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.AddProductionEncryptionAndSigningCertificate(
                    certificatePath,
                    certificatePassPhrase,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
                ApplyIssuerPolicy(configuration, serverBuilder);
            });
        }
        // else：保持 ABP 默认开发证书行为（与本次修改前生产实际走的分支一致），
        // 避免因证书缺失/密码错误导致启动崩溃。要启用稳定证书：确认证书文件
        // 已随镜像发布且密码正确（TryGetProductionSigningCertificate 会提前验证）。

        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new KnowledgeHubMenuContributor());
        });

        // Store all DateTime values as UTC in database.
        // Combined with UtcDateTimeConverter in JSON, ensures frontend
        // Angular DatePipe correctly converts UTC to China timezone (UTC+8).
        Configure<Volo.Abp.Timing.AbpClockOptions>(options =>
        {
            options.Kind = DateTimeKind.Utc;
        });
    }

    /// <summary>
    /// OpenIddict issuer 策略。
    ///
    /// <para>
    /// <b>默认（动态 issuer）：</b>不调用 <c>SetIssuer</c>，OpenIddict 从当前请求推导 issuer。
    /// 这是「域名绑定多租户」的前提 —— 每个学校域名签发自己域名的令牌，
    /// SPA（origin = 当前域名）拿到的令牌 <c>iss</c> 与浏览器 origin 一致，discovery 校验才能通过。
    /// 平台域名下推导出的 issuer 与原先写死的 <c>AuthServer:Authority</c> 相同，行为不变。
    /// </para>
    ///
    /// <para>
    /// <b>回退开关：</b>配置 <c>AuthServer:StaticIssuer=true</c> 时恢复写死 issuer
    /// （用于反代未正确透传 X-Forwarded-Proto 的老部署）。
    /// </para>
    ///
    /// <para>
    /// <b>前提：</b>反代必须把<b>原始协议</b>透传给 API（容器内 nginx 用
    /// <c>map $http_x_forwarded_proto</c> 保留外层传下来的值），否则推导出的 scheme 会是 http，
    /// 与浏览器的 https origin 不一致。
    /// </para>
    /// </summary>
    private static void ApplyIssuerPolicy(
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        OpenIddictServerBuilder serverBuilder)
    {
        var useStaticIssuer = configuration.GetValue<bool>("AuthServer:StaticIssuer");
        if (!useStaticIssuer)
        {
            // 动态 issuer：OpenIddict 按请求 URL 推导
            return;
        }

        var authority = configuration["AuthServer:Authority"];
        if (!string.IsNullOrWhiteSpace(authority))
        {
            serverBuilder.SetIssuer(new Uri(authority));
        }
    }

    /// <summary>
    /// 校验生产签名证书是否可用：文件存在且密码正确。
    /// 提前在 PreConfigureServices 阶段验证，避免把会抛异常的配置注册进去导致启动崩溃；
    /// 不可用时调用方保持 ABP 默认行为（本次修改前生产实际走的就是该分支）。
    /// 证书路径走 AuthServer:CertificatePath（默认 openiddict.pfx），支持绝对路径，
    /// 相对路径按应用基目录（容器内 /app）解析。
    /// </summary>
    private static bool TryGetProductionSigningCertificate(
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        out string certificatePath,
        out string certificatePassPhrase)
    {
        certificatePath = configuration["AuthServer:CertificatePath"];
        if (string.IsNullOrWhiteSpace(certificatePath))
        {
            certificatePath = "openiddict.pfx";
        }
        if (!Path.IsPathRooted(certificatePath))
        {
            certificatePath = Path.Combine(AppContext.BaseDirectory, certificatePath);
        }

        certificatePassPhrase = configuration["AuthServer:CertificatePassPhrase"]!;
        try
        {
            if (string.IsNullOrWhiteSpace(certificatePassPhrase))
            {
                return false;
            }

            if (!File.Exists(certificatePath))
            {
                return false;
            }

            using var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath,
                certificatePassPhrase,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
            return certificate.HasPrivateKey;
        }
        catch
        {
            return false;
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        // ------------------------------------------------------------------
        // 域名 → 租户 解析：注册到解析链 index 0，域名优先于令牌。
        //
        // ABP 默认链为 CurrentUser(令牌) → QueryString → Route → Header → Cookie，
        // 令牌优先会让「拿 A 校令牌访问 B 校域名」仍显示 A 校内容，与「账户也是自己租户的」相悖。
        // 本模块是依赖链最外层，其 Configure 一定晚于 AbpMultiTenancyModule（插入 CurrentUser）
        // 与 AbpAspNetCoreMultiTenancyModule（插入 QueryString/Route/Header/Cookie），
        // 因此 Insert(0) 能保证最终顺序为：学校域名 → 令牌 → 头/Cookie。
        //
        // 平台域名（如 www.iketong.com）不匹配任何学校域名 → 不处理 → 行为与改造前完全一致。
        // ------------------------------------------------------------------
        Configure<Volo.Abp.MultiTenancy.AbpTenantResolveOptions>(options =>
        {
            options.TenantResolvers.Insert(0, new SchoolDomainTenantResolveContributor());
        });

        if (!configuration.GetValue<bool>("App:DisablePII"))
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }

        if (!configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata"))
        {
            Configure<OpenIddictServerAspNetCoreOptions>(options =>
            {
                options.DisableTransportSecurityRequirement = true;
            });
            
            Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        Configure<Microsoft.AspNetCore.Identity.IdentityOptions>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
            options.Password.RequiredUniqueChars = 1;
        });

        ConfigureStudio(hostingEnvironment);
        ConfigureAuthentication(context);
        ConfigureUrls(configuration);
        ConfigureBundles(hostingEnvironment);
        ConfigureConventionalControllers();
        ConfigureHealthChecks(context);
        ConfigureSwagger(context, configuration);
        ConfigureVirtualFileSystem(context);
        ConfigureCors(context, configuration);

        // 内存缓存：资源文件预览状态/路径的短时缓存，降低轮询压力
        context.Services.AddMemoryCache();

        // 响应压缩：ABP API 大量返回 JSON（列表/详情），Brotli/Gzip 可显著降低传输体积。
        // 仅压缩默认文本类 MIME（application/json 等），PDF/视频等已压缩二进制不处理。
        context.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        context.Services.Configure<BrotliCompressionProviderOptions>(options =>
            options.Level = System.IO.Compression.CompressionLevel.Fastest);
        context.Services.Configure<GzipCompressionProviderOptions>(options =>
            options.Level = System.IO.Compression.CompressionLevel.Fastest);

        context.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        
        context.Services.Configure<MeilisearchOptions>(configuration.GetSection("Meilisearch"));
        context.Services.Configure<EmbeddingServiceOptions>(configuration.GetSection("EmbeddingService"));
        context.Services.Configure<LiteParseOptions>(configuration.GetSection("Liteparse"));
        context.Services.Configure<WasmMirrorOptions>(configuration.GetSection("WasmMirror"));
        // 遗留节点：仍绑定以兼容旧配置文件，实际生效值合并进 ResourceProcessingFlow
#pragma warning disable CS0618 // ResourceMediaOptions 已合并，仅作兼容映射
        context.Services.Configure<ResourceMediaOptions>(configuration.GetSection("ResourceMedia"));
#pragma warning restore CS0618
        // 资源处理流水线触发时机：上传→草稿，发布→预览，院校审核→索引，联盟审核→学生可见
        // 唯一真相源；旧 ResourceMedia 节点的显式配置会在下方 PostConfigure 中映射进来。
        context.Services.Configure<ResourceProcessingFlowOptions>(configuration.GetSection("ResourceProcessingFlow"));
        context.Services.PostConfigure<ResourceProcessingFlowOptions>(flow =>
        {
            var legacy = configuration.GetSection("ResourceMedia");
            ResourceProcessingFlowMapper.ApplyLegacy(
                flow,
                legacy.GetValue<bool?>("StartAfterIndexing"),
                legacy.GetValue<bool?>("GenerateOnApproval"));
        });
        // 上传大小限制：App:MaxFileSizeBytes（默认 500MB，env: App__MaxFileSizeBytes）
        context.Services.Configure<KnowledgeHub.Common.AppUploadOptions>(configuration.GetSection("App"));
        // 文件存储根路径：Storage:RootPath（默认 uploads，env: Storage__RootPath）
        context.Services.Configure<KnowledgeHub.Common.FileStorageOptions>(configuration.GetSection("Storage"));

        // 统一的 Polly 瞬时故障重试策略：5xx / 408 / 网络异常 / 429，指数退避重试 3 次。
        // 用于 Gotenberg 转换、Meili 索引、LiteParse 解析、Embedding 等外部调用，
        // 降低瞬时抖动导致的"生成失败"；重试仍失败则任务标记失败，由用户在跟踪页手动重启。
        IAsyncPolicy<HttpResponseMessage> transientRetry() =>
            HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

        context.Services.AddHttpClient("LiteParse", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LiteParseOptions>>();
            client.BaseAddress = new Uri(options.Value.Host);
            client.Timeout = TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds);
        }).AddPolicyHandler(transientRetry());

        context.Services.AddHttpClient(GotenbergConversionService.GotenbergHttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OfficeConversionOptions>>();
            client.BaseAddress = new Uri(options.Value.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.Value.ConversionTimeoutSeconds);
        }).AddPolicyHandler(transientRetry());

        // ── Hangfire 队列（PostgreSQL 持久化，重启不丢任务）──
        // 转换任务与 AI 生成任务分别走 conversion / ai 队列，互相隔离、可并发。
        context.Services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(Hangfire.CompatibilityLevel.Version_180);
            config.UseSimpleAssemblyNameTypeSerializer();
            config.UseRecommendedSerializerSettings();

            // 存储后端：Hangfire:Storage = Postgres（默认）| Redis
            // Redis 可把作业存储从 PostgreSQL 卸载到 Redis（复用已有的 Redis 容器），
            // 避免后台任务轮询与 API 请求争抢 PG 连接池、降低持久化开销。
            var storage = configuration["Hangfire:Storage"] ?? "Postgres";
            if (string.Equals(storage, "Redis", StringComparison.OrdinalIgnoreCase))
            {
                var redisConnection = configuration["Hangfire:Redis:ConnectionString"];
                if (string.IsNullOrWhiteSpace(redisConnection))
                {
                    redisConnection = configuration["Redis:Configuration"];
                }
                if (string.IsNullOrWhiteSpace(redisConnection))
                {
                    throw new AbpInitializationException(
                        "Hangfire:Storage=Redis 但未配置 Hangfire:Redis:ConnectionString 或 Redis:Configuration");
                }

                config.UseRedisStorage(redisConnection, new RedisStorageOptions
                {
                    Prefix = configuration["Hangfire:Redis:Prefix"] ?? "{knowledgehub-hangfire}:",
                    Db = configuration.GetValue("Hangfire:Redis:Db", 0),
                });
            }
            else
            {
                // PostgreSQL：使用独立连接串（ConnectionStrings:Hangfire），
                // 避免与 API 请求争抢同一个连接池；未配置时回退到 Default。
                var hangfireConnectionString = configuration.GetConnectionString("Hangfire")
                    ?? configuration.GetConnectionString("Default");
                config.UsePostgreSqlStorage(
                    hangfireConnectionString,
                    new Hangfire.PostgreSql.PostgreSqlStorageOptions
                    {
                        SchemaName = "hangfire",
                        PrepareSchemaIfNecessary = true
                    });
            }
        });

        // ── 按队列拆分独立 BackgroundJobServer ──
        // 每个队列一个 server，独立 WorkerCount：长任务（AI / 转换 / 媒体）互不阻塞，
        // 也不会因某个队列积压占满共享 worker 导致其他队列饥饿。
        // WorkerCount 由 Hangfire:Workers:{Queue} 配置，<=0 表示不启动该队列的 server。
        void AddQueueServer(string queue, int workerCount)
        {
            if (workerCount <= 0)
            {
                return;
            }

            context.Services.AddHangfireServer(options =>
            {
                options.ServerName = $"knowledgehub-{queue}";
                options.Queues = new[] { queue };
                options.WorkerCount = workerCount;
            });
        }

        AddQueueServer("default", configuration.GetValue("Hangfire:Workers:Default", 2));
        AddQueueServer("ai", configuration.GetValue("Hangfire:Workers:Ai", 2));
        AddQueueServer("media", configuration.GetValue("Hangfire:Workers:Media", 2));
        // 索引任务（文档/视频）也走 Hangfire 持久化，避免重启丢失、job 表永久 Pending
        AddQueueServer("indexing", configuration.GetValue("Hangfire:Workers:Indexing", 2));
        // AI 生成任务队列（ai 队列，PostgreSQL 持久化）
        context.Services.AddSingleton<IAiTaskQueue, HangfireAiTaskQueue>();
        // 资源媒体处理队列（media 队列，缩略图/预览 ETL）
        context.Services.AddSingleton<IResourceMediaJobQueue, HangfireResourceMediaJobQueue>();
        // 文档/视频索引队列（indexing 队列，Hangfire 持久化）
        context.Services.AddSingleton<KnowledgeHub.Search.Indexing.IIndexingJobQueue, HangfireIndexingJobQueue>();
        // 智学分析队列（试卷 OCR / 知识点抽取 / 模型训练 / 先修挖掘）
        context.Services.AddSingleton<KnowledgeHub.LearningAnalytics.ILearningAnalyticsJobQueue, HangfireLearningAnalyticsJobQueue>();

        context.Services.AddHttpClient<IMeiliSearchService, KnowledgeHub.Application.Search.MeiliSearchService>()
            .AddPolicyHandler(transientRetry());
        context.Services.AddScoped<KnowledgeHub.Application.Search.MeiliSearchService>();
        // 复用下方注册的 "MeiliSearch" 命名客户端（连接池由 IHttpClientFactory 管理），
        // 避免每个请求 new HttpClient 导致 socket/TIME_WAIT 耗尽。
        context.Services.AddScoped<KnowledgeHub.Resources.ISearchService>(sp =>
            new global::KnowledgeHub.Resources.MeiliSearchService(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("MeiliSearch")));
        context.Services.AddHttpClient<IEmbeddingService, EmbeddingService>()
            .AddPolicyHandler(transientRetry());
        context.Services.AddTransient<ITeachingAgentRuntimeClient, TeachingAgentRuntimeClient>();
        context.Services.AddScoped<ISearchAnalyticsService, SearchAnalyticsService>();
        context.Services.AddHttpClient<IMeiliSearchAdminAppService, MeiliSearchAdminAppService>()
            .AddPolicyHandler(transientRetry());
        context.Services.AddHttpClient("MeiliSearch", client =>
        {
            client.BaseAddress = new Uri(configuration["Meilisearch:Host"] ?? "http://localhost:7700");
            client.Timeout = TimeSpan.FromSeconds(30);
        }).AddPolicyHandler(transientRetry());

        // HTTP 代理：用于 HTTPS 页面加载 HTTP 仿真资源（Unity WebGL 等）
        context.Services.AddHttpClient("HttpProxy", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(15);
        });

        // 图片代理：用于前端画布合成证书（跨域读取 OSS 图片，避免画布被污染）
        context.Services.AddHttpClient("ImageProxy", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.IsJobExecutionEnabled = true;
        });

        Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new NullableGuidConverter());
            // Serialize DateTime as UTC to fix timezone display on frontend.
            // PostgreSQL stores timestamp without timezone; without this,
            // dates are serialized without Z suffix and Angular DatePipe treats them
            // as local server time (UTC) instead of converting to China timezone (UTC+8).
            options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
            options.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
        });
    }

    private void ConfigureStudio(IHostEnvironment hostingEnvironment)
    {
        if (hostingEnvironment.IsProduction())
        {
            Configure<AbpStudioClientOptions>(options =>
            {
                options.IsLinkEnabled = false;
            });
        }
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
            options.Applications["Angular"].RootUrl = configuration["App:AngularUrl"];
            options.Applications["Angular"].Urls[AccountUrlNames.PasswordReset] = "account/reset-password";
            options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"]?.Split(',') ?? Array.Empty<string>());
        });
    }

    private void ConfigureBundles(IHostEnvironment hostingEnvironment)
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );

            options.ScriptBundles.Configure(
                LeptonXLiteThemeBundles.Scripts.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-scripts.js");
                    if (hostingEnvironment.IsDevelopment())
                    {
                        bundle.AddFiles("/dev-login-helper.js");
                    }
                }
            );
        });
    }


    private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<KnowledgeHubDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}KnowledgeHub.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<KnowledgeHubDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}KnowledgeHub.Domain"));
                options.FileSets.ReplaceEmbeddedByPhysical<KnowledgeHubApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}KnowledgeHub.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<KnowledgeHubApplicationModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}KnowledgeHub.Application"));
            });
        }
    }

    private void ConfigureConventionalControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(KnowledgeHubApplicationModule).Assembly);
        });
    }

    private static void ConfigureSwagger(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAbpSwaggerGenWithOidc(
            configuration["AuthServer:Authority"]!,
            ["KnowledgeHub"],
            [AbpSwaggerOidcFlows.AuthorizationCode],
            null,
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "KnowledgeHub API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            });
    }

    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(
                        configuration["App:CorsOrigins"]?
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim().RemovePostFix("/"))
                            .ToArray() ?? Array.Empty<string>()
                    )
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    private void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddKnowledgeHubHealthChecks();
    }


    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        app.UseForwardedHeaders();

        // 响应压缩需尽早注册，才能覆盖后续中间件写出的响应体
        app.UseResponseCompression();

        // 公共只读列表接口：禁用浏览器 HTTP 缓存。
        //
        // 原因：这些数据可被后台随时改动（资讯下架/上架、实训发布、资源审核等），
        // 一旦允许浏览器缓存 60s，后台改完到用户看到之间会出现最长 60s 的数据陈旧；
        // 而且这层缓存在浏览器里，后台的写操作**无法失效它**——前端即使清了自己的
        // 内存缓存，重新请求时浏览器仍会直接回放旧副本。
        //
        // 「减少重复导航/刷新时的请求」这个职责由前端的 ClientCacheService 承担
        // （同样 60s TTL，但后台写操作后会通过拦截器精确失效，见
        // angular/src/app/shared/cache/cache-invalidation.interceptor.ts）。
        // Vary 仍按租户/用户/语言区分，避免中间代理串数据。
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsGet(context.Request.Method) &&
                IsPublicListPath(context.Request.Path))
            {
                context.Response.OnStarting(() =>
                {
                    if (context.Response.StatusCode == StatusCodes.Status200OK)
                    {
                        context.Response.Headers.CacheControl = "no-store";
                        context.Response.Headers.Append("Vary", "__tenant");
                        context.Response.Headers.Append("Vary", "Authorization");
                        context.Response.Headers.Append("Vary", "Accept-Language");
                    }
                    return Task.CompletedTask;
                });
            }
            await next();
        });

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // WebSocket 中间件必须放在最前面，
        // 否则 UseAbpRequestLocalization 等中间件可能阻止 WebSocket 升级
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        });

        // 映射 WebSocket 端点（在 UseRouting 之前，避免被路由系统当成未知方法返回 405）
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/api/recruitment-live/ws" && context.WebSockets.IsWebSocketRequest)
            {
                var handler = context.RequestServices.GetRequiredService<RecruitmentLiveWebSocketHandler>();
                var ws = await context.WebSockets.AcceptWebSocketAsync();
                await handler.HandleAsync(ws, context);
                return;
            }
            await next();
        });

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
        }

        app.UseRouting();
        // CORS 需在静态文件（/uploads）之前注册：否则静态文件中间件直接短路响应，
        // 媒体文件响应缺少 Access-Control-Allow-Origin，前端 fetch 下载会失败并退回新窗口打开。
        app.UseCors();
        app.UseMiddleware<GrantAllPoliciesMiddleware>();
        app.MapAbpStaticAssets();
        app.UseAbpStudioLink();
        app.UseAbpSecurityHeaders();

        var fileStorageRoot = context.ServiceProvider
            .GetRequiredService<IOptions<KnowledgeHub.Common.FileStorageOptions>>()
            .Value.ResolveRootPath(env.ContentRootPath);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(fileStorageRoot),
            RequestPath = "/uploads",
            OnPrepareResponse = ctx =>
            {
                // 安全：上传目录可能被写入任意扩展名文件，禁止浏览器内联渲染可脚本化类型，
                // 避免同源存储型 XSS（html/svg/js/xml 一律作为附件下载）。
                ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                var path = ctx.File.Name;
                var ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
                if (ext is ".html" or ".htm" or ".xhtml" or ".svg" or ".js" or ".mjs"
                    or ".xml" or ".xsl" or ".swf" or ".wasm")
                {
                    ctx.Context.Response.Headers["Content-Disposition"] = "attachment";
                    ctx.Context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
                }
            }
        });

        // 仿真实训 WASM 本地镜像：开发/容器场景下用 Kestrel 兜底托管。
        // 生产场景主要由 etc/docker/nginx-proxy.conf 直接读取 /wasm/，此处仍保留以便诊断。
        var wasmOptions = context.ServiceProvider
            .GetRequiredService<IOptions<WasmMirrorOptions>>().Value;
        if (wasmOptions.Enabled)
        {
            var resolvedRoot = Path.IsPathRooted(wasmOptions.RootPath)
                ? wasmOptions.RootPath
                : Path.GetFullPath(Path.Combine(env.ContentRootPath, wasmOptions.RootPath));

            // 拒绝包含 ".." 的路径段
            var segments = resolvedRoot.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!segments.Any(s => s == "..") && Directory.Exists(resolvedRoot))
            {
                var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
                provider.Mappings[".wasm"] = "application/wasm";
                provider.Mappings[".unityweb"] = "application/octet-stream";
                provider.Mappings[".data"] = "application/octet-stream";
                provider.Mappings[".br"] = "application/octet-stream";
                provider.Mappings[".mem"] = "application/octet-stream";
                provider.Mappings[".symbols"] = "application/octet-stream";
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(resolvedRoot),
                    RequestPath = wasmOptions.PublicBasePath,
                    ContentTypeProvider = provider,
                    ServeUnknownFileTypes = true,
                    DefaultContentType = "application/octet-stream",
                    OnPrepareResponse = ctx =>
                    {
                        ctx.Context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                        ctx.Context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
                        // 禁用 HTTP 缓存，确保浏览器获取最新镜像文件
                        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                        ctx.Context.Response.Headers["Pragma"] = "no-cache";
                        ctx.Context.Response.Headers["Expires"] = "0";
                    }
                });
            }
            else
            {
                context.ServiceProvider
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("WasmMirror")
                    .LogWarning(
                        "WasmMirror root not found or unsafe path: {Path}; wasm static serving disabled.",
                        resolvedRoot);
            }
        }

        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        // 域名绑定：令牌所属租户与域名解析出的租户不一致时显式拒绝（跨校账号访问他校域名）。
        // 必须放在 UseMultiTenancy 之后 —— 那时 CurrentTenant 才是域名解析的结果。
        app.UseMiddleware<SchoolDomainTenantGuardMiddleware>();

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();

        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "KnowledgeHub API");

            var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
            options.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
        });

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();

        // ── Hangfire Dashboard + RecurringJob ──
        // Dashboard 路径 /hangfire。生产环境请通过 nginx/IP 白名单限制访问。
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
        });
        RegisterAiTaskRecoveryRecurringJob();
        RegisterResourceMediaRecurringJob();
        RegisterResourceMediaRecoveryRecurringJob();
        RegisterIndexingTaskRecoveryRecurringJob();
        RegisterLearningAnalyticsNightlyRecurringJob();

        app.UseConfiguredEndpoints();
    }

    /// <summary>
    /// 公共只读列表接口路径（学生端资讯/资源/微专业/实训等）。
    /// 这些接口响应带 no-store，禁止浏览器 HTTP 缓存，避免后台改动后出现数据陈旧。
    /// </summary>
    private static bool IsPublicListPath(PathString path)
    {
        return path.StartsWithSegments("/api/app/news-article/published-list")
            || path.StartsWithSegments("/api/app/news-article/hot-list")
            || path.StartsWithSegments("/api/app/news-category/tree")
            || path.StartsWithSegments("/api/app/resource/filtered-list")
            || path.StartsWithSegments("/api/app/resource/categories")
            || path.StartsWithSegments("/api/app/micro-major/published")
            || path.StartsWithSegments("/api/app/practicum/published");
    }

    /// <summary>
    /// 注册 AI 任务恢复 RecurringJob（每 5 分钟）：清理中断的 Running 任务。
    /// </summary>
    private void RegisterAiTaskRecoveryRecurringJob()
    {
        RecurringJob.AddOrUpdate<AiTaskRecoveryService>(
            "ai-task-recovery",
            job => job.RecoverAsync(),
            Cron.MinuteInterval(5),
            new RecurringJobOptions { QueueName = "default" });
    }

    /// <summary>
    /// 注册资源媒体处理维护 RecurringJob（每 30 分钟）：回填遗留资源 + 清理孤儿生成物。
    /// </summary>
    private void RegisterResourceMediaRecurringJob()
    {
        RecurringJob.AddOrUpdate<ResourceMediaMaintenanceJob>(
            "resource-media-maintenance",
            job => job.RunAsync(),
            Cron.MinuteInterval(30),
            new RecurringJobOptions { QueueName = "media" });
    }

    /// <summary>
    /// 注册媒体任务恢复 RecurringJob（每 5 分钟）：把中断的 Running 任务标记为失败。
    /// </summary>
    private void RegisterResourceMediaRecoveryRecurringJob()
    {
        RecurringJob.AddOrUpdate<ResourceMediaTaskRecoveryService>(
            "resource-media-recovery",
            job => job.RecoverAsync(),
            Cron.MinuteInterval(5),
            new RecurringJobOptions { QueueName = "media" });
    }

    /// <summary>
    /// 注册智学分析夜间任务（每天 03:00）：掌握度模型重训 + 先修关系挖掘（+ 可选开课前衔接推送）。
    /// </summary>
    private void RegisterLearningAnalyticsNightlyRecurringJob()
    {
        RecurringJob.AddOrUpdate<KnowledgeHub.LearningAnalytics.LearningAnalyticsNightlyJob>(
            "learning-analytics-nightly",
            job => job.ExecuteAsync(),
            "0 3 * * *",
            new RecurringJobOptions { QueueName = "default" });
    }

    /// <summary>
    /// 注册索引任务恢复 RecurringJob（每 10 分钟）：把中断/超时或过期排队的文档/视频索引任务标记失败。
    /// </summary>
    private void RegisterIndexingTaskRecoveryRecurringJob()
    {
        RecurringJob.AddOrUpdate<KnowledgeHub.Search.Indexing.IndexingTaskRecoveryService>(
            "indexing-task-recovery",
            job => job.RecoverAsync(),
            Cron.MinuteInterval(10),
            new RecurringJobOptions { QueueName = "default" });
    }
}
