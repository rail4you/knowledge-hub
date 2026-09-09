import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { GetSpecialEduStudentOptionsInput, SpecialEduCourseOptionDto, SpecialEduStudentOptionDto, SpecialEduTeacherOptionDto } from '../../special-education/dtos/models';

@Injectable({
  providedIn: 'root',
})
export class SpecialEduOptionService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getCourseOptions = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduCourseOptionDto[]>({
      method: 'GET',
      url: '/api/app/special-edu-option/course-options',
    },
    { apiName: this.apiName,...config });
  

  getStudentOptions = (input: GetSpecialEduStudentOptionsInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduStudentOptionDto[]>({
      method: 'GET',
      url: '/api/app/special-edu-option/student-options',
      params: { courseId: input.courseId },
    },
    { apiName: this.apiName,...config });
  

  getTeacherOptions = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SpecialEduTeacherOptionDto[]>({
      method: 'GET',
      url: '/api/app/special-edu-option/teacher-options',
    },
    { apiName: this.apiName,...config });
}