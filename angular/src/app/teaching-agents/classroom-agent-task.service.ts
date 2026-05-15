import { Rest, RestService } from '@abp/ng.core';
import { inject, Injectable } from '@angular/core';
import {
  ClassroomAgentTask,
  ClassroomAgentTaskDetail,
  CreateClassroomAgentTaskPayload,
  PagedResultDto,
  StudentAgentTask,
  TaskCreationOptions,
} from './models';

@Injectable({ providedIn: 'root' })
export class ClassroomAgentTaskService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getCreateOptions(config?: Partial<Rest.Config>) {
    return this.restService.request<any, TaskCreationOptions>({
      method: 'GET',
      url: '/api/app/classroom-agent-task/create-options',
    }, { apiName: this.apiName, ...config });
  }

  create(input: CreateClassroomAgentTaskPayload, config?: Partial<Rest.Config>) {
    return this.restService.request<any, ClassroomAgentTask>({
      method: 'POST',
      url: '/api/app/classroom-agent-task',
      body: input,
    }, { apiName: this.apiName, ...config });
  }

  publish(id: string, config?: Partial<Rest.Config>) {
    return this.restService.request<any, ClassroomAgentTask>({
      method: 'POST',
      url: `/api/app/classroom-agent-task/${id}/publish`,
    }, { apiName: this.apiName, ...config });
  }

  delete(id: string, config?: Partial<Rest.Config>) {
    return this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/classroom-agent-task/${id}`,
    }, { apiName: this.apiName, ...config });
  }

  getTeacherList(input: { skipCount?: number; maxResultCount?: number; sorting?: string }, config?: Partial<Rest.Config>) {
    return this.restService.request<any, PagedResultDto<ClassroomAgentTask>>({
      method: 'GET',
      url: '/api/app/classroom-agent-task/teacher-task-list',
      params: input,
    }, { apiName: this.apiName, ...config });
  }

  getStudentList(input: { skipCount?: number; maxResultCount?: number; sorting?: string }, config?: Partial<Rest.Config>) {
    return this.restService.request<any, PagedResultDto<StudentAgentTask>>({
      method: 'GET',
      url: '/api/app/classroom-agent-task/student-task-list',
      params: input,
    }, { apiName: this.apiName, ...config });
  }

  get(id: string, config?: Partial<Rest.Config>) {
    return this.restService.request<any, ClassroomAgentTaskDetail>({
      method: 'GET',
      url: `/api/app/classroom-agent-task/${id}/task-detail`,
    }, { apiName: this.apiName, ...config });
  }

  respondToHelp(assignmentId: string, response: string, config?: Partial<Rest.Config>) {
    return this.restService.request<any, any>({
      method: 'POST',
      url: `/api/app/classroom-agent-task/respond-to-student-help/${assignmentId}`,
      body: { response },
    }, { apiName: this.apiName, ...config });
  }
}
