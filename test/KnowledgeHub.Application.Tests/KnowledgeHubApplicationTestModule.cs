using Volo.Abp.Modularity;

namespace KnowledgeHub;

[DependsOn(
    typeof(KnowledgeHubApplicationModule),
    typeof(KnowledgeHubDomainTestModule)
)]
public class KnowledgeHubApplicationTestModule : AbpModule
{

}
