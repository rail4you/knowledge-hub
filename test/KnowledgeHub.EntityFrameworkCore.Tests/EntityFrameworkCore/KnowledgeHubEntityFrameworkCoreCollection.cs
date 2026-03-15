using Xunit;

namespace KnowledgeHub.EntityFrameworkCore;

[CollectionDefinition(KnowledgeHubTestConsts.CollectionDefinitionName)]
public class KnowledgeHubEntityFrameworkCoreCollection : ICollectionFixture<KnowledgeHubEntityFrameworkCoreFixture>
{

}
