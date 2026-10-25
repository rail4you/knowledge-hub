import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import type { ChatThreadDto, ResourceForChatDto } from '../../../proxy/application/ai/dtos/models';
import { StudentSkeletonComponent } from '../../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../../shared/student-error/student-error.component';
import { StudentAiChatService } from './student-ai-chat.service';

/** 学生端 AI 助手的四种模式 */
export type StudentAiMode = 'ask' | 'resource' | 'history' | 'rules';

/** 对话消息 */
interface StudentChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
}

/**
 * 学生端 · AI 学习助手（对齐原型 #ai「专业 AI 助手」）
 *
 * 与教师端 ai/chat/chat.component 完全分离：本组件有自己的服务
 * （StudentAiChatService）与自己的数据接线，不再包装教师端组件。
 *
 * 数据来源（真实接口）：
 *   · 可提问资源 GET  /api/app/chat/resources-with-page-index（生成代理）
 *   · 资源热词     GET  /api/app/chat/hot-words/{resourceId}（生成代理）
 *   · 我的会话     GET  /api/app/chat/my-threads（生成代理）
 *   · 会话详情     GET  /api/app/chat/thread/{threadId}（生成代理）
 *   · 删除会话     DEL  /api/app/chat/thread/{threadId}（生成代理）
 *   · 流式提问     POST /api/learning/ai/chat（SSE，尚未纳入生成代理）
 *
 * 诚实性约束：AI 回答是**参考建议**，不构成结论或评分；
 * 教学评价由教师确认（见页面「使用说明」与信任中心待建设说明）。
 */
@Component({
  selector: 'app-student-chat',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
  ],
  templateUrl: './student-chat.component.html',
  styleUrls: ['./student-chat.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentChatComponent implements OnInit {
  private readonly chatService = inject(StudentAiChatService);
  private readonly message = inject(NzMessageService);

  /** 当前模式 */
  readonly mode = signal<StudentAiMode>('ask');

  /** 资源面板 */
  readonly resources = signal<ResourceForChatDto[]>([]);
  readonly resourcesLoading = signal(false);
  readonly resourcesFailed = signal(false);
  readonly resourceQuery = signal('');
  readonly selectedResourceId = signal<string | null>(null);

  /** 会话面板 */
  readonly threads = signal<ChatThreadDto[]>([]);
  readonly threadsLoading = signal(false);
  readonly threadsFailed = signal(false);
  readonly currentThreadId = signal<string | null>(null);

  /** 对话 */
  readonly messages = signal<StudentChatMessage[]>([]);
  readonly input = signal('');
  readonly sending = signal(false);
  readonly chatError = signal('');

  /** 资源热词（作为提问建议） */
  readonly hotWords = signal<string[]>([]);

  /** 模式导航（原型左侧 4 模式） */
  readonly modes: { key: StudentAiMode; label: string; hint: string; icon: string }[] = [
    { key: 'ask', label: '专业问答', hint: '围绕专业与课程提问', icon: 'question-circle' },
    { key: 'resource', label: '资源问答', hint: '基于已建索引资源提问', icon: 'file-text' },
    { key: 'history', label: '学习记录', hint: '查看与继续历史会话', icon: 'history' },
    { key: 'rules', label: '使用说明', hint: 'AI 使用与数据授权要点', icon: 'safety-certificate' },
  ];

  /** 快捷提问（原型 suggestion chips）：只是提问模板，不含任何业务数据 */
  readonly quickPrompts = [
    '我想梳理本门课程的核心概念，请给出分阶段的学习建议。',
    '请帮我把一个复杂知识点拆成三步，并指出每一步的检验方式。',
    '我准备做项目实训，请列出开始前需要确认的关键环节。',
    '我的理解是……，请指出还需要对照哪些资料才能确认。',
  ];

  /** 过滤后的资源 */
  readonly filteredResources = computed(() => {
    const q = this.resourceQuery().trim().toLowerCase();
    const list = this.resources();
    if (!q) return list;
    return list.filter(r => (r.name || '').toLowerCase().includes(q));
  });

  /** 当前选中的资源 */
  readonly selectedResource = computed(
    () => this.resources().find(r => r.id === this.selectedResourceId()) ?? null,
  );

  /** 当前会话 */
  readonly currentThread = computed(
    () => this.threads().find(t => t.id === this.currentThreadId()) ?? null,
  );

  /** 是否为资源问答模式（决定提问是否带 resourceId） */
  readonly isResourceMode = computed(() => this.mode() === 'resource');

  /** 模式标题 */
  readonly modeTitle = computed(
    () => this.modes.find(m => m.key === this.mode())?.label ?? '专业问答',
  );

  ngOnInit(): void {
    this.loadResources();
    this.loadThreads();
  }

  /** 载入可提问资源 */
  private loadResources(): void {
    this.resourcesLoading.set(true);
    this.resourcesFailed.set(false);
    this.chatService.getResources().subscribe({
      next: list => {
        this.resources.set(list || []);
        this.resourcesLoading.set(false);
      },
      error: () => {
        this.resources.set([]);
        this.resourcesFailed.set(true);
        this.resourcesLoading.set(false);
      },
    });
  }

  /** 载入我的会话 */
  private loadThreads(): void {
    this.threadsLoading.set(true);
    this.threadsFailed.set(false);
    this.chatService.getThreads().subscribe({
      next: list => {
        this.threads.set(list || []);
        this.threadsLoading.set(false);
      },
      error: () => {
        this.threads.set([]);
        this.threadsFailed.set(true);
        this.threadsLoading.set(false);
      },
    });
  }

  /** 切换模式 */
  switchMode(mode: StudentAiMode): void {
    this.mode.set(mode);
    this.chatError.set('');
    if (mode === 'resource' && this.selectedResourceId() && !this.hotWords().length) {
      this.loadHotWords(this.selectedResourceId()!);
    }
  }

  /** 重新加载资源 */
  reloadResources(): void {
    this.loadResources();
  }

  /** 重新加载会话 */
  reloadThreads(): void {
    this.loadThreads();
  }

  /** 选择资源 */
  selectResource(id: string | undefined): void {
    if (!id) return;
    this.selectedResourceId.set(id);
    this.hotWords.set([]);
    this.loadHotWords(id);
  }

  /** 载入资源热词 */
  private loadHotWords(resourceId: string): void {
    this.chatService.getHotWords(resourceId, 12).subscribe({
      next: list => this.hotWords.set((list || []).map(w => w.word || '').filter(Boolean)),
      error: () => this.hotWords.set([]),
    });
  }

  /** 打开历史会话 */
  openThread(thread: ChatThreadDto): void {
    if (!thread.id) return;
    this.currentThreadId.set(thread.id);
    this.chatService.getThread(thread.id).subscribe({
      next: detail => {
        const list = (detail?.messages || []).map(m => ({
          id: m.id || `${m.role}-${m.createdAt}`,
          role: m.role === 'user' ? ('user' as const) : ('assistant' as const),
          content: m.content || '',
          createdAt: m.createdAt || '',
        }));
        this.messages.set(list);
      },
      error: () => this.messages.set([]),
    });
  }

  /** 删除会话 */
  deleteThread(thread: ChatThreadDto, event: Event): void {
    event.stopPropagation();
    if (!thread.id) return;
    this.chatService.deleteThread(thread.id).subscribe({
      next: () => {
        if (this.currentThreadId() === thread.id) {
          this.currentThreadId.set(null);
          this.messages.set([]);
        }
        this.loadThreads();
        this.message.success('会话已删除');
      },
      error: () => this.message.error('删除会话失败'),
    });
  }

  /** 新会话 */
  newThread(): void {
    this.currentThreadId.set(null);
    this.messages.set([]);
    this.chatError.set('');
    this.mode.set('ask');
  }

  /** 发送提问（流式） */
  send(text?: string): void {
    const message = (text ?? this.input()).trim();
    if (!message || this.sending()) return;

    const userMessage: StudentChatMessage = {
      id: `u-${Date.now()}`,
      role: 'user',
      content: message,
      createdAt: new Date().toISOString(),
    };
    const assistantId = `a-${Date.now()}`;

    this.messages.update(list => [
      ...list,
      userMessage,
      { id: assistantId, role: 'assistant', content: '', createdAt: new Date().toISOString() },
    ]);
    this.input.set('');
    this.sending.set(true);
    this.chatError.set('');

    this.chatService
      .streamChat({
        message,
        threadId: this.currentThreadId(),
        resourceId: this.isResourceMode() ? this.selectedResourceId() : null,
      })
      .subscribe({
        next: chunk => {
          if (chunk.threadId && !this.currentThreadId()) {
            this.currentThreadId.set(chunk.threadId);
          }
          if (chunk.content) {
            this.messages.update(list =>
              list.map(m => (m.id === assistantId ? { ...m, content: m.content + chunk.content } : m)),
            );
          }
          if (chunk.isError) {
            this.chatError.set(chunk.content || 'AI 服务返回了错误，请稍后重试。');
          }
        },
        error: err => {
          this.sending.set(false);
          this.chatError.set(this.errMsg(err, 'AI 服务暂时不可用，请稍后重试。'));
          // 移除空的助手气泡，避免留下无内容的占位
          this.messages.update(list => list.filter(m => m.id !== assistantId || m.content.trim()));
        },
        complete: () => {
          this.sending.set(false);
          this.loadThreads();
        },
      });
  }

  private errMsg(err: unknown, fallback: string): string {
    const e = err as { error?: { error?: { message?: string }; message?: string }; message?: string };
    return e?.error?.error?.message || e?.error?.message || (err instanceof Error ? err.message : fallback);
  }
}
