import type { ResourceLibraryStatus } from '../resource-library-status.enum';
import type { ResourceLibraryMemberRole } from '../resource-library-member-role.enum';

export interface AddResourceLibraryMemberDto {
  memberTenantId?: string;
}

export interface CreateResourceLibraryDto {
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  primaryMajorId?: string | null;
  isPublic?: boolean | null;
  sortOrder?: number | null;
}

export interface ResourceLibraryDto {
  id?: string;
  tenantId?: string | null;
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  primaryMajorId?: string | null;
  primaryMajorName?: string | null;
  ownerTenantId?: string;
  ownerTenantName?: string | null;
  status?: ResourceLibraryStatus;
  isPublic?: boolean;
  sortOrder?: number;
  resourceCount?: number;
  courseCount?: number;
  memberCount?: number;
  currentRole?: number;
  isOwner?: boolean;
  members?: ResourceLibraryMemberDto[];
}

export interface ResourceLibraryListRequestDto {
  primaryMajorId?: string | null;
  onlyJoined?: boolean | null;
}

export interface ResourceLibraryMemberDto {
  id?: string;
  memberTenantId?: string;
  memberTenantName?: string | null;
  role?: ResourceLibraryMemberRole;
  joinedTime?: string;
}

export interface UpdateResourceLibraryDto {
  name?: string | null;
  description?: string | null;
  coverUrl?: string | null;
  primaryMajorId?: string | null;
  isPublic?: boolean | null;
  sortOrder?: number | null;
  status?: ResourceLibraryStatus | null;
}
