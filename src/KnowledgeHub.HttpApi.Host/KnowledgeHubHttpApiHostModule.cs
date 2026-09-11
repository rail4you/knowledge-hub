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
                serverBuilder.SetIssuer(new Uri(configuration["AuthServer:Authority"]!));
            });
        }

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

    private void PreConfigureServicesPre75(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
        {
            options.AddDevelopmentEncryptionAndSigningCertificate = false;
        });

        PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
        {
            serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx", configuration["AuthServer:CertificatePassPhrase"]!);
            serverBuilder.SetIssuer(new Uri(configuration["AuthServer:Authority"]!));
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

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
        context.Services.Configure<ResourceMediaOptions>(configuration.GetSection("ResourceMedia"));
        // 上传大小限制：App:MaxFileSizeBytes（默认 500MB，env: App__MaxFileSizeBytes）
        context.Services.Configure<KnowledgeHub.Common.AppUploadOptions>(configuration.GetSection("App"));
        // 文件存储根路径：Storage:RootPath（默认 uploads，env: Storage__RootPath）
        context.Services.Configure<KnowledgeHub.Common.FileStorageOptions>(configuration.GetSection("Storage"));

        context.Services.AddHttpClient("LiteParse", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LiteParseOptions>>();
            client.BaseAddress = new Uri(options.Value.Host);
            client.Timeout = TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds);
        });

        context.Services.AddHttpClient(GotenbergConversionService.GotenbergHttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OfficeConversionOptions>>();
            client.BaseAddress = new Uri(options.Value.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.Value.ConversionTimeoutSeconds);
        });

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
        AddQueueServer("conversion", configuration.GetValue("Hangfire:Workers:Conversion", 1));
        AddQueueServer("ai", configuration.GetValue("Hangfire:Workers:Ai", 2));
        AddQueueServer("media", configuration.GetValue("Hangfire:Workers:Media", 2));
        context.Services.AddSingleton<IConversionTaskQueue, HangfireConversionTaskQueue>();
        // AI 生成任务队列（ai 队列，PostgreSQL 持久化）
        context.Services.AddSingleton<IAiTaskQueue, HangfireAiTaskQueue>();
        // 资源媒体处理队列（media 队列，缩略图/预览 ETL）
        context.Services.AddSingleton<IResourceMediaJobQueue, HangfireResourceMediaJobQueue>();

        context.Services.AddHttpClient<IMeiliSearchService, KnowledgeHub.Application.Search.MeiliSearchService>();
        context.Services.AddScoped<KnowledgeHub.Application.Search.MeiliSearchService>();
        // 复用下方注册的 "MeiliSearch" 命名客户端（连接池由 IHttpClientFactory 管理），
        // 避免每个请求 new HttpClient 导致 socket/TIME_WAIT 耗尽。
        context.Services.AddScoped<KnowledgeHub.Resources.ISearchService>(sp =>
            new global::KnowledgeHub.Resources.MeiliSearchService(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("MeiliSearch")));
        context.Services.AddHttpClient<IEmbeddingService, EmbeddingService>();
        context.Services.AddTransient<ITeachingAgentRuntimeClient, TeachingAgentRuntimeClient>();
        context.Services.AddScoped<ISearchAnalyticsService, SearchAnalyticsService>();
        context.Services.AddHttpClient<IMeiliSearchAdminAppService, MeiliSearchAdminAppService>();
        context.Services.AddHttpClient("MeiliSearch", client =>
        {
            client.BaseAddress = new Uri(configuration["Meilisearch:Host"] ?? "http://localhost:7700");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

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

        // 公共只读列表接口：允许浏览器私有缓存（60s），减少重复导航/刷新时的请求。
        // Vary 按租户/用户/语言分别缓存，避免不同上下文串数据。
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsGet(context.Request.Method) &&
                IsCacheablePublicListPath(context.Request.Path))
            {
                context.Response.OnStarting(() =>
                {
                    if (context.Response.StatusCode == StatusCodes.Status200OK)
                    {
                        context.Response.Headers.CacheControl = "private, max-age=60";
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
            RequestPath = "/uploads"
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

        app.UseCors();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

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
        RegisterOfficeConversionRecurringJobs(context);
        RegisterAiTaskRecoveryRecurringJob();
        RegisterResourceMediaRecurringJob();
        RegisterResourceMediaRecoveryRecurringJob();

        app.UseConfiguredEndpoints();
    }

    /// <summary>
    /// 命中浏览器私有缓存的公共只读列表接口路径（学生端资讯/资源/微专业/实训等）。
    /// </summary>
    private static bool IsCacheablePublicListPath(PathString path)
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
    /// 注册 Office 转换预热 RecurringJob（每 ReprocessPeriodMinutes 分钟跑一轮）。
    /// </summary>
    private void RegisterOfficeConversionRecurringJobs(ApplicationInitializationContext context)
    {
        var options = context.ServiceProvider
            .GetRequiredService<IOptions<OfficeConversionOptions>>().Value;
        if (options.ReprocessPeriodMinutes <= 0)
        {
            return;
        }

        RecurringJob.AddOrUpdate<OfficeConversionReprocessJob>(
            "office-conversion-reprocess",
            job => job.RunAsync(),
            Cron.MinuteInterval(Math.Max(1, options.ReprocessPeriodMinutes)),
            new RecurringJobOptions { QueueName = "conversion" });
    }
}
