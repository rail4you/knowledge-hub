using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub;

public class KnowledgeHubDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    public async Task SeedAsync(DataSeedContext context)
    {
        // Tenant management is handled through the UI/API
        // Default tenants can be created programmatically if needed
    }
}
