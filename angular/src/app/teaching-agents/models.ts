export interface PagedResultDto<T> {
  items: T[];
  totalCount: number;
}

export interface TeachingAgentSkillBinding {
  code: string;
  name: string;
  description: string;
  enabled: boolean;
}

export interface TeachingAgentPreset {
  code: string;
  name: string;
  description: string;
  systemPrompt: string;
  welcomeMessage?: string;
  suggestedTaskPrompt?: string;
  skills: TeachingAgentSkillBinding[];
}

export interface TeachingAgentVersion {
  id: string;
  teachingAgentId: string;
  versionNumber: number;
  systemPrompt: string;
  welcomeMessage?: string;
  modelId: string;
  temperature: number;
  skills: TeachingAgentSkillBinding[];
  versionNote?: string;
  isPublished: boolean;
  creationTime?: string;
}

export interface TeachingAgent {
  id: string;
  name: string;
  description?: string;
  ownerUserId: string;
  ownerUserName: string;
  visibility: number;
  status: number;
  publishedVersionId?: string;
  draftVersion?: TeachingAgentVersion;
  creationTime?: string;
  lastModificationTime?: string;
}

export interface TeachingAgentDetail extends TeachingAgent {
  publishedVersion?: TeachingAgentVersion;
  versions: TeachingAgentVersion[];
}

export interface CreateUpdateTeachingAgentPayload {
  name: string;
  description?: string;
  visibility: number;
  systemPrompt: string;
  welcomeMessage?: string;
  modelId: string;
  temperature: number;
  versionNote?: string;
  skills: TeachingAgentSkillBinding[];
}

export const FIXED_TEACHING_AGENT_MODEL = 'qwen-plus';

export interface TeachingAgentOption {
  id: string;
  name: string;
  versionId: string;
  versionNumber: number;
}

export interface StudentOption {
  id: string;
  name: string;
  userName: string;
}

export interface CourseOption {
  id: string;
  title: string;
}

export interface ResourceOption {
  id: string;
  name: string;
  resourceType: number;
}

export interface TaskCreationOptions {
  agents: TeachingAgentOption[];
  students: StudentOption[];
  courses: CourseOption[];
  resources: ResourceOption[];
}

export interface TeachingAgentCourseChapter {
  id: string;
  title: string;
  description?: string;
  sortOrder: number;
}

export interface TeachingAgentKnowledgeResource {
  id: string;
  chapterId?: string;
  name: string;
  description?: string;
  content?: string;
  importanceLevel: string;
  difficulty: number;
}

export interface TeachingAgentCourseContext {
  id: string;
  title: string;
  description?: string;
  teacherName?: string;
  major?: string;
  semester?: string;
  credits?: number;
  difficulty: number;
  chapters: TeachingAgentCourseChapter[];
  knowledgeResources: TeachingAgentKnowledgeResource[];
}

export interface TeachingAgentResourceContext {
  id: string;
  name: string;
  description?: string;
  resourceType: number;
  categoryName?: string;
  fileExtension?: string;
  originalFileName?: string;
}

export interface TeachingAgentExerciseContext {
  id: string;
  title: string;
  questionContent: string;
  type: number;
  difficulty: number;
  score: number;
  chapterId?: string;
  answerExplanation?: string;
}

export interface TaskTargetSnapshot {
  targetType: number;
  course?: TeachingAgentCourseContext;
  resource?: TeachingAgentResourceContext;
  exercises: TeachingAgentExerciseContext[];
}

export interface CreateClassroomAgentTaskPayload {
  title: string;
  description?: string;
  teachingAgentVersionId: string;
  taskPrompt: string;
  targetType: number;
  targetId: string;
  dueTime?: string | null;
  studentIds: string[];
}

export interface ClassroomAgentAssignment {
  id: string;
  classroomAgentTaskId: string;
  studentId: string;
  studentName: string;
  status: number;
  startedAt?: string;
  completedAt?: string;
  lastActiveAt?: string;
  submissionSummary?: string;
  helpReason?: string;
}

export interface ClassroomAgentTask {
  id: string;
  title: string;
  description?: string;
  teachingAgentId: string;
  teachingAgentVersionId: string;
  teachingAgentName: string;
  teachingAgentVersionNumber: number;
  taskPrompt: string;
  targetType: number;
  targetId: string;
  targetSnapshot?: TaskTargetSnapshot;
  dueTime?: string;
  publishStatus: number;
  assignmentCount: number;
  completedCount: number;
  needsHelpCount: number;
  creationTime?: string;
}

export interface ClassroomAgentTaskDetail extends ClassroomAgentTask {
  assignments: ClassroomAgentAssignment[];
}

export interface StudentAgentTask {
  assignmentId: string;
  taskId: string;
  title: string;
  description?: string;
  teachingAgentName: string;
  status: number;
  dueTime?: string;
  lastActiveAt?: string;
}

export interface AgentRunMessage {
  id: string;
  agentRunId: string;
  role: string;
  content: string;
  toolCallsJson: string;
  creationTime?: string;
}

export interface AgentRun {
  id: string;
  classroomAgentAssignmentId: string;
  threadId: string;
  runtimeStatus: number;
  startedAt: string;
  endedAt?: string;
  lastError?: string;
}

export interface AgentRunDetail {
  task: ClassroomAgentTaskDetail;
  assignment: ClassroomAgentAssignment;
  run: AgentRun;
  messages: AgentRunMessage[];
}

export interface AgentMessageChunk {
  content: string;
  threadId: string;
  isComplete: boolean;
}

export const TEACHING_AGENT_VISIBILITY = {
  private: 0,
  school: 1,
  public: 2,
} as const;

export const TEACHING_AGENT_STATUS = {
  draft: 0,
  published: 1,
  archived: 2,
} as const;

export const CLASSROOM_AGENT_TARGET_TYPE = {
  course: 0,
  resource: 1,
  exerciseSet: 2,
} as const;

export const CLASSROOM_AGENT_PUBLISH_STATUS = {
  draft: 0,
  published: 1,
  closed: 2,
} as const;

export const CLASSROOM_AGENT_ASSIGNMENT_STATUS = {
  pending: 0,
  inProgress: 1,
  submitted: 2,
  needsTeacherHelp: 3,
} as const;

export const DEFAULT_SKILL_CATALOG: TeachingAgentSkillBinding[] = [
  {
    code: 'course_explainer',
    name: '课程讲解',
    description: '读取课程大纲、章节与知识点，负责课堂讲解和结构化梳理。',
    enabled: true,
  },
  {
    code: 'resource_guide',
    name: '资源导读',
    description: '围绕课程资源进行摘要、重点提示与阅读引导。',
    enabled: true,
  },
  {
    code: 'exercise_coach',
    name: '习题辅导',
    description: '读取习题上下文，给出逐步提示与答题策略。',
    enabled: true,
  },
  {
    code: 'classroom_task_assistant',
    name: '课堂任务助手',
    description: '围绕教师任务目标引导学生完成步骤并输出提交摘要。',
    enabled: true,
  },
];

export function formatDateTime(value?: string): string {
  if (!value) {
    return '未设置';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

export function assignmentStatusLabel(status: number): string {
  switch (status) {
    case CLASSROOM_AGENT_ASSIGNMENT_STATUS.inProgress:
      return '进行中';
    case CLASSROOM_AGENT_ASSIGNMENT_STATUS.submitted:
      return '已提交';
    case CLASSROOM_AGENT_ASSIGNMENT_STATUS.needsTeacherHelp:
      return '需教师介入';
    default:
      return '待开始';
  }
}

export function publishStatusLabel(status: number): string {
  switch (status) {
    case CLASSROOM_AGENT_PUBLISH_STATUS.published:
      return '已发布';
    case CLASSROOM_AGENT_PUBLISH_STATUS.closed:
      return '已关闭';
    default:
      return '草稿';
  }
}

export function targetTypeLabel(targetType: number): string {
  switch (targetType) {
    case CLASSROOM_AGENT_TARGET_TYPE.resource:
      return '资源任务';
    case CLASSROOM_AGENT_TARGET_TYPE.exerciseSet:
      return '习题任务';
    default:
      return '课程任务';
  }
}

export function visibilityLabel(visibility: number): string {
  switch (visibility) {
    case TEACHING_AGENT_VISIBILITY.school:
      return '校内共享';
    case TEACHING_AGENT_VISIBILITY.public:
      return '全局公开';
    default:
      return '仅自己可见';
  }
}

export function agentStatusLabel(status: number): string {
  switch (status) {
    case TEACHING_AGENT_STATUS.published:
      return '已发布';
    case TEACHING_AGENT_STATUS.archived:
      return '已停用';
    default:
      return '草稿';
  }
}
