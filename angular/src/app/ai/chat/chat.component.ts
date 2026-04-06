import { Component, signal, inject, ViewChild, ElementRef, ChangeDetectionStrategy, OnDestroy, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
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

  formatMessage(content: string): string {
    if (!content) return '';
    return content
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/\n/g, '<br>')
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      .replace(/`(.*?)`/g, '<code>$1</code>');
  }
}
