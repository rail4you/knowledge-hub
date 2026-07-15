using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Application.AI.Summary;

public interface IDocumentSummaryService : ITransientDependency
{
    Task GenerateAndPersistAsync(DocumentSummaryGenerationJobArgs args);
}