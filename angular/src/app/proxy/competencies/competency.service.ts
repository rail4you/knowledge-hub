import type { CompetencyDto, CompetencyKnowledgeDto, CourseAchievementDto, CourseCompetencyDto, LinkCompetencyKnowledgeInput, SaveCompetencyInput, SetCourseCompetencyInput, StudentAchievementDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class CompetencyService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: SaveCompetencyInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompetencyDto>({
      method: 'POST',
      url: '/api/app/competency',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/competency/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAchievement = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseAchievementDto>({
      method: 'GET',
      url: `/api/app/competency/achievement/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getByCourse = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseCompetencyDto[]>({
      method: 'GET',
      url: `/api/app/competency/by-course/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getKnowledgeLinks = (competencyId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompetencyKnowledgeDto[]>({
      method: 'GET',
      url: `/api/app/competency/knowledge-links/${competencyId}`,
    },
    { apiName: this.apiName,...config });
  

  getLinkedKnowledgeResourceIds = (courseId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'GET',
      url: `/api/app/competency/linked-knowledge-resource-ids/${courseId}`,
    },
    { apiName: this.apiName,...config });
  

  getStudentAchievement = (courseId: string, studentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, StudentAchievementDto>({
      method: 'GET',
      url: '/api/app/competency/student-achievement',
      params: { courseId, studentId },
    },
    { apiName: this.apiName,...config });
  

  getTree = (majorId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompetencyDto[]>({
      method: 'GET',
      url: '/api/app/competency/tree',
      params: { majorId },
    },
    { apiName: this.apiName,...config });
  

  linkKnowledge = (input: LinkCompetencyKnowledgeInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompetencyKnowledgeDto>({
      method: 'POST',
      url: '/api/app/competency/link-knowledge',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  setCourseSupport = (input: SetCourseCompetencyInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CourseCompetencyDto[]>({
      method: 'POST',
      url: '/api/app/competency/set-course-support',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  unlinkKnowledge = (linkId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/competency/unlink-knowledge/${linkId}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: SaveCompetencyInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CompetencyDto>({
      method: 'PUT',
      url: `/api/app/competency/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}