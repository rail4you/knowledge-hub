import type { FullAuditedEntityDto } from '@abp/ng.core';
import type { CheckInSessionStatus } from '../enums/check-in-session-status.enum';
import type { ClassroomTokenKind } from '../enums/classroom-token-kind.enum';
import type { GroupMode } from '../enums/group-mode.enum';
import type { GroupTaskStatus } from '../enums/group-task-status.enum';
import type { ClassroomPracticeStatus } from '../enums/classroom-practice-status.enum';

export interface AssignGroupItemInput {
  name?: string;
  studentIds?: string[];
}

export interface AssignGroupsInput {
  groupTaskId?: string;
  groups?: AssignGroupItemInput[];
}

export interface CheckInAbsentDto {
  studentId?: string;
  studentName?: string | null;
  studentUserName?: string | null;
}

export interface CheckInRecordDto {
  id?: string;
  sessionId?: string;
  studentId?: string;
  studentName?: string | null;
  studentUserName?: string | null;
  checkedInAt?: string;
  location?: string | null;
}

export interface CheckInRosterDto {
  sessionId?: string;
  checkedIn?: CheckInRecordDto[];
  absent?: CheckInAbsentDto[];
}

export interface CheckInSessionDto extends FullAuditedEntityDto<string> {
  courseId?: string;
  courseTitle?: string | null;
  title?: string;
  description?: string | null;
  token?: string;
  status?: CheckInSessionStatus;
  startTime?: string;
  endTime?: string | null;
  requireLocation?: boolean;
  latitude?: number | null;
  longitude?: number | null;
  radiusMeters?: number | null;
  checkedInCount?: number;
  expectedCount?: number;
  iCheckedIn?: boolean;
  shortUrl?: string;
}

export interface ClassroomTokenSessionDto {
  kind?: ClassroomTokenKind;
  sessionId?: string;
  tenantId?: string;
  tenantName?: string | null;
  courseId?: string;
  courseTitle?: string | null;
  title?: string;
  description?: string | null;
  status?: string;
  isOpen?: boolean;
  startTime?: string | null;
  endTime?: string | null;
  dueAt?: string | null;
  questionCount?: number | null;
  maxScore?: number;
  participated?: boolean;
  myGroupName?: string | null;
}

export interface CreateCheckInSessionInput {
  courseId?: string;
  title?: string;
  description?: string | null;
  durationMinutes?: number | null;
  requireLocation?: boolean;
  latitude?: number | null;
  longitude?: number | null;
  radiusMeters?: number | null;
}

export interface CreateDiscussionInput {
  courseId?: string;
  chapterId?: string | null;
  knowledgeResourceId?: string | null;
  title?: string;
  content?: string;
}

export interface CreateDiscussionReplyInput {
  discussionId?: string;
  parentReplyId?: string | null;
  content?: string;
}

export interface CreateGroupTaskInput {
  courseId?: string;
  title?: string;
  description?: string | null;
  dueAt?: string | null;
  maxScore?: number;
  groupMode?: GroupMode;
  groupCount?: number | null;
  publishImmediately?: boolean;
}

export interface CreatePracticeSessionInput {
  courseId?: string;
  title?: string;
  chapterId?: string | null;
  knowledgeResourceIds?: string[] | null;
  questionCount?: number;
}

export interface DiscussionDto extends FullAuditedEntityDto<string> {
  courseId?: string;
  chapterId?: string | null;
  knowledgeResourceId?: string | null;
  title?: string;
  content?: string;
  authorId?: string;
  authorName?: string | null;
  isPinned?: boolean;
  isResolved?: boolean;
  replyCount?: number;
  viewCount?: number;
  lastReplyAt?: string | null;
  isMine?: boolean;
  replies?: DiscussionReplyDto[];
}

export interface DiscussionReplyDto extends FullAuditedEntityDto<string> {
  discussionId?: string;
  parentReplyId?: string | null;
  content?: string;
  authorId?: string;
  authorName?: string | null;
  isAccepted?: boolean;
}

export interface DoCheckInInput {
  token?: string;
  latitude?: number | null;
  longitude?: number | null;
}

export interface DoCheckInResultDto {
  sessionId?: string;
  title?: string;
  checkedInAt?: string;
  isNew?: boolean;
}

export interface GetDiscussionListInput {
  courseId?: string | null;
  chapterId?: string | null;
  onlyUnresolved?: boolean;
  onlyMine?: boolean;
  filter?: string | null;
  skipCount?: number;
  maxResultCount?: number;
}

export interface GradeGroupTaskInput {
  submissionId?: string;
  score?: number;
  feedback?: string | null;
}

export interface GroupMemberDto {
  studentId?: string;
  studentName?: string | null;
  studentUserName?: string | null;
}

export interface GroupTaskDetailDto extends GroupTaskDto {
  groups?: StudentGroupDto[];
}

export interface GroupTaskDto extends FullAuditedEntityDto<string> {
  courseId?: string;
  courseTitle?: string | null;
  title?: string;
  description?: string | null;
  token?: string;
  status?: GroupTaskStatus;
  groupMode?: GroupMode;
  publishAt?: string | null;
  dueAt?: string | null;
  maxScore?: number;
  groupCount?: number;
  submittedGroupCount?: number;
  studentCount?: number;
  shortUrl?: string;
}

export interface GroupTaskSubmissionDto {
  id?: string;
  groupTaskId?: string;
  groupId?: string;
  groupName?: string | null;
  content?: string | null;
  attachmentResourceId?: string | null;
  attachmentFileName?: string | null;
  submittedById?: string;
  submittedByName?: string | null;
  submittedAt?: string;
  score?: number | null;
  feedback?: string | null;
  gradedAt?: string | null;
}

export interface MyCheckInRecordDto {
  sessionId?: string;
  sessionTitle?: string;
  courseId?: string;
  courseTitle?: string | null;
  checkedInAt?: string;
  startTime?: string;
}

export interface PracticeAnswerResultDto {
  exerciseId?: string;
  title?: string;
  answer?: string | null;
  correctAnswer?: string | null;
  isCorrect?: boolean | null;
  answerExplanation?: string | null;
}

export interface PracticeAttemptResultDto {
  attemptId?: string;
  sessionId?: string;
  submittedAt?: string;
  correctCount?: number;
  totalCount?: number;
  accuracy?: number;
  answers?: PracticeAnswerResultDto[];
}

export interface PracticeItemStatsDto {
  exerciseId?: string;
  title?: string;
  answeredCount?: number;
  correctCount?: number;
  correctRate?: number;
}

export interface PracticePaperDto {
  sessionId?: string;
  title?: string;
  status?: ClassroomPracticeStatus;
  courseId?: string;
  courseTitle?: string | null;
  items?: PracticePaperItemDto[];
  myAttempt?: PracticeAttemptResultDto | null;
}

export interface PracticePaperItemDto {
  exerciseId?: string;
  sortOrder?: number;
  title?: string;
  questionContent?: string;
  type?: number;
  options?: string | null;
}

export interface PracticeSessionDto extends FullAuditedEntityDto<string> {
  courseId?: string;
  courseTitle?: string | null;
  title?: string;
  token?: string;
  status?: ClassroomPracticeStatus;
  chapterId?: string | null;
  knowledgeResourceIds?: string | null;
  questionCount?: number;
  startedAt?: string;
  endedAt?: string | null;
  submittedCount?: number;
  expectedCount?: number;
  submitRate?: number;
  shortUrl?: string;
}

export interface PracticeStatsDto {
  sessionId?: string;
  title?: string;
  submittedCount?: number;
  expectedCount?: number;
  submitRate?: number;
  averageAccuracy?: number;
  items?: PracticeItemStatsDto[];
  submitters?: PracticeSubmitterDto[];
  notSubmitted?: CheckInAbsentDto[];
}

export interface PracticeSubmitterDto {
  studentId?: string;
  studentName?: string | null;
  submittedAt?: string;
  correctCount?: number;
  totalCount?: number;
}

export interface ResolveClassroomTokenInput {
  token?: string;
  kind?: ClassroomTokenKind;
}

export interface StudentGroupDto {
  id?: string;
  name?: string;
  sortOrder?: number;
  members?: GroupMemberDto[];
  submission?: GroupTaskSubmissionDto | null;
}

export interface SubmitGroupTaskInput {
  token?: string;
  content?: string | null;
  attachmentResourceId?: string | null;
}

export interface SubmitPracticeAnswerInput {
  exerciseId?: string;
  answer?: string | null;
}

export interface SubmitPracticeInput {
  token?: string;
  answers?: SubmitPracticeAnswerInput[];
}
