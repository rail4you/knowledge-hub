import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { EditionDto, EditionUpgradeInputDto } from '../install/dto/models';

@Injectable({
  providedIn: 'root',
})
export class EditionService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getCurrentEdition = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, EditionDto>({
      method: 'GET',
      url: '/api/app/edition/current',
    },
    { apiName: this.apiName,...config });
  

  upgradeToStandard = (input: EditionUpgradeInputDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/edition/upgrade',
      body: input,
    },
    { apiName: this.apiName,...config });
}