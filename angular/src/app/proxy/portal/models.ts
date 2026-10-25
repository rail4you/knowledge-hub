
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
  isShared?: boolean;
  semesterHours?: number;
  credits?: number;
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
  isShared?: boolean;
  semesterHours?: number;
  credits?: number;
}

export interface PublicHomeStatsDto {
  tenantCount?: number;
  totalCourseCount?: number;
  totalResourceCount?: number;
  totalMicroMajorCount?: number;
}

export interface PublicLibraryCourseDto {
  id?: string;
  title?: string;
  coverImageUrl?: string | null;
  teacherName?: string | null;
  majorName?: string | null;
  description?: string | null;
  studentCount?: number;
  semesterHours?: number;
  credits?: number;
  difficulty?: number;
}

export interface PublicLibraryMemberDto {
  memberTenantId?: string;
  memberTenantName?: string | null;
  role?: number;
  joinedTime?: string;
}

export interface PublicLibraryMicroMajorDto {
  id?: string;
  title?: string;
  summary?: string | null;
  description?: string | null;
  coverImageUrl?: string | null;
  collaborationUnit?: string | null;
  courseCount?: number;
  resourceCount?: number;
}

export interface PublicLibraryResourceDto {
  id?: string;
  name?: string;
  fileExtension?: string | null;
  originalFileName?: string | null;
  fileSize?: number;
  downloadCount?: number;
  coverUrl?: string | null;
  resourceTypeName?: string | null;
  creationTime?: string | null;
}

export interface PublicLibraryTeacherDto {
  name?: string;
  courseCount?: number;
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

export interface PublicResourceLibraryDetailDto extends PublicResourceLibraryDto {
  members?: PublicLibraryMemberDto[];
  courses?: PublicLibraryCourseDto[];
  resources?: PublicLibraryResourceDto[];
  microMajors?: PublicLibraryMicroMajorDto[];
  teachers?: PublicLibraryTeacherDto[];
  studentCount?: number;
}

export interface PublicResourceLibraryDto {
  id?: string;
  name?: string;
  description?: string | null;
  coverUrl?: string | null;
  primaryMajorId?: string | null;
  primaryMajorName?: string | null;
  ownerTenantId?: string;
  ownerTenantName?: string | null;
  ownerSchoolLogoUrl?: string | null;
  principal?: string | null;
  memberCount?: number;
  partnerNames?: string[];
  courseCount?: number;
  resourceCount?: number;
  microMajorCount?: number;
  status?: number;
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
  libraryName?: string | null;
  schoolName?: string | null;
  tenantDescription?: string | null;
  coverImage?: string | null;
  courseCount?: number;
  resourceCount?: number;
  microMajorCount?: number;
}
