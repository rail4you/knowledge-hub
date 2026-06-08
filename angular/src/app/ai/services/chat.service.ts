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

export interface CaseAnalysisGenerationInput {
  resourceId: string;
  focusArea?: string;
}

export interface CareerGuidanceGenerationInput {
  resourceId: string;
  careerGoal?: string;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly apiUrl = '/api/learning/ai';
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
          this.ngZone.run(() => {
            observer.next(data);
            observer.complete();
          });
        })
        .catch(err => {
          this.ngZone.run(() => observer.error(err));
        });
    });
  }

  /**
   * P1-13：获取当前用户已审核通过的"简历"资源，供职业规划下拉使用。
   * 后端过滤：IsResume=true AND Status IN (SchoolApproved, LeagueApproved) AND CreatorId=currentUser。
   * 与 getResources() 的差异：getResources 返回全部已建索引资源（用于 AI 通用对话 / 教案 / 案例分析），
   * getResumes 仅返回当前用户的简历资源（用于职业规划）。
   */
  getResumes(): Observable<ResourceForChat[]> {
    return new Observable<ResourceForChat[]>(observer => {
      fetch(`${this.apiUrl}/resumes`, {
        credentials: 'include',
      })
        .then(async response => {
          if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
          }
          const data = await response.json();
          this.ngZone.run(() => {
            observer.next(data);
            observer.complete();
          });
        })
        .catch(err => {
          this.ngZone.run(() => observer.error(err));
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

  generateCaseAnalysis(input: CaseAnalysisGenerationInput): Observable<ChatMessageChunk> {
    return new Observable<ChatMessageChunk>(observer => {
      const body = JSON.stringify(input);

      fetch(`${this.apiUrl}/generate-case-analysis`, {
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

  exportCaseAnalysisDocx(caseAnalysisJson: string): Promise<Blob> {
    return fetch(`${this.apiUrl}/export-case-analysis-docx`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ caseAnalysisJson }),
    }).then(async response => {
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      return response.blob();
    });
  }

  generateCareerGuidance(input: CareerGuidanceGenerationInput): Observable<ChatMessageChunk> {
    return new Observable<ChatMessageChunk>(observer => {
      const body = JSON.stringify(input);

      fetch(`${this.apiUrl}/generate-career-guidance`, {
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

  exportCareerGuidanceDocx(careerGuidanceJson: string): Promise<Blob> {
    return fetch(`${this.apiUrl}/export-career-guidance-docx`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ careerGuidanceJson }),
    }).then(async response => {
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      return response.blob();
    });
  }
}
