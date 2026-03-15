using Volo.Abp.Modularity;

namespace KnowledgeHub;

/* Inherit from this class for your domain layer tests. */
public abstract class KnowledgeHubDomainTestBase<TStartupModule> : KnowledgeHubTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
