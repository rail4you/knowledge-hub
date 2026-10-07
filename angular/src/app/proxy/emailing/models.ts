
export interface SendSmtpTestEmailDto {
  to?: string;
}

export interface SmtpConfigDto {
  host?: string;
  port?: number;
  userName?: string;
  hasPassword?: boolean;
  fromAddress?: string;
  fromName?: string;
  isConfigured?: boolean;
}

export interface SmtpTestResultDto {
  success?: boolean;
  error?: string | null;
}

export interface UpdateSmtpConfigDto {
  host?: string;
  port?: number;
  userName?: string;
  password?: string | null;
  clearPassword?: boolean;
  fromAddress?: string;
  fromName?: string;
}
