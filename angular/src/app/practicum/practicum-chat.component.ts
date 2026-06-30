import {
  Component, OnInit, OnDestroy, inject, signal, computed,
  ViewChild, ElementRef, AfterViewChecked, ChangeDetectionStrategy, HostListener
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { ConfigStateService } from '@abp/ng.core';
import { Subject, takeUntil, Subscription } from 'rxjs';
import {
  PracticumChatService,
  PracticumChatMessageDto,
  PracticumChatSenderType,
  PracticumChatMessageType,
  SendPracticumChatMessageDto,
  PracticumAgentConfigDto,
} from '../practicum/practicum-chat.service';
import { OssUploadService } from '../shared/oss-upload.service';
import type { PracticumProjectDetailDto } from '../proxy/practicums/dtos/models';
import { PracticumService } from '../proxy/practicums/practicum.service';

@Component({
  selector: 'app-practicum-chat',
  standalone: true,
  imports: [
    CommonModule, DatePipe, FormsModule, RouterModule,
    NzButtonModule, NzIconModule, NzInputModule, NzSpinModule,
    NzAvatarModule, NzTooltipModule, NzUploadModule,
  ],
  templateUrl: './practicum-chat.component.html',
  styleUrls: ['./practicum-chat.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumChatComponent implements OnInit, OnDestroy, AfterViewChecked {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly chatService = inject(PracticumChatService);
  private readonly practicumService = inject(PracticumService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly configState = inject(ConfigStateService);
  private readonly message = inject(NzMessageService);

  @ViewChild('messagesContainer') messagesContainer!: ElementRef;
  @ViewChild('fileInput') fileInput!: ElementRef;

  projectId = '';
  projectDetail: PracticumProjectDetailDto | null = null;
  agentConfig: PracticumAgentConfigDto = {};

  messages = signal<PracticumChatMessageDto[]>([]);
  inputContent = signal('');
  isLoading = signal(false);
  isConnecting = signal(false);
  isAgentReplying = signal(false);
  loadingHistory = signal(false);
  hasMoreHistory = signal(true);
  uploadingFile = signal(false);

  private destroy$ = new Subject<void>();
  private sseSubscription: Subscription | null = null;
  private shouldScrollToBottom = true;
  currentUserId = '';
  private oldestMessageId: string | null = null;

  readonly senderTypes = PracticumChatSenderType;
  readonly messageTypes = PracticumChatMessageType;

  // Agent name for mention
  agentName = computed(() => this.agentConfig.agentName || '小智');
  agentReplyTimeout: any = null;

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('id') || '';
    if (!this.projectId) {
      this.router.navigate(['/']);
      return;
    }

    const currentUser = this.configState.getDeep('currentUser') as any;
    this.currentUserId = currentUser?.id || '';

    this.loadProject();
    this.loadAgentConfig();
    this.connectSse();
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) {
      this.scrollToBottom();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.chatService.disconnect(this.projectId);
    if (this.agentReplyTimeout) clearTimeout(this.agentReplyTimeout);
  }

  // ─── Data Loading ─────────────────────────────────

  private loadProject(): void {
    this.practicumService.getDetail(this.projectId).subscribe({
      next: detail => {
        this.projectDetail = detail;
        this.loadHistory();
      },
      error: () => this.message.error('加载实训项目详情失败'),
    });
  }

  private loadAgentConfig(): void {
    this.chatService.getAgentConfig(this.projectId).subscribe({
      next: config => { this.agentConfig = config; },
      error: () => {},
    });
  }

  private loadHistory(beforeId?: string): void {
    this.loadingHistory.set(true);
    this.chatService.getMessages(this.projectId, beforeId, 25).subscribe({
      next: msgs => {
        if (beforeId) {
          // Prepend older messages
          this.messages.update(existing => [...msgs, ...existing]);
          if (msgs.length < 25) this.hasMoreHistory.set(false);
          if (msgs.length > 0) this.oldestMessageId = msgs[msgs.length - 1].id;
        } else {
          this.messages.set(msgs);
          this.oldestMessageId = msgs.length > 0 ? msgs[0].id : null;
          if (msgs.length < 25) this.hasMoreHistory.set(false);
        }
        this.loadingHistory.set(false);
      },
      error: () => {
        this.loadingHistory.set(false);
        this.message.error('加载聊天记录失败');
      },
    });
  }

  // ─── SSE Connection ──────────────────────────────

  private connectSse(): void {
    this.isConnecting.set(true);
    this.chatService.connect(this.projectId).subscribe({
      next: () => {
        this.isConnecting.set(false);
        // Listen for incoming SSE messages
        this.sseSubscription = this.chatService.messages$
          .pipe(takeUntil(this.destroy$))
          .subscribe(msg => {
            // Check if this is the AI reply we've been waiting for
            if (msg.senderType === PracticumChatSenderType.AIAgent) {
              this.isAgentReplying.set(false);
              if (this.agentReplyTimeout) {
                clearTimeout(this.agentReplyTimeout);
                this.agentReplyTimeout = null;
              }
            }
            // Dedup: avoid adding message already inserted locally by sendMessage()
            const exists = this.messages().some(m => m.id === msg.id);
            if (!exists) {
              this.messages.update(existing => [...existing, msg]);
              this.shouldScrollToBottom = true;
            }
          });
      },
      error: () => {
        this.isConnecting.set(false);
        this.message.warning('聊天连接已断开，请刷新页面重试');
      },
    });
  }

  // ─── Message Sending ─────────────────────────────

  sendMessage(): void {
    const content = this.inputContent().trim();
    if (!content || this.isLoading()) return;

    this.inputContent.set('');
    this.isLoading.set(true);

    const dto: SendPracticumChatMessageDto = {
      projectId: this.projectId,
      content,
      messageType: PracticumChatMessageType.Text,
    };

    this.chatService.sendMessage(dto).subscribe({
      next: (msg) => {
        this.isLoading.set(false);
        // Add message locally immediately (SSE will deliver it too, but this is a fallback)
        const exists = this.messages().some(m => m.id === msg.id);
        if (!exists) {
          this.messages.update(existing => [...existing, msg]);
        }

        // Check if message triggered AI
        if (content.includes(`@${this.agentName()}`)) {
          this.isAgentReplying.set(true);
          // Timeout: if AI doesn't reply within 30s, hide typing indicator
          this.agentReplyTimeout = setTimeout(() => {
            this.isAgentReplying.set(false);
          }, 30000);
        }
        this.shouldScrollToBottom = true;
      },
      error: () => {
        this.isLoading.set(false);
        this.message.error('发送失败，请重试');
      },
    });
  }

  onKeyPress(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  // ─── File Upload ────────────────────────────────

  triggerFileUpload(): void {
    this.fileInput?.nativeElement?.click();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (file.size > 25 * 1024 * 1024) {
      this.message.error('文件大小不能超过 25MB');
      input.value = '';
      return;
    }

    this.uploadingFile.set(true);
    this.ossUploadService.uploadFile(file).subscribe({
      next: (result) => {
        this.uploadingFile.set(false);
        // Send file message
        const dto: SendPracticumChatMessageDto = {
          projectId: this.projectId,
          content: file.name,
          messageType: PracticumChatMessageType.File,
          attachmentUrl: result.url,
          attachmentName: result.originalFileName,
          attachmentSize: file.size,
        };
        this.chatService.sendMessage(dto).subscribe({
          next: () => { this.shouldScrollToBottom = true; },
          error: () => this.message.error('文件消息发送失败'),
        });
      },
      error: () => {
        this.uploadingFile.set(false);
        this.message.error('文件上传失败');
      },
    });
    input.value = '';
  }

  // ─── Infinite Scroll ─────────────────────────────

  onScroll(): void {
    const el = this.messagesContainer?.nativeElement;
    if (!el) return;
    this.shouldScrollToBottom = el.scrollTop + el.clientHeight >= el.scrollHeight - 50;
  }

  loadMoreHistory(): void {
    if (this.loadingHistory() || !this.hasMoreHistory()) return;
    const oldest = this.messages()[0];
    if (!oldest) return;
    this.loadHistory(oldest.id);
  }

  // ─── Mention ──────────────────────────────────────

  insertAgentMention(): void {
    const name = this.agentName();
    const current = this.inputContent();
    if (current.includes(`@${name}`)) return;
    this.inputContent.set(current ? `${current} @${name} ` : `@${name} `);
  }

  // ─── Navigation ──────────────────────────────────

  goBack(): void {
    const isTeacher = this.isTeacherRoute();
    if (isTeacher) {
      this.router.navigate(['/practicum/project', this.projectId]);
    } else {
      this.router.navigate(['/student/practicums', this.projectId]);
    }
  }

  isTeacherRoute(): boolean {
    return this.router.url.startsWith('/practicum/project');
  }

  // ─── Helpers ─────────────────────────────────────

  private scrollToBottom(): void {
    try {
      const el = this.messagesContainer?.nativeElement;
      if (el) {
        el.scrollTop = el.scrollHeight;
      }
    } catch {}
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  isOwnMessage(msg: PracticumChatMessageDto): boolean {
    return msg.senderId === this.currentUserId;
  }
}
