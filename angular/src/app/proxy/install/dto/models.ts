
export interface EditionDto {
  edition?: string;
  maxTenantCount?: number;
  isAllianceEnabled?: boolean;
  isTwoLevelApprovalEnabled?: boolean;
}

export interface EditionUpgradeInputDto {
  licenseKey?: string;
}

export interface InstallInputDto {
  licenseKey?: string;
  edition?: string;
  adminUsername?: string;
  adminPassword?: string;
  adminEmail?: string;
}

export interface InstallStatusDto {
  isInstalled?: boolean;
  currentEdition?: string;
}
