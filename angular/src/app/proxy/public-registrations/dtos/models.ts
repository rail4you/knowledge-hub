
export interface PublicRegistrationDto {
  userName: string;
  email: string;
  password: string;
  name?: string | null;
  surname?: string | null;
  phoneNumber?: string | null;
}

export interface PublicRegistrationResultDto {
  userId?: string;
  userName?: string;
  socialSchoolName?: string;
  socialSchoolTenantId?: string;
}
