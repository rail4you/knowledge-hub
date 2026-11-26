import type { ClassroomTokenSessionDto, ResolveClassroomTokenInput } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ClassroomTokenService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  resolve = (input: ResolveClassroomTokenInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClassroomTokenSessionDto>({
      method: 'POST',
      url: '/api/app/classroom-token/resolve',
      body: input,
    },
    { apiName: this.apiName,...config });
}