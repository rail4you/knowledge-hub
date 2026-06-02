import type { AgentMessageChunkDto, AgentRunDetailDto, ClassroomAgentAssignmentDto, NeedTeacherHelpDto, SendAgentRunMessageDto, SubmitAgentAssignmentDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class AgentRunService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  createOrGetForAssignment = (assignmentId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AgentRunDetailDto>({
      method: 'POST',
      url: `/api/app/agent-run/or-get-for-assignment/${assignmentId}`,
    },
    { apiName: this.apiName,...config });
  

  getDetail = (runId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AgentRunDetailDto>({
      method: 'GET',
      url: `/api/app/agent-run/detail/${runId}`,
    },
    { apiName: this.apiName,...config });
  

  markNeedTeacherHelp = (assignmentId: string, input: NeedTeacherHelpDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClassroomAgentAssignmentDto>({
      method: 'POST',
      url: `/api/app/agent-run/mark-need-teacher-help/${assignmentId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  sendMessage = (assignmentId: string, input: SendAgentRunMessageDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AgentMessageChunkDto>({
      method: 'POST',
      url: `/api/app/agent-run/send-message/${assignmentId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  sendMessageStream = (assignmentId: string, input: SendAgentRunMessageDto, cancellationToken?: any, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AgentMessageChunkDto[]>({
      method: 'POST',
      url: `/api/app/agent-run/send-message-stream/${assignmentId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  submitAssignment = (assignmentId: string, input: SubmitAgentAssignmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ClassroomAgentAssignmentDto>({
      method: 'POST',
      url: `/api/app/agent-run/submit-assignment/${assignmentId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}