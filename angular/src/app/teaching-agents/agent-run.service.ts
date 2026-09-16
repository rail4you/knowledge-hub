import { inject, Injectable } from '@angular/core';
import { timeout } from 'rxjs/operators';
import { Rest, RestService } from '@abp/ng.core';
import {
  AgentMessageChunk,
  AgentRunDetail,
  ClassroomAgentAssignment,
} from './models';

@Injectable({ providedIn: 'root' })
export class AgentRunService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  getRun(assignmentId: string, config?: Partial<Rest.Config>) {
    return this.restService.request<any, AgentRunDetail>({
      method: 'GET',
      url: `/api/teaching-agent-assignments/${assignmentId}/run`,
    }, { apiName: this.apiName, ...config });
  }

  submit(assignmentId: string, summary: string, config?: Partial<Rest.Config>) {
    return this.restService.request<any, ClassroomAgentAssignment>({
      method: 'POST',
      url: `/api/teaching-agent-assignments/${assignmentId}/submit`,
      body: { summary },
    }, { apiName: this.apiName, ...config });
  }

  markNeedHelp(assignmentId: string, reason: string, config?: Partial<Rest.Config>) {
    return this.restService.request<any, ClassroomAgentAssignment>({
      method: 'POST',
      url: `/api/teaching-agent-assignments/${assignmentId}/need-help`,
      body: { reason },
    }, { apiName: this.apiName, ...config });
  }

  chat(assignmentId: string, message: string) {
    // 一次性返回完整回复（智能体一次调用整体给出结果）。
    return this.restService.request<any, AgentMessageChunk>({
      method: 'POST',
      url: `/api/teaching-agents/runs/${assignmentId}/chat`,
      body: { message },
    }, { apiName: this.apiName }).pipe(timeout({ first: 30_000 }));
  }
}
