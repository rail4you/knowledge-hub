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
  hasPageIndex?: boolean;
  hasSummary?: boolean;
  categoryId?: string;
  categoryName?: string;
}

export interface ChatThread {
  id: string;
  title?: string;
  resourceId?: string;
  resourceName?: string;
  messageCount: number;
  lastMessage?: string;
  createdAt: string;
  messages: ChatThreadMessage[];
}

export interface ChatThreadMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
}

export interface LessonPlanGenerationInput {
  resourceId: string;
  topic: string;
  subject?: string;
  grade?: string;
  duration: number;
  customPrompt?: string;
}

export interface LessonPlanChapter {
  order: number;
  title: string;
  summary?: string;
}

export interface LessonPlanChapterParseInput {
  resourceId: string;
  customPrompt?: string;
}

export interface MultiChapterLessonPlanGenerationInput {
  resourceId: string;
  topic: string;
  subject?: string;
  grade?: string;
  duration: number;
  customPrompt?: string;
  chapters: LessonPlanChapter[];
}

export interface LessonPlanStreamEvent {
  content?: string;
  message?: string;
  progress: number;
  chapterIndex?: number | null;
  chapterTotal?: number | null;
  isComplete: boolean;
  isError?: boolean;
  resultJson?: string;
}

export interface CaseAnalysisGenerationInput {
  resourceId: string;
  focusArea?: string;
}

export interface CareerGuidanceGenerationInput {
  resourceId?: string;
  careerGoal?: string;
  /** 学生端：直接提供简历文本（优先级高于 resourceId） */
  resumeContent?: string;
  resumeTitle?: string;
  /** 简历附件的存储路径（.docx/.pdf），后端读取全文交给 AI 解析 */
  attachmentUrl?: string;
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

  // ===== Thread Management =====

  getThreads(): Observable<ChatThread[]> {
    return new Observable<ChatThread[]>(observer => {
      fetch(`${this.apiUrl}/threads`, {
        credentials: 'include',
      })
        .then(async response => {
          if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
          const data = await response.json();
          this.ngZone.run(() => { observer.next(data); observer.complete(); });
        })
        .catch(err => { this.ngZone.run(() => observer.error(err)); });
    });
  }

  getThread(threadId: string): Observable<ChatThread> {
    return new Observable<ChatThread>(observer => {
      fetch(`${this.apiUrl}/threads/${threadId}`, {
        credentials: 'include',
      })
        .then(async response => {
          if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
          const data = await response.json();
          this.ngZone.run(() => { observer.next(data); observer.complete(); });
        })
        .catch(err => { this.ngZone.run(() => observer.error(err)); });
    });
  }

  deleteThread(threadId: string): Observable<void> {
    return new Observable<void>(observer => {
      fetch(`${this.apiUrl}/threads/${threadId}`, {
        method: 'DELETE',
        credentials: 'include',
      })
        .then(async response => {
          if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
          this.ngZone.run(() => { observer.next(); observer.complete(); });
        })
        .catch(err => { this.ngZone.run(() => observer.error(err)); });
    });
  }

  clearAllThreads(): Observable<void> {
    return new Observable<void>(observer => {
      fetch(`${this.apiUrl}/threads`, {
        method: 'DELETE',
        credentials: 'include',
      })
        .then(async response => {
          if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
          this.ngZone.run(() => { observer.next(); observer.complete(); });
        })
        .catch(err => { this.ngZone.run(() => observer.error(err)); });
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

  // ===== Multi-chapter lesson plan workflow =====

  parseChapters(input: LessonPlanChapterParseInput): Observable<LessonPlanStreamEvent> {
    return this.streamLessonPlanEvent(`${this.apiUrl}/parse-chapters`, input);
  }

  generateMultiChapterLessonPlan(input: MultiChapterLessonPlanGenerationInput): Observable<LessonPlanStreamEvent> {
    return this.streamLessonPlanEvent(`${this.apiUrl}/generate-multi-chapter-lesson-plan`, input);
  }

  exportMultiChapterLessonPlanDocx(lessonPlanJson: string): Promise<Blob> {
    return fetch(`${this.apiUrl}/export-multi-chapter-lesson-plan-docx`, {
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

  private streamLessonPlanEvent(url: string, body: unknown): Observable<LessonPlanStreamEvent> {
    return new Observable<LessonPlanStreamEvent>(observer => {
      // 取消订阅时 abort 底层的 fetch，让后端 RequestAborted 联动取消大模型调用，
      // 避免用户点“取消/返回”后请求仍在后台空跑。
      const controller = new AbortController();
      let settled = false;

      const emit = (chunk: LessonPlanStreamEvent) => {
        if (settled) return;
        this.ngZone.run(() => observer.next(chunk));
      };
      const done = () => {
        if (settled) return;
        settled = true;
        this.ngZone.run(() => observer.complete());
      };
      const fail = (err: unknown) => {
        if (settled) return;
        settled = true;
        // 主动取消视为正常结束，由调用方按 cancel 流程处理，不走 error 弹窗。
        if (controller.signal.aborted) {
          this.ngZone.run(() => observer.complete());
        } else {
          this.ngZone.run(() => observer.error(err));
        }
      };

      fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(body),
        signal: controller.signal,
      }).then(async response => {
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }

        const reader = response.body?.getReader();
        if (!reader) {
          done();
          return;
        }

        const decoder = new TextDecoder();
        let buffer = '';

        const pumpLine = (line: string) => {
          if (!line.startsWith('data: ')) return;
          try {
            const chunk = JSON.parse(line.slice(6));
            emit({
              content: chunk.content ?? undefined,
              message: chunk.message ?? undefined,
              progress: chunk.progress ?? 0,
              chapterIndex: chunk.chapterIndex ?? null,
              chapterTotal: chunk.chapterTotal ?? null,
              isComplete: chunk.isComplete ?? false,
              isError: chunk.isError ?? false,
              resultJson: chunk.resultJson ?? undefined,
            });
          } catch {
            // skip malformed JSON
          }
        };

        while (true) {
          const { done: readerDone, value } = await reader.read();
          if (readerDone) break;

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n');
          buffer = lines.pop() || '';

          for (const line of lines) pumpLine(line);
        }
        // 流结束时 buffer 里可能残留最后一行（无换行结尾），补处理一次，
        // 否则最终的 resultJson 事件会被丢掉而表现为“一直转圈”。
        buffer += decoder.decode();
        if (buffer.trim().length > 0) pumpLine(buffer.trim());

        done();
      }).catch(fail);

      return () => controller.abort();
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
