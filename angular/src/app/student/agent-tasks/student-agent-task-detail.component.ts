import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal, viewChild, ElementRef } from '@angular/core';
import { TimeoutError } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { MarkdownComponent, provideMarkdown } from 'ngx-markdown';
import { fixCjkMarkdown } from '../../shared/markdown-cjk-fix.util';
import { AgentRunService } from '../../teaching-agents/agent-run.service';
import {
  AgentRunDetail,
  AgentRunMessage,
  assignmentStatusLabel,
  formatDateTime,
  targetTypeLabel,
} from '../../teaching-agents/models';

/** 教师介入状态机：none 未求助 / waiting 等待回复 / replied 已回复可继续 / done-help 已提交且求助过 / done-solo 已提交独立完成 */
export type HelpState = 'none' | 'waiting' | 'replied' | 'done-help' | 'done-solo';

@Component({
  selector: 'app-student-agent-task-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzSpinModule,
    MarkdownComponent,
  ],
  providers: [provideMarkdown()],
  templateUrl: './student-agent-task-detail.component.html',
  styleUrls: ['./student-agent-task-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAgentTaskDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly agentRunService = inject(AgentRunService);
  private readonly messageListRef = viewChild<ElementRef<HTMLDivElement>>('messageList');

  /** CJK 毗邻加粗预处理（marked 原生不渲染 这是**文本?**测试 这类写法）。 */
  readonly fixMarkdown = fixCjkMarkdown;

  /** 是否正在等待智能体回复（用于尾部打字 loading 提示）。 */
  readonly waitingReply = computed(() =>
    this.sending() && this.messages().length > 0 &&
    (this.messages()[this.messages().length - 1].content.trim().length === 0)
  );

  readonly loading = signal(false);
  readonly sending = signal(false);
  readonly submitting = signal(false);
  readonly detail = signal<AgentRunDetail | null>(null);
  readonly messages = signal<AgentRunMessage[]>([]);
  readonly draftMessage = signal('');
  readonly chatError = signal('');
  readonly summary = signal('');
  readonly helpReason = signal('');
  readonly submitModalVisible = signal(false);
  readonly helpModalVisible = signal(false);

  readonly isCompleted = computed(() => this.detail()?.assignment.status === 2);
  readonly isHelpActive = computed(() => this.detail()?.assignment.status === 3);

  /** 学生已发送的用户消息轮数（用于进度展示）。 */
  readonly userTurnCount = computed(() =>
    this.messages().filter(m => m.role === 'user' && m.content.trim().length > 0).length
  );

  /** 教师介入状态机：左侧「教师介入」卡与进度步骤共用。 */
  readonly helpState = computed<HelpState>(() => {
    const assignment = this.detail()?.assignment;
    if (!assignment) return 'none';
    const completed = assignment.status === 2;
    const waiting = assignment.status === 3;
    const hasHelp = !!assignment.helpReason?.trim();
    if (completed) return hasHelp ? 'done-help' : 'done-solo';
    if (waiting) return 'waiting';
    // 教师回复后状态回到进行中，但 helpReason 仍保留 → 视为已回复
    if (hasHelp) return assignment.teacherResponse?.trim() ? 'replied' : 'waiting';
    return 'none';
  });

  /** 对话步骤状态：0 未开始 / 1 对话中 / 2 已提交。 */
  readonly chatStepStatus = computed<'wait' | 'process' | 'finish'>(() => {
    if (this.isCompleted()) return 'finish';
    return this.userTurnCount() > 0 ? 'process' : 'wait';
  });

  targetText(targetType?: number): string {
    return targetTypeLabel(targetType ?? 0);
  }

  /** 将聊天区滚动到最新一条消息（下一帧执行，等待视图/内容更新）。 */
  private scrollMessagesToBottom(): void {
    requestAnimationFrame(() => {
      const el = this.messageListRef()?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
  }

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    const assignmentId = this.route.snapshot.paramMap.get('id');
    if (!assignmentId) {
      return;
    }

    this.loading.set(true);
    try {
      const detail = await this.agentRunService.getRun(assignmentId).toPromise();
      this.detail.set(detail ?? null);
      const configuredWelcome = detail?.task.welcomeMessage?.trim();
      const agentName = detail?.task.teachingAgentName || '';
      const fallbackWelcome = `你好！我是${agentName}智能体。先阅读上方任务说明，再用聊天区逐步完成本次任务，最后使用「提交任务」提交所有对话信息。`;
      this.messages.set(detail?.messages?.length ? detail.messages : [{
        id: 'welcome',
        agentRunId: detail?.run.id || '',
        role: 'assistant',
        content: configuredWelcome || fallbackWelcome,
        toolCallsJson: '[]',
      }]);
      this.scrollMessagesToBottom();
    } finally {
      this.loading.set(false);
    }
  }

  async sendMessage(): Promise<void> {
    const assignmentId = this.detail()?.assignment.id;
    const content = this.draftMessage().trim();
    if (!assignmentId || !content) {
      return;
    }

    const userMessage: AgentRunMessage = {
      id: `user-${Date.now()}`,
      agentRunId: this.detail()?.run.id || '',
      role: 'user',
      content,
      toolCallsJson: '[]',
    };

    const assistantMessage: AgentRunMessage = {
      id: `assistant-${Date.now()}`,
      agentRunId: this.detail()?.run.id || '',
      role: 'assistant',
      content: '',
      toolCallsJson: '[]',
    };

    this.messages.update(items => [...items, userMessage, assistantMessage]);
    this.draftMessage.set('');
    this.chatError.set('');
    this.sending.set(true);
    this.scrollMessagesToBottom();

    // 一次性返回：收到完整回复后一次性填入智能体消息。
    this.agentRunService.chat(assignmentId, content).subscribe({
      next: chunk => {
        this.messages.update(items =>
          items.map(item =>
            item.id === assistantMessage.id
              ? { ...item, content: chunk?.content || '' }
              : item
          )
        );
      },
      error: error => {
        const isTimeout = error && (error.name === 'TimeoutError' || error instanceof TimeoutError);
        const message = isTimeout
          ? '智能体响应超时（未能收到回复），请稍后重试或换一个问题。'
          : (error instanceof Error ? error.message : '智能体暂时不可用，请稍后重试。');
        this.chatError.set(message);
        this.messages.update(items =>
          items.filter(item => item.id === assistantMessage.id || item.content.trim().length > 0)
        );
        this.sending.set(false);
      },
      complete: () => {
        this.sending.set(false);
        this.scrollMessagesToBottom();
      },
    });
  }

  async submitSummary(): Promise<void> {
    const assignmentId = this.detail()?.assignment.id;
    if (!assignmentId || !this.summary().trim()) {
      return;
    }

    this.submitting.set(true);
    try {
      const assignment = await this.agentRunService.submit(assignmentId, this.summary().trim()).toPromise();
      this.submitModalVisible.set(false);
      await this.router.navigate(['/student/agent-tasks']);
    } finally {
      this.submitting.set(false);
    }
  }

  async requestHelp(): Promise<void> {
    const assignmentId = this.detail()?.assignment.id;
    if (!assignmentId || !this.helpReason().trim()) {
      return;
    }

    this.submitting.set(true);
    try {
      const assignment = await this.agentRunService.markNeedHelp(assignmentId, this.helpReason().trim()).toPromise();
      this.detail.update(current => current ? { ...current, assignment: assignment ?? current.assignment } : current);
      this.helpReason.set('');
      this.helpModalVisible.set(false);
    } finally {
      this.submitting.set(false);
    }
  }

  statusText(status?: number): string {
    return assignmentStatusLabel(status ?? 0);
  }

  /** 状态 tag 色：对齐原型 tag（gold 高亮异常态） */
  statusClass(status?: number): string {
    switch (status) {
      case 1: return 'primary';
      case 2: return 'green';
      case 3: return 'gold';
      default: return 'gray';
    }
  }

  formatDate(value?: string): string {
    return formatDateTime(value);
  }
}
