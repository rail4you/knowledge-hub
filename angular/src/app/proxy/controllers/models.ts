
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
  /** 机构中文名（学校档案名 → 专业库展示名 → 租户名） */
  schoolName?: string | null;
  /** 学校徽标 */
  logoUrl?: string | null;
  /** 原始租户名（slug） */
  tenantName?: string | null;
  /** 是否平台唯一「社会学校」（公开注册用户的归属机构） */
  isPublicSchool?: boolean;
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
