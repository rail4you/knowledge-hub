import { Injectable, inject, NgZone } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ChatInput {
  message: string;
  threadId?: string;
  agent?: string;
}

export interface ChatMessageChunk {
  content: string;
  threadId: string;
  isComplete: boolean;
}

export interface FileUrl {
  url: string;
  type: string;
}

export interface ChatThread {
  id: string;
  createdAt: Date;
  messages: ChatMessage[];
}

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  createdAt: Date;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  private aguiUrl = 'http://localhost:5000';

  chat(input: ChatInput, agent: string = ''): Observable<ChatMessageChunk> {
    const endpoint = agent ? `/${agent}` : '/';
    return new Observable<ChatMessageChunk>(observer => {
      const messages = [
        { role: 'user', content: input.message }
      ];
      
      const body = JSON.stringify({ messages });
      
      fetch(`${this.aguiUrl}${endpoint}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body
      }).then(async response => {
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const reader = response.body?.getReader();
        if (!reader) {
          observer.complete();
          return;
        }
        
        const decoder = new TextDecoder();
        let buffer = '';
        
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;
          
          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n');
          buffer = lines.pop() || '';
          
          for (const line of lines) {
            if (line.startsWith('data: ')) {
              try {
                const event = JSON.parse(line.slice(6));
                if (event.type === 'TEXT_MESSAGE_CONTENT' && event.delta) {
                  observer.next({
                    content: event.delta,
                    threadId: event.threadId || '',
                    isComplete: false
                  });
                } else if (event.type === 'RUN_FINISHED') {
                  observer.next({
                    content: '',
                    threadId: event.threadId || '',
                    isComplete: true
                  });
                }
              } catch {
                // skip malformed JSON
              }
            }
          }
        }
        
        observer.complete();
      }).catch(err => {
        observer.error(err);
      });
      
      return () => {};
    });
  }
}