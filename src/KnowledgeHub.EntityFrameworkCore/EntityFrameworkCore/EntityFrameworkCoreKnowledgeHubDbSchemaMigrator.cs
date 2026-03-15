using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using KnowledgeHub.Data;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.EntityFrameworkCore;

public class EntityFrameworkCoreKnowledgeHubDbSchemaMigrator
    : IKnowledgeHubDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreKnowledgeHubDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolving the KnowledgeHubDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<KnowledgeHubDbContext>()
            .Database
            .MigrateAsync();
    }
}
