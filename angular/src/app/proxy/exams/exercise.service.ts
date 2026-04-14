import type { CreateUpdateExerciseDto, ExerciseDto, ExerciseImportResultDto, GenerateExerciseInput, GradeEssayInput, GradingResultDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ExerciseService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateUpdateExerciseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExerciseDto>({
      method: 'POST',
      url: '/api/app/exercise',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/exercise/${id}`,
    },
    { apiName: this.apiName,...config });
  

  generateByAI = (input: GenerateExerciseInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExerciseDto[]>({
      method: 'POST',
      url: '/api/app/exercise/generate-by-aI',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExerciseDto>({
      method: 'GET',
      url: `/api/app/exercise/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getByChapter = (chapterId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExerciseDto[]>({
      method: 'GET',
      url: `/api/app/exercise/by-chapter/${chapterId}`,
    },
    { apiName: this.apiName,...config });
  

  getByCourse = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExerciseDto[]>({
      method: 'GET',
      url: `/api/app/exercise/by-course/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ExerciseDto>>({
      method: 'GET',
      url: '/api/app/exercise',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  gradeEssay = (input: GradeEssayInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, GradingResultDto>({
      method: 'POST',
      url: '/api/app/exercise/grade-essay',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateExerciseDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExerciseDto>({
      method: 'PUT',
      url: `/api/app/exercise/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });


  importFromExcel = (courseId: string, file: File, config?: Partial<Rest.Config>) => {
    const formData = new FormData();
    formData.append('file', file);

    return this.restService.request<any, ExerciseImportResultDto>({
      method: 'POST',
      url: `/api/app/exercise/import-from-excel/${courseId}`,
      body: formData,
    },
    { apiName: this.apiName, ...config });
  };
}