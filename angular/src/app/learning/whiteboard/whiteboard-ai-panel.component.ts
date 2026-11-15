import {
  Component, ChangeDetectionStrategy, inject, signal, input, output, OnDestroy, OnInit,
  ElementRef, viewChild, effect,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { RestService } from '@abp/ng.core';
import { lastValueFrom } from 'rxjs';

/** 助手一轮问答 */
interface AssistantTurn {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  imageUrl?: string | null;
  imagePrompt?: string | null;
  /** 是否正在等待后端 */
  pending?: boolean;
  /** 出错了（气泡里显示原因） */
  error?: string | null;
  context?: AssistantContext | null;
}

interface AssistantContext {
  courseId?: string | null;
  courseName?: string | null;
  boardTitle?: string | null;
  canvasTextCount: number;
  chatMessageCount: number;
  selectedText?: string | null;
}

export interface AssistantAskInput {
  whiteboardId: string;
  message: string;
  mode: 'text' | 'image';
  selectedText?: string | null;
  history: { role: string; content: string }[];
}

export interface AssistantHistoryItem {
  id: string;
  role: string;
  content: string;
  imageUrl?: string | null;
  imagePrompt?: string | null;
  creationTime: string;
}

export interface AssistantReply {
  content: string;
  imageUrl?: string | null;
  imagePrompt?: string | null;
  context: AssistantContext;
}

/** 快速提问：都是「一句话让助手上手」的教研常用动作 */
const QUICK_PROMPTS: { label: string; message: string; mode: 'text' | 'image' }[] = [
  { label: '补充这个概念', message: '把我刚写的这句话补成一段可以直接讲课的板书内容，控制在 5 行以内。', mode: 'text' },
  { label: '提炼关键点', message: '基于白板现有内容，提炼 3~5 个关键知识点，每条一句话。', mode: 'text' },
  { label: '出课堂提问', message: '围绕这个主题，给我 3 个课堂上能用的提问（由浅入深）。', mode: 'text' },
  { label: '生成配图', message: '按这个主题生成一张适合课堂展示的教学配图。', mode: 'image' },
];

/**
 * 白板右侧「AI 助手」面板（课程助教）。
 *
 * <p><b>它知道什么</b>：白板关联的课程（课程名 / 简介 / 章节）、白板标题与主题、
 * 画布上已经写/贴的内容、协作频道的最近讨论、本次会话的历史，以及老师**当前选中的那段板书**。
 * 上下文全部在后端按白板 Id 组装（见 WhiteboardAssistantAppService），前端只送
 * 「白板 Id + 这一句话 + 会话历史 + 选中文本」。</p>
 *
 * <p><b>产出怎么用</b>：回复气泡下面给出三个动作 —— 插入新文本 / 替换所选 / 追加到所选；
 * 图片模式给出缩略图 + 「插入图片」。画布写入全部交给父组件（它持有 Konva 与实时通道），
 * 这里只发事件，避免面板与画布耦合。</p>
 */
@Component({
  selector: 'app-whiteboard-ai-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzIconModule, NzTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="ai">
      <!-- 上下文条：让老师清楚助手看到了什么 -->
      <div class="ai__ctx">
        @if (courseName()) {
          <span class="ai__ctx-item" nz-tooltip [nzTooltipTitle]="'助手会读取课程简介与章节目录'">
            <span nz-icon nzType="book"></span>{{ courseName() }}
          </span>
        } @else {
          <span class="ai__ctx-item is-warn" nz-tooltip nzTooltipTitle="在「新建白板」时关联课程，助手就能拿到课程与章节">
            <span nz-icon nzType="book"></span>未关联课程
          </span>
        }
        @if (selectionText()) {
          <span class="ai__ctx-item is-active" nz-tooltip [nzTooltipTitle]="selectionText()!">
            <span nz-icon nzType="aim"></span>已选板书：{{ brief(selectionText()!) }}
          </span>
        } @else {
          <span class="ai__ctx-item is-muted">点选画布文本可让助手改这一段</span>
        }
      </div>

      @if (turns().length > 0) {
        <div class="ai__bar">
          <span class="ai__bar-count">已聊 {{ turns().length }} 轮</span>
          <button type="button" class="ai__bar-clear" (click)="clearHistory()">
            <span nz-icon nzType="clear"></span> 清空会话
          </button>
        </div>
      }

      <!-- 消息流 -->
      <div class="ai__msgs" [class.has-turns]="turns().length > 0" #msgList (scroll)="onScroll()">
        @if (turns().length === 0) {
          <div class="ai__welcome">
            <p class="ai__welcome-title">
              <span nz-icon nzType="robot"></span>
              我是这门课的助教
            </p>
            <p class="ai__welcome-text">
              白板上的文字、你们的讨论我都会读；选中一段板书还能直接让它改写、扩写。
            </p>
            <div class="ai__quick">
              @for (q of quickPrompts; track q.label) {
                <button class="ai__quick-btn" type="button" (click)="askQuick(q)">
                  {{ q.label }}
                </button>
              }
            </div>
          </div>
        }

        @for (t of turns(); track t.id) {
          <div class="ai__turn" [class.is-user]="t.role === 'user'">
            <div class="ai__who">
              @if (t.role === 'user') { 我 } @else { <span nz-icon nzType="robot"></span> 助教 }
            </div>
            <div class="ai__bubble" [class.is-error]="!!t.error">
              @if (t.pending) {
                <span class="ai__typing">
                  <i></i><i></i><i></i>
                  {{ busyText() }}
                </span>
              } @else if (t.error) {
                {{ t.error }}
              } @else {
                @if (t.imageUrl) {
                  <img class="ai__img" [src]="t.imageUrl" alt="AI 生成的教学配图" />
                }
                <div class="ai__text">{{ t.content }}</div>
                @if (t.imagePrompt) {
                  <p class="ai__prompt">画面描述：{{ t.imagePrompt }}</p>
                }
              }
            </div>

            @if (t.role === 'assistant' && !t.pending) {
              <div class="ai__actions">
                @if (t.imageUrl) {
                  <button nz-button nzSize="small" nzType="primary" (click)="insertImage.emit({ url: t.imageUrl!, prompt: t.imagePrompt || t.content })">
                    <span nz-icon nzType="picture"></span> 插入图片
                  </button>
                } @else if (t.content) {
                  <button nz-button nzSize="small" (click)="insertText.emit(t.content)">
                    <span nz-icon nzType="plus"></span> 新建文本
                  </button>
                  <button
                    nz-button
                    nzSize="small"
                    [disabled]="!selectionKey()"
                    nz-tooltip
                    [nzTooltipTitle]="selectionKey() ? '用助手内容替换选中的那段板书' : '先在画布上点选一段文本'"
                    (click)="applyToSelection(t.content, 'replace')"
                  >
                    替换所选
                  </button>
                  <button
                    nz-button
                    nzSize="small"
                    [disabled]="!selectionKey()"
                    nz-tooltip
                    [nzTooltipTitle]="selectionKey() ? '保留原内容并追加助手内容' : '先在画布上点选一段文本'"
                    (click)="applyToSelection(t.content, 'append')"
                  >
                    追加到所选
                  </button>
                }
              </div>
            }
          </div>
        }
      </div>

      <!-- 输入 -->
      <div class="ai__composer">
        <div class="ai__modes">
          <button
            type="button"
            class="ai__mode"
            [class.is-active]="mode() === 'text'"
            (click)="mode.set('text')"
          >
            <span nz-icon nzType="font-size"></span> 文本
          </button>
          <button
            type="button"
            class="ai__mode"
            [class.is-active]="mode() === 'image'"
            (click)="mode.set('image')"
          >
            <span nz-icon nzType="picture"></span> 配图
          </button>
        </div>
        <div class="ai__input-row">
          <textarea
            nz-input
            class="ai__input"
            rows="2"
            [placeholder]="mode() === 'image' ? '描述你想要的画面（如：细胞膜结构示意图）' : '问问助教，或直接写一句要补全的话…'"
            [ngModel]="draft()"
            (ngModelChange)="draft.set($event)"
            (keydown)="onKeydown($event)"
          ></textarea>
          <button
            nz-button
            nzType="primary"
            class="ai__send"
            [disabled]="!draft().trim() || busy()"
            (click)="send()"
          >
            <span nz-icon nzType="send"></span>
          </button>
        </div>
      </div>
    </section>
  `,
  styles: [`
    :host { display: flex; flex-direction: column; height: 100%; min-height: 0; }

    .ai { display: flex; flex-direction: column; height: 100%; min-height: 0; background: #fcfdfd; }

    /* 上下文条 */
    .ai__ctx {
      flex: 0 0 auto; display: flex; flex-wrap: wrap; gap: 6px;
      padding: 8px 12px; background: #f7faf9; border-bottom: 1px solid #eef2f1;
    }
    .ai__ctx-item {
      display: inline-flex; align-items: center; gap: 4px;
      max-width: 100%; padding: 1px 8px; border-radius: 999px;
      background: #fff; border: 1px solid #e3eae8;
      font-size: 11px; color: #4b5c62;
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    }
    .ai__ctx-item.is-active { border-color: #bfe3d3; background: #f2fbf6; color: #0f7a5a; }
    .ai__ctx-item.is-warn { border-color: #f0d8b3; background: #fffaf1; color: #a8730f; }
    .ai__ctx-item.is-muted { border-style: dashed; color: #96a5aa; }

    /* 会话工具条 */
    .ai__bar {
      flex: 0 0 auto; display: flex; align-items: center; gap: 8px;
      padding: 4px 12px; border-bottom: 1px solid #f1f5f4; background: #fff;
    }
    .ai__bar-count { font-size: 11px; color: #96a5aa; }
    .ai__bar-clear {
      margin-left: auto; display: inline-flex; align-items: center; gap: 4px;
      padding: 2px 8px; border-radius: 999px; cursor: pointer;
      border: 1px solid #e6ecea; background: #fff; color: #7d8d93; font-size: 11px;
    }
    .ai__bar-clear:hover { border-color: #e0b4ad; color: #b93a3a; background: #fff7f6; }

    /* 消息流 */
    .ai__msgs {
      flex: 1 1 auto; min-height: 0; overflow-y: auto;
      padding: 12px; display: flex; flex-direction: column; gap: 12px;
    }
    /* 只有在有对话后才把内容压到底部；首屏欢迎语从顶部开始读起来更自然 */
    .ai__msgs.has-turns::before { content: ''; margin-top: auto; }

    .ai__welcome { padding: 4px 2px 0; }
    .ai__welcome-title {
      margin: 0 0 6px; display: flex; align-items: center; gap: 6px;
      font-size: 13px; font-weight: 650; color: #22333a;
    }
    .ai__welcome-text { margin: 0 0 10px; font-size: 12px; line-height: 1.6; color: #7d8d93; }
    .ai__quick { display: flex; flex-wrap: wrap; gap: 6px; }
    .ai__quick-btn {
      padding: 4px 10px; border-radius: 999px; cursor: pointer;
      border: 1px solid #d9e6e2; background: #fff; color: #2b4a45;
      font-size: 11.5px; transition: all .15s ease;
    }
    .ai__quick-btn:hover { border-color: #087f7a; color: #087f7a; background: #f2fbf9; }

    .ai__turn { display: flex; flex-direction: column; gap: 4px; align-items: flex-start; }
    .ai__turn.is-user { align-items: flex-end; }
    .ai__who { font-size: 10.5px; color: #96a5aa; display: inline-flex; align-items: center; gap: 4px; }

    .ai__bubble {
      max-width: 100%; padding: 8px 11px; border-radius: 10px 10px 10px 3px;
      background: #fff; border: 1px solid #e7edeb;
      font-size: 13px; line-height: 1.62; color: #22333a;
      white-space: pre-wrap; word-break: break-word;
    }
    .ai__turn.is-user .ai__bubble {
      background: #eef6f4; border-color: #d5e8e2; border-radius: 10px 10px 3px 10px;
    }
    .ai__bubble.is-error { background: #fff5f5; border-color: #f3d3d3; color: #b93a3a; }
    .ai__img { display: block; width: 100%; max-width: 240px; border-radius: 8px; margin-bottom: 6px; }
    .ai__prompt { margin: 6px 0 0; font-size: 10.5px; line-height: 1.5; color: #96a5aa; }

    .ai__typing { display: inline-flex; align-items: center; gap: 4px; color: #7d8d93; font-size: 12px; }
    .ai__typing i {
      width: 4px; height: 4px; border-radius: 50%; background: #18a058; display: inline-block;
      animation: ai-dot 1s infinite ease-in-out;
    }
    .ai__typing i:nth-child(2) { animation-delay: .15s; }
    .ai__typing i:nth-child(3) { animation-delay: .3s; margin-right: 4px; }
    @keyframes ai-dot { 0%, 80%, 100% { opacity: .25; } 40% { opacity: 1; } }

    .ai__actions { display: flex; flex-wrap: wrap; gap: 6px; }
    .ai__actions button { font-size: 11.5px; height: 26px; padding: 0 10px; }

    /* 输入 */
    .ai__composer { flex: 0 0 auto; border-top: 1px solid #eef2f1; background: #fff; padding: 8px 10px 10px; }
    .ai__modes { display: flex; gap: 6px; margin-bottom: 6px; }
    .ai__mode {
      display: inline-flex; align-items: center; gap: 4px;
      padding: 2px 9px; border-radius: 999px; cursor: pointer;
      border: 1px solid #e0e8e5; background: #fff; color: #64757a; font-size: 11.5px;
    }
    .ai__mode.is-active { border-color: #087f7a; background: #f1faf8; color: #087f7a; }
    .ai__input-row { display: flex; align-items: flex-end; gap: 6px; }
    .ai__input {
      flex: 1 1 auto; resize: none; min-height: 46px; max-height: 96px;
      padding: 6px 10px; border-radius: 9px; font-size: 13px; line-height: 1.5;
    }
    .ai__send { flex: 0 0 auto; height: 32px; width: 34px; padding: 0; }
  `],
})
export class WhiteboardAiPanelComponent implements OnDestroy {
  private readonly rest = inject(RestService);
  private readonly message = inject(NzMessageService);
  private readonly listRef = viewChild<ElementRef<HTMLDivElement>>('msgList');

  /** 白板 Id（必填：上下文由后端按它组装） */
  readonly whiteboardId = input.required<string>();
  /** 白板关联课程名（仅用于顶部上下文条展示） */
  readonly courseName = input<string | null>(null);
  /** 当前选中的画布元素（只有文本 / 便签有值） */
  readonly selectionKey = input<string | null>(null);
  readonly selectionText = input<string | null>(null);

  readonly insertText = output<string>();
  readonly insertImage = output<{ url: string; prompt: string }>();
  readonly replaceSelection = output<string>();
  readonly appendSelection = output<string>();

  readonly turns = signal<AssistantTurn[]>([]);
  readonly draft = signal('');
  readonly mode = signal<'text' | 'image'>('text');
  readonly busy = signal(false);
  readonly busyText = signal('正在思考…');
  readonly quickPrompts = QUICK_PROMPTS;

  private destroyed = false;
  private seq = 0;
  /** 已经回放过历史的白板 Id（同一块白板不重复拉取） */
  private historyLoadedFor: string | null = null;

  constructor() {
    // 换白板 / 首次进入时回放「我在这块白板上的助手会话」（落库在 AppWhiteboardAssistantMessages）
    effect(() => {
      const id = this.whiteboardId();
      if (!id || this.historyLoadedFor === id) return;
      this.historyLoadedFor = id;
      this.loadHistory(id);
    });
  }

  ngOnDestroy(): void {
    this.destroyed = true;
  }

  private loadHistory(whiteboardId: string): void {
    void lastValueFrom(
      this.rest.request<any, AssistantHistoryItem[]>(
        {
          method: 'GET',
          url: `/api/app/whiteboard-assistant/history/${whiteboardId}`,
          params: { maxResultCount: 40 },
        },
        { apiName: 'KnowledgeHub' },
      ),
    )
      .then((rows) => {
        if (this.destroyed || !rows?.length) return;
        this.turns.set(
          rows.map((r) => ({
            id: this.nextId('h'),
            role: r.role === 'assistant' ? 'assistant' : 'user',
            content: r.content,
            imageUrl: r.imageUrl,
            imagePrompt: r.imagePrompt,
          })),
        );
        this.scrollToBottom(true);
      })
      .catch(() => undefined);
  }

  /** 清空当前白板的助手会话（先清屏，再让后端删；失败则提示） */
  clearHistory(): void {
    const whiteboardId = this.whiteboardId();
    if (!whiteboardId) return;
    this.turns.set([]);
    void lastValueFrom(
      this.rest.request<any, void>(
        { method: 'POST', url: `/api/app/whiteboard-assistant/clear-history/${whiteboardId}` },
        { apiName: 'KnowledgeHub' },
      ),
    ).catch(() => this.message.warning('清空失败，请稍后再试'));
  }

  onKeydown(ev: KeyboardEvent): void {
    if (ev.key !== 'Enter' || ev.shiftKey) return;
    ev.preventDefault();
    this.send();
  }

  askQuick(q: { label: string; message: string; mode: 'text' | 'image' }): void {
    this.draft.set(q.message);
    this.mode.set(q.mode);
    this.send();
  }

  send(): void {
    const text = this.draft().trim();
    if (!text || this.busy()) return;

    const mode = this.mode();
    this.draft.set('');
    this.busy.set(true);
    this.busyText.set(mode === 'image' ? '正在生成配图（约 20~60 秒）…' : '正在思考…');

    const userTurn: AssistantTurn = { id: this.nextId('u'), role: 'user', content: text };
    const replyTurn: AssistantTurn = { id: this.nextId('a'), role: 'assistant', content: '', pending: true };
    this.turns.update((list) => [...list, userTurn, replyTurn]);
    this.scrollToBottom(true);

    // 会话历史：后端优先用库里持久化的记录；这里仍带上最近几轮作为兜底
    const history = this.turns()
      .filter((t) => !t.pending && !t.error && t.content)
      .slice(-8)
      .map((t) => ({ role: t.role, content: t.content }));

    const body: AssistantAskInput = {
      whiteboardId: this.whiteboardId(),
      message: text,
      mode,
      selectedText: this.selectionText() || null,
      history,
    };

    void lastValueFrom(
      this.rest.request<any, AssistantReply>(
        { method: 'POST', url: '/api/app/whiteboard-assistant/ask', body },
        { apiName: 'KnowledgeHub' },
      ),
    )
      .then((reply) => {
        if (this.destroyed) return;
        this.patchTurn(replyTurn.id, {
          content: reply.content || '',
          imageUrl: reply.imageUrl || null,
          imagePrompt: reply.imagePrompt || null,
          context: reply.context,
          pending: false,
        });
      })
      .catch((err: any) => {
        if (this.destroyed) return;
        const text = err?.error?.error?.message || '助手暂时没有响应，请稍后再试。';
        this.patchTurn(replyTurn.id, { pending: false, error: text });
        this.message.error(text);
      })
      .finally(() => {
        if (this.destroyed) return;
        this.busy.set(false);
        this.scrollToBottom(true);
      });
  }

  applyToSelection(content: string, kind: 'replace' | 'append'): void {
    if (!this.selectionKey()) {
      this.message.warning('先在画布上点选一段文本，再让助手改它');
      return;
    }
    if (kind === 'replace') this.replaceSelection.emit(content);
    else this.appendSelection.emit(content);
  }

  onScroll(): void {
    const el = this.listEl();
    if (!el) return;
    this.atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 48;
  }

  private atBottom = true;
  private listEl(): HTMLDivElement | null {
    return this.listRef()?.nativeElement ?? null;
  }

  private scrollToBottom(force: boolean): void {
    if (!force && !this.atBottom) return;
    setTimeout(() => {
      const el = this.listEl();
      if (el) el.scrollTop = el.scrollHeight;
    }, 0);
  }

  private patchTurn(id: string, patch: Partial<AssistantTurn>): void {
    this.turns.update((list) => list.map((t) => (t.id === id ? { ...t, ...patch } : t)));
  }

  private nextId(prefix: string): string {
    this.seq += 1;
    return `${prefix}${this.seq}`;
  }

  brief(text: string): string {
    const one = text.replace(/\s+/g, ' ').trim();
    return one.length > 16 ? `${one.slice(0, 16)}…` : one;
  }

  /** 清空会话（切白板时由父组件调用，避免把上一块白板的历史带过来） */
  reset(): void {
    this.turns.set([]);
    this.draft.set('');
    this.busy.set(false);
  }
}
