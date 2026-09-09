
export interface CareerGuidanceGenerationInputDto {
  resourceId?: string | null;
  careerGoal?: string | null;
  resumeContent?: string | null;
  resumeTitle?: string | null;
  attachmentUrl?: string | null;
}

export interface CaseAnalysisGenerationInputDto {
  resourceId?: string;
  focusArea?: string | null;
}

export interface ChatInputDto {
  message?: string;
  threadId?: string | null;
  resourceId?: string | null;
  fileUrls?: FileUrlDto[] | null;
}

export interface ChatMessageChunkDto {
  content?: string;
  threadId?: string;
  isComplete?: boolean;
}

export interface ChatMessageDto {
  id?: string;
  role?: string;
  content?: string;
  createdAt?: string;
}

export interface ChatThreadDto {
  id?: string;
  title?: string | null;
  resourceId?: string | null;
  resourceName?: string | null;
  messageCount?: number;
  lastMessage?: string | null;
  createdAt?: string;
  messages?: ChatMessageDto[];
}

export interface FileUrlDto {
  url?: string;
  type?: string;
}

export interface LessonPlanGenerationInputDto {
  resourceId?: string;
  topic?: string;
  subject?: string | null;
  grade?: string | null;
  duration?: number;
  customPrompt?: string | null;
}

export interface ResourceForChatDto {
  id?: string;
  name?: string;
  fileExtension?: string | null;
  sourceFormat?: string | null;
  nodeCount?: number;
  hasPageIndex?: boolean;
  hasSummary?: boolean;
  categoryId?: string | null;
  categoryName?: string | null;
}
