import type { CreateExamDto, ExamDetailDto, ExamListItemDto, ExamListQueryDto, ExamSubmissionItemDto, ExamSubmissionQueryDto, GenerateExamInput, GradeSubmissionInput, StudentExamListItemDto, StudentExamPaperDto, StudentExamResultDto, SubmitExamInput } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ExamService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  autoGrade = (studentExamId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentExamResultDto>({
      method: 'POST',
      url: `/api/app/exam/auto-grade/${studentExamId}`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateExamDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExamDetailDto>({
      method: 'POST',
      url: '/api/app/exam',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/exam/${id}`,
    },
    { apiName: this.apiName,...config });
  

  exportPaperPdf = (examId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/exam/export-paper-pdf/${examId}`,
    },
    { apiName: this.apiName,...config });
  

  exportScoresPdf = (examId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/exam/export-scores-pdf/${examId}`,
    },
    { apiName: this.apiName,...config });
  

  generate = (input: GenerateExamInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExamDetailDto>({
      method: 'POST',
      url: '/api/app/exam/generate',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExamDetailDto>({
      method: 'GET',
      url: `/api/app/exam/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: ExamListQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ExamListItemDto>>({
      method: 'GET',
      url: '/api/app/exam',
      params: { courseId: input.courseId, chapterId: input.chapterId, type: input.type, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyExams = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentExamListItemDto[]>({
      method: 'GET',
      url: `/api/app/exam/my-exams/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getMyResult = (studentExamId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentExamResultDto>({
      method: 'GET',
      url: `/api/app/exam/my-result/${studentExamId}`,
    },
    { apiName: this.apiName,...config });
  

  getSubmission = (studentExamId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentExamResultDto>({
      method: 'GET',
      url: `/api/app/exam/submission/${studentExamId}`,
    },
    { apiName: this.apiName,...config });
  

  getSubmissions = (input: ExamSubmissionQueryDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ExamSubmissionItemDto>>({
      method: 'GET',
      url: '/api/app/exam/submissions',
      params: { examId: input.examId, courseId: input.courseId, status: input.status, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  grade = (studentExamId: string, input: GradeSubmissionInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentExamResultDto>({
      method: 'POST',
      url: `/api/app/exam/grade/${studentExamId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  start = (examId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentExamPaperDto>({
      method: 'POST',
      url: `/api/app/exam/start/${examId}`,
    },
    { apiName: this.apiName,...config });
  

  submit = (studentExamId: string, input: SubmitExamInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentExamResultDto>({
      method: 'POST',
      url: `/api/app/exam/submit/${studentExamId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateExamDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExamDetailDto>({
      method: 'PUT',
      url: `/api/app/exam/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}