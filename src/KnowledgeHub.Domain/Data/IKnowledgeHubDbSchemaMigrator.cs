using System.Threading.Tasks;

namespace KnowledgeHub.Data;

public interface IKnowledgeHubDbSchemaMigrator
{
    Task MigrateAsync();
}
