using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Application.Contracts.Search;

public interface ISearchStatisticsAppService : IApplicationService
{
    Task<SearchDashboardDto> GetDashboardAsync(SearchStatsQueryDto input);
}
