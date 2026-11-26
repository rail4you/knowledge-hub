
export interface CreateWhiteboardDto {
  title?: string;
  topic?: string | null;
  subject?: string | null;
  description?: string | null;
  courseId?: string | null;
}

export interface InvitableTeacherDto {
  userId?: string;
  userName?: string;
  name?: string | null;
  joined?: boolean;
}

export interface InviteWhiteboardMembersDto {
  userIds?: string[];
  role?: number;
}

export interface UpdateWhiteboardDto {
  title?: string | null;
  topic?: string | null;
  subject?: string | null;
  description?: string | null;
  courseId?: string | null;
  clearCourse?: boolean;
}

export interface WhiteboardAssistantAskInputDto {
  whiteboardId?: string;
  message?: string;
  mode?: string;
  selectedText?: string | null;
  history?: WhiteboardAssistantTurnDto[] | null;
}

export interface WhiteboardAssistantContextDto {
  courseId?: string | null;
  courseName?: string | null;
  boardTitle?: string | null;
  canvasTextCount?: number;
  chatMessageCount?: number;
  selectedText?: string | null;
}

export interface WhiteboardAssistantHistoryItemDto {
  id?: string;
  role?: string;
  content?: string;
  imageUrl?: string | null;
  imagePrompt?: string | null;
  creationTime?: string;
}

export interface WhiteboardAssistantReplyDto {
  content?: string;
  imageUrl?: string | null;
  imagePrompt?: string | null;
  context?: WhiteboardAssistantContextDto;
}

export interface WhiteboardAssistantTurnDto {
  role?: string;
  content?: string;
}

export interface WhiteboardDetailDto extends WhiteboardListItemDto {
  description?: string | null;
  status?: number;
  creatorId?: string | null;
  viewX?: number;
  viewY?: number;
  viewScale?: number;
  members?: WhiteboardMemberDto[];
  elements?: WhiteboardElementDto[];
  canEdit?: boolean;
  myColor?: string;
  myName?: string;
}

export interface WhiteboardElementDto {
  key?: string;
  json?: string;
  elementType?: number;
  lastEditorName?: string | null;
  lastModificationTime?: string | null;
}

export interface WhiteboardListItemDto {
  id?: string;
  title?: string;
  topic?: string | null;
  subject?: string | null;
  courseId?: string | null;
  courseName?: string | null;
  creatorName?: string;
  memberCount?: number;
  elementCount?: number;
  isOwner?: boolean;
  creationTime?: string;
  lastModificationTime?: string | null;
}

export interface WhiteboardMemberDto {
  userId?: string;
  userName?: string;
  color?: string;
  role?: number;
  roleName?: string;
  joinedAt?: string;
  online?: boolean;
}
