using KnowledgeHub.Samples;
using Xunit;

namespace KnowledgeHub.EntityFrameworkCore.Domains;

[Collection(KnowledgeHubTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<KnowledgeHubEntityFrameworkCoreTestModule>
{

}
