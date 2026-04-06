
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

export interface ResourceForChatDto {
  id?: string;
  name?: string;
  fileExtension?: string | null;
  sourceFormat?: string | null;
  nodeCount?: number;
}
