using System.Threading.Tasks;
using KnowledgeHub.Users;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Users;

public interface IUserImportAppService : IApplicationService
{
    Task<UserImportResultDto> ImportAsync(byte[] excelFile);
}
