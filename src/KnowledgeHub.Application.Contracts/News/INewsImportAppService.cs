using System.Threading.Tasks;
using KnowledgeHub.News.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.News;

public interface INewsImportAppService : IApplicationService
{
    Task<NewsImportResultDto> ImportAsync(byte[] excelFile);
}
