using KnowledgeHub.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace KnowledgeHub.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(KnowledgeHubEntityFrameworkCoreModule),
    typeof(KnowledgeHubApplicationContractsModule)
)]
public class KnowledgeHubDbMigratorModule : AbpModule
{
}
