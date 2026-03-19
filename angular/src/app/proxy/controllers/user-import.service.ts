import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { UserImportResultDto } from '../users/models';

@Injectable({
  providedIn: 'root',
})
export class UserImportService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  import = (excelFile: number[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserImportResultDto>({
      method: 'POST',
      url: '/api/app/user-import',
      body: excelFile,
    },
    { apiName: this.apiName,...config });
}