import type { SendSmtpTestEmailDto, SmtpConfigDto, SmtpTestResultDto, UpdateSmtpConfigDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class SmtpConfigService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmtpConfigDto>({
      method: 'GET',
      url: '/api/app/smtp-config',
    },
    { apiName: this.apiName,...config });
  

  sendTestEmail = (input: SendSmtpTestEmailDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmtpTestResultDto>({
      method: 'POST',
      url: '/api/app/smtp-config/send-test-email',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (input: UpdateSmtpConfigDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmtpConfigDto>({
      method: 'PUT',
      url: '/api/app/smtp-config',
      body: input,
    },
    { apiName: this.apiName,...config });
}