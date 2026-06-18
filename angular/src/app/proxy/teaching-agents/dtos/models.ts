import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { AgentRunStatus } from '../enums/agent-run-status.enum';
import type { ClassroomAgentAssignmentStatus } from '../enums/classroom-agent-assignment-status.enum';
import type { ClassroomAgentTaskTargetType } from '../enums/classroom-agent-task-target-type.enum';
import type { ClassroomAgentTaskPublishStatus } from '../enums/classroom-agent-task-publish-status.enum';
import type { TeachingAgentVisibility } from '../enums/teaching-agent-visibility.enum';
import type { ResourceType } from '../../resources/enums/resource-type.enum';
import type { TeachingAgentStatus } from '../enums/teaching-agent-status.enum';
import type { ExerciseType } from '../../exams/enums/exercise-type.enum';

export interface AgentMessageChunkDto {
  content?: string;
  threadId?: string;
  isComplete?: boolean;
}

export interface AgentRunDetailDto {
  task?: ClassroomAgentTaskDetailDto;
  assignment?: ClassroomAgentAssignmentDto;
  run?: AgentRunDto;
  messages?: AgentRunMessageDto[];
}

export interface AgentRunDto extends FullAuditedEntityDto<string> {
  classroomAgentAssignmentId?: string;
  threadId?: string;
  runtimeStatus?: AgentRunStatus;
  startedAt?: string;
  endedAt?: string | null;
  lastError?: string | null;
}

export interface AgentRunMessageDto extends FullAuditedEntityDto<string> {
  agentRunId?: string;
  role?: string;
  content?: string;
  toolCallsJson?: string;
}

export interface ClassroomAgentAssignmentDto extends FullAuditedEntityDto<string> {
  classroomAgentTaskId?: string;
  studentId?: string;
  studentName?: string;
  status?: ClassroomAgentAssignmentStatus;
  startedAt?: string | null;
  completedAt?: string | null;
  lastActiveAt?: string | null;
  submissionSummary?: string | null;
  helpReason?: string | null;
  teacherResponse?: string | null;
}

export interface ClassroomAgentTaskDetailDto extends ClassroomAgentTaskDto {
  assignments?: ClassroomAgentAssignmentDto[];
}

export interface ClassroomAgentTaskDto extends FullAuditedEntityDto<string> {
  title?: string;
  description?: string | null;
  teachingAgentId?: string;
  teachingAgentVersionId?: string;
  teachingAgentName?: string;
  teachingAgentVersionNumber?: number;
  taskPrompt?: string;
  targetType?: ClassroomAgentTaskTargetType;
  targetId?: string;
  targetSnapshot?: TaskTargetSnapshotDto | null;
  dueTime?: string | null;
  publishStatus?: ClassroomAgentTaskPublishStatus;
  assignmentCount?: number;
  completedCount?: number;
  needsHelpCount?: number;
}

export interface CloneTeachingAgentFromPresetDto {
  presetCode?: string;
  name?: string;
  visibility?: TeachingAgentVisibility;
}

export interface CourseOptionDto {
  id?: string;
  title?: string;
}

export interface CreateClassroomAgentTaskDto {
  title?: string;
  description?: string | null;
  teachingAgentVersionId?: string;
  taskPrompt?: string;
  targetType?: ClassroomAgentTaskTargetType;
  targetId?: string;
  dueTime?: string | null;
  studentIds?: string[];
}

export interface CreateUpdateTeachingAgentDto {
  name?: string;
  description?: string | null;
  visibility?: TeachingAgentVisibility;
  systemPrompt?: string;
  welcomeMessage?: string | null;
  modelId?: string;
  temperature?: number;
  versionNote?: string | null;
  skills?: TeachingAgentSkillBindingDto[];
}

export interface NeedTeacherHelpDto {
  reason?: string;
}

export interface PagedTeachingAgentRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface PublishTeachingAgentVersionDto {
  versionNote?: string | null;
}

export interface ResourceOptionDto {
  id?: string;
  name?: string;
  resourceType?: ResourceType;
}

export interface SendAgentRunMessageDto {
  message?: string;
}

export interface StudentAgentTaskDto {
  assignmentId?: string;
  taskId?: string;
  title?: string;
  description?: string | null;
  teachingAgentName?: string;
  status?: ClassroomAgentAssignmentStatus;
  dueTime?: string | null;
  lastActiveAt?: string | null;
}

export interface StudentOptionDto {
  id?: string;
  name?: string;
  userName?: string;
}

export interface SubmitAgentAssignmentDto {
  summary?: string;
}

export interface TaskCreationOptionsDto {
  agents?: TeachingAgentOptionDto[];
  students?: StudentOptionDto[];
  courses?: CourseOptionDto[];
  resources?: ResourceOptionDto[];
}

export interface TaskTargetSnapshotDto {
  targetType?: ClassroomAgentTaskTargetType;
  course?: TeachingAgentCourseContextDto | null;
  resource?: TeachingAgentResourceContextDto | null;
  exercises?: TeachingAgentExerciseContextDto[];
}

export interface TeacherRespondDto {
  response?: string;
}

export interface TeachingAgentCourseChapterDto {
  id?: string;
  title?: string;
  description?: string | null;
  sortOrder?: number;
}

export interface TeachingAgentCourseContextDto {
  id?: string;
  title?: string;
  description?: string | null;
  teacherName?: string | null;
  majorId?: string | null;
  majorName?: string | null;
  semester?: string | null;
  credits?: number | null;
  difficulty?: number;
  chapters?: TeachingAgentCourseChapterDto[];
  knowledgeResources?: TeachingAgentKnowledgeResourceDto[];
}

export interface TeachingAgentDetailDto extends TeachingAgentDto {
  publishedVersion?: TeachingAgentVersionDto | null;
  versions?: TeachingAgentVersionDto[];
}

export interface TeachingAgentDto extends FullAuditedEntityDto<string> {
  name?: string;
  description?: string | null;
  ownerUserId?: string;
  ownerUserName?: string;
  visibility?: TeachingAgentVisibility;
  status?: TeachingAgentStatus;
  publishedVersionId?: string | null;
  draftVersion?: TeachingAgentVersionDto | null;
}

export interface TeachingAgentExerciseContextDto {
  id?: string;
  title?: string;
  questionContent?: string;
  type?: ExerciseType;
  difficulty?: number;
  score?: number;
  chapterId?: string | null;
  answerExplanation?: string | null;
}

export interface TeachingAgentKnowledgeResourceDto {
  id?: string;
  chapterId?: string | null;
  name?: string;
  description?: string | null;
  content?: string | null;
  importanceLevel?: string;
  difficulty?: number;
}

export interface TeachingAgentOptionDto {
  id?: string;
  name?: string;
  versionId?: string;
  versionNumber?: number;
}

export interface TeachingAgentPresetDto {
  code?: string;
  name?: string;
  description?: string;
  systemPrompt?: string;
  welcomeMessage?: string;
  suggestedTaskPrompt?: string;
  skills?: TeachingAgentSkillBindingDto[];
}

export interface TeachingAgentResourceContextDto {
  id?: string;
  name?: string;
  description?: string | null;
  resourceType?: ResourceType;
  categoryName?: string | null;
  fileExtension?: string | null;
  originalFileName?: string | null;
}

export interface TeachingAgentSkillBindingDto {
  code?: string;
  name?: string;
  description?: string;
  enabled?: boolean;
}

export interface TeachingAgentVersionDto extends FullAuditedEntityDto<string> {
  teachingAgentId?: string;
  versionNumber?: number;
  systemPrompt?: string;
  welcomeMessage?: string | null;
  modelId?: string;
  temperature?: number;
  skills?: TeachingAgentSkillBindingDto[];
  versionNote?: string | null;
  isPublished?: boolean;
}
