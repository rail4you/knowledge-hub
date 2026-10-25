import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal, viewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { AgentRunService } from '../../proxy/teaching-agents/agent-run.service';
import type {
  AgentRunDetailDto,
  AgentRunMessageDto,
} from '../../proxy/teaching-agents/dtos/models';
import { ClassroomAgentAssignmentStatus } from '../../proxy/teaching-agents/enums/classroom-agent-assignment-status.enum';
import { ClassroomAgentTaskPublishStatus } from '../../proxy/teaching-agents/enums/classroom-agent-task-publish-status.enum';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import {
  ASSIGNMENT_STATUS_LABEL,
  PUBLISH_STATUS_LABEL,
  TARGET_TYPE_LABEL,
  assignmentStatusClass,
} from './student-agent-task-labels';

/** 教师介入状态机：none 未求助 / waiting 等待回复 / replied 已回复 / done-help 已提交且求助过 / done-solo 已提交独立完成 */
export type HelpState = 'none' | 'waiting' | 'replied' | 'done-help' | 'done-solo';

/**
 * 学生端 · 智能体课堂任务详情（对齐原型 #classroom-tasks 的学生提交区）
 *
 * 交互对齐原型：**提交 = 一段思考文本 + 「需要教师帮助」勾选**
 *   · 未勾选 → 提交思考（submit-assignment）
 *   · 勾选   → 记录卡点并请求教师介入（mark-need-teacher-help），教师回复后回到「进行中」
 *
 * 诚实性约束（不可省略）：
 *   · 智能体回复是 **AI 生成的参考建议**，不是结论，也不参与评分；
 *   · 智能体不决定分数，「AI 建议」与「教师确认」在界面上分开呈现；
 *   · 是否完成、是否得分由教师确认（后端也无任何评分字段）。
 *
 * 数据来源（真实接口，生成代理 proxy/teaching-agents）：
 *   · 任务与对话 GET  POST /api/app/agent-run/or-get-for-assignment/{assignmentId}
 *   · 发送消息 POST /api/app/agent-run/send-message/{assignmentId}
 *   · 提交     POST /api/app/agent-run/submit-assignment/{assignmentId}
 *   · 求助     POST /api/app/agent-run/mark-need-teacher-help/{assignmentId}
 */
@Component({
  selector: 'app-student-agent-task-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
  ],
  templateUrl: './student-agent-task-detail.component.html',
  styleUrls: ['./student-agent-task-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAgentTaskDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  readonly router = inject(Router);
  private readonly agentRunService = inject(AgentRunService);
  private readonly message = inject(NzMessageService);
  private readonly messageListRef = viewChild<ElementRef<HTMLDivElement>>('messageList');

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly detail = signal<AgentRunDetailDto | null>(null);
  readonly messages = signal<AgentRunMessageDto[]>([]);

  /** 对话输入 */
  readonly draftMessage = signal('');
  readonly sending = signal(false);
  readonly chatError = signal('');

  /** 提交区 */
  readonly summary = signal('');
  readonly needTeacherHelp = signal(false);
  readonly submitting = signal(false);

  readonly assignmentId = signal('');

  /** 已提交 */
  readonly isSubmitted = computed(
    () => this.detail()?.assignment?.status === ClassroomAgentAssignmentStatus.Submitted,
  );

  /** 教师已确认结束（任务发布状态为「已结束」） */
  readonly isClosed = computed(
    () => this.detail()?.task?.publishStatus === ClassroomAgentTaskPublishStatus.Closed,
  );

  /** 是否已超期（截止时间已过） */
  readonly isOverdue = computed(() => {
    const due = this.detail()?.task?.dueTime;
    if (!due) return false;
    return new Date(due).getTime() < Date.now();
  });

  /** 提交按钮可用性：已提交 / 教师已结束 / 超期 → 禁用 */
  readonly canSubmit = computed(() => !this.isSubmitted() && !this.isClosed() && !this.isOverdue());

  /** 关闭提交区的原因（用于说明为什么不能提交） */
  readonly submitDisabledReason = computed(() => {
    if (this.isSubmitted()) return '你已提交本次思考，等待教师确认。';
    if (this.isClosed()) return '教师已确认结束本次任务，不能再提交。';
    if (this.isOverdue()) return '本次任务已超期，不能再提交。';
    return '';
  });

  /** 学生已发出的对话轮数 */
  readonly userTurnCount = computed(
    () => this.messages().filter(m => m.role === 'user' && (m.content || '').trim().length > 0).length,
  );

  /** 对话步骤状态 */
  readonly chatStepStatus = computed<'wait' | 'process' | 'finish'>(() => {
    if (this.isSubmitted()) return 'finish';
    return this.userTurnCount() > 0 ? 'process' : 'wait';
  });

  /** 教师介入状态机 */
  readonly helpState = computed<HelpState>(() => {
    const assignment = this.detail()?.assignment;
    if (!assignment) return 'none';
    const hasHelp = !!(assignment.helpReason || '').trim();
    if (this.isSubmitted()) return hasHelp ? 'done-help' : 'done-solo';
    if (assignment.status === ClassroomAgentAssignmentStatus.NeedsTeacherHelp) return 'waiting';
    if (hasHelp) return (assignment.teacherResponse || '').trim() ? 'replied' : 'waiting';
    return 'none';
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/student/agent-tasks']);
      return;
    }
    this.assignmentId.set(id);
    this.load();
  }

  /** 载入任务与已有对话 */
  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.agentRunService.createOrGetForAssignment(this.assignmentId()).subscribe({
      next: detail => {
        this.detail.set(detail ?? null);
        this.messages.set(detail?.messages || []);
        this.loading.set(false);
        this.scrollToBottom();
      },
      error: () => {
        this.detail.set(null);
        this.messages.set([]);
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  /** 重新加载 */
  reload(): void {
    this.load();
  }

  /** 与智能体对话（回复为 AI 生成的参考建议，不参与评分） */
  sendMessage(): void {
    const content = this.draftMessage().trim();
    if (!content || this.sending() || this.isSubmitted()) return;

    this.sending.set(true);
    this.chatError.set('');
    this.agentRunService.sendMessage(this.assignmentId(), { message: content }).subscribe({
      next: chunk => {
        this.draftMessage.set('');
        this.sending.set(false);
        this.load();
        if (!chunk?.content) {
          this.chatError.set('智能体这次没有返回内容，可以换一种问法再试一次。');
        }
      },
      error: err => {
        this.sending.set(false);
        this.chatError.set(this.errMsg(err, '智能体暂时不可用，请稍后重试。'));
      },
    });
  }

  /**
   * 提交区提交：
   *   · 勾选「需要教师帮助」→ mark-need-teacher-help（记录卡点，等待教师回复）
   *   · 未勾选              → submit-assignment（提交本次思考，等待教师确认）
   */
  submit(): void {
    const text = this.summary().trim();
    if (!text || this.submitting() || !this.canSubmit()) return;

    this.submitting.set(true);
    if (this.needTeacherHelp()) {
      this.agentRunService.markNeedTeacherHelp(this.assignmentId(), { reason: text }).subscribe({
        next: () => {
          this.submitting.set(false);
          this.summary.set('');
          this.needTeacherHelp.set(false);
          this.message.success('已发出求助，等待教师回复');
          this.load();
        },
        error: err => {
          this.submitting.set(false);
          this.message.error(this.errMsg(err, '求助提交失败'));
        },
      });
      return;
    }

    this.agentRunService.submitAssignment(this.assignmentId(), { summary: text }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.summary.set('');
        this.message.success('思考已提交，等待教师确认');
        this.load();
      },
      error: err => {
        this.submitting.set(false);
        this.message.error(this.errMsg(err, '提交失败'));
      },
    });
  }

  /** 状态中文 */
  statusText(status?: number | null): string {
    return ASSIGNMENT_STATUS_LABEL[status ?? 0] ?? '未知状态';
  }

  statusClass(status?: number | null): string {
    return assignmentStatusClass(status);
  }

  publishStatusText(status?: number | null): string {
    return PUBLISH_STATUS_LABEL[status ?? 0] ?? '未知状态';
  }

  targetText(targetType?: number | null): string {
    return TARGET_TYPE_LABEL[targetType ?? 0] ?? '课堂任务';
  }

  /** 日期格式化 */
  formatDate(value?: string | null): string {
    if (!value) return '未设置';
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return value;
    return new Intl.DateTimeFormat('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    }).format(d);
  }

  /** 滚动到最新消息 */
  private scrollToBottom(): void {
    requestAnimationFrame(() => {
      const el = this.messageListRef()?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    });
  }

  private errMsg(err: unknown, fallback: string): string {
    const e = err as { error?: { error?: { message?: string }; message?: string }; message?: string };
    return e?.error?.error?.message || e?.error?.message || fallback;
  }
}
