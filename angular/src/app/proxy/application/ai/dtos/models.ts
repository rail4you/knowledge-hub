
export interface CareerGuidanceGenerationInputDto {
  resourceId?: string;
  careerGoal?: string | null;
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
}

export interface ResourceForChatDto {
  id?: string;
  name?: string;
  fileExtension?: string | null;
  sourceFormat?: string | null;
  nodeCount?: number;
}
