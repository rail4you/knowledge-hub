using System;
using System.Threading.Tasks;
using KnowledgeHub.DoubleHigh.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace KnowledgeHub.DoubleHigh;

public interface IDoubleHighAppService : IApplicationService
{
    Task<DoubleHighProjectDto> GetAsync(Guid id);
    Task<DoubleHighProjectDetailDto> GetDetailAsync(Guid id);
    Task<PagedResultDto<DoubleHighProjectDto>> GetListAsync(PagedDoubleHighProjectRequestDto input);
    Task<DoubleHighProjectDto> CreateAsync(CreateUpdateDoubleHighProjectDto input);
    Task<DoubleHighProjectDto> UpdateAsync(Guid id, CreateUpdateDoubleHighProjectDto input);
    Task DeleteAsync(Guid id);
    Task<DoubleHighDashboardDto> CollectProjectAsync(Guid projectId);
    Task<DoubleHighIndicatorValueSnapshotDto> SaveManualValueAsync(SaveDoubleHighIndicatorValueDto input);
    Task<DoubleHighEvidenceDto> AddEvidenceAsync(CreateDoubleHighEvidenceDto input);
    Task DeleteEvidenceAsync(Guid id);
    Task<PagedResultDto<DoubleHighReportDto>> GetReportListAsync(GetDoubleHighReportsInput input);
    Task<IRemoteStreamContent> ExportReportAsync(Guid projectId);
}
