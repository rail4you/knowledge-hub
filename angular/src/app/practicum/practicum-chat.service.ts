import { Injectable, inject, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject, from, of, throwError } from 'rxjs';
import { map, catchError, retryWhen, delay, take } from 'rxjs/operators';
import { RestService } from '@abp/ng.core';
import type { Rest } from '@abp/ng.core';

export enum PracticumChatSenderType {
  Student = 0,
  Teacher = 1,
  AIAgent = 2,
}

export enum PracticumChatMessageType {
  Text = 0,
  File = 1,
  Image = 2,
}

export interface PracticumChatMessageDto {
  id: string;
  projectId: string;
  senderId?: string;
  senderType: PracticumChatSenderType;
  senderName: string;
  content: string;
  messageType: PracticumChatMessageType;
  attachmentUrl?: string;
  attachmentName?: string;
  attachmentSize?: number;
  isAgentReply: boolean;
  creationTime: string;
}

export interface SendPracticumChatMessageDto {
  projectId: string;
  content: string;
  attachmentUrl?: string;
  attachmentName?: string;
  attachmentSize?: number;
  messageType: PracticumChatMessageType;
}

export interface PracticumAgentConfigDto {
  agentName?: string;
  agentPrompt?: string;
}

export interface UpdatePracticumAgentConfigDto {
  agentName?: string;
  agentPrompt?: string;
}

@Injectable({ providedIn: 'root' })
export class PracticumChatService implements OnDestroy {
  private readonly restService = inject(RestService);
  private readonly http = inject(HttpClient);

  private eventSources = new Map<string, EventSource>();
  private messageSubject = new Subject<PracticumChatMessageDto>();
  public messages$ = this.messageSubject.asObservable();

  ngOnDestroy(): void {
    this.disconnect();
  }

  // ─── SSE Connection ────────────────────────────────

  connect(projectId: string): Observable<void> {
    // Don't disconnect other connections — each project has its own SSE stream
    // Only reconnect if the existing EventSource for this project has been closed
    const existing = this.eventSources.get(projectId);
    if (existing && existing.readyState !== EventSource.CLOSED) {
      return new Observable<void>(observer => {
        observer.next();
        observer.complete();
      });
    }

    const baseUrl = this.getApiBaseUrl();
    const url = `${baseUrl}/api/learning/practicum-chat/stream/${projectId}`;

    return new Observable<void>(observer => {
      const es = new EventSource(url);
      this.eventSources.set(projectId, es);

      es.onopen = () => {
        observer.next();
      };

      es.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data);
          if (data.id) {
            this.messageSubject.next(data as PracticumChatMessageDto);
          }
        } catch (e) {
          // Ignore parse errors for heartbeat/connection events
        }
      };

      es.onerror = () => {
        if (es.readyState === EventSource.CLOSED) {
          this.eventSources.delete(projectId);
          observer.error(new Error('SSE connection closed'));
        }
      };
    });
  }

  disconnect(projectId?: string): void {
    if (projectId) {
      const es = this.eventSources.get(projectId);
      if (es) {
        es.close();
        this.eventSources.delete(projectId);
      }
    } else {
      // Disconnect all
      this.eventSources.forEach(es => es.close());
      this.eventSources.clear();
    }
  }

  // ─── HTTP Methods ───────────────────────────────────

  sendMessage(input: SendPracticumChatMessageDto): Observable<PracticumChatMessageDto> {
    return this.http.post<PracticumChatMessageDto>(
      `${this.getApiBaseUrl()}/api/learning/practicum-chat/send`,
      input,
    );
  }

  getMessages(projectId: string, beforeId?: string, maxCount: number = 25): Observable<PracticumChatMessageDto[]> {
    let url = `${this.getApiBaseUrl()}/api/learning/practicum-chat/messages/${projectId}?maxCount=${maxCount}`;
    if (beforeId) {
      url += `&beforeId=${beforeId}`;
    }
    return this.http.get<PracticumChatMessageDto[]>(url);
  }

  getAgentConfig(projectId: string): Observable<PracticumAgentConfigDto> {
    return from(this.restService.request<any, PracticumAgentConfigDto>(
      { method: 'GET', url: `/api/app/practicum/agent-config/${projectId}` },
      { apiName: 'KnowledgeHub' },
    ));
  }

  updateAgentConfig(projectId: string, input: UpdatePracticumAgentConfigDto): Observable<void> {
    return from(this.restService.request<any, void>(
      { method: 'PUT', url: `/api/app/practicum/agent-config/${projectId}`, body: input },
      { apiName: 'KnowledgeHub' },
    ));
  }

  // ─── OSS Upload ────────────────────────────────────

  uploadFile(file: File): Observable<{ url: string; name: string; size: number }> {
    // Reuse existing OSS upload flow.
    // The PracticumChatComponent will inject OssUploadService directly.
    return throwError(() => new Error('Use OssUploadService directly'));
  }

  // ─── Helpers ──────────────────────────────────────

  private getApiBaseUrl(): string {
    // The SSE endpoint uses the learning area controller which is on the same origin
    // when using Angular dev server proxy. For production, use relative path.
    return '';
  }
}
