import type { EnterpriseMemberRole } from '../enterprise-member-role.enum';
import type { EnterpriseCertificationStatus } from '../enterprise-certification-status.enum';

export interface BindEnterpriseMemberDto {
  enterpriseProfileId?: string;
  userId?: string;
  role?: EnterpriseMemberRole;
  isPrimary?: boolean;
}

export interface CreateUpdateEnterpriseProfileDto {
  name?: string;
  unifiedSocialCreditCode?: string | null;
  industry?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  summary?: string | null;
  logoUrl?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
}

export interface EnterpriseMemberDto {
  id?: string;
  enterpriseProfileId?: string;
  userId?: string;
  userName?: string | null;
  role?: EnterpriseMemberRole;
  isPrimary?: boolean;
}

export interface EnterpriseProfileDto {
  id?: string;
  tenantId?: string | null;
  name?: string;
  unifiedSocialCreditCode?: string | null;
  industry?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  summary?: string | null;
  logoUrl?: string | null;
  certificationStatus?: EnterpriseCertificationStatus;
  certificationStatusName?: string;
  rejectReason?: string | null;
  ownerUserId?: string | null;
  certifiedAt?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
  isUsable?: boolean;
  memberCount?: number;
}

export interface SchoolValidityDto {
  tenantId?: string;
  tenantName?: string | null;
  schoolName?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
  isValid?: boolean;
  readOnlyAfterExpiry?: boolean;
  statusName?: string;
}

export interface SetSchoolValidityDto {
  tenantId?: string;
  validFrom?: string | null;
  validTo?: string | null;
  readOnlyAfterExpiry?: boolean;
}
