import {
  ChangeDetectionStrategy, Component, OnInit, OnDestroy, inject, signal,
  ViewChild, ElementRef
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { ConfigStateService } from '@abp/ng.core';
import { Subject, takeUntil, Subscription } from 'rxjs';
import {
  CreatePracticumSubmissionDto,
  PracticumGuidanceRecordDto,
  PracticumProjectDetailDto,
  PracticumService,
  PracticumSubmissionDto,
  PracticumSubmissionStatus,
} from './practicum.service';
import {
  PracticumChatService,
  PracticumChatMessageDto,
  PracticumChatSenderType,
  PracticumChatMessageType,
  SendPracticumChatMessageDto,
  PracticumAgentConfigDto,
} from './practicum-chat.service';

type SubmissionForm = {
  content: string;
  attachmentUrls: string;
  linkUrl: string;
  screenshotUrls: string;
};

@Component({
  selector: 'app-practicum-detail',
  standalone: true,
  imports: [
    CommonModule, FormsModule, DatePipe,
    NzButtonModule, NzCardModule, NzDescriptionsModule, NzDividerModule,
    NzIconModule, NzInputModule, NzTableModule, NzTabsModule, NzTagModule,
    NzAvatarModule, NzSpinModule, NzTooltipModule, RouterModule,
  ],
  templateUrl: './practicum-detail.component.html',
  styleUrls: ['./practicum-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly practicumService = inject(PracticumService);
  private readonly chatService = inject(PracticumChatService);
  private readonly configState = inject(ConfigStateService);
  private readonly message = inject(NzMessageService);

  private destroy$ = new Subject<void>();
  private sseSubscription: Subscription | null = null;

  @ViewChild('messagesContainer') messagesContainer!: ElementRef;

  // ─── Project state ──────────────────────────
  readonly project = signal<PracticumProjectDetailDto | null>(null);
  readonly submissions = signal<PracticumSubmissionDto[]>([]);
  readonly guidances = signal<PracticumGuidanceRecordDto[]>([]);
  readonly submissionStatuses = PracticumSubmissionStatus;

  private enrollmentId: string | null = null;
  forms: Record<string, SubmissionForm> = {};

  // ─── Tab state ──────────────────────────────
  activeTab = signal<number>(0);

  // ─── Chat state ─────────────────────────────
  chatMessages = signal<PracticumChatMessageDto[]>([]);
  chatInput = signal('');
  chatSending = signal(false);
  chatConnecting = signal(false);
  chatLoading = signal(false);
  agentConfig = signal<PracticumAgentConfigDto>({});
  agentReplying = signal(false);
  currentUserId = '';
  agentReplyTimeout: any = null;

  readonly chatSenderTypes = PracticumChatSenderType;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;

    const currentUser = this.configState.getDeep('currentUser') as any;
    this.currentUserId = currentUser?.id || '';

    this.loadProject(id);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.chatService.disconnect(this.project()?.id);
    if (this.sseSubscription) this.sseSubscription.unsubscribe();
    if (this.agentReplyTimeout) clearTimeout(this.agentReplyTimeout);
  }

  // ─── Project Data ─────────────────────────────────

  loadProject(id: string): void {
    this.practicumService.getDetail(id).subscribe({
      next: project => {
        this.project.set(project);
        for (const task of project.tasks) {
          this.forms[task.id] = this.forms[task.id] || { content: '', attachmentUrls: '', linkUrl: '', screenshotUrls: '' };
        }
        if (project.isCurrentUserEnrolled) {
          this.loadMyData(project.id);
        }
      },
    });
  }

  loadMyData(projectId: string): void {
    this.practicumService.getMyEnrollments().subscribe({
      next: enrollments => {
        const enrollment = enrollments.find(x => x.projectId === projectId);
        this.enrollmentId = enrollment?.id || null;
        if (!this.enrollmentId) return;
        this.practicumService.getSubmissionList({
          projectId, enrollmentId: this.enrollmentId, skipCount: 0, maxResultCount: 200,
        }).subscribe(result => this.submissions.set(result.items || []));
        this.practicumService.getGuidanceList(this.enrollmentId).subscribe(items => this.guidances.set(items || []));
      },
    });
  }

  enroll(): void {
    const project = this.project();
    if (!project) return;
    this.practicumService.enroll(project.id).subscribe({
      next: () => { this.message.success('已加入实训项目'); this.loadProject(project.id); },
      error: () => this.message.error('加入实训失败'),
    });
  }

  submit(taskId: string): void {
    const project = this.project();
    const form = this.forms[taskId];
    if (!project || !form) return;
    const input: CreatePracticumSubmissionDto = {
      projectId: project.id, taskId,
      content: form.content, attachmentUrls: form.attachmentUrls,
      linkUrl: form.linkUrl, screenshotUrls: form.screenshotUrls,
    };
    this.practicumService.createSubmission(input).subscribe({
      next: () => {
        this.message.success('任务成果已提交');
        this.forms[taskId] = { content: '', attachmentUrls: '', linkUrl: '', screenshotUrls: '' };
        this.loadMyData(project.id); this.loadProject(project.id);
      },
      error: () => this.message.error('提交失败'),
    });
  }

  getTaskSubmissions(taskId: string): PracticumSubmissionDto[] {
    return this.submissions().filter(x => x.taskId === taskId);
  }

  getSubmissionStatusLabel(status: PracticumSubmissionStatus): string {
    const labels: Record<number, string> = {
      [PracticumSubmissionStatus.Submitted]: '已提交',
      [PracticumSubmissionStatus.Returned]: '已退回',
      [PracticumSubmissionStatus.Reviewed]: '已评阅',
    };
    return labels[status] || '未知';
  }

  // ─── Tab Switch ─────────────────────────────────

  selectTab(index: number): void {
    this.activeTab.set(index);
    if (index === 3) this.initChat();
  }

  // ─── Chat ───────────────────────────────────────

  initChat(): void {
    const projectId = this.project()?.id;
    if (!projectId) return;

    // Load agent config
    this.chatService.getAgentConfig(projectId).subscribe({
      next: config => this.agentConfig.set(config),
      error: () => {},
    });

    // Load history
    if (this.chatMessages().length === 0) {
      this.chatLoading.set(true);
      this.chatService.getMessages(projectId, undefined, 30).subscribe({
        next: msgs => { this.chatMessages.set(msgs); this.chatLoading.set(false); },
        error: () => { this.chatLoading.set(false); this.message.error('加载聊天记录失败'); },
      });
    }

    // Connect SSE
    this.chatConnecting.set(true);
    this.chatService.connect(projectId).subscribe({
      next: () => {
        this.chatConnecting.set(false);
        this.sseSubscription?.unsubscribe();
        this.sseSubscription = this.chatService.messages$
          .pipe(takeUntil(this.destroy$))
          .subscribe(msg => {
            if (msg.projectId !== projectId) return; // Only for this project
            if (msg.senderType === PracticumChatSenderType.AIAgent) {
              this.agentReplying.set(false);
              if (this.agentReplyTimeout) { clearTimeout(this.agentReplyTimeout); this.agentReplyTimeout = null; }
            }
            const exists = this.chatMessages().some(m => m.id === msg.id);
            if (!exists) {
              this.chatMessages.update(existing => [...existing, msg]);
            }
          });
      },
      error: () => { this.chatConnecting.set(false); this.message.warning('聊天连接失败'); },
    });
  }

  chatSendMessage(): void {
    const content = this.chatInput().trim();
    const projectId = this.project()?.id;
    if (!content || !projectId || this.chatSending()) return;

    this.chatInput.set('');
    this.chatSending.set(true);

    const dto: SendPracticumChatMessageDto = {
      projectId, content,
      messageType: PracticumChatMessageType.Text,
    };

    this.chatService.sendMessage(dto).subscribe({
      next: (msg) => {
        this.chatSending.set(false);
        const exists = this.chatMessages().some(m => m.id === msg.id);
        if (!exists) this.chatMessages.update(existing => [...existing, msg]);
        if (content.includes(`@${this.agentName()}`)) {
          this.agentReplying.set(true);
          this.agentReplyTimeout = setTimeout(() => this.agentReplying.set(false), 30000);
        }
      },
      error: () => { this.chatSending.set(false); this.message.error('发送失败'); },
    });
  }

  onChatKey(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.chatSendMessage();
    }
  }

  agentName(): string {
    return this.agentConfig().agentName || '小智';
  }

  insertMention(): void {
    const name = this.agentName();
    const current = this.chatInput();
    if (current.includes(`@${name}`)) return;
    this.chatInput.set(current ? `${current} @${name} ` : `@${name} `);
  }

  isOwnChatMessage(msg: PracticumChatMessageDto): boolean {
    return msg.senderId === this.currentUserId;
  }
}
