import { Injectable, NgZone, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ChatService as ChatProxyService } from '../../../proxy/application/ai/chat.service';
import type { ChatThreadDto, ResourceForChatDto } from '../../../proxy/application/ai/dtos/models';
import { AuthFetchService } from '../../../shared/utils/auth-fetch.service';

/** 学生端 AI 对话流式分片 */
export interface StudentChatChunk {
  content: string;
  threadId: string;
  isComplete: boolean;
  isError?: boolean;
}

/** 提问入参 */
export interface StudentChatInput {
  message: string;
  /** 续聊的会话 id；首次提问留空，由后端创建 */
  threadId?: string | null;
  /** 资源问答时绑定的资源 id */
  resourceId?: string | null;
}

/**
 * 学生端「AI 学习助手」专用服务。
 *
 * 与教师端 ai/chat/chat.component 的区别：
 *   · 教师端组件是教师工作台的一部分，学生端不再包装它；
 *   · 会话 / 资源 / 历史记录全部走**生成的代理** ChatService
 *     （/api/app/chat/*），不依赖教师端的手写 service；
 *   · 只有「流式提问」这一条接口尚未进入生成代理
 *     （/api/learning/ai/chat，SSE），因此在本文件内单独实现。
 */
@Injectable({ providedIn: 'root' })
export class StudentAiChatService {
  /** 生成代理：线程 / 资源 / 热词 */
  private readonly proxy = inject(ChatProxyService);
  private readonly authFetch = inject(AuthFetchService);
  private readonly ngZone = inject(NgZone);

  private readonly chatUrl = '/api/learning/ai/chat';

  /** 可提问的资源（已建索引） */
  getResources(): Observable<ResourceForChatDto[]> {
    return this.proxy.getResourcesWithPageIndex();
  }

  /** 资源热词（用于给出提问建议） */
  getHotWords(resourceId: string, count = 12): Observable<{ word?: string; count?: number }[]> {
    return this.proxy.getHotWords(resourceId, count);
  }

  /** 我的会话列表 */
  getThreads(): Observable<ChatThreadDto[]> {
    return this.proxy.getMyThreads();
  }

  /** 单个会话（含消息） */
  getThread(threadId: string): Observable<ChatThreadDto> {
    return this.proxy.getThread(threadId);
  }

  /** 删除会话 */
  deleteThread(threadId: string): Observable<void> {
    return this.proxy.deleteThread(threadId);
  }

  /** 清空会话 */
  clearThreads(): Observable<void> {
    return this.proxy.clearAllThreads();
  }

  /**
   * 流式提问（SSE）。
   * 后端逐条推送 `data: {content, threadId, isComplete}`。
   */
  streamChat(input: StudentChatInput): Observable<StudentChatChunk> {
    return new Observable<StudentChatChunk>(observer => {
      const controller = new AbortController();
      let settled = false;

      const emit = (chunk: StudentChatChunk) => {
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
        this.ngZone.run(() => observer.error(err));
      };

      const body = JSON.stringify({
        message: input.message,
        threadId: input.threadId || null,
        resourceId: input.resourceId || null,
      });

      this.authFetch
        .fetch(this.chatUrl, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body,
          signal: controller.signal,
        })
        .then(async response => {
          if (!response.ok) {
            throw new Error(`AI 服务暂时不可用（HTTP ${response.status}）`);
          }
          const reader = response.body?.getReader();
          if (!reader) {
            done();
            return;
          }
          const decoder = new TextDecoder();
          let buffer = '';

          const pump = (line: string) => {
            if (!line.startsWith('data: ')) return;
            try {
              const raw = JSON.parse(line.slice(6));
              emit({
                content: raw.content || '',
                threadId: raw.threadId || input.threadId || '',
                isComplete: !!raw.isComplete,
                isError: !!raw.isError,
              });
            } catch {
              // 忽略无法解析的分片
            }
          };

          while (true) {
            const { done: readerDone, value } = await reader.read();
            if (readerDone) break;
            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split('\n');
            buffer = lines.pop() || '';
            lines.forEach(pump);
          }
          buffer += decoder.decode();
          if (buffer.trim()) pump(buffer.trim());
          done();
        })
        .catch(err => {
          if (controller.signal.aborted) {
            done();
            return;
          }
          fail(err);
        });

      return () => controller.abort();
    });
  }
}
