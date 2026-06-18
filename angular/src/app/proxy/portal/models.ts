
export interface CourseBriefDto {
  id?: string;
  title?: string;
  coverImageUrl?: string | null;
  teacherName?: string | null;
  majorName?: string | null;
  studentCount?: number;
}

export interface MaterialBriefDto {
  id?: string;
  name?: string;
  fileExtension?: string | null;
  downloadCount?: number;
  coverUrl?: string | null;
}

export interface MicroMajorBriefDto {
  id?: string;
  title?: string;
  coverImageUrl?: string | null;
  courseCount?: number;
}

export interface NewsBriefDto {
  id?: string;
  title?: string;
  publishedAt?: string | null;
}

export interface PartnerBriefDto {
  id?: string;
  name?: string;
}

export interface PortalHomeDataDto {
  tenantInfo?: TenantBriefDto;
  stats?: PortalStatsDto;
  microMajors?: MicroMajorBriefDto[];
  featuredCourses?: CourseBriefDto[];
  latestMaterials?: MaterialBriefDto[];
  latestNews?: NewsBriefDto[];
  partners?: PartnerBriefDto[];
}

export interface PortalStatsDto {
  courseCount?: number;
  resourceCount?: number;
  studentCount?: number;
  microMajorCount?: number;
}

export interface PublicHomeStatsDto {
  tenantCount?: number;
  totalCourseCount?: number;
  totalResourceCount?: number;
  totalMicroMajorCount?: number;
}

export interface TenantBriefDto {
  id?: string;
  name?: string;
  description?: string | null;
  logoUrl?: string | null;
  industryField?: string | null;
}

export interface TenantResourceSummaryDto {
  id?: string;
  name?: string;
  description?: string | null;
  logoUrl?: string | null;
  industryField?: string | null;
  tenantType?: number;
  tenantName?: string | null;
  tenantDescription?: string | null;
  coverImage?: string | null;
  courseCount?: number;
  resourceCount?: number;
  microMajorCount?: number;
}

// ── Public Browse DTOs ──

export interface PublicBrowseDto {
  courses?: PublicCourseDto[];
  resources?: PublicResourceDto[];
  microMajors?: PublicMicroMajorDto[];
  tenants?: PublicBrowseFilterOption[];
  majors?: PublicBrowseFilterOption[];
  totalCourseCount?: number;
  totalResourceCount?: number;
  totalMicroMajorCount?: number;
}

export interface PublicCourseDto {
  id?: string;
  title?: string;
  coverImageUrl?: string | null;
  teacherName?: string | null;
  majorName?: string | null;
  majorId?: string | null;
  tenantName?: string | null;
  tenantId?: string;
  studentCount?: number;
  description?: string | null;
}

export interface PublicResourceDto {
  id?: string;
  name?: string;
  fileExtension?: string | null;
  downloadCount?: number;
  coverUrl?: string | null;
  tenantName?: string | null;
  tenantId?: string;
}

export interface PublicMicroMajorDto {
  id?: string;
  title?: string;
  coverImageUrl?: string | null;
  courseCount?: number;
  tenantName?: string | null;
  tenantId?: string;
}

export interface PublicBrowseFilterOption {
  id?: string;
  name?: string;
}
