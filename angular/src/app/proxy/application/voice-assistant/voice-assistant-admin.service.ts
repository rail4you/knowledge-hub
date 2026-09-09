import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { SetVoiceAssistantTenantEnabledDto, VoiceAssistantTenantStateDto } from '../../voice-assistant/models';

@Injectable({
  providedIn: 'root',
})
export class VoiceAssistantAdminService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getTenantStates = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, VoiceAssistantTenantStateDto[]>({
      method: 'GET',
      url: '/api/app/voice-assistant-admin/tenant-states',
    },
    { apiName: this.apiName,...config });
  

  setTenantEnabled = (input: SetVoiceAssistantTenantEnabledDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/voice-assistant-admin/set-tenant-enabled',
      body: input,
    },
    { apiName: this.apiName,...config });
}