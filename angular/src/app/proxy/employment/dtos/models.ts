import type { EmploymentGuidanceSourceType } from '../enums/employment-guidance-source-type.enum';
import type { EmploymentOutcomeStatus } from '../enums/employment-outcome-status.enum';
import type { EmploymentJobType } from '../enums/employment-job-type.enum';
import type { EmploymentJobStatus } from '../enums/employment-job-status.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { EmploymentInterviewResult } from '../enums/employment-interview-result.enum';
import type { EmploymentApplicationStatus } from '../enums/employment-application-status.enum';

export interface CareerGuidanceStudentDto {
  studentId?: string;
  studentName?: string;
  resumes?: StudentResumeDto[];
}

export interface CompleteInterviewDto {
  completionMessage?: string;
}

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

export interface CreateStudentCareerGuidanceRecordDto {
  studentId?: string;
  title?: string;
  content?: string;
  careerGoal?: string | null;
}

export interface CreateUpdateEmploymentOutcomeDto {
  id?: string | null;
  evidenceUrl?: string | null;
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
  interviewerId?: string | null;
  interviewerName?: string;
  interviewerPhone?: string | null;
  scheduledAt?: string;
  timeZone?: string | null;
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
  videoUrl?: string | null;
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
  evidenceUrl?: string | null;
  isVerified?: boolean;
  verifiedAt?: string | null;
  verifyNote?: string | null;
}

export interface EmploymentOutcomeImportFailItemDto {
  rowNumber?: number;
  studentName?: string | null;
  reason?: string;
}

export interface EmploymentOutcomeImportResultDto {
  totalCount?: number;
  successCount?: number;
  failCount?: number;
  failItems?: EmploymentOutcomeImportFailItemDto[];
}

export interface EmploymentOutcomeStudentDto {
  id?: string;
  name?: string;
}

export interface EmploymentStatisticsInput {
  major?: string | null;
  grade?: string | null;
  status?: EmploymentOutcomeStatus | null;
  days?: number | null;
}

export interface EmploymentStatisticsRowDto {
  major?: string;
  grade?: string;
  status?: EmploymentOutcomeStatus;
  studentCount?: number;
  outcomeCount?: number;
  verifiedStudentCount?: number;
}

export interface EmploymentWorkbenchBreakdownDto {
  key?: number;
  label?: string;
  count?: number;
  verifiedCount?: number;
}

export interface EmploymentWorkbenchCompanyRowDto {
  companyName?: string;
  jobCount?: number;
  applicationCount?: number;
  offeredCount?: number;
}

export interface EmploymentWorkbenchDto {
  studentTotal?: number;
  studentWithApplication?: number;
  studentWithOutcome?: number;
  studentVerified?: number;
  studentEmployed?: number;
  employmentRate?: number;
  jobTotal?: number;
  jobPublished?: number;
  jobPendingReview?: number;
  jobClosed?: number;
  applicationTotal?: number;
  applicationPending?: number;
  applicationInInterview?: number;
  applicationOffered?: number;
  applicationRejected?: number;
  interviewTotal?: number;
  interviewUpcoming?: number;
  interviewPendingResult?: number;
  interviewPassed?: number;
  interviewFailed?: number;
  outcomeTotal?: number;
  outcomeVerified?: number;
  outcomeUnverified?: number;
  outcomePrimaryUnverified?: number;
  pendingJobs?: EmploymentWorkbenchTaskDto[];
  upcomingInterviews?: EmploymentWorkbenchTaskDto[];
  pendingOutcomes?: EmploymentWorkbenchTaskDto[];
  funnel?: EmploymentWorkbenchFunnelDto;
  outcomeStatuses?: EmploymentWorkbenchBreakdownDto[];
  applicationStatuses?: EmploymentWorkbenchBreakdownDto[];
  majors?: EmploymentWorkbenchMajorRowDto[];
  companies?: EmploymentWorkbenchCompanyRowDto[];
}

export interface EmploymentWorkbenchFunnelDto {
  studentTotal?: number;
  withResume?: number;
  withApplication?: number;
  interviewed?: number;
  withOutcome?: number;
  verified?: number;
  employed?: number;
}

export interface EmploymentWorkbenchMajorRowDto {
  major?: string;
  studentTotal?: number;
  appliedStudents?: number;
  outcomeStudents?: number;
  verifiedStudents?: number;
  employedStudents?: number;
  employmentRate?: number;
}

export interface EmploymentWorkbenchTaskDto {
  id?: string;
  title?: string;
  subtitle?: string | null;
  extra?: string | null;
  time?: string | null;
}

export interface GetEmploymentGuidanceRecordsInput extends PagedAndSortedResultRequestDto {
  studentId?: string | null;
  applicationId?: string | null;
}

export interface GetEmploymentOutcomeListInput extends PagedAndSortedResultRequestDto {
  studentId?: string | null;
  status?: EmploymentOutcomeStatus | null;
  onlyPrimary?: boolean | null;
  confirmedFrom?: string | null;
  confirmedTo?: string | null;
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

export interface ImportEmploymentOutcomesInput {
  fileBase64?: string;
  fileName?: string | null;
}

export interface ImportJobsInput {
  fileBase64?: string;
  fileName?: string | null;
}

export interface InterviewScheduleDto extends FullAuditedEntityDto<string> {
  applicationId?: string;
  jobPostingId?: string;
  jobTitle?: string | null;
  studentId?: string;
  studentName?: string | null;
  employerUserId?: string | null;
  interviewerId?: string | null;
  interviewerName?: string;
  interviewerPhone?: string | null;
  scheduledAt?: string;
  timeZone?: string | null;
  location?: string | null;
  meetingUrl?: string | null;
  note?: string | null;
  result?: EmploymentInterviewResult;
  summary?: string | null;
  resultComment?: string | null;
  resultRecordedAt?: string | null;
  completionMessage?: string | null;
  completedAt?: string | null;
}

export interface InterviewerCandidateDto {
  id?: string;
  name?: string;
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
  hasSnapshot?: boolean;
  snapshotAt?: string | null;
}

export interface JobCompetencyGraphDetailDto extends JobCompetencyGraphDto {
  tasks?: JobCoreTaskDto[];
  courses?: JobCourseCoverageDto[];
  courseId?: string | null;
  overallAchievement?: number | null;
}

export interface JobCompetencyGraphDto {
  id?: string;
  title?: string;
  description?: string | null;
  majorId?: string | null;
  jobPostingId?: string | null;
  jobPostingTitle?: string | null;
  job?: JobPostingBriefDto | null;
  version?: number;
  isActive?: boolean;
  taskCount?: number;
  competencyCount?: number;
  measurableCompetencyCount?: number;
  courseCount?: number;
}

export interface JobCoreTaskDto {
  id?: string;
  graphId?: string;
  title?: string;
  description?: string | null;
  weight?: number;
  sortOrder?: number;
  competencies?: JobTaskCompetencyDto[];
}

export interface JobCourseCoverageDto {
  courseId?: string;
  courseTitle?: string;
  supportedCompetencyCount?: number;
  totalCompetencyCount?: number;
  coverage?: number;
}

export interface JobCourseSupportDto {
  courseId?: string;
  courseTitle?: string;
  weight?: number;
  isCurrent?: boolean;
}

export interface JobGraphCoverageDto {
  graphId?: string;
  title?: string;
  taskCount?: number;
  competencyCount?: number;
  measurableCompetencyCount?: number;
  competencyCoverage?: number;
  tasksWithoutCompetency?: number;
  overallAchievement?: number | null;
  gaps?: string[];
}

export interface JobImportFailItemDto {
  rowNumber?: number;
  jobTitle?: string | null;
  reason?: string;
}

export interface JobImportResultDto {
  totalCount?: number;
  successCount?: number;
  failCount?: number;
  failItems?: JobImportFailItemDto[];
}

export interface JobOptionDto {
  id?: string;
  title?: string;
  companyName?: string | null;
  skillTags?: string | null;
  statusLabel?: string;
  hasGraph?: boolean;
  graphId?: string | null;
}

export interface JobPostingBriefDto {
  id?: string;
  title?: string;
  companyName?: string | null;
  industry?: string | null;
  salaryRange?: string | null;
  location?: string | null;
  status?: number;
  statusLabel?: string;
  skillTags?: string | null;
  publishedAt?: string | null;
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
  applicationStatus?: EmploymentApplicationStatus | null;
  videoUrl?: string | null;
}

export interface JobTaskCompetencyDto {
  id?: string;
  coreTaskId?: string;
  competencyId?: string;
  code?: string | null;
  name?: string;
  weight?: number;
  requiredLevel?: number;
  knowledgeCount?: number;
  achievement?: number | null;
  courses?: JobCourseSupportDto[];
}

export interface LinkJobTaskCompetencyInput {
  coreTaskId?: string;
  competencyId?: string;
  weight?: number;
  requiredLevel?: number;
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

export interface SaveJobCompetencyGraphInput {
  title?: string;
  description?: string | null;
  majorId?: string | null;
  jobPostingId?: string | null;
  version?: number;
  isActive?: boolean;
}

export interface SaveJobCoreTaskInput {
  graphId?: string;
  title?: string;
  description?: string | null;
  weight?: number;
  sortOrder?: number;
}

export interface StudentApplicationStatDto {
  studentId?: string;
  studentName?: string;
  jobTitle?: string;
  companyName?: string;
  status?: number;
  appliedAt?: string;
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

export interface VerifyEmploymentOutcomeDto {
  verified?: boolean;
  note?: string | null;
  evidenceUrl?: string | null;
}
