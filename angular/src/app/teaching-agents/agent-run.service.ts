import { inject, Injectable, NgZone } from '@angular/core';
import { Observable } from 'rxjs';
import { Rest, RestService } from '@abp/ng.core';
import {
  AgentMessageChunk,
  AgentRunDetail,
  ClassroomAgentAssignment,
} from './models';

@Injectable({ providedIn: 'root' })
export class AgentRunService {
  private readonly restService = inject(RestService);
  private readonly ngZone = inject(NgZone);
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

  chat(assignmentId: string, message: string): Observable<AgentMessageChunk> {
    return new Observable<AgentMessageChunk>(observer => {
      fetch(`/api/teaching-agents/runs/${assignmentId}/chat`, {
        method: 'POST',
        headers: {
          Accept: 'text/event-stream',
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({ message }),
      }).then(async response => {
        if (!response.ok) {
          const errorText = (await response.text()).trim();
          throw new Error(errorText || `HTTP error! status: ${response.status}`);
        }

        const reader = response.body?.getReader();
        if (!reader) {
          this.ngZone.run(() => observer.complete());
          return;
        }

        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) {
            break;
          }

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n');
          buffer = lines.pop() || '';

          for (const line of lines) {
            if (!line.startsWith('data: ')) {
              continue;
            }

            try {
              const chunk = JSON.parse(line.slice(6)) as AgentMessageChunk;
              this.ngZone.run(() => observer.next(chunk));
            } catch {
              // Skip malformed chunks.
            }
          }
        }

        if (buffer.startsWith('data: ')) {
          try {
            const chunk = JSON.parse(buffer.slice(6)) as AgentMessageChunk;
            this.ngZone.run(() => observer.next(chunk));
          } catch {
            // Skip malformed trailing chunk.
          }
        }

        this.ngZone.run(() => observer.complete());
      }).catch(error => {
        this.ngZone.run(() => observer.error(error));
      });
    });
  }
}
