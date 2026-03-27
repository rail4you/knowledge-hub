
export interface ChatInputDto {
  message?: string;
  threadId?: string | null;
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
  createdAt?: string;
  messages?: ChatMessageDto[];
}

export interface FileUrlDto {
  url?: string;
  type?: string;
}
