import type { EmploymentGuidanceSourceType } from '../enums/employment-guidance-source-type.enum';
import type { EmploymentOutcomeStatus } from '../enums/employment-outcome-status.enum';
import type { EmploymentJobType } from '../enums/employment-job-type.enum';
import type { EmploymentJobStatus } from '../enums/employment-job-status.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { EmploymentInterviewResult } from '../enums/employment-interview-result.enum';
import type { EmploymentApplicationStatus } from '../enums/employment-application-status.enum';

export interface CreateEmploymentGuidanceRecordDto {
  studentId?: string;
  applicationId?: string | null;
  title?: string;
  content?: string;
  sourceType?: EmploymentGuidanceSourceType;
  careerGoal?: string | null;
}

export interface CreateJobApplicationDto {
  jobPostingId?: string;
  resumeId?: string;
  coverLetter?: string | null;
}

export interface CreateMyAIGuidanceRecordDto {
  title?: string;
  content?: string;
  careerGoal?: string | null;
}

export interface CreateUpdateEmploymentOutcomeDto {
  id?: string | null;
  studentId?: string;
  applicationId?: string | null;
  employerName?: string;
  jobTitle?: string;
  status?: EmploymentOutcomeStatus;
  employmentType?: string | null;
  region?: string | null;
  salaryRange?: string | null;
  startDate?: string | null;
  confirmedAt?: string;
  remark?: string | null;
  isPrimary?: boolean;
}

export interface CreateUpdateInterviewScheduleDto {
  applicationId?: string;
  interviewerName?: string;
  interviewerPhone?: string | null;
  scheduledAt?: string;
  location?: string | null;
  meetingUrl?: string | null;
  note?: string | null;
}

export interface CreateUpdateJobPostingDto {
  companyName?: string | null;
  industry?: string | null;
  title?: string;
  summary?: string | null;
  description?: string;
  location?: string | null;
  address?: string | null;
  jobType?: EmploymentJobType;
  educationRequirement?: string | null;
  salaryRange?: string | null;
  recruitmentCount?: number;
  skillTags?: string | null;
  benefits?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  deadline?: string | null;
  status?: EmploymentJobStatus;
}

export interface CreateUpdateStudentResumeDto {
  title?: string;
  fullName?: string;
  phoneNumber?: string | null;
  email?: string | null;
  schoolName?: string | null;
  major?: string | null;
  grade?: string | null;
  className?: string | null;
  studentNumber?: string | null;
  summary?: string | null;
  skills?: string | null;
  educationExperience?: string | null;
  internshipExperience?: string | null;
  projectExperience?: string | null;
  certificateText?: string | null;
  attachmentUrl?: string | null;
  isDefault?: boolean;
}

export interface EmployerProfileDto {
  userId?: string;
  contactName?: string | null;
  phoneNumber?: string | null;
  email?: string | null;
  companyName?: string | null;
  unifiedSocialCreditCode?: string | null;
  position?: string | null;
  industry?: string | null;
  partnerSchool?: string | null;
  remark?: string | null;
}

export interface EmploymentGuidanceRecordDto extends FullAuditedEntityDto<string> {
  studentId?: string;
  studentName?: string | null;
  applicationId?: string | null;
  teacherId?: string | null;
  teacherName?: string | null;
  title?: string;
  content?: string;
  sourceType?: EmploymentGuidanceSourceType;
  careerGoal?: string | null;
  guidedAt?: string;
}

export interface EmploymentOutcomeDto extends FullAuditedEntityDto<string> {
  studentId?: string;
  studentName?: string | null;
  applicationId?: string | null;
  employerName?: string;
  jobTitle?: string;
  status?: EmploymentOutcomeStatus;
  employmentType?: string | null;
  region?: string | null;
  salaryRange?: string | null;
  startDate?: string | null;
  confirmedAt?: string;
  remark?: string | null;
  isPrimary?: boolean;
}

export interface EmploymentStatisticsInput {
  major?: string | null;
  grade?: string | null;
  status?: EmploymentOutcomeStatus | null;
}

export interface EmploymentStatisticsRowDto {
  major?: string;
  grade?: string;
  status?: EmploymentOutcomeStatus;
  studentCount?: number;
  outcomeCount?: number;
}

export interface GetEmploymentGuidanceRecordsInput extends PagedAndSortedResultRequestDto {
  studentId?: string | null;
  applicationId?: string | null;
}

export interface GetEmploymentOutcomeListInput extends PagedAndSortedResultRequestDto {
  studentId?: string | null;
  status?: EmploymentOutcomeStatus | null;
  onlyPrimary?: boolean | null;
}

export interface GetInterviewSchedulesInput extends PagedAndSortedResultRequestDto {
  jobPostingId?: string | null;
  studentId?: string | null;
  applicationId?: string | null;
  result?: EmploymentInterviewResult | null;
}

export interface GetJobApplicationsInput extends PagedAndSortedResultRequestDto {
  jobPostingId?: string | null;
  studentId?: string | null;
  status?: EmploymentApplicationStatus | null;
}

export interface GetManageJobsInput extends PagedJobPostingRequestDto {
  status?: EmploymentJobStatus | null;
}

export interface InterviewScheduleDto extends FullAuditedEntityDto<string> {
  applicationId?: string;
  jobPostingId?: string;
  jobTitle?: string | null;
  studentId?: string;
  studentName?: string | null;
  employerUserId?: string | null;
  interviewerName?: string;
  interviewerPhone?: string | null;
  scheduledAt?: string;
  location?: string | null;
  meetingUrl?: string | null;
  note?: string | null;
  result?: EmploymentInterviewResult;
  summary?: string | null;
  resultComment?: string | null;
  resultRecordedAt?: string | null;
}

export interface JobApplicationDto extends FullAuditedEntityDto<string> {
  jobPostingId?: string;
  jobTitle?: string | null;
  companyName?: string | null;
  studentId?: string;
  studentName?: string | null;
  resumeId?: string;
  resumeTitle?: string | null;
  coverLetter?: string | null;
  status?: EmploymentApplicationStatus;
  appliedAt?: string;
  reviewedAt?: string | null;
  employerRemark?: string | null;
}

export interface JobPostingDto extends FullAuditedEntityDto<string> {
  employerUserId?: string;
  companyName?: string;
  industry?: string | null;
  title?: string;
  summary?: string | null;
  description?: string;
  location?: string | null;
  address?: string | null;
  jobType?: EmploymentJobType;
  educationRequirement?: string | null;
  salaryRange?: string | null;
  recruitmentCount?: number;
  skillTags?: string | null;
  benefits?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  deadline?: string | null;
  status?: EmploymentJobStatus;
  reviewComment?: string | null;
  reviewedAt?: string | null;
  publishedAt?: string | null;
  viewCount?: number;
  applicationCount?: number;
  hasApplied?: boolean;
}

export interface PagedJobPostingRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  location?: string | null;
  jobType?: EmploymentJobType | null;
}

export interface RecordInterviewResultDto {
  result?: EmploymentInterviewResult;
  summary?: string | null;
  resultComment?: string | null;
}

export interface ReviewJobPostingDto {
  status?: EmploymentJobStatus;
  reviewComment?: string | null;
}

export interface StudentResumeDto extends FullAuditedEntityDto<string> {
  studentId?: string;
  title?: string;
  fullName?: string;
  phoneNumber?: string | null;
  email?: string | null;
  schoolName?: string | null;
  major?: string | null;
  grade?: string | null;
  className?: string | null;
  studentNumber?: string | null;
  summary?: string | null;
  skills?: string | null;
  educationExperience?: string | null;
  internshipExperience?: string | null;
  projectExperience?: string | null;
  certificateText?: string | null;
  attachmentUrl?: string | null;
  isDefault?: boolean;
  versionNo?: number;
  lastUsedAt?: string | null;
}

export interface UpdateEmployerProfileDto {
  contactName?: string | null;
  phoneNumber?: string | null;
  email?: string | null;
  companyName?: string;
  unifiedSocialCreditCode?: string;
  position?: string;
  industry?: string | null;
  partnerSchool?: string | null;
  remark?: string | null;
}

export interface UpdateJobApplicationStatusDto {
  status?: EmploymentApplicationStatus;
  employerRemark?: string | null;
}
