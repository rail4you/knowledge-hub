import type { ResourceLibraryMemberDto } from '../../resource-libraries/dtos/models';

export interface DomainSettingsDto {
  baseDomains?: string[];
}

export interface PublicSchoolDto {
  tenantId?: string | null;
  name?: string | null;
  logoUrl?: string | null;
  configured?: boolean;
}

export interface SchoolDto {
  id?: string;
  tenantId?: string;
  name?: string;
  logoUrl?: string | null;
  bannerUrl?: string | null;
  introduction?: string | null;
  website?: string | null;
  address?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  isPublic?: boolean;
  sortOrder?: number;
  isPublicSchool?: boolean;
  domain?: string | null;
  slug?: string | null;
}

export interface SchoolLibraryDto {
  id?: string;
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  primaryMajorId?: string | null;
  primaryMajorName?: string | null;
  ownerTenantId?: string;
  ownerTenantName?: string | null;
  resourceCount?: number;
  courseCount?: number;
  members?: ResourceLibraryMemberDto[];
}

export interface SchoolMajorDto {
  id?: string;
  name?: string;
  code?: string | null;
  description?: string | null;
  courseCount?: number;
  resourceCount?: number;
}

export interface SchoolProfileDto {
  school?: SchoolDto;
  majorCount?: number;
  courseCount?: number;
  resourceCount?: number;
  libraryCount?: number;
  majors?: SchoolMajorDto[];
  libraries?: SchoolLibraryDto[];
}

export interface SetBaseDomainsDto {
  baseDomains?: string[];
}

export interface UpdateSchoolDto {
  name?: string | null;
  logoUrl?: string | null;
  bannerUrl?: string | null;
  introduction?: string | null;
  website?: string | null;
  address?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  isPublic?: boolean | null;
  sortOrder?: number | null;
  domain?: string | null;
  slug?: string | null;
}
