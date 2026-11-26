
export interface CreateStudioDto {
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  leaderUserId?: string | null;
}

export interface CreateStudioMaterialDto {
  resourceId?: string;
  title?: string;
  description?: string | null;
  submitNow?: boolean;
}

export interface ReviewStudioMaterialDto {
  approved?: boolean;
  comment?: string | null;
}

export interface StudioDetailDto extends StudioListItemDto {
  members?: StudioMemberDto[];
  materials?: StudioMaterialDto[];
}

export interface StudioListItemDto {
  id?: string;
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  leaderUserId?: string | null;
  leaderName?: string | null;
  status?: number;
  statusName?: string;
  memberCount?: number;
  publishedCount?: number;
  pendingCount?: number;
  isLeader?: boolean;
  isMember?: boolean;
  canReview?: boolean;
  creationTime?: string;
}

export interface StudioMaterialDto {
  id?: string;
  studioId?: string;
  resourceId?: string;
  title?: string;
  description?: string | null;
  status?: number;
  statusName?: string;
  submittedByName?: string;
  submittedAt?: string;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  reviewComment?: string | null;
  publishedAt?: string | null;
  fileExtension?: string | null;
  fileSize?: number | null;
  originalFileName?: string | null;
  canSubmit?: boolean;
  canReview?: boolean;
  canPreview?: boolean;
}

export interface StudioMemberDto {
  userId?: string;
  userName?: string;
  role?: number;
  roleName?: string;
  joinedAt?: string;
}

export interface StudioMemberInputDto {
  userId?: string;
  role?: number;
}

export interface UpdateStudioDto {
  name?: string | null;
  description?: string | null;
  coverUrl?: string | null;
  leaderUserId?: string | null;
  status?: number | null;
}
