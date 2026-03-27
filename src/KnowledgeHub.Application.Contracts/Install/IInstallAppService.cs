using System.Threading.Tasks;
using KnowledgeHub.Install.Dto;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Install;

public interface IInstallAppService : IApplicationService
{
    Task<InstallStatusDto> GetStatusAsync();
    Task InstallAsync(InstallInputDto input);
}