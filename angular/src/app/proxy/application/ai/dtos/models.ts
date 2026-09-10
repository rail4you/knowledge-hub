
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
