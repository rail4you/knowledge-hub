using Volo.Abp.Modularity;

namespace KnowledgeHub;

public abstract class KnowledgeHubApplicationTestBase<TStartupModule> : KnowledgeHubTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
