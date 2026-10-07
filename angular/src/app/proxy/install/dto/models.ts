
export interface EditionDto {
  edition?: string;
  maxTenantCount?: number;
  isAllianceEnabled?: boolean;
  isTwoLevelApprovalEnabled?: boolean;
  isSpecialEducationEnabled?: boolean;
}

export interface EditionUpgradeInputDto {
  licenseKey?: string;
  installToken?: string | null;
}

export interface InstallInputDto {
  licenseKey?: string;
  installToken?: string | null;
  edition?: string;
  adminUsername?: string;
  adminPassword?: string;
  adminEmail?: string;
}

export interface InstallStatusDto {
  isInstalled?: boolean;
  currentEdition?: string;
}
