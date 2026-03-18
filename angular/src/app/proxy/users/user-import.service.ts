import type { UserImportResultDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class UserImportService {
  private restService = inject(RestService);
  apiName = 'Default';

  import = (input: number[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserImportResultDto>({
      method: 'POST',
      url: '/api/app/user-import',
      body: input,
    },
    { apiName: this.apiName,...config });

  importUsingUrl = (input: Uint8Array, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserImportResultDto>({
      method: 'POST',
      url: '/api/app/user-import',
      body: Array.from(input),
    },
    { apiName: this.apiName,...config });
}
