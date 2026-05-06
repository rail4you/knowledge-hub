import { Component, signal, inject, ViewChild, ElementRef, ChangeDetectionStrategy, OnDestroy, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzTypographyModule } from 'ng-zorro-antd/typography';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzListModule } from 'ng-zorro-antd/list';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { Subject, takeUntil } from 'rxjs';
import { marked } from 'marked';
import { ChatService, ResourceForChat } from '../services/chat.service';

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  createdAt: Date;
}

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [
    CommonModule,
    NzInputModule,
    NzButtonModule,
    NzCardModule,
    NzSpinModule,
    NzIconModule,
    NzAvatarModule,
    NzTypographyModule,
    NzEmptyModule,
    NzListModule,
    NzTagModule,
    NzDividerModule
  ],
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChatComponent implements OnInit, OnDestroy {
  private readonly chatService = inject(ChatService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly destroy$ = new Subject<void>();

  @ViewChild('messagesContainer') messagesContainer!: ElementRef;

  messages = signal<ChatMessage[]>([]);
  inputMessage = signal('');
  isLoading = signal(false);
  threadId = signal<string>('');
  resources = signal<ResourceForChat[]>([]);
  selectedResource = signal<ResourceForChat | null>(null);
  resourceSearchText = signal('');
  isResourcesLoading = signal(false);
  inputPlaceholder = computed(() =>
    this.selectedResource()
      ? `输入关于《${this.selectedResource()!.name}》的问题，按 Enter 发送...`
      : '输入您的问题，按 Enter 发送...'
  );
  welcomeTitle = computed(() =>
    this.selectedResource()
      ? `关于《${this.selectedResource()!.name}》的问答`
      : '你好，我是文档问答助手'
  );
  welcomeDescription = computed(() =>
    this.selectedResource()
      ? '请输入关于这份资源的问题'
      : '请从左侧选择一份资源，或直接开始提问'
  );

  filteredResources = computed(() => {
    const search = this.resourceSearchText().toLowerCase();
    if (!search) return this.resources();
    return this.resources().filter(r =>
      r.name.toLowerCase().includes(search)
    );
  });

  ngOnInit() {
    this.loadResources();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadResources() {
    this.isResourcesLoading.set(true);
    this.chatService.getResources()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (data) => {
          this.resources.set(data);
          this.isResourcesLoading.set(false);
        },
        error: (err) => {
          console.error('Failed to load resources:', err);
          this.isResourcesLoading.set(false);
        }
      });
  }

  selectResource(resource: ResourceForChat) {
    const current = this.selectedResource();
    if (current?.id === resource.id) {
      // Deselect
      this.selectedResource.set(null);
    } else {
      this.selectedResource.set(resource);
      // Clear chat when switching document
      this.messages.set([]);
      this.threadId.set('');
    }
  }

  newChat() {
    this.messages.set([]);
    this.threadId.set('');
    this.selectedResource.set(null);
  }

  sendMessage() {
    const content = this.inputMessage().trim();
    if (!content || this.isLoading()) return;

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content,
      createdAt: new Date()
    };

    this.messages.update(msgs => [...msgs, userMessage]);
    this.inputMessage.set('');
    this.isLoading.set(true);
    this.scrollToBottom();

    const assistantMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'assistant',
      content: '',
      createdAt: new Date()
    };
    this.messages.update(msgs => [...msgs, assistantMessage]);

    const selectedRes = this.selectedResource();

    this.chatService.chat({
      message: content,
      threadId: this.threadId() || undefined,
      resourceId: selectedRes?.id,
    })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (chunk) => {
          if (chunk.content) {
            this.messages.update(msgs => {
              const lastMsg = msgs[msgs.length - 1];
              if (lastMsg && lastMsg.role === 'assistant') {
                return [...msgs.slice(0, -1), { ...lastMsg, content: lastMsg.content + chunk.content }];
              }
              return msgs;
            });
            this.scrollToBottom();
          }
          if (chunk.threadId && !this.threadId()) {
            this.threadId.set(chunk.threadId);
          }
        },
        error: (err) => {
          console.error('Chat error:', err);
          this.isLoading.set(false);
          this.messages.update(msgs => {
            const lastMsg = msgs[msgs.length - 1];
            if (lastMsg && lastMsg.role === 'assistant') {
              return [...msgs.slice(0, -1), { ...lastMsg, content: '抱歉，发生了错误，请稍后重试。' }];
            }
            return msgs;
          });
        },
        complete: () => {
          this.isLoading.set(false);
        }
      });
  }

  onKeyPress(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  private scrollToBottom() {
    setTimeout(() => {
      const el = this.messagesContainer?.nativeElement;
      if (el) {
        el.scrollTop = el.scrollHeight;
      }
    }, 0);
  }

  formatMessage(content: string): SafeHtml {
    if (!content) return '';
    const cleaned = this.sanitizeAssistantContent(content);
    const html = marked.parse(cleaned, { async: false }) as string;
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  private sanitizeAssistantContent(content: string): string {
    let cleaned = content;

    cleaned = cleaned.replace(/<think\b[^>]*>[\s\S]*?<\/think>/gi, '');
    cleaned = cleaned.replace(/<thinking\b[^>]*>[\s\S]*?<\/thinking>/gi, '');
    cleaned = cleaned.replace(/<details\b[^>]*>\s*<summary\b[^>]*>\s*(?:思考|推理|工具|检索|thinking|reasoning|tool)[\s\S]*?<\/details>/gi, '');
    cleaned = cleaned.replace(/```(?:think|thinking|thought|reasoning|tool|tool_call|tool_result)[\s\S]*?```/gi, '');

    // Hide unfinished reasoning/tool blocks while streaming.
    cleaned = cleaned.replace(/<think\b[^>]*>[\s\S]*$/gi, '');
    cleaned = cleaned.replace(/<thinking\b[^>]*>[\s\S]*$/gi, '');
    cleaned = cleaned.replace(/<details\b[^>]*>\s*<summary\b[^>]*>\s*(?:思考|推理|工具|检索|thinking|reasoning|tool)[\s\S]*$/gi, '');
    cleaned = cleaned.replace(/```(?:think|thinking|thought|reasoning|tool|tool_call|tool_result)[\s\S]*$/gi, '');

    cleaned = cleaned
      .split('\n')
      .filter(line => !this.isAuxiliaryLine(line))
      .join('\n');

    cleaned = cleaned.replace(/\n{3,}/g, '\n\n').trim();
    return cleaned || '正在整理答案...';
  }

  private isAuxiliaryLine(line: string): boolean {
    const normalized = line.trim();
    if (!normalized) return false;

    const auxiliaryPatterns = [
      /^思考(过程|如下)?[:：]?$/i,
      /^推理(过程|如下)?[:：]?$/i,
      /^工具(调用|使用|返回|结果)?[:：]?$/i,
      /^检索(过程|步骤|结果)?[:：]?$/i,
      /^调用工具[:：]?/i,
      /^正在调用[:：]?/i,
      /^已(?:调用|检索|搜索|读取)[:：]?/i,
      /^我先(调用|搜索|查看|读取|检索)/i,
      /^让我先(调用|搜索|查看|读取|检索)/i,
      /^先调用\s+/i,
      /^\[?(?:tool|reasoning|thinking)[_\s-]?(?:call|result|step)?\]?[:：]?/i,
      /^SearchPageIndex\b/i,
      /^get_document\b/i,
      /^get_document_structure\b/i,
      /^get_page_content\b/i,
      /^tool[_\s-]?call/i,
      /^tool[_\s-]?result/i,
      /^reasoning[:：]?/i,
      /^thinking[:：]?/i,
    ];

    return auxiliaryPatterns.some(pattern => pattern.test(normalized));
  }
}
