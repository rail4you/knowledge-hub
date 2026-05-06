import type { PagedResultDto, PagedResultRequestDto, Rest } from '@abp/ng.core';
import { RestService } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ResourceDto } from '../proxy/resources/models';

@Injectable({
  providedIn: 'root',
})
export class StudentResourceCollectionService {
  private readonly restService = inject(RestService);

  getCollectedList = (input: PagedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ResourceDto>>(
      {
        method: 'GET',
        url: '/api/app/resource/collected-list',
        params: {
          skipCount: input.skipCount,
          maxResultCount: input.maxResultCount,
        },
      },
      {
        apiName: 'KnowledgeHub',
        ...config,
      }
    );
}
