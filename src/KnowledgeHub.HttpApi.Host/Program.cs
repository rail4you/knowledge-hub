using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace KnowledgeHub;

public class Program
{
    public async static Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Async(c => c.File("Logs/logs.txt"))
            .WriteTo.Async(c => c.Console())
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting KnowledgeHub.HttpApi.Host.");
            var builder = WebApplication.CreateBuilder(args);

            // 上传文件大小限制（env: App__MaxFileSizeBytes，默认 500MB）。
            // 超大 PPTX 上传后由 PptxImagePreprocessor 预压缩，故放开到 500MB。
            var uploadOptions = KnowledgeHub.Common.AppUploadOptions.FromConfiguration(builder.Configuration);
            builder.WebHost.ConfigureKestrel((context, options) =>
            {
                options.Limits.MaxRequestBodySize = uploadOptions.MaxFileSizeBytes;
            });
            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = uploadOptions.MaxFileSizeBytes;
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartHeadersLengthLimit = int.MaxValue;
            });
            Log.Information("Step 1: WebApplication.CreateBuilder done");
            builder.Host
                .AddAppSettingsSecretsJson()
                .UseAutofac()
                .UseSerilog((context, services, loggerConfiguration) =>
                {
                    loggerConfiguration
                        .ReadFrom.Configuration(context.Configuration)
                        .ReadFrom.Services(services)
                        .WriteTo.Async(c => c.AbpStudio(services));
                });
            Log.Information("Step 2: Host configured");
            await builder.AddApplicationAsync<KnowledgeHubHttpApiHostModule>();
            Log.Information("Step 3: AddApplicationAsync done");
            var app = builder.Build();
            Log.Information("Step 4: Build done");
            await app.InitializeApplicationAsync();
            Log.Information("Step 5: InitializeApplicationAsync done");
            await app.RunAsync();
            Log.Information("Step 6: RunAsync done");
            return 0;
        }
        catch (Exception ex)
        {
            if (ex is HostAbortedException)
            {
                throw;
            }

            Log.Fatal(ex, "Host terminated unexpectedly!");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
