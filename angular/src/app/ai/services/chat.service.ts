import { Injectable, inject, NgZone } from '@angular/core';
import { Observable } from 'rxjs';

export interface ChatInput {
  message: string;
  threadId?: string;
  resourceId?: string;
  agent?: string;
}



export interface ChatMessageChunk {
  content: string;
  threadId: string;
  isComplete: boolean;
}

export interface ResourceForChat {
  id: string;
  name: string;
  fileExtension?: string;
  sourceFormat?: string;
  nodeCount: number;
}

export interface LessonPlanGenerationInput {
  resourceId: string;
  topic: string;
  subject?: string;
  grade?: string;
  duration: number;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly apiUrl = 'https://localhost:44305/api/learning/ai';
  private readonly ngZone = inject(NgZone);

  getResources(): Observable<ResourceForChat[]> {
    return new Observable<ResourceForChat[]>(observer => {
      fetch(`${this.apiUrl}/resources`, {
        credentials: 'include',
      })
        .then(async response => {
          if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
          }
          const data = await response.json();
          observer.next(data);
          observer.complete();
        })
        .catch(err => {
          observer.error(err);
        });
    });
  }

  chat(input: ChatInput): Observable<ChatMessageChunk> {
    return new Observable<ChatMessageChunk>(observer => {
      const body = JSON.stringify({
        message: input.message,
        threadId: input.threadId || null,
        resourceId: input.resourceId || null,
      });

      fetch(`${this.apiUrl}/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body,
      }).then(async response => {
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }

        const reader = response.body?.getReader();
        if (!reader) {
          observer.complete();
          return;
        }

        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n');
          buffer = lines.pop() || '';

          for (const line of lines) {
            if (line.startsWith('data: ')) {
              try {
                const chunk = JSON.parse(line.slice(6));
                this.ngZone.run(() => {
                  observer.next({
                    content: chunk.content || '',
                    threadId: chunk.threadId || '',
                    isComplete: chunk.isComplete || false,
                  });
                });
              } catch {
                // skip malformed JSON
              }
            }
          }
        }

        this.ngZone.run(() => observer.complete());
      }).catch(err => {
        this.ngZone.run(() => observer.error(err));
      });

      return () => {};
    });
  }

  generateLessonPlan(input: LessonPlanGenerationInput): Observable<ChatMessageChunk> {
    return new Observable<ChatMessageChunk>(observer => {
      const body = JSON.stringify(input);

      fetch(`${this.apiUrl}/generate-lesson-plan`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body,
      }).then(async response => {
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }

        const reader = response.body?.getReader();
        if (!reader) {
          observer.complete();
          return;
        }

        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n');
          buffer = lines.pop() || '';

          for (const line of lines) {
            if (line.startsWith('data: ')) {
              try {
                const chunk = JSON.parse(line.slice(6));
                this.ngZone.run(() => {
                  observer.next({
                    content: chunk.content || '',
                    threadId: chunk.threadId || '',
                    isComplete: chunk.isComplete || false,
                  });
                });
              } catch {
                // skip malformed JSON
              }
            }
          }
        }

        this.ngZone.run(() => observer.complete());
      }).catch(err => {
        this.ngZone.run(() => observer.error(err));
      });

      return () => {};
    });
  }

  exportLessonPlanDocx(lessonPlanJson: string): Promise<Blob> {
    return fetch(`${this.apiUrl}/export-lesson-plan-docx`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ lessonPlanJson }),
    }).then(async response => {
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      return response.blob();
    });
  }
}
