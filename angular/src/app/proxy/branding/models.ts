
export interface BrandingDto {
  appTitle?: string;
  appSubtitle?: string;
  footerText?: string;
  logoUrl?: string;
}

export interface UpdateBrandingDto {
  appTitle: string;
  appSubtitle?: string;
  footerText?: string;
  logoUrl?: string | null;
}
