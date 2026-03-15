using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Data;

/* This is used if database provider does't define
 * IKnowledgeHubDbSchemaMigrator implementation.
 */
public class NullKnowledgeHubDbSchemaMigrator : IKnowledgeHubDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
