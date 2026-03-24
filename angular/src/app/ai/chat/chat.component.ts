import { Component, signal, inject, ViewChild, ElementRef, ChangeDetectionStrategy, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzTypographyModule } from 'ng-zorro-antd/typography';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { Subject, takeUntil } from 'rxjs';
import { ChatService } from '../services/chat.service';

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
    NzEmptyModule
  ],
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChatComponent implements OnDestroy {
  private readonly chatService = inject(ChatService);
  private readonly destroy$ = new Subject<void>();
  
  @ViewChild('messagesContainer') messagesContainer!: ElementRef;
  
  messages = signal<ChatMessage[]>([]);
  inputMessage = signal('');
  isLoading = signal(false);
  threadId = signal<string>('');
  
  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
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
    
    this.chatService.chat({ message: content, threadId: this.threadId() })
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
