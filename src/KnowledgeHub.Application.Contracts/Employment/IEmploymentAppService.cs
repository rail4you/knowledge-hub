using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Employment.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace KnowledgeHub.Employment;

public interface IEmploymentAppService : IApplicationService
{
    Task<EmployerProfileDto> GetMyEmployerProfileAsync();
    Task<EmployerProfileDto> UpdateMyEmployerProfileAsync(UpdateEmployerProfileDto input);

    Task<JobPostingDto> GetJobAsync(Guid id);
    Task<PagedResultDto<JobPostingDto>> GetPublishedJobListAsync(PagedJobPostingRequestDto input);
    Task<PagedResultDto<JobPostingDto>> GetManageJobListAsync(GetManageJobsInput input);
    Task<JobPostingDto> CreateJobAsync(CreateUpdateJobPostingDto input);
    Task<JobPostingDto> UpdateJobAsync(Guid id, CreateUpdateJobPostingDto input);
    Task DeleteJobAsync(Guid id);
    Task<JobPostingDto> ReviewJobAsync(Guid id, ReviewJobPostingDto input);

    Task<List<StudentResumeDto>> GetMyResumeListAsync();
    Task<StudentResumeDto> CreateResumeAsync(CreateUpdateStudentResumeDto input);
    Task<StudentResumeDto> UpdateResumeAsync(Guid id, CreateUpdateStudentResumeDto input);
    Task DeleteResumeAsync(Guid id);
    Task<StudentResumeDto> SetDefaultResumeAsync(Guid id);

    Task<JobApplicationDto> CreateApplicationAsync(CreateJobApplicationDto input);
    Task<PagedResultDto<JobApplicationDto>> GetMyApplicationListAsync(GetJobApplicationsInput input);
    Task<PagedResultDto<JobApplicationDto>> GetJobApplicationListAsync(GetJobApplicationsInput input);
    Task<JobApplicationDto> UpdateApplicationStatusAsync(Guid id, UpdateJobApplicationStatusDto input);

    Task<InterviewScheduleDto> ScheduleInterviewAsync(CreateUpdateInterviewScheduleDto input);
    Task<InterviewScheduleDto> UpdateInterviewAsync(Guid id, CreateUpdateInterviewScheduleDto input);
    Task<InterviewScheduleDto> RecordInterviewResultAsync(Guid id, RecordInterviewResultDto input);
    Task<PagedResultDto<InterviewScheduleDto>> GetInterviewListAsync(GetInterviewSchedulesInput input);

    Task<EmploymentGuidanceRecordDto> CreateGuidanceRecordAsync(CreateEmploymentGuidanceRecordDto input);
    Task<PagedResultDto<EmploymentGuidanceRecordDto>> GetMyGuidanceRecordListAsync(GetEmploymentGuidanceRecordsInput input);
    Task<PagedResultDto<EmploymentGuidanceRecordDto>> GetGuidanceRecordListAsync(GetEmploymentGuidanceRecordsInput input);

    Task<EmploymentOutcomeDto> SaveOutcomeAsync(CreateUpdateEmploymentOutcomeDto input);
    Task<PagedResultDto<EmploymentOutcomeDto>> GetOutcomeListAsync(GetEmploymentOutcomeListInput input);
    Task<List<EmploymentStatisticsRowDto>> GetStatisticsAsync(EmploymentStatisticsInput input);
    Task<IRemoteStreamContent> ExportStatisticsAsync(EmploymentStatisticsInput input);
}
