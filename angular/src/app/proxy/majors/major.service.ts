import type { CreateUpdateMajorDto, MajorDto, MajorLookupDto, PagedMajorRequestDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MajorService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateUpdateMajorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MajorDto>({
      method: 'POST',
      url: '/api/app/major',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/major/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MajorDto>({
      method: 'GET',
      url: `/api/app/major/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedMajorRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MajorDto>>({
      method: 'GET',
      url: '/api/app/major',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getLookupList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, MajorLookupDto[]>({
      method: 'GET',
      url: '/api/app/major/lookup-list',
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateMajorDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MajorDto>({
      method: 'PUT',
      url: `/api/app/major/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}