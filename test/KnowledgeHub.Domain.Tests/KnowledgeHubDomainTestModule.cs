using Volo.Abp.Modularity;

namespace KnowledgeHub;

[DependsOn(
    typeof(KnowledgeHubDomainModule),
    typeof(KnowledgeHubTestBaseModule)
)]
public class KnowledgeHubDomainTestModule : AbpModule
{

}
