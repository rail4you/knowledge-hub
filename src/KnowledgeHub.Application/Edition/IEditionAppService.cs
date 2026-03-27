using System.Threading.Tasks;
using KnowledgeHub.Install.Dto;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Edition;

public interface IEditionAppService : IApplicationService
{
    Task<EditionDto> GetCurrentEditionAsync();
    
    Task UpgradeToStandardAsync(EditionUpgradeInputDto input);
}