export interface InstallStatusDto {
  isInstalled: boolean;
  currentEdition: string;
}

export interface InstallInputDto {
  licenseKey: string;
  installToken?: string;
  edition: string;
  adminUsername: string;
  adminPassword: string;
  adminEmail: string;
}