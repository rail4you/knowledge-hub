import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';

export interface SmtpConfigDto {
  host: string;
  port: number;
  userName: string;
  hasPassword: boolean;
  fromAddress: string;
  fromName: string;
  isConfigured: boolean;
}

export interface UpdateSmtpConfigDto {
  host: string;
  port: number;
  userName: string;
  password?: string;
  clearPassword?: boolean;
  fromAddress: string;
  fromName: string;
}

export interface SmtpTestResultDto {
  success: boolean;
  error?: string;
}

/**
 * SMTP 邮件设置（手写服务，非 proxy 生成）：
 * GET/PUT /api/app/smtp-config、POST /api/app/smtp-config/send-test-email。
 * 密码只写不读（后端加密存储，从不回传）。
 */
@Injectable({ providedIn: 'root' })
export class SmtpSettingsService {
  private readonly rest = inject(RestService);
  private readonly apiName = 'KnowledgeHub';
  private readonly url = '/api/app/smtp-config';

  get(): Observable<SmtpConfigDto> {
    return this.rest.request<void, SmtpConfigDto>(
      { method: 'GET', url: this.url },
      { apiName: this.apiName },
    );
  }

  update(input: UpdateSmtpConfigDto): Observable<SmtpConfigDto> {
    return this.rest.request<UpdateSmtpConfigDto, SmtpConfigDto>(
      { method: 'PUT', url: this.url, body: input },
      { apiName: this.apiName },
    );
  }

  sendTest(to: string): Observable<SmtpTestResultDto> {
    return this.rest.request<{ to: string }, SmtpTestResultDto>(
      { method: 'POST', url: `${this.url}/send-test-email`, body: { to } },
      { apiName: this.apiName },
    );
  }
}
