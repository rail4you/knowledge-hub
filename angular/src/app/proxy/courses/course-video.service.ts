import type { CourseVideoDto, SaveCourseVideoInput, SaveVideoKnowledgeMarkInput, VideoKnowledgeMarkDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { IFormFile } from '../microsoft/asp-net-core/http/models';

@Injectable({
  providedIn: 'root',
})
export class CourseVideoService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/course-video/${id}`,
    },
    { apiName: this.apiName,...config });
  

  deleteMark = (markId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/course-video/mark/${markId}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseVideoDto>({
      method: 'GET',
      url: `/api/app/course-video/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (courseId: string, chapterId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseVideoDto[]>({
      method: 'GET',
      url: '/api/app/course-video',
      params: { courseId, chapterId },
    },
    { apiName: this.apiName,...config });
  

  getMarks = (courseVideoId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VideoKnowledgeMarkDto[]>({
      method: 'GET',
      url: `/api/app/course-video/marks/${courseVideoId}`,
    },
    { apiName: this.apiName,...config });
  

  reorder = (courseId: string, orderedIds: string[], config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/course-video/reorder/${courseId}`,
      body: orderedIds,
    },
    { apiName: this.apiName,...config });
  

  saveMark = (input: SaveVideoKnowledgeMarkInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, VideoKnowledgeMarkDto>({
      method: 'POST',
      url: '/api/app/course-video/save-mark',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: SaveCourseVideoInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseVideoDto>({
      method: 'PUT',
      url: `/api/app/course-video/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateDuration = (id: string, durationSeconds: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseVideoDto>({
      method: 'PUT',
      url: `/api/app/course-video/${id}/duration`,
      params: { durationSeconds },
    },
    { apiName: this.apiName,...config });
  

  upload = (courseId: string, file: IFormFile, title?: string, chapterId?: string, knowledgeResourceId?: string, description?: string, sortOrder?: number, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseVideoDto>({
      method: 'POST',
      url: '/api/app/course-video/upload',
      params: { courseId, title, chapterId, knowledgeResourceId, description, sortOrder },
      body: file,
    },
    { apiName: this.apiName,...config });
}