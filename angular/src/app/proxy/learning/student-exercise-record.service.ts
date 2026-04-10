import type { ChapterProgressDto, CourseLearningOverviewDto, GetCourseLearningOverviewInput, GetLearningStatisticsInput, GetStudentExerciseRecordsInput, MarkAnswerViewedInput, SaveExerciseRecordInput, StudentExerciseRecordDto, StudentLearningStatisticsDto, SubmitSelfAssessmentInput } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class StudentExerciseRecordService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  exportLearningStatistics = (input: GetLearningStatisticsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: '/api/app/student-exercise-record/export-learning-statistics',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getChapterProgress = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<ChapterProgressDto>>({
      method: 'GET',
      url: `/api/app/student-exercise-record/chapter-progress/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getCourseLearningOverview = (input: GetCourseLearningOverviewInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseLearningOverviewDto>({
      method: 'GET',
      url: '/api/app/student-exercise-record/course-learning-overview',
      params: { courseId: input.courseId, tenantId: input.tenantId },
    },
    { apiName: this.apiName,...config });
  

  getLearningStatistics = (input: GetLearningStatisticsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StudentLearningStatisticsDto>>({
      method: 'GET',
      url: '/api/app/student-exercise-record/learning-statistics',
      params: { courseId: input.courseId, chapterId: input.chapterId, tenantId: input.tenantId, startTime: input.startTime, endTime: input.endTime, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getRecordsByChapter = (input: GetStudentExerciseRecordsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StudentExerciseRecordDto>>({
      method: 'GET',
      url: '/api/app/student-exercise-record/records-by-chapter',
      params: { courseId: input.courseId, chapterId: input.chapterId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getRecordsByCourse = (input: GetStudentExerciseRecordsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StudentExerciseRecordDto>>({
      method: 'GET',
      url: '/api/app/student-exercise-record/records-by-course',
      params: { courseId: input.courseId, chapterId: input.chapterId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  markAnswerViewed = (input: MarkAnswerViewedInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/student-exercise-record/mark-answer-viewed',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  saveOrUpdateRecord = (input: SaveExerciseRecordInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentExerciseRecordDto>({
      method: 'POST',
      url: '/api/app/student-exercise-record/save-or-update-record',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submitSelfAssessment = (input: SubmitSelfAssessmentInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/student-exercise-record/submit-self-assessment',
      body: input,
    },
    { apiName: this.apiName,...config });
}