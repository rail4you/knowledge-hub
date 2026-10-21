import type { AnalysisReportFileDto, ClassWeaknessDto, ConfirmPaperScriptInput, CourseLinkConfigDto, CourseLinkDto, CourseStudentDto, CreatePaperBatchInput, ExtractKnowledgePointsResultDto, KnowledgePointDto, MasteryThresholdDto, MiningResultDto, ModelTrainResultDto, OcrCapabilitiesDto, PaperAnalysisOverviewDto, PaperBatchDto, PaperScriptRowDto, PaperUploadImageDto, PrerequisiteMappingDto, PushResultDto, RecommendationEvaluationDto, RecommendationWeightsDto, SaveCourseLinkInput, SavePrerequisiteInput, SetThresholdInput, StudentAnalysisDashboardDto, StudentReportPreviewDto, TeachingInsightDto, UploadPaperScriptInput, UploadPaperScriptResultDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LearningAnalyticsService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  applyBestRecommendationWeights = (courseId?: string, topK: number = 5, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecommendationWeightsDto>({
      method: 'POST',
      url: '/api/app/learning-analytics/apply-best-recommendation-weights',
      params: { courseId, topK },
    },
    { apiName: this.apiName,...config });
  

  applyRecommendationWeights = (input: RecommendationWeightsDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecommendationWeightsDto>({
      method: 'POST',
      url: '/api/app/learning-analytics/apply-recommendation-weights',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  archiveReport = (batchId: string, type: string = "class", config?: Partial<Rest.Config>) =>
    this.restService.request<any, AnalysisReportFileDto>({
      method: 'POST',
      url: `/api/app/learning-analytics/archive-report/${batchId}`,
      params: { type },
    },
    { apiName: this.apiName,...config });
  

  clearThreshold = (input: SetThresholdInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/learning-analytics/clear-threshold',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  confirmPaperScript = (scriptId: string, input: ConfirmPaperScriptInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/learning-analytics/confirm-paper-script/${scriptId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  createPaperBatch = (input: CreatePaperBatchInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaperBatchDto>({
      method: 'POST',
      url: '/api/app/learning-analytics/paper-batch',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  deleteCourseLink = (courseLinkId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/learning-analytics/course-link/${courseLinkId}`,
    },
    { apiName: this.apiName,...config });
  

  deletePrerequisite = (prerequisiteId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/learning-analytics/prerequisite/${prerequisiteId}`,
    },
    { apiName: this.apiName,...config });
  

  deleteReportFile = (fileId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/learning-analytics/report-file/${fileId}`,
    },
    { apiName: this.apiName,...config });
  

  downloadReportFile = (fileId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/learning-analytics/download-report-file/${fileId}`,
    },
    { apiName: this.apiName,...config });
  

  ensureCourseKnowledgePoints = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgePointDto[]>({
      method: 'POST',
      url: `/api/app/learning-analytics/ensure-course-knowledge-points/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  evaluateRecommendation = (courseId?: string, topK: number = 5, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecommendationEvaluationDto>({
      method: 'POST',
      url: '/api/app/learning-analytics/evaluate-recommendation',
      params: { courseId, topK },
    },
    { apiName: this.apiName,...config });
  

  exportDocxReport = (batchId: string, type: string = "class", config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/learning-analytics/export-docx-report/${batchId}`,
      params: { type },
    },
    { apiName: this.apiName,...config });
  

  exportStudentReports = (batchId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'POST',
      responseType: 'blob',
      url: `/api/app/learning-analytics/export-student-reports/${batchId}`,
    },
    { apiName: this.apiName,...config });
  

  extractKnowledgePoints = (courseId: string, overwrite?: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ExtractKnowledgePointsResultDto>({
      method: 'POST',
      url: `/api/app/learning-analytics/extract-knowledge-points/${courseId}`,
      params: { overwrite },
    },
    { apiName: this.apiName,...config });
  

  getClassWeakness = (batchId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClassWeaknessDto>({
      method: 'GET',
      url: `/api/app/learning-analytics/class-weakness/${batchId}`,
    },
    { apiName: this.apiName,...config });
  

  getCourseLink = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseLinkConfigDto>({
      method: 'GET',
      url: `/api/app/learning-analytics/course-link/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getCourseStudents = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseStudentDto[]>({
      method: 'GET',
      url: `/api/app/learning-analytics/course-students/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getKnowledgePoints = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, KnowledgePointDto[]>({
      method: 'GET',
      url: `/api/app/learning-analytics/knowledge-points/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getOcrCapabilities = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, OcrCapabilitiesDto>({
      method: 'GET',
      url: '/api/app/learning-analytics/ocr-capabilities',
    },
    { apiName: this.apiName,...config });
  

  getPaperBatch = (batchId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaperBatchDto>({
      method: 'GET',
      url: `/api/app/learning-analytics/paper-batch/${batchId}`,
    },
    { apiName: this.apiName,...config });
  

  getPaperOverview = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaperAnalysisOverviewDto>({
      method: 'GET',
      url: '/api/app/learning-analytics/paper-overview',
    },
    { apiName: this.apiName,...config });
  

  getPaperScripts = (batchId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaperScriptRowDto[]>({
      method: 'GET',
      url: `/api/app/learning-analytics/paper-scripts/${batchId}`,
    },
    { apiName: this.apiName,...config });
  

  getPaperUploadImages = (batchId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PaperUploadImageDto[]>({
      method: 'GET',
      url: `/api/app/learning-analytics/paper-upload-images/${batchId}`,
    },
    { apiName: this.apiName,...config });
  

  getRecommendationWeights = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, RecommendationWeightsDto>({
      method: 'GET',
      url: '/api/app/learning-analytics/recommendation-weights',
    },
    { apiName: this.apiName,...config });
  

  getReportFiles = (batchId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AnalysisReportFileDto[]>({
      method: 'GET',
      url: `/api/app/learning-analytics/report-files/${batchId}`,
    },
    { apiName: this.apiName,...config });
  

  getStudentDashboard = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentAnalysisDashboardDto>({
      method: 'GET',
      url: '/api/app/learning-analytics/student-dashboard',
    },
    { apiName: this.apiName,...config });
  

  getStudentReportPreview = (batchId: string, student?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentReportPreviewDto>({
      method: 'GET',
      url: `/api/app/learning-analytics/student-report-preview/${batchId}`,
      params: { student },
    },
    { apiName: this.apiName,...config });
  

  getTeachingInsight = (batchId: string, useAiAdvice: boolean = true, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TeachingInsightDto>({
      method: 'GET',
      url: `/api/app/learning-analytics/teaching-insight/${batchId}`,
      params: { useAiAdvice },
    },
    { apiName: this.apiName,...config });
  

  getThreshold = (courseId: string, knowledgePoint?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MasteryThresholdDto>({
      method: 'GET',
      url: `/api/app/learning-analytics/threshold/${courseId}`,
      params: { knowledgePoint },
    },
    { apiName: this.apiName,...config });
  

  markNotificationRead = (notificationId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/learning-analytics/mark-notification-read/${notificationId}`,
    },
    { apiName: this.apiName,...config });
  

  minePrerequisites = (courseId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MiningResultDto>({
      method: 'POST',
      url: '/api/app/learning-analytics/mine-prerequisites',
      params: { courseId },
    },
    { apiName: this.apiName,...config });
  

  pushPreCourseReviews = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PushResultDto>({
      method: 'POST',
      url: '/api/app/learning-analytics/push-pre-course-reviews',
    },
    { apiName: this.apiName,...config });
  

  pushReports = (batchId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PushResultDto>({
      method: 'POST',
      url: `/api/app/learning-analytics/push-reports/${batchId}`,
    },
    { apiName: this.apiName,...config });
  

  runPaperOcr = (batchId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/learning-analytics/run-paper-ocr/${batchId}`,
    },
    { apiName: this.apiName,...config });
  

  saveCourseLink = (input: SaveCourseLinkInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseLinkDto>({
      method: 'POST',
      url: '/api/app/learning-analytics/save-course-link',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  savePrerequisite = (input: SavePrerequisiteInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PrerequisiteMappingDto>({
      method: 'POST',
      url: '/api/app/learning-analytics/save-prerequisite',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  setThreshold = (input: SetThresholdInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/learning-analytics/set-threshold',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  trainMasteryModel = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ModelTrainResultDto>({
      method: 'POST',
      url: '/api/app/learning-analytics/train-mastery-model',
    },
    { apiName: this.apiName,...config });
  

  uploadPaperScript = (input: UploadPaperScriptInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UploadPaperScriptResultDto>({
      method: 'POST',
      url: '/api/app/learning-analytics/upload-paper-script',
      body: input,
    },
    { apiName: this.apiName,...config });
}