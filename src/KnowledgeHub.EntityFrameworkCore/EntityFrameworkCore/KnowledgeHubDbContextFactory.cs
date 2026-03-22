using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace KnowledgeHub.EntityFrameworkCore;

/* This class is needed for EF Core console commands
 * (like Add-Migration and Update-Database commands) */
public class KnowledgeHubDbContextFactory : IDesignTimeDbContextFactory<KnowledgeHubDbContext>
{
    public KnowledgeHubDbContext CreateDbContext(string[] args)
    {
        // https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        
        var configuration = BuildConfiguration();
        
        KnowledgeHubEfCoreEntityExtensionMappings.Configure();

        var builder = new DbContextOptionsBuilder<KnowledgeHubDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Default"));
        
        return new KnowledgeHubDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../KnowledgeHub.HttpApi.Host/"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables();

        return builder.Build();
    }
}
