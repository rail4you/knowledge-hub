import type { CreateEmploymentGuidanceRecordDto, CreateJobApplicationDto, CreateUpdateEmploymentOutcomeDto, CreateUpdateInterviewScheduleDto, CreateUpdateJobPostingDto, CreateUpdateStudentResumeDto, EmployerProfileDto, EmploymentGuidanceRecordDto, EmploymentOutcomeDto, EmploymentStatisticsInput, EmploymentStatisticsRowDto, GetEmploymentGuidanceRecordsInput, GetEmploymentOutcomeListInput, GetInterviewSchedulesInput, GetJobApplicationsInput, GetManageJobsInput, InterviewScheduleDto, JobApplicationDto, JobPostingDto, PagedJobPostingRequestDto, RecordInterviewResultDto, ReviewJobPostingDto, StudentResumeDto, UpdateEmployerProfileDto, UpdateJobApplicationStatusDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class EmploymentService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  createApplication = (input: CreateJobApplicationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobApplicationDto>({
      method: 'POST',
      url: '/api/app/employment/application',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createGuidanceRecord = (input: CreateEmploymentGuidanceRecordDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmploymentGuidanceRecordDto>({
      method: 'POST',
      url: '/api/app/employment/guidance-record',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createJob = (input: CreateUpdateJobPostingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobPostingDto>({
      method: 'POST',
      url: '/api/app/employment/job',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createResume = (input: CreateUpdateStudentResumeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentResumeDto>({
      method: 'POST',
      url: '/api/app/employment/resume',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  deleteJob = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/employment/${id}/job`,
    },
    { apiName: this.apiName,...config });
  

  deleteResume = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/employment/${id}/resume`,
    },
    { apiName: this.apiName,...config });
  

  exportStatistics = (input: EmploymentStatisticsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: '/api/app/employment/export-statistics',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getGuidanceRecordList = (input: GetEmploymentGuidanceRecordsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<EmploymentGuidanceRecordDto>>({
      method: 'GET',
      url: '/api/app/employment/guidance-record-list',
      params: { studentId: input.studentId, applicationId: input.applicationId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getInterviewList = (input: GetInterviewSchedulesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<InterviewScheduleDto>>({
      method: 'GET',
      url: '/api/app/employment/interview-list',
      params: { jobPostingId: input.jobPostingId, studentId: input.studentId, applicationId: input.applicationId, result: input.result, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getJob = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobPostingDto>({
      method: 'GET',
      url: `/api/app/employment/${id}/job`,
    },
    { apiName: this.apiName,...config });
  

  getJobApplicationList = (input: GetJobApplicationsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<JobApplicationDto>>({
      method: 'GET',
      url: '/api/app/employment/job-application-list',
      params: { jobPostingId: input.jobPostingId, studentId: input.studentId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getManageJobList = (input: GetManageJobsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<JobPostingDto>>({
      method: 'GET',
      url: '/api/app/employment/manage-job-list',
      params: { status: input.status, filter: input.filter, location: input.location, jobType: input.jobType, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyApplicationList = (input: GetJobApplicationsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<JobApplicationDto>>({
      method: 'GET',
      url: '/api/app/employment/my-application-list',
      params: { jobPostingId: input.jobPostingId, studentId: input.studentId, status: input.status, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyEmployerProfile = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmployerProfileDto>({
      method: 'GET',
      url: '/api/app/employment/my-employer-profile',
    },
    { apiName: this.apiName,...config });
  

  getMyGuidanceRecordList = (input: GetEmploymentGuidanceRecordsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<EmploymentGuidanceRecordDto>>({
      method: 'GET',
      url: '/api/app/employment/my-guidance-record-list',
      params: { studentId: input.studentId, applicationId: input.applicationId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyResumeList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentResumeDto[]>({
      method: 'GET',
      url: '/api/app/employment/my-resume-list',
    },
    { apiName: this.apiName,...config });
  

  getOutcomeList = (input: GetEmploymentOutcomeListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<EmploymentOutcomeDto>>({
      method: 'GET',
      url: '/api/app/employment/outcome-list',
      params: { studentId: input.studentId, status: input.status, onlyPrimary: input.onlyPrimary, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getPublishedJobList = (input: PagedJobPostingRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<JobPostingDto>>({
      method: 'GET',
      url: '/api/app/employment/published-job-list',
      params: { filter: input.filter, location: input.location, jobType: input.jobType, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getStatistics = (input: EmploymentStatisticsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmploymentStatisticsRowDto[]>({
      method: 'GET',
      url: '/api/app/employment/statistics',
      params: { major: input.major, grade: input.grade, status: input.status },
    },
    { apiName: this.apiName,...config });
  

  recordInterviewResult = (id: string, input: RecordInterviewResultDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InterviewScheduleDto>({
      method: 'POST',
      url: `/api/app/employment/${id}/record-interview-result`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  reviewJob = (id: string, input: ReviewJobPostingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobPostingDto>({
      method: 'POST',
      url: `/api/app/employment/${id}/review-job`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  saveOutcome = (input: CreateUpdateEmploymentOutcomeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmploymentOutcomeDto>({
      method: 'POST',
      url: '/api/app/employment/save-outcome',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  scheduleInterview = (input: CreateUpdateInterviewScheduleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InterviewScheduleDto>({
      method: 'POST',
      url: '/api/app/employment/schedule-interview',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  setDefaultResume = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentResumeDto>({
      method: 'POST',
      url: `/api/app/employment/${id}/set-default-resume`,
    },
    { apiName: this.apiName,...config });
  

  updateApplicationStatus = (id: string, input: UpdateJobApplicationStatusDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobApplicationDto>({
      method: 'PUT',
      url: `/api/app/employment/${id}/application-status`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateInterview = (id: string, input: CreateUpdateInterviewScheduleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InterviewScheduleDto>({
      method: 'PUT',
      url: `/api/app/employment/${id}/interview`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateJob = (id: string, input: CreateUpdateJobPostingDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, JobPostingDto>({
      method: 'PUT',
      url: `/api/app/employment/${id}/job`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateMyEmployerProfile = (input: UpdateEmployerProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmployerProfileDto>({
      method: 'PUT',
      url: '/api/app/employment/my-employer-profile',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateResume = (id: string, input: CreateUpdateStudentResumeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentResumeDto>({
      method: 'PUT',
      url: `/api/app/employment/${id}/resume`,
      body: input,
    },
    { apiName: this.apiName,...config });
}