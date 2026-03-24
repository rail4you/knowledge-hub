import type { ExerciseDto, CreateUpdateExerciseDto, PagedExerciseRequestDto } from './dtos/exercise.dto';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class ExerciseService {
  private restService = inject(RestService);
  apiName = 'Default';

  getByCourse = (courseId: string, config?: Partial<Rest.Config>): Observable<ExerciseDto[]> =>
    this.restService.request<any, ExerciseDto[]>({
      method: 'GET',
      url: `/api/app/exercise/by-course/${courseId}`,
    },
    { apiName: this.apiName, ...config });

  getByChapter = (chapterId: string, config?: Partial<Rest.Config>): Observable<ExerciseDto[]> =>
    this.restService.request<any, ExerciseDto[]>({
      method: 'GET',
      url: `/api/app/exercise/by-chapter/${chapterId}`,
    },
    { apiName: this.apiName, ...config });

  get = (id: string, config?: Partial<Rest.Config>): Observable<ExerciseDto> =>
    this.restService.request<any, ExerciseDto>({
      method: 'GET',
      url: `/api/app/exercise/${id}`,
    },
    { apiName: this.apiName, ...config });

  getList = (input: PagedExerciseRequestDto, config?: Partial<Rest.Config>): Observable<PagedResultDto<ExerciseDto>> =>
    this.restService.request<any, PagedResultDto<ExerciseDto>>({
      method: 'GET',
      url: '/api/app/exercise',
      params: { courseId: input.courseId, chapterId: input.chapterId, type: input.type, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName, ...config });

  create = (input: CreateUpdateExerciseDto, config?: Partial<Rest.Config>): Observable<ExerciseDto> =>
    this.restService.request<any, ExerciseDto>({
      method: 'POST',
      url: '/api/app/exercise',
      body: input,
    },
    { apiName: this.apiName, ...config });

  update = (id: string, input: CreateUpdateExerciseDto, config?: Partial<Rest.Config>): Observable<ExerciseDto> =>
    this.restService.request<any, ExerciseDto>({
      method: 'PUT',
      url: `/api/app/exercise/${id}`,
      body: input,
    },
    { apiName: this.apiName, ...config });

  delete = (id: string, config?: Partial<Rest.Config>): Observable<void> =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/exercise/${id}`,
    },
    { apiName: this.apiName, ...config });
}
