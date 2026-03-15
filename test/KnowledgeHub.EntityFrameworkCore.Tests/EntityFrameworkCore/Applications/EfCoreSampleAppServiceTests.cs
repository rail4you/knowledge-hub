using KnowledgeHub.Samples;
using Xunit;

namespace KnowledgeHub.EntityFrameworkCore.Applications;

[Collection(KnowledgeHubTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<KnowledgeHubEntityFrameworkCoreTestModule>
{

}
