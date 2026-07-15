using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Search;

public interface ISummaryGenerationAppService : IApplicationService
{
    Task<GenerateSummaryResultDto> GenerateForResourceAsync(GenerateSummaryInputDto input);

    Task<BatchGenerateSummaryResultDto> BatchGenerateAsync(BatchGenerateSummaryInputDto input);
}