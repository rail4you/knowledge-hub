
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
