import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AgentRunService } from '../../teaching-agents/agent-run.service';
import {
  AgentRunDetail,
  AgentRunMessage,
  assignmentStatusLabel,
  formatDateTime,
  targetTypeLabel,
} from '../../teaching-agents/models';

@Component({
  selector: 'app-student-agent-task-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzAlertModule,
    NzButtonModule,
    NzCardModule,
    NzDividerModule,
    NzEmptyModule,
    NzInputModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './student-agent-task-detail.component.html',
  styleUrls: ['./student-agent-task-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAgentTaskDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly agentRunService = inject(AgentRunService);

  readonly loading = signal(false);
  readonly sending = signal(false);
  readonly submitting = signal(false);
  readonly detail = signal<AgentRunDetail | null>(null);
  readonly messages = signal<AgentRunMessage[]>([]);
  readonly draftMessage = signal('');
  readonly chatError = signal('');
  readonly summary = signal('');
  readonly helpReason = signal('');

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
      this.messages.set(detail?.messages?.length ? detail.messages : [{
        id: 'welcome',
        agentRunId: detail?.run.id || '',
        role: 'assistant',
        content: '课堂智能体已就绪。先阅读任务说明，再用聊天区逐步完成本次任务。',
        toolCallsJson: '[]',
      }]);
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

    this.agentRunService.chat(assignmentId, content).subscribe({
      next: chunk => {
        this.messages.update(items =>
          items.map(item => item.id === assistantMessage.id ? { ...item, content: item.content + chunk.content } : item)
        );
      },
      error: error => {
        const message = error instanceof Error ? error.message : '智能体暂时不可用，请稍后重试。';
        this.chatError.set(message);
        this.messages.update(items =>
          items.filter(item => item.id !== assistantMessage.id || item.content.trim().length > 0)
        );
        this.sending.set(false);
      },
      complete: () => {
        this.sending.set(false);
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
      this.detail.update(current => current ? { ...current, assignment: assignment ?? current.assignment } : current);
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
    } finally {
      this.submitting.set(false);
    }
  }

  statusText(status?: number): string {
    return assignmentStatusLabel(status ?? 0);
  }

  formatDate(value?: string): string {
    return formatDateTime(value);
  }

  targetText(value?: number): string {
    return targetTypeLabel(value ?? 0);
  }
}
