import type { ClassroomAgentAssignmentDto, ClassroomAgentTaskDetailDto, ClassroomAgentTaskDto, CreateClassroomAgentTaskDto, StudentAgentTaskDto, TaskCreationOptionsDto, TeacherRespondDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ClassroomAgentTaskService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateClassroomAgentTaskDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClassroomAgentTaskDto>({
      method: 'POST',
      url: '/api/app/classroom-agent-task',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/classroom-agent-task/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getCreateOptions = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TaskCreationOptionsDto>({
      method: 'GET',
      url: '/api/app/classroom-agent-task/create-options',
    },
    { apiName: this.apiName,...config });
  

  getStudentTaskList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<StudentAgentTaskDto>>({
      method: 'GET',
      url: '/api/app/classroom-agent-task/student-task-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getTaskDetail = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClassroomAgentTaskDetailDto>({
      method: 'GET',
      url: `/api/app/classroom-agent-task/${id}/task-detail`,
    },
    { apiName: this.apiName,...config });
  

  getTeacherTaskList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ClassroomAgentTaskDto>>({
      method: 'GET',
      url: '/api/app/classroom-agent-task/teacher-task-list',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  publish = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClassroomAgentTaskDto>({
      method: 'POST',
      url: `/api/app/classroom-agent-task/${id}/publish`,
    },
    { apiName: this.apiName,...config });
  

  respondToStudentHelp = (assignmentId: string, input: TeacherRespondDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClassroomAgentAssignmentDto>({
      method: 'POST',
      url: `/api/app/classroom-agent-task/respond-to-student-help/${assignmentId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}