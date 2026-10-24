
export interface CurrentTenantDto {
  isSchoolDomain?: boolean;
  tenantId?: string | null;
  tenantName?: string | null;
  schoolName?: string | null;
  logoUrl?: string | null;
  domain?: string | null;
  slug?: string | null;
  baseDomains?: string[];
}

export interface JobVideoUploadResultDto {
  url?: string;
  originalFileName?: string;
  size?: number;
}

export interface LoginAttemptInfoDto {
  userExists?: boolean;
  maxFailedAccessAttempts?: number;
  accessFailedCount?: number;
  remainingAttempts?: number;
  isLockedOut?: boolean;
  lockoutEnd?: string | null;
}

export interface OssUploadResultDto {
  url?: string;
  objectKey?: string;
  originalFileName?: string;
  size?: number;
}

export interface ResumeUploadResultDto {
  url?: string;
  filePath?: string;
  originalFileName?: string;
  size?: number;
}

export interface TenantInfoDto {
  id?: string | null;
  name?: string | null;
}

export interface TenantStatsDto {
  courseCount?: number;
  resourceCount?: number;
  studentCount?: number;
  microMajorCount?: number;
  newsCount?: number;
}

export interface TenantWithStatsDto {
  id?: string | null;
  name?: string | null;
  courseCount?: number;
  resourceCount?: number;
  studentCount?: number;
}
