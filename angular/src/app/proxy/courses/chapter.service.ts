import type { ChapterDto, ChapterImportResultDto, CreateUpdateChapterDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';
import type { ChapterOrderDto } from './dtos/models';

@Injectable({
  providedIn: 'root',
})
export class ChapterService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateUpdateChapterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChapterDto>({
      method: 'POST',
      url: '/api/app/chapter',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/chapter/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteByCourse = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/chapter/by-course/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChapterDto>({
      method: 'GET',
      url: `/api/app/chapter/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getChapterTree = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChapterDto[]>({
      method: 'GET',
      url: `/api/app/chapter/chapter-tree/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getChaptersByCourse = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChapterDto[]>({
      method: 'GET',
      url: `/api/app/chapter/chapters-by-course/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ChapterDto>>({
      method: 'GET',
      url: '/api/app/chapter',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  importFromExcel = (courseId: string, file: IFormFile, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChapterImportResultDto>({
      method: 'POST',
      url: `/api/app/chapter/import-from-excel/${courseId}`,
      body: file,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateChapterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ChapterDto>({
      method: 'PUT',
      url: `/api/app/chapter/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  
  reorderChapters = (orders: ChapterOrderDto[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: '/api/app/chapter/reorder',
      body: orders,
    },
    { apiName: this.apiName,...config });
}