using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.MicroMajors.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.MicroMajors;

public interface IMicroMajorAppService : IApplicationService
{
    Task<MicroMajorDto> GetAsync(Guid id);
    Task<MicroMajorDetailDto> GetDetailAsync(Guid id);
    Task<PagedResultDto<MicroMajorDto>> GetListAsync(PagedMicroMajorRequestDto input);
    Task<PagedResultDto<MicroMajorDto>> GetPublishedAsync(PagedMicroMajorRequestDto input);
    Task<List<MicroMajorEnrollmentDto>> GetMyEnrollmentsAsync();
    Task<List<MicroMajorCertificateDto>> GetMyCertificatesAsync();
    Task<List<MicroMajorResourceDto>> GetResourcesAsync(Guid microMajorId);
    Task<PagedResultDto<MicroMajorEnrollmentDto>> GetEnrollmentListAsync(GetMicroMajorEnrollmentsInput input);
    Task<MicroMajorDto> CreateAsync(CreateUpdateMicroMajorDto input);
    Task<MicroMajorDto> UpdateAsync(Guid id, CreateUpdateMicroMajorDto input);
    Task DeleteAsync(Guid id);
    Task EnrollAsync(Guid microMajorId);
    Task ApproveEnrollmentAsync(Guid enrollmentId);
    Task RejectEnrollmentAsync(Guid enrollmentId);
    Task<MicroMajorCertificateDto> IssueCertificateAsync(IssueCertificateInputDto input);
}
