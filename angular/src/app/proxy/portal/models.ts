
export interface CourseBriefDto {
  id?: string;
  title?: string;
  coverImageUrl?: string | null;
  teacherName?: string | null;
  majorName?: string | null;
  majorNames?: string[];
  majorIds?: string[];
  studentCount?: number;
  difficulty?: number;
}

export interface MaterialBriefDto {
  id?: string;
  name?: string;
  fileExtension?: string | null;
  downloadCount?: number;
  coverUrl?: string | null;
  fileSize?: number;
  originalFileName?: string | null;
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
  publishedCourses?: CourseBriefDto[];
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

export interface PublicBrowseFilterOption {
  id?: string;
  name?: string;
}

export interface PublicCourseDto {
  id?: string;
  title?: string;
  coverImageUrl?: string | null;
  teacherName?: string | null;
  majorName?: string | null;
  majorId?: string | null;
  majorNames?: string[];
  majorIds?: string[];
  tenantName?: string | null;
  tenantId?: string;
  studentCount?: number;
  description?: string | null;
  /** 共享课标记：共享到社会学校，所有注册用户都能学习 */
  isShared?: boolean;
}

export interface PublicHomeStatsDto {
  tenantCount?: number;
  totalCourseCount?: number;
  totalResourceCount?: number;
  totalMicroMajorCount?: number;
}

export interface PublicMicroMajorDto {
  id?: string;
  title?: string;
  coverImageUrl?: string | null;
  courseCount?: number;
  tenantName?: string | null;
  tenantId?: string;
}

export interface PublicResourceDto {
  id?: string;
  name?: string;
  fileExtension?: string | null;
  downloadCount?: number;
  coverUrl?: string | null;
  tenantName?: string | null;
  tenantId?: string;
  fileSize?: number;
  originalFileName?: string | null;
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
  /** 建设单位（学校名称） */
  schoolName?: string | null;
  /** 资源库名称（后台配置的库名） */
  libraryName?: string | null;
}
