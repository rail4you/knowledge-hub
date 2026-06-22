import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';

export enum EmploymentJobStatus {
  Draft = 0,
  PendingReview = 1,
  Published = 2,
  Rejected = 3,
  Closed = 4,
}

export enum EmploymentJobType {
  FullTime = 0,
  Internship = 1,
  PartTime = 2,
  Apprenticeship = 3,
}

export enum EmploymentApplicationStatus {
  Submitted = 0,
  Viewed = 1,
  InterviewScheduled = 2,
  Offered = 3,
  Rejected = 4,
  Withdrawn = 5,
}

export enum EmploymentInterviewResult {
  Pending = 0,
  Passed = 1,
  Deferred = 2,
  Failed = 3,
}

export enum EmploymentGuidanceSourceType {
  Manual = 0,
  AI = 1,
}

export enum EmploymentOutcomeStatus {
  Intention = 0,
  Signed = 1,
  Employed = 2,
  FurtherStudy = 3,
  Entrepreneurship = 4,
  Unemployed = 5,
}

export interface EmployerProfileDto {
  userId: string;
  contactName?: string;
  phoneNumber?: string;
  email?: string;
  companyName?: string;
  unifiedSocialCreditCode?: string;
  position?: string;
  industry?: string;
  partnerSchool?: string;
  remark?: string;
}

export interface UpdateEmployerProfileDto {
  contactName?: string;
  phoneNumber?: string;
  email?: string;
  companyName: string;
  unifiedSocialCreditCode: string;
  position: string;
  industry?: string;
  partnerSchool?: string;
  remark?: string;
}

export interface JobPostingDto {
  id: string;
  employerUserId: string;
  companyName: string;
  industry?: string;
  title: string;
  summary?: string;
  description: string;
  location?: string;
  address?: string;
  jobType: EmploymentJobType;
  educationRequirement?: string;
  salaryRange?: string;
  recruitmentCount: number;
  skillTags?: string;
  benefits?: string;
  contactName?: string;
  contactPhone?: string;
  contactEmail?: string;
  deadline?: string;
  status: EmploymentJobStatus;
  reviewComment?: string;
  reviewedAt?: string;
  publishedAt?: string;
  viewCount: number;
  applicationCount: number;
  hasApplied: boolean;
  creationTime: string;
}

export interface CreateUpdateJobPostingDto {
  companyName?: string;
  industry?: string;
  title: string;
  summary?: string;
  description: string;
  location?: string;
  address?: string;
  jobType: EmploymentJobType;
  educationRequirement?: string;
  salaryRange?: string;
  recruitmentCount: number;
  skillTags?: string;
  benefits?: string;
  contactName?: string;
  contactPhone?: string;
  contactEmail?: string;
  deadline?: string;
  status: EmploymentJobStatus;
}

export interface ReviewJobPostingDto {
  status: EmploymentJobStatus;
  reviewComment?: string;
}

export interface PagedJobPostingRequestDto {
  filter?: string;
  location?: string;
  jobType?: EmploymentJobType;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface GetManageJobsInput extends PagedJobPostingRequestDto {
  status?: EmploymentJobStatus;
}

export interface StudentResumeDto {
  id: string;
  studentId: string;
  title: string;
  fullName: string;
  phoneNumber?: string;
  email?: string;
  schoolName?: string;
  major?: string;
  grade?: string;
  className?: string;
  studentNumber?: string;
  summary?: string;
  skills?: string;
  educationExperience?: string;
  internshipExperience?: string;
  projectExperience?: string;
  certificateText?: string;
  attachmentUrl?: string;
  isDefault: boolean;
  versionNo: number;
  lastUsedAt?: string;
  creationTime: string;
}

export interface CreateUpdateStudentResumeDto {
  title: string;
  fullName: string;
  phoneNumber?: string;
  email?: string;
  schoolName?: string;
  major?: string;
  grade?: string;
  className?: string;
  studentNumber?: string;
  summary?: string;
  skills?: string;
  educationExperience?: string;
  internshipExperience?: string;
  projectExperience?: string;
  certificateText?: string;
  attachmentUrl?: string;
  isDefault: boolean;
}

export interface JobApplicationDto {
  id: string;
  jobPostingId: string;
  jobTitle?: string;
  companyName?: string;
  studentId: string;
  studentName?: string;
  resumeId: string;
  resumeTitle?: string;
  coverLetter?: string;
  status: EmploymentApplicationStatus;
  appliedAt: string;
  reviewedAt?: string;
  employerRemark?: string;
}

export interface CreateJobApplicationDto {
  jobPostingId: string;
  resumeId: string;
  coverLetter?: string;
}

export interface GetJobApplicationsInput {
  jobPostingId?: string;
  studentId?: string;
  status?: EmploymentApplicationStatus;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface UpdateJobApplicationStatusDto {
  status: EmploymentApplicationStatus;
  employerRemark?: string;
}

export interface InterviewScheduleDto {
  id: string;
  applicationId: string;
  jobPostingId: string;
  jobTitle?: string;
  studentId: string;
  studentName?: string;
  employerUserId?: string;
  /** P1-8：面试官用户 ID（与 interviewerName 二选一） */
  interviewerId?: string;
  interviewerName: string;
  interviewerPhone?: string;
  scheduledAt: string;
  location?: string;
  meetingUrl?: string;
  note?: string;
  result: EmploymentInterviewResult;
  summary?: string;
  resultComment?: string;
  resultRecordedAt?: string;
  creationTime: string;
}

export interface CreateUpdateInterviewScheduleDto {
  applicationId: string;
  /** P1-8：面试官用户 ID（可选；未选则用 interviewerName 自由文本） */
  interviewerId?: string;
  interviewerName: string;
  interviewerPhone?: string;
  scheduledAt: string;
  location?: string;
  meetingUrl?: string;
  note?: string;
}

export interface RecordInterviewResultDto {
  result: EmploymentInterviewResult;
  summary?: string;
  resultComment?: string;
}

export interface GetInterviewSchedulesInput {
  jobPostingId?: string;
  studentId?: string;
  applicationId?: string;
  result?: EmploymentInterviewResult;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface EmploymentGuidanceRecordDto {
  id: string;
  studentId: string;
  studentName?: string;
  applicationId?: string;
  teacherId?: string;
  teacherName?: string;
  title: string;
  content: string;
  sourceType: EmploymentGuidanceSourceType;
  careerGoal?: string;
  guidedAt: string;
  creationTime: string;
}

export interface CreateEmploymentGuidanceRecordDto {
  studentId: string;
  applicationId?: string;
  title: string;
  content: string;
  sourceType: EmploymentGuidanceSourceType;
  careerGoal?: string;
}

export interface CreateMyAIGuidanceRecordDto {
  title: string;
  content: string;
  careerGoal?: string;
}

export interface GetEmploymentGuidanceRecordsInput {
  studentId?: string;
  applicationId?: string;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface EmploymentOutcomeDto {
  id: string;
  studentId: string;
  studentName?: string;
  applicationId?: string;
  employerName: string;
  jobTitle: string;
  status: EmploymentOutcomeStatus;
  employmentType?: string;
  region?: string;
  salaryRange?: string;
  startDate?: string;
  confirmedAt: string;
  remark?: string;
  isPrimary: boolean;
  creationTime: string;
}

export interface CreateUpdateEmploymentOutcomeDto {
  id?: string;
  studentId: string;
  applicationId?: string;
  employerName: string;
  jobTitle: string;
  status: EmploymentOutcomeStatus;
  employmentType?: string;
  region?: string;
  salaryRange?: string;
  startDate?: string;
  confirmedAt: string;
  remark?: string;
  isPrimary: boolean;
}

export interface GetEmploymentOutcomeListInput {
  studentId?: string;
  status?: EmploymentOutcomeStatus;
  onlyPrimary?: boolean;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface EmploymentStatisticsInput {
  major?: string;
  grade?: string;
  status?: EmploymentOutcomeStatus;
}

export interface EmploymentStatisticsRowDto {
  major: string;
  grade: string;
  status: EmploymentOutcomeStatus;
  studentCount: number;
  outcomeCount: number;
}

@Injectable({
  providedIn: 'root',
})
export class EmploymentService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getMyEmployerProfile = () =>
    this.restService.request<any, EmployerProfileDto>({
      method: 'GET',
      url: '/api/app/employment/my-employer-profile',
    }, { apiName: this.apiName });

  updateMyEmployerProfile = (input: UpdateEmployerProfileDto) =>
    this.restService.request<any, EmployerProfileDto>({
      method: 'PUT',
      url: '/api/app/employment/my-employer-profile',
      body: input,
    }, { apiName: this.apiName });

  getJob = (id: string) =>
    this.restService.request<any, JobPostingDto>({
      method: 'GET',
      url: `/api/app/employment/${id}/job`,
    }, { apiName: this.apiName });

  getPublishedJobList = (input: PagedJobPostingRequestDto) =>
    this.restService.request<any, PagedResultDto<JobPostingDto>>({
      method: 'GET',
      url: '/api/app/employment/published-job-list',
      params: input,
    }, { apiName: this.apiName });

  getManageJobList = (input: GetManageJobsInput) =>
    this.restService.request<any, PagedResultDto<JobPostingDto>>({
      method: 'GET',
      url: '/api/app/employment/manage-job-list',
      params: input,
    }, { apiName: this.apiName });

  createJob = (input: CreateUpdateJobPostingDto) =>
    this.restService.request<any, JobPostingDto>({
      method: 'POST',
      url: '/api/app/employment/job',
      body: input,
    }, { apiName: this.apiName });

  updateJob = (id: string, input: CreateUpdateJobPostingDto) =>
    this.restService.request<any, JobPostingDto>({
      method: 'PUT',
      url: `/api/app/employment/${id}/job`,
      body: input,
    }, { apiName: this.apiName });

  deleteJob = (id: string) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/employment/${id}/job`,
    }, { apiName: this.apiName });

  reviewJob = (id: string, input: ReviewJobPostingDto) =>
    this.restService.request<any, JobPostingDto>({
      method: 'POST',
      url: `/api/app/employment/${id}/review-job`,
      body: input,
    }, { apiName: this.apiName });

  getMyResumeList = () =>
    this.restService.request<any, StudentResumeDto[]>({
      method: 'GET',
      url: '/api/app/employment/my-resume-list',
    }, { apiName: this.apiName });

  createResume = (input: CreateUpdateStudentResumeDto) =>
    this.restService.request<any, StudentResumeDto>({
      method: 'POST',
      url: '/api/app/employment/resume',
      body: input,
    }, { apiName: this.apiName });

  updateResume = (id: string, input: CreateUpdateStudentResumeDto) =>
    this.restService.request<any, StudentResumeDto>({
      method: 'PUT',
      url: `/api/app/employment/${id}/resume`,
      body: input,
    }, { apiName: this.apiName });

  deleteResume = (id: string) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/employment/${id}/resume`,
    }, { apiName: this.apiName });

  setDefaultResume = (id: string) =>
    this.restService.request<any, StudentResumeDto>({
      method: 'POST',
      url: `/api/app/employment/${id}/set-default-resume`,
    }, { apiName: this.apiName });

  createApplication = (input: CreateJobApplicationDto) =>
    this.restService.request<any, JobApplicationDto>({
      method: 'POST',
      url: '/api/app/employment/application',
      body: input,
    }, { apiName: this.apiName });

  getMyApplicationList = (input: GetJobApplicationsInput) =>
    this.restService.request<any, PagedResultDto<JobApplicationDto>>({
      method: 'GET',
      url: '/api/app/employment/my-application-list',
      params: input,
    }, { apiName: this.apiName });

  getJobApplicationList = (input: GetJobApplicationsInput) =>
    this.restService.request<any, PagedResultDto<JobApplicationDto>>({
      method: 'GET',
      url: '/api/app/employment/job-application-list',
      params: input,
    }, { apiName: this.apiName });

  updateApplicationStatus = (id: string, input: UpdateJobApplicationStatusDto) =>
    this.restService.request<any, JobApplicationDto>({
      method: 'PUT',
      url: `/api/app/employment/${id}/application-status`,
      body: input,
    }, { apiName: this.apiName });

  scheduleInterview = (input: CreateUpdateInterviewScheduleDto) =>
    this.restService.request<any, InterviewScheduleDto>({
      method: 'POST',
      url: '/api/app/employment/schedule-interview',
      body: input,
    }, { apiName: this.apiName });

  updateInterview = (id: string, input: CreateUpdateInterviewScheduleDto) =>
    this.restService.request<any, InterviewScheduleDto>({
      method: 'PUT',
      url: `/api/app/employment/${id}/interview`,
      body: input,
    }, { apiName: this.apiName });

  recordInterviewResult = (id: string, input: RecordInterviewResultDto) =>
    this.restService.request<any, InterviewScheduleDto>({
      method: 'POST',
      url: `/api/app/employment/${id}/record-interview-result`,
      body: input,
    }, { apiName: this.apiName });

  getInterviewList = (input: GetInterviewSchedulesInput) =>
    this.restService.request<any, PagedResultDto<InterviewScheduleDto>>({
      method: 'GET',
      url: '/api/app/employment/interview-list',
      params: input,
    }, { apiName: this.apiName });

  createGuidanceRecord = (input: CreateEmploymentGuidanceRecordDto) =>
    this.restService.request<any, EmploymentGuidanceRecordDto>({
      method: 'POST',
      url: '/api/app/employment/guidance-record',
      body: input,
    }, { apiName: this.apiName });

  createMyAIGuidanceRecord = (input: CreateMyAIGuidanceRecordDto) =>
    this.restService.request<any, EmploymentGuidanceRecordDto>({
      method: 'POST',
      url: '/api/app/employment/my-aIGuidance-record',
      body: input,
    }, { apiName: this.apiName });

  getMyGuidanceRecordList = (input: GetEmploymentGuidanceRecordsInput) =>
    this.restService.request<any, PagedResultDto<EmploymentGuidanceRecordDto>>({
      method: 'GET',
      url: '/api/app/employment/my-guidance-record-list',
      params: input,
    }, { apiName: this.apiName });

  getGuidanceRecordList = (input: GetEmploymentGuidanceRecordsInput) =>
    this.restService.request<any, PagedResultDto<EmploymentGuidanceRecordDto>>({
      method: 'GET',
      url: '/api/app/employment/guidance-record-list',
      params: input,
    }, { apiName: this.apiName });

  deleteMyGuidanceRecord = (id: string) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/employment/my-guidance-record/${id}`,
    }, { apiName: this.apiName });

  saveOutcome = (input: CreateUpdateEmploymentOutcomeDto) =>
    this.restService.request<any, EmploymentOutcomeDto>({
      method: 'POST',
      url: '/api/app/employment/save-outcome',
      body: input,
    }, { apiName: this.apiName });

  getOutcomeList = (input: GetEmploymentOutcomeListInput) =>
    this.restService.request<any, PagedResultDto<EmploymentOutcomeDto>>({
      method: 'GET',
      url: '/api/app/employment/outcome-list',
      params: input,
    }, { apiName: this.apiName });

  getStatistics = (input: EmploymentStatisticsInput) =>
    this.restService.request<any, EmploymentStatisticsRowDto[]>({
      method: 'GET',
      url: '/api/app/employment/statistics',
      params: input,
    }, { apiName: this.apiName });

  exportStatistics = (input: EmploymentStatisticsInput) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      url: '/api/app/employment/export-statistics',
      body: input,
      responseType: 'blob' as 'json',
    }, { apiName: this.apiName });

  uploadResumeAttachment = (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return this.restService.request<any, ResumeUploadResultDto>({
      method: 'POST',
      url: '/api/app/resume-upload',
      body: formData,
    }, { apiName: this.apiName });
  }
}

export interface ResumeUploadResultDto {
  url: string;
  filePath: string;
  originalFileName: string;
  size: number;
}
