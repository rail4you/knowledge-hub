using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Practicums.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace KnowledgeHub.Practicums;

public interface IPracticumAppService : IApplicationService
{
    Task<PracticumProjectDto> GetAsync(Guid id);
    Task<PracticumProjectDetailDto> GetDetailAsync(Guid id);
    Task<PagedResultDto<PracticumProjectDto>> GetListAsync(PagedPracticumProjectRequestDto input);
    Task<PagedResultDto<PracticumProjectDto>> GetPublishedAsync(PagedPracticumProjectRequestDto input);
    Task<PracticumProjectDto> CreateAsync(CreateUpdatePracticumProjectDto input);
    Task<PracticumProjectDto> UpdateAsync(Guid id, CreateUpdatePracticumProjectDto input);
    Task DeleteAsync(Guid id);
    Task EnrollAsync(Guid projectId);
    Task<List<PracticumEnrollmentDto>> GetMyEnrollmentsAsync();
    Task<PagedResultDto<PracticumEnrollmentDto>> GetEnrollmentListAsync(GetPracticumEnrollmentsInput input);
    Task<PracticumSubmissionDto> CreateSubmissionAsync(CreatePracticumSubmissionDto input);
    Task<PagedResultDto<PracticumSubmissionDto>> GetSubmissionListAsync(GetPracticumSubmissionsInput input);
    Task<PracticumGuidanceRecordDto> AddGuidanceAsync(CreatePracticumGuidanceRecordDto input);
    Task<List<PracticumGuidanceRecordDto>> GetGuidanceListAsync(Guid enrollmentId);
    Task<PracticumAssessmentDto> ScoreEnrollmentAsync(Guid enrollmentId, CreatePracticumAssessmentDto input);
    Task<List<PracticumTimelineItemDto>> GetTimelineAsync(Guid enrollmentId);
    Task<IRemoteStreamContent> ExportAssessmentsAsync(Guid? projectId);
    Task<PracticumAgentConfigDto> GetAgentConfigAsync(Guid projectId);
    Task UpdateAgentConfigAsync(Guid projectId, UpdatePracticumAgentConfigDto input);
}
