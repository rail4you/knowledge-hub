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
import { NzUploadModule } from 'ng-zorro-antd/upload';
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
import { PracticumService } from '../proxy/practicums/practicum.service';
import type { PracticumProjectDetailDto } from '../proxy/practicums/dtos/models';

export interface ChatContact {
  id: string;
  name: string;
  type: 'teacher' | 'student' | 'agent';
  avatarColor: string;
  lastMessage?: string;
  lastTime?: string;
  unread?: number;
}

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
  selectedContactId = signal<string>('all');
  showSidebar = signal(true);

  private destroy$ = new Subject<void>();
  private sseSubscription: Subscription | null = null;
  private shouldScrollToBottom = true;
  currentUserId = '';
  private oldestMessageId: string | null = null;

  readonly senderTypes = PracticumChatSenderType;
  readonly messageTypes = PracticumChatMessageType;

  agentName = computed(() => this.agentConfig.agentName || '小智');
  agentReplyTimeout: any = null;

  /** Filtered messages based on selected contact */
  readonly filteredMessages = computed(() => {
    const contactId = this.selectedContactId();
    if (contactId === 'all') return this.messages();
    if (contactId === 'agent') {
      return this.messages().filter(m => m.senderType === PracticumChatSenderType.AIAgent);
    }
    return this.messages().filter(m => m.senderId === contactId && m.senderType !== PracticumChatSenderType.AIAgent);
  });

  readonly selectedContact = computed(() => {
    const id = this.selectedContactId();
    if (id === 'all') return null;
    return this.contacts().find(c => c.id === id) || null;
  });

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

  /** 从消息中提取联系人列表（自动响应消息变化） */
  readonly contacts = computed(() => {
    const msgs = this.messages();
    const agent = this.agentName();
    const seenIds = new Set<string>();
    const list: ChatContact[] = [];

    list.push({ id: 'agent', name: agent, type: 'agent', avatarColor: 'linear-gradient(135deg, #6366f1, #8b5cf6)' });
    seenIds.add('agent');

    for (const msg of msgs) {
      if (msg.senderType === PracticumChatSenderType.AIAgent) continue;
      const key = msg.senderId || 'unknown_' + msg.senderName;
      if (seenIds.has(key)) continue;
      seenIds.add(key);

      const isCurrentUser = msg.senderId === this.currentUserId;
      list.push({
        id: msg.senderId || key,
        name: isCurrentUser ? `${msg.senderName}（我）` : msg.senderName,
        type: msg.senderType === PracticumChatSenderType.Teacher ? 'teacher' : 'student',
        avatarColor: msg.senderType === PracticumChatSenderType.Teacher ? '#1e6ce8' : '#10b981',
      });
    }

    return list;
  });

  private loadHistory(beforeId?: string): void {
    this.loadingHistory.set(true);
    this.chatService.getMessages(this.projectId, beforeId, 25).subscribe({
      next: msgs => {
        if (beforeId) {
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
        this.sseSubscription = this.chatService.messages$
          .pipe(takeUntil(this.destroy$))
          .subscribe(msg => {
            if (msg.senderType === PracticumChatSenderType.AIAgent) {
              this.isAgentReplying.set(false);
              if (this.agentReplyTimeout) {
                clearTimeout(this.agentReplyTimeout);
                this.agentReplyTimeout = null;
              }
            }
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

  // ─── Contact Selection ──────────────────────────

  selectContact(contactId: string): void {
    this.selectedContactId.set(contactId);
    this.shouldScrollToBottom = true;
    // Close sidebar on mobile
    if (window.innerWidth < 768) {
      this.showSidebar.set(false);
    }
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
        const exists = this.messages().some(m => m.id === msg.id);
        if (!exists) {
          this.messages.update(existing => [...existing, msg]);
        }

        if (content.includes(`@${this.agentName()}`)) {
          this.isAgentReplying.set(true);
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
    const oldest = this.filteredMessages()[0];
    if (!oldest) return;
    this.loadHistory(oldest.id);
  }

  // ─── Mention ──────────────────────────────────────

  getMentionText(): string {
    return `@${this.agentName()} `;
  }

  insertAgentMention(): void {
    const mention = this.getMentionText();
    const current = this.inputContent();
    if (current.includes(mention.trim())) return;
    this.inputContent.set(current ? `${current} ${mention}` : mention);
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

  toggleSidebar(): void {
    this.showSidebar.update(v => !v);
  }

  // ─── Helpers ─────────────────────────────────────

  autoResize(event: Event): void {
    const textarea = event.target as HTMLTextAreaElement;
    if (textarea) {
      textarea.style.height = 'auto';
      textarea.style.height = Math.min(textarea.scrollHeight, 120) + 'px';
    }
  }

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

  getContactName(senderId: string | undefined, senderType: PracticumChatSenderType, senderName: string): string {
    if (senderType === PracticumChatSenderType.AIAgent) return this.agentName();
    if (senderId === this.currentUserId) return '我';
    const contact = this.contacts().find(c => c.id === senderId);
    return contact?.name || senderName || '未知';
  }

  getContactColor(senderId: string | undefined, senderType: PracticumChatSenderType): string {
    if (senderType === PracticumChatSenderType.AIAgent) return 'linear-gradient(135deg, #6366f1, #8b5cf6)';
    if (senderId === this.currentUserId) return '#2563eb';
    const contact = this.contacts().find(c => c.id === senderId);
    return contact?.avatarColor || '#64748b';
  }
}
