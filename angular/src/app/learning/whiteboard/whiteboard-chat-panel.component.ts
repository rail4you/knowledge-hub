import {
  Component, ChangeDetectionStrategy, inject, signal, computed, effect,
  input, output, ElementRef, viewChild, OnDestroy, untracked, NgZone,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Subscription } from 'rxjs';
import {
  WhiteboardRealtimeService, ChatMessage, VoiceSignalEvent, VoiceStateEvent,
} from './whiteboard-realtime.service';

/** 语音通话中一条对端连接（mesh 里每个参与者一条） */
interface PeerState {
  pc: RTCPeerConnection;
  audio: HTMLAudioElement | null;
  /** 远端描述还没设好时先缓存 candidate（否则 addIceCandidate 会抛错） */
  pendingCandidates: RTCIceCandidateInit[];
  remoteSet: boolean;
  analyser?: AnalyserNode;
  source?: MediaStreamAudioSourceNode;
  rafId?: number;
}

/**
 * 白板右侧协作侧边栏：**文字聊天 + 语音通话**（参考主流会议 / 协作软件的右侧面板）。
 *
 * <p><b>布局口径</b>（对齐 Google Meet / Teams / Discord）：</p>
 * <ol>
 * <li>顶部：语音条 —— 加入/挂断、麦克风开关、通话时长、在麦成员头像（说话高亮 + 静音角标）；</li>
 * <li>中部：消息流 —— 自己的消息靠右，系统提示（加入/离开语音）居中灰字；</li>
 * <li>底部：输入框 —— Enter 发送 / Shift+Enter 换行，带「正在输入」提示。</li>
 * </ol>
 *
 * <p><b>语音为什么是 WebRTC mesh 而不是服务端转发</b>：教研讨论通常 2~5 人，
 * 两两直连（每人对其他 N-1 人建一条 RTCPeerConnection）延迟最低、服务端零媒体带宽；
 * 人数再大才需要 SFU。信令（offer/answer/candidate）走已有的白板 SignalR Hub，
 * 不引入新服务，也不必改部署。</p>
 *
 * <p><b>谁发 offer</b>：后加入者向「已经在麦上的人」逐个发 offer（服务端 JoinVoice 返回这批人）。
 * 先到的人只应答，避免双方同时 offer 造成 glare。</p>
 *
 * <p><b>说话检测</b>：本地用 AudioContext AnalyserNode 算 RMS，不上报服务端 ——
 * 每端自己算，既省带宽也不会因为采样差异出现「别人看你绿、你自己不绿」。</p>
 */
@Component({
  selector: 'app-whiteboard-chat-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzIconModule, NzTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="collab" [class.is-collapsed]="collapsed() && !embedded()" [class.is-embedded]="embedded()">
      @if (!embedded()) {
      <header class="collab__head">
        <span class="collab__title">
          <span nz-icon nzType="comment"></span>
          协作
        </span>
        <button
          class="collab__collapse"
          nz-button
          nzType="text"
          nzSize="small"
          nz-tooltip
          [nzTooltipTitle]="collapsed() ? '展开协作面板' : '收起协作面板'"
          (click)="collapseToggle.emit()"
        >
          <span nz-icon [nzType]="collapsed() ? 'left' : 'right'"></span>
        </button>
      </header>
      }

      <!-- 语音条 -->
      <section class="voice" [class.is-live]="inVoice()">
        <div class="voice__head">
          <div class="voice__label">
            <span nz-icon [nzType]="inVoice() ? 'sound' : 'audio'"></span>
            <span>{{ inVoice() ? '语音通话中' : '语音通话' }}</span>
            @if (inVoice()) {
              <span class="voice__timer">{{ callDuration() }}</span>
            }
          </div>

          @if (inVoice()) {
            <div class="voice__ctrls">
              <button
                nz-button
                nzSize="small"
                nzShape="circle"
                nz-tooltip
                [nzTooltipTitle]="rt.selfMuted() ? '取消静音' : '静音'"
                [nzType]="rt.selfMuted() ? 'primary' : 'default'"
                (click)="toggleMic()"
              >
                <span nz-icon [nzType]="rt.selfMuted() ? 'audio-muted' : 'audio'"></span>
              </button>
              <button nz-button nzSize="small" nzDanger nz-tooltip nzTooltipTitle="挂断" (click)="stopVoice()">
                <span nz-icon nzType="disconnect"></span>
              </button>
            </div>
          } @else {
            <button nz-button nzType="primary" nzSize="small" [nzLoading]="joining()" (click)="startVoice()">
              <span nz-icon nzType="phone"></span>
              加入语音
            </button>
          }
        </div>

        @if (inVoice()) {
          <div class="voice__people">
            @for (p of rt.voicePeople(); track p.userId) {
              <span
                class="chip"
                [class.is-speaking]="speaking().has(p.userId) && !p.muted"
                [class.is-me]="p.userId === rt.me()?.userId"
                [title]="p.userName + (p.muted ? '（已静音）' : '')"
              >
                <i class="chip__avatar" [style.background]="p.color">{{ initial(p.userName) }}</i>
                <span class="chip__name">{{ p.userId === rt.me()?.userId ? '我' : p.userName }}</span>
                <span nz-icon class="chip__mic" [class.is-off]="p.muted"
                      [nzType]="p.muted ? 'audio-muted' : 'audio'"></span>
              </span>
            }
            @if (rt.voicePeople().length <= 1) {
              <span class="voice__waiting">等待其他教师加入语音…</span>
            }
          </div>
        } @else if (voiceBlocked()) {
          <p class="voice__hint" title="当前页面不是 HTTPS，浏览器禁用了麦克风">
            当前页面非 HTTPS（{{ hostName }}），麦克风不可用
          </p>
        } @else {
          <p class="voice__hint" title="麦克风未静音时双方可互相听到，声音只走浏览器点对点，不经过服务器">
            未静音时双方可互相听到 · 声音点对点直连
          </p>
        }
      </section>

      <!-- 消息流 -->
      <div class="msgs" #msgList (scroll)="onScroll()">
        @if (messages().length === 0) {
          <div class="msgs__empty">
            <span nz-icon nzType="message"></span>
            <p>还没有讨论记录，在下面说一句试试</p>
          </div>
        }

        @for (m of messages(); track m.id) {
          @if (m.kind === 1) {
            <div class="sys">{{ m.text }}</div>
          } @else {
            <div class="msg" [class.is-mine]="m.senderId === rt.me()?.userId">
              @if (m.senderId !== rt.me()?.userId) {
                <i class="msg__avatar" [style.background]="m.senderColor || colorOf(m.senderName)">
                  {{ initial(m.senderName) }}
                </i>
              }
              <div class="msg__body">
                <div class="msg__meta">
                  <span class="msg__who">{{ m.senderId === rt.me()?.userId ? '我' : m.senderName }}</span>
                  <span class="msg__time">{{ timeOf(m.creationTime) }}</span>
                  @if (m.pending) { <span class="msg__pending">发送中…</span> }
                </div>
                <div class="msg__bubble">{{ m.text }}</div>
              </div>
            </div>
          }
        }

        @if (typingText()) {
          <div class="typing">{{ typingText() }}</div>
        }
      </div>

      <!-- 输入 -->
      <div class="composer">
        <textarea
          nz-input
          class="composer__input"
          rows="1"
          placeholder="说点什么…（Enter 发送）"
          [ngModel]="draft()"
          (ngModelChange)="onDraft($event)"
          (keydown)="onKeydown($event)"
        ></textarea>
        <button
          nz-button
          nzType="primary"
          class="composer__send"
          [disabled]="!draft().trim() || !connected()"
          (click)="send()"
        >
          <span nz-icon nzType="send"></span>
        </button>
      </div>
    </aside>
  `,
  styles: [`
    :host { display: block; height: 100%; }

    .collab {
      display: flex; flex-direction: column;
      width: 320px; height: 100%;
      background: #fff; border-left: 1px solid #e0e8e5;
      transition: width .18s ease;
    }
    .collab.is-collapsed { width: 0; border-left: none; overflow: hidden; }
    .collab.is-embedded { width: 100%; height: 100%; border-left: none; }

    .collab__head {
      display: flex; align-items: center; gap: 8px;
      padding: 6px 8px 6px 14px; border-bottom: 1px solid #eef2f1;
      flex: 0 0 auto;
    }
    .collab__title {
      display: inline-flex; align-items: center; gap: 6px;
      font-size: 13px; font-weight: 650; color: #1f2430;
    }
    .collab__collapse { margin-left: auto; }

    /* ── 语音条 ── */
    .voice { flex: 0 0 auto; padding: 8px 14px; background: #f7faf9; border-bottom: 1px solid #eef2f1; }
    .voice.is-live { background: linear-gradient(180deg, #eefaf3, #f7fbf9); }
    .voice__head { display: flex; align-items: center; gap: 8px; }
    .voice__label { display: inline-flex; align-items: center; gap: 6px; font-size: 12.5px; font-weight: 600; color: #22333a; }
    .voice__timer {
      font-variant-numeric: tabular-nums; font-size: 11.5px; font-weight: 600;
      color: #0f7a5a; background: #dff5e8; border-radius: 999px; padding: 1px 8px;
    }
    .voice__ctrls { display: inline-flex; align-items: center; gap: 6px; margin-left: auto; }
    .voice__head > button { margin-left: auto; }
    .voice__people { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 7px; }
    .voice__waiting { font-size: 11.5px; color: #8a9aa0; align-self: center; }
    .voice__hint {
      margin: 6px 0 0; font-size: 11px; line-height: 1.4; color: #87979d;
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    }

    .chip {
      display: inline-flex; align-items: center; gap: 5px;
      padding: 2px 8px 2px 2px; border-radius: 999px;
      background: #fff; border: 1px solid #e3eae8; font-size: 11.5px; color: #3c4a50;
      transition: box-shadow .15s ease, border-color .15s ease;
    }
    .chip.is-me { border-color: #bfe3d3; background: #f4fbf7; }
    .chip.is-speaking { border-color: #18a058; box-shadow: 0 0 0 2px rgba(24,160,88,.18); }
    .chip__avatar {
      width: 20px; height: 20px; border-radius: 50%; color: #fff;
      display: grid; place-items: center; font-size: 10px; font-weight: 600; font-style: normal;
    }
    .chip__name { max-width: 84px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .chip__mic { font-size: 11px; color: #18a058; }
    .chip__mic.is-off { color: #c94f4f; }

    /* ── 消息流 ── */
    .msgs {
      flex: 1 1 auto; min-height: 0; overflow-y: auto;
      padding: 10px 12px; display: flex; flex-direction: column; gap: 9px;
      background: #fcfdfd;
    }
    /* 消息贴着输入框底部排列（对齐 Meet / Teams 的聊天面板）：
       用 ::before 撑开顶部留白，比 justify-content:flex-end 更稳 —— 后者在内容溢出时会切掉顶部。 */
    .msgs::before { content: ''; margin-top: auto; }
    /* 空状态贴着输入框上方（不居中悬空），避免一片大白里孤零零一个图标 */
    .msgs__empty { margin: 0 0 2px; text-align: center; color: #96a5aa; padding: 0 6px; }
    .msgs__empty span { font-size: 18px; }
    .msgs__empty p { margin: 6px 0 2px; font-size: 12px; color: #6d7d83; }
    .msgs__empty small { font-size: 10.5px; line-height: 1.5; }

    .sys { align-self: center; font-size: 11px; color: #93a3a8; text-align: center; padding: 1px 6px; }

    .msg { display: flex; gap: 8px; align-items: flex-start; max-width: 100%; }
    .msg.is-mine { flex-direction: row-reverse; }
    .msg__avatar {
      flex: 0 0 auto; width: 24px; height: 24px; border-radius: 50%; color: #fff;
      display: grid; place-items: center; font-size: 10.5px; font-weight: 600; font-style: normal;
    }
    .msg__body { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
    .msg.is-mine .msg__body { align-items: flex-end; }
    .msg__meta { display: flex; align-items: center; gap: 6px; font-size: 10.5px; color: #96a5aa; }
    .msg__who { color: #6d7d83; font-weight: 600; }
    .msg__pending { color: #c2a24a; }
    .msg__bubble {
      padding: 7px 11px; border-radius: 10px 10px 10px 3px;
      background: #fff; border: 1px solid #e7edeb;
      font-size: 13px; line-height: 1.6; color: #22333a;
      white-space: pre-wrap; word-break: break-word; max-width: 232px;
    }
    .msg.is-mine .msg__bubble {
      background: #e9f6f1; border-color: #cfe9dd; border-radius: 10px 10px 3px 10px;
    }

    .typing { font-size: 11px; color: #96a5aa; padding-left: 2px; }

    /* ── 输入 ── */
    .composer {
      flex: 0 0 auto; display: flex; align-items: flex-end; gap: 6px;
      padding: 8px 10px 10px; border-top: 1px solid #eef2f1; background: #fff;
    }
    .composer__input {
      flex: 1 1 auto; resize: none; max-height: 88px; min-height: 32px; height: 32px;
      padding: 5px 10px; border-radius: 9px; font-size: 13px; line-height: 1.5;
    }
    .composer__send { flex: 0 0 auto; height: 32px; width: 34px; padding: 0; }
  `],
})
export class WhiteboardChatPanelComponent implements OnDestroy {
  readonly rt = inject(WhiteboardRealtimeService);
  private readonly message = inject(NzMessageService);
  private readonly zone = inject(NgZone);
  private readonly listRef = viewChild<ElementRef<HTMLDivElement>>('msgList');

  /** 侧边栏是否收起（由房间页控制，收起后仍保持连接与通话） */
  readonly collapsed = input(false);
  /**
   * 嵌入模式：由房间页的「协作 / AI 助手」侧边栏外壳托管，
   * 自带标题栏（含收起按钮）隐藏，宽度交给外壳。
   */
  readonly embedded = input(false);
  readonly collapseToggle = output<void>();
  /** 未读数变化（房间页在标题栏显示小红点） */
  readonly unreadChange = output<number>();

  /** 消息列表（含本地乐观插入的 pending 条目） */
  readonly messages = signal<(ChatMessage & { pending?: boolean })[]>([]);
  readonly draft = signal('');
  readonly joining = signal(false);
  readonly speaking = signal<ReadonlySet<string>>(new Set());
  readonly callDuration = signal('00:00');
  readonly typingText = signal('');

  readonly connected = computed(() => this.rt.boardId() !== null);
  readonly inVoice = computed(() => this.rt.selfInVoice());
  readonly hostName = typeof location !== 'undefined' ? location.host : '';
  /** 非安全上下文（http + 非 localhost）浏览器会拒绝 getUserMedia */
  readonly voiceBlocked = computed(() => {
    if (typeof location === 'undefined') return false;
    const host = location.hostname;
    return location.protocol !== 'https:' && host !== 'localhost' && host !== '127.0.0.1';
  });

  private unread = 0;
  private atBottom = true;
  private subs: Subscription[] = [];

  // ── 语音运行时（不进信号：属于 DOM/WebRTC 资源） ──
  private localStream: MediaStream | null = null;
  private audioCtx: AudioContext | null = null;
  private sink?: HTMLDivElement;
  private peers = new Map<string, PeerState>();
  /** offer 之前先到的 candidate（极少见，但会有） */
  private earlyCandidates = new Map<string, RTCIceCandidateInit[]>();
  private timerId?: number;
  private callStartedAt = 0;

  private readonly typingTimers = new Map<string, number>();
  private lastTypingSent = 0;
  private lastSeenIds = new Set<string>();

  constructor() {
    this.ensureSubs();

    // 历史回放（join 时一次性下发）：与实时消息按 id 合并去重
    effect(() => {
      const seed = this.rt.chatHistory();
      if (!seed.length) return;
      untracked(() => this.mergeMessages(seed, false));
    });

    // 收起 → 展开时清未读并滚到底部（主流软件行为）
    effect(() => {
      const isCollapsed = this.collapsed();
      untracked(() => {
        if (isCollapsed) return;
        this.setUnread(0);
        setTimeout(() => this.scrollToBottom(true), 0);
      });
    });
  }

  /** 远端音频的隐藏容器（DOM 节点不进模板，避免多路 <audio> 干扰界面结构） */
  private ensureSink(): HTMLDivElement {
    if (!this.sink) {
      this.sink = document.createElement('div');
      this.sink.style.display = 'none';
      document.body.appendChild(this.sink);
    }
    return this.sink;
  }

  private ensureSubs(): void {
    if (this.subs.length) return;

    this.subs.push(
      this.rt.chatMessage$.subscribe((m) => this.mergeMessages([m], true)),
      this.rt.chatTyping$.subscribe((d) => this.onTyping(d.userId, d.userName)),
      this.rt.voiceState$.subscribe((d) => this.onVoiceState(d)),
      this.rt.voiceSignal$.subscribe((d) => void this.onVoiceSignal(d)),
      this.rt.presenceLeave$.subscribe((d) => this.teardownPeer(d.userId)),
    );
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
    this.subs = [];
    void this.stopVoice();
    this.typingTimers.forEach((id) => clearTimeout(id));
    this.sink?.remove();
    void this.audioCtx?.close().catch(() => undefined);
    this.audioCtx = null;
  }

  // ── 消息 ───────────────────────────────────────────────────

  onDraft(v: string): void {
    this.draft.set(v);
    this.pingTyping();
  }

  onKeydown(ev: KeyboardEvent): void {
    if (ev.key !== 'Enter' || ev.shiftKey) return;
    ev.preventDefault();
    this.send();
  }

  send(): void {
    const text = this.draft().trim();
    if (!text) return;

    const clientId = `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 8)}`;
    // 乐观插入：本地立刻出现，服务端返回同 clientId 的落库版本后替换
    const pending: ChatMessage & { pending?: boolean } = {
      id: `local-${clientId}`,
      clientId,
      senderId: this.rt.me()?.userId || '',
      senderName: this.rt.me()?.name || '我',
      senderColor: this.rt.me()?.color,
      text,
      kind: 0,
      creationTime: new Date().toISOString(),
      pending: true,
    };
    this.messages.update((list) => [...list, pending]);
    this.draft.set('');
    this.scrollToBottom(true);

    void this.rt.sendChat(text, clientId).then((saved) => {
      if (!saved) {
        this.messages.update((list) => list.filter((m) => m.clientId !== clientId));
        this.message.error('消息发送失败，请检查网络后重试');
        return;
      }
      this.mergeMessages([saved], false);
    });
  }

  /** 合并消息：按 id / clientId 去重，并按时间排序（历史 + 实时 + 乐观插入混在一起） */
  private mergeMessages(incoming: ChatMessage[], fromRealtime: boolean): void {
    const list = [...this.messages()];
    let added = 0;

    for (const m of incoming) {
      const idx = list.findIndex(
        (x) => x.id === m.id || (!!m.clientId && x.clientId === m.clientId) || (!!x.clientId && x.clientId === m.clientId),
      );
      if (idx >= 0) list[idx] = { ...m };
      else {
        list.push({ ...m });
        if (!this.lastSeenIds.has(m.id)) added++;
      }
      this.lastSeenIds.add(m.id);
    }

    this.messages.set(list);

    if (!added) return;

    const mine = incoming.some((m) => m.senderId === this.rt.me()?.userId);
    const shouldNotify = fromRealtime && !mine;
    const stick = mine || this.atBottom || this.collapsed();
    setTimeout(() => this.scrollToBottom(stick || mine), 0);

    if (shouldNotify && (this.collapsed() || !this.atBottom)) {
      this.setUnread(this.unread + added);
    }
  }

  onScroll(): void {
    const el = this.listRef()?.nativeElement;
    if (!el) return;
    this.atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 48;
    if (this.atBottom && this.unread > 0) this.setUnread(0);
  }

  private scrollToBottom(force: boolean): void {
    const el = this.listRef()?.nativeElement;
    if (!el) return;
    if (force || this.atBottom) el.scrollTop = el.scrollHeight;
  }

  private setUnread(n: number): void {
    this.unread = n;
    this.unreadChange.emit(n);
  }

  private pingTyping(): void {
    const now = Date.now();
    if (now - this.lastTypingSent < 2000) return;
    this.lastTypingSent = now;
    void this.rt.typing();
  }

  private onTyping(userId: string, userName: string): void {
    if (userId === this.rt.me()?.userId) return;
    this.typingNames.set(userId, userName);

    const old = this.typingTimers.get(userId);
    if (old) clearTimeout(old);
    this.typingTimers.set(
      userId,
      window.setTimeout(() => {
        this.typingTimers.delete(userId);
        this.typingNames.delete(userId);
        this.refreshTypingText();
      }, 3200),
    );
    this.refreshTypingText();
  }

  private typingNames = new Map<string, string>();

  private refreshTypingText(): void {
    const names = [...this.typingTimers.keys()]
      .map((id) => this.typingNames.get(id) || '有人')
      .filter(Boolean);
    this.typingText.set(names.length ? `${names.join('、')} 正在输入…` : '');
  }

  // ── 语音：加入 / 离开 / 静音 ────────────────────────────────

  async startVoice(): Promise<void> {
    if (this.joining() || this.inVoice()) return;
    if (!this.connected()) {
      this.message.warning('协同连接尚未建立，请稍后再试');
      return;
    }
    if (this.voiceBlocked()) {
      this.message.warning('当前页面不是 HTTPS，浏览器禁用了麦克风；请用 https 域名或 localhost 打开');
      return;
    }
    if (!navigator.mediaDevices?.getUserMedia) {
      this.message.warning('当前浏览器不支持麦克风采集');
      return;
    }

    this.joining.set(true);
    try {
      this.localStream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
        video: false,
      });

      this.ensureAudioContext();
      const meId = this.rt.me()?.userId;
      if (meId && this.localStream) this.monitorSpeaking(meId, this.localStream, true);

      // 后加入者负责发 offer：先拿「已经在麦上的人」
      const others = await this.rt.joinVoice();
      this.callStartedAt = Date.now();
      this.startTimer();

      for (const p of others) await this.makeOffer(p.userId);
      if (!others.length) this.message.info('已进入语音，等待其他教师加入后自动接通');
    } catch (err: any) {
      this.message.error(this.explainMicError(err));
      this.releaseLocalAudio();
      await this.rt.leaveVoice();
    } finally {
      this.joining.set(false);
    }
  }

  async stopVoice(): Promise<void> {
    this.stopTimer();
    this.releaseLocalAudio();
    for (const id of [...this.peers.keys()]) this.teardownPeer(id);
    if (this.rt.selfInVoice()) await this.rt.leaveVoice();
    this.callDuration.set('00:00');
  }

  /** 麦克风开关：只切换本地音轨 enabled，不重连、不影响其他人 */
  async toggleMic(): Promise<void> {
    const muted = !this.rt.selfMuted();
    this.localStream?.getAudioTracks().forEach((t) => (t.enabled = !muted));
    await this.rt.setMic(muted);
    if (muted) this.setSpeaking(this.rt.me()?.userId || '', false);
  }

  private explainMicError(err: any): string {
    const name = err?.name || '';
    if (name === 'NotAllowedError') return '麦克风权限被拒绝，请在地址栏允许麦克风后重试';
    if (name === 'NotFoundError') return '没有检测到麦克风设备';
    if (name === 'NotReadableError') return '麦克风被其他程序占用';
    return '无法开启麦克风，请检查设备与权限';
  }

  private releaseLocalAudio(): void {
    this.localStream?.getTracks().forEach((t) => t.stop());
    this.localStream = null;
    const meId = this.rt.me()?.userId;
    if (meId) {
      this.stopMonitor(meId);
      this.setSpeaking(meId, false);
    }
  }

  // ── WebRTC mesh ────────────────────────────────────────────

  private iceServers(): RTCIceServer[] {
    // 公网 STUN 只为拿到 srflx 候选；同校 / 同局域网时 host 候选直连即可。
    return [
      { urls: ['stun:stun.l.google.com:19302', 'stun:stun1.l.google.com:19302'] },
    ];
  }

  private createPeer(userId: string): PeerState {
    this.teardownPeer(userId);

    const pc = new RTCPeerConnection({ iceServers: this.iceServers() });
    const state: PeerState = {
      pc,
      audio: null,
      pendingCandidates: this.earlyCandidates.get(userId) || [],
      remoteSet: false,
    };
    this.earlyCandidates.delete(userId);
    this.peers.set(userId, state);

    if (this.localStream) {
      for (const track of this.localStream.getAudioTracks()) pc.addTrack(track, this.localStream);
    }

    pc.onicecandidate = (ev) => {
      if (ev.candidate) void this.rt.voiceSignal(userId, 'candidate', JSON.stringify(ev.candidate));
    };

    pc.ontrack = (ev) => {
      const stream = ev.streams[0] ?? new MediaStream([ev.track]);
      this.attachRemoteAudio(userId, stream);
    };

    pc.onconnectionstatechange = () => {
      if (pc.connectionState === 'connected') this.flushCandidates(userId);
      if (pc.connectionState === 'failed' || pc.connectionState === 'closed') this.teardownPeer(userId);
    };

    return state;
  }

  private async makeOffer(userId: string): Promise<void> {
    try {
      const st = this.createPeer(userId);
      const offer = await st.pc.createOffer();
      await st.pc.setLocalDescription(offer);
      await this.rt.voiceSignal(userId, 'offer', JSON.stringify(offer));
    } catch {
      this.teardownPeer(userId);
    }
  }

  private async onVoiceSignal(d: VoiceSignalEvent): Promise<void> {
    if (!this.rt.selfInVoice()) return;
    if (d.fromUserId === this.rt.me()?.userId) return;

    try {
      if (d.type === 'offer') {
        const st = this.createPeer(d.fromUserId);
        await st.pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(d.payload)));
        st.remoteSet = true;
        await this.flushCandidates(d.fromUserId);
        const answer = await st.pc.createAnswer();
        await st.pc.setLocalDescription(answer);
        await this.rt.voiceSignal(d.fromUserId, 'answer', JSON.stringify(answer));
        return;
      }

      const st = this.peers.get(d.fromUserId);
      if (!st) return;

      if (d.type === 'answer') {
        await st.pc.setRemoteDescription(new RTCSessionDescription(JSON.parse(d.payload)));
        st.remoteSet = true;
        await this.flushCandidates(d.fromUserId);
        return;
      }

      // candidate：远端描述未就绪时先缓存
      const candidate = JSON.parse(d.payload) as RTCIceCandidateInit;
      if (!st.remoteSet) st.pendingCandidates.push(candidate);
      else await st.pc.addIceCandidate(candidate).catch(() => undefined);
    } catch {
      /* 单次信令失败不致命：ICE 会重试 / 对端超时后重连 */
    }
  }

  private async flushCandidates(userId: string): Promise<void> {
    const st = this.peers.get(userId);
    if (!st || !st.remoteSet || !st.pendingCandidates.length) return;
    const list = [...st.pendingCandidates];
    st.pendingCandidates = [];
    for (const c of list) await st.pc.addIceCandidate(c).catch(() => undefined);
  }

  private onVoiceState(d: VoiceStateEvent): void {
    if (d.userId === this.rt.me()?.userId) return;
    if (!d.inVoice) {
      this.teardownPeer(d.userId);
      this.message.info(`${d.userName} 离开了语音通话`);
    }
  }

  /** 远端音频挂到隐藏的 <audio> 上（autoplay 允许：加入通话是用户手势） */
  private attachRemoteAudio(userId: string, stream: MediaStream): void {
    const st = this.peers.get(userId);
    if (!st) return;
    if (!st.audio) {
      const audio = document.createElement('audio');
      audio.autoplay = true;
      audio.setAttribute('playsinline', 'true');
      this.ensureSink().appendChild(audio);
      st.audio = audio;
    }
    st.audio.srcObject = stream;
    void st.audio.play().catch(() => undefined);
    this.monitorSpeaking(userId, stream, false);
  }

  private teardownPeer(userId: string): void {
    const st = this.peers.get(userId);
    if (!st) return;
    this.peers.delete(userId);

    if (st.rafId) cancelAnimationFrame(st.rafId);
    try { st.source?.disconnect(); } catch { /* ignore */ }
    if (st.audio) {
      st.audio.srcObject = null;
      st.audio.remove();
      st.audio = null;
    }
    try { st.pc.close(); } catch { /* ignore */ }

    this.setSpeaking(userId, false);
  }

  // ── 说话检测（本地 RMS，不上报服务端） ─────────────────────

  private ensureAudioContext(): void {
    if (this.audioCtx) return;
    const Ctor = window.AudioContext || (window as any).webkitAudioContext;
    if (!Ctor) return;
    this.audioCtx = new Ctor();
  }

  private monitorSpeaking(userId: string, stream: MediaStream, isSelf: boolean): void {
    const st = this.peers.get(userId);
    if (isSelf) {
      if (!this.audioCtx) return;
      // 自己的音轨用独立 analyser（不进 peers，否则 teardown 会误删）
      this.stopMonitor(userId);
      const src = this.audioCtx.createMediaStreamSource(stream);
      const analyser = this.audioCtx.createAnalyser();
      analyser.fftSize = 512;
      src.connect(analyser);
      this.selfMonitor = { src, analyser, rafId: 0 };
      this.loopLevel(userId, analyser, true);
      return;
    }

    if (!st || !this.audioCtx) return;
    if (st.analyser) return;

    const src = this.audioCtx.createMediaStreamSource(stream);
    const analyser = this.audioCtx.createAnalyser();
    analyser.fftSize = 512;
    src.connect(analyser);
    st.source = src;
    st.analyser = analyser;
    this.loopLevel(userId, analyser, false);
  }

  private selfMonitor?: { src: MediaStreamAudioSourceNode; analyser: AnalyserNode; rafId: number };

  private loopLevel(userId: string, analyser: AnalyserNode, isSelf: boolean): void {
    const buf = new Uint8Array(analyser.fftSize);
    const tick = () => {
      analyser.getByteTimeDomainData(buf);
      let sum = 0;
      for (let i = 0; i < buf.length; i++) {
        const v = (buf[i] - 128) / 128;
        sum += v * v;
      }
      const rms = Math.sqrt(sum / buf.length);
      const muted = isSelf ? this.rt.selfMuted() : false;
      this.setSpeaking(userId, !muted && rms > 0.045);

      const rafId = requestAnimationFrame(tick);
      if (isSelf && this.selfMonitor) this.selfMonitor.rafId = rafId;
      else {
        const st = this.peers.get(userId);
        if (st) st.rafId = rafId;
      }
    };
    const id = requestAnimationFrame(tick);
    if (isSelf && this.selfMonitor) this.selfMonitor.rafId = id;
    else {
      const st = this.peers.get(userId);
      if (st) st.rafId = id;
    }
  }

  private stopMonitor(userId: string): void {
    const target = userId === this.rt.me()?.userId ? this.selfMonitor : undefined;
    if (!target) return;
    if (target.rafId) cancelAnimationFrame(target.rafId);
    try { target.src.disconnect(); } catch { /* ignore */ }
    this.selfMonitor = undefined;
  }

  private setSpeaking(userId: string, speaking: boolean): void {
    if (!userId) return;
    const current = this.speaking();
    if (current.has(userId) === speaking) return;
    this.zone.run(() => {
      const next = new Set(current);
      if (speaking) next.add(userId);
      else next.delete(userId);
      this.speaking.set(next);
    });
  }

  // ── 通话计时 ───────────────────────────────────────────────

  private startTimer(): void {
    this.stopTimer();
    this.timerId = window.setInterval(() => {
      const sec = Math.floor((Date.now() - this.callStartedAt) / 1000);
      const mm = Math.floor(sec / 60).toString().padStart(2, '0');
      const ss = (sec % 60).toString().padStart(2, '0');
      this.callDuration.set(`${mm}:${ss}`);
    }, 1000);
  }

  private stopTimer(): void {
    if (this.timerId) clearInterval(this.timerId);
    this.timerId = undefined;
  }

  // ── 小工具 ─────────────────────────────────────────────────

  initial(name: string): string {
    return (name || '师').trim().slice(0, 1);
  }

  colorOf(name: string): string {
    const palette = ['#2f6fed', '#e0662f', '#18a058', '#a24bd6', '#d6a21e'];
    let h = 0;
    for (let i = 0; i < (name || '').length; i++) h = (h * 31 + name.charCodeAt(i)) % 997;
    return palette[h % palette.length];
  }

  timeOf(iso: string): string {
    if (!iso) return '';
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '';
    const today = new Date();
    const sameDay = d.toDateString() === today.toDateString();
    const hm = `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}`;
    return sameDay ? hm : `${d.getMonth() + 1}/${d.getDate()} ${hm}`;
  }
}
