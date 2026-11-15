import { Injectable, inject, NgZone, signal, computed } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { WhiteboardService } from './whiteboard.service';

/** 在线成员 */
export interface PresenceUser {
  userId: string;
  userName: string;
  color: string;
  online?: boolean;
  /** 是否在语音通话中（Join 快照里带，PresenceJoin 里默认 false） */
  inVoice?: boolean;
  muted?: boolean;
}

/** 远端光标 */
export interface RemoteCursor {
  userId: string;
  name: string;
  color: string;
  x: number;
  y: number;
}

/** 元素增量（upsert / delete） */
export interface ElementDelta {
  key: string;
  elementType?: number;
  json?: string;
  editorName?: string;
}

/** 协作消息（右侧聊天侧边栏） */
export interface ChatMessage {
  id: string;
  /** 前端临时 Id：用于把本地乐观气泡替换成落库版本 */
  clientId?: string | null;
  senderId: string;
  senderName: string;
  senderColor?: string | null;
  text: string;
  /** 0 = 普通发言，1 = 系统提示 */
  kind: number;
  creationTime: string;
}

/** 语音通话状态变更 */
export interface VoiceStateEvent {
  userId: string;
  userName: string;
  inVoice: boolean;
  muted: boolean;
}

/** WebRTC 信令 */
export interface VoiceSignalEvent {
  fromUserId: string;
  fromUserName: string;
  type: 'offer' | 'answer' | 'candidate';
  payload: string;
}

/** 语音通话参与者（JoinVoice 返回：已经在麦上的人） */
export interface VoiceParticipant {
  userId: string;
  userName: string;
}

/** 花名册里的一条（在线成员 + 语音状态） */
export interface VoicePerson {
  userId: string;
  userName: string;
  color: string;
  muted: boolean;
}

/** Join 结果 */
export interface JoinOutcome {
  myUserId: string;
  myColor: string;
  myName: string;
  onlineMembers: PresenceUser[];
  elements: { key: string; elementType: number; json: string }[];
  chatHistory?: ChatMessage[];
}

/**
 * 智能白板实时协同（SignalR）。
 *
 * <p>一个白板 = 一个群组。进入房间时 Join 拿到「在线成员 + 画布全量 + 聊天历史」，
 * 之后只走增量：元素 upsert / delete、光标、成员上下线、聊天消息、语音状态与信令。</p>
 *
 * <p><b>为什么本地监听用 Subject 而不是直接改组件状态</b>：Hub 回调在 Angular zone 外，
 * 统一在 zone.run 里 next()，组件侧只订阅、不用关心变更检测。</p>
 *
 * <p><b>会话级状态放这里的理由</b>：聊天窗口会折叠/展开，语音通话更是必须跨「侧边栏收起」
 * 存活；把「当前白板 / 我是谁 / 谁在麦上 / 我在不在麦上」放到 root 级单例里，
 * 组件只订阅，避免每次重建都要靠入参把上下文再传一遍。</p>
 */
@Injectable({ providedIn: 'root' })
export class WhiteboardRealtimeService {
  private readonly zone = inject(NgZone);
  private readonly api = inject(WhiteboardService);

  private conn: HubConnection | null = null;

  readonly elementUpserted$ = new Subject<ElementDelta>();
  readonly elementDeleted$ = new Subject<{ key: string }>();
  readonly cursorMoved$ = new Subject<RemoteCursor>();
  readonly presenceJoin$ = new Subject<PresenceUser>();
  readonly presenceLeave$ = new Subject<{ userId: string; userName: string }>();
  readonly reconnected$ = new Subject<void>();

  // 协作侧边栏：文字聊天 + 语音信令
  readonly chatMessage$ = new Subject<ChatMessage>();
  readonly chatTyping$ = new Subject<{ userId: string; userName: string }>();
  readonly voiceState$ = new Subject<VoiceStateEvent>();
  readonly voiceSignal$ = new Subject<VoiceSignalEvent>();

  // ── 会话级状态（信号）──────────────────────────────────────
  /** 当前白板 Id（join 后写入，聊天/语音方法都不再需要调用方传） */
  readonly boardId = signal<string | null>(null);
  /** 我自己的身份与配色 */
  readonly me = signal<{ userId: string; name: string; color: string } | null>(null);
  /** 进入白板时下发的聊天历史（侧边栏初始化用，之后靠 chatMessage$ 增量） */
  readonly chatHistory = signal<ChatMessage[]>([]);
  /** 语音花名册：在麦上的人（含静音标记） */
  readonly voicePeople = signal<VoicePerson[]>([]);
  /** 我是否在语音通话中 */
  readonly selfInVoice = signal(false);
  /** 我是否静音 */
  readonly selfMuted = signal(false);
  readonly voiceCount = computed(() => this.voicePeople().length);

  get connected(): boolean {
    return this.conn?.state === HubConnectionState.Connected;
  }

  /**
   * 连接并加入白板。
   * @returns Join 结果（在线成员 + 画布全量元素 + 聊天历史）
   */
  async join(whiteboardId: string): Promise<JoinOutcome> {
    await this.disconnect();

    const token = this.api.accessToken();
    const url = `/signalr/whiteboards${token ? `?access_token=${encodeURIComponent(token)}` : ''}`;

    this.conn = new HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    // 注意：SignalR 客户端要求 handler 不返回值，否则会报
    // "Result given for 'xxx' method but server is not expecting a result"。
    this.conn.on('ElementUpserted', (d: ElementDelta) => {
      this.zone.run(() => this.elementUpserted$.next(d));
    });
    this.conn.on('ElementDeleted', (d: { key: string }) => {
      this.zone.run(() => this.elementDeleted$.next(d));
    });
    this.conn.on('CursorMoved', (d: RemoteCursor) => {
      this.zone.run(() => this.cursorMoved$.next(d));
    });
    this.conn.on('PresenceJoin', (d: PresenceUser) => {
      this.zone.run(() => this.presenceJoin$.next(d));
    });
    this.conn.on('PresenceLeave', (d: { userId: string; userName: string }) => {
      this.zone.run(() => {
        // 掉线的人一定不在麦上了，花名册同步移除
        this.voicePeople.update((list) => list.filter((p) => p.userId !== d.userId));
        this.presenceLeave$.next(d);
      });
    });

    this.conn.on('ChatMessage', (d: ChatMessage) => {
      this.zone.run(() => this.chatMessage$.next(d));
    });
    this.conn.on('ChatTyping', (d: { userId: string; userName: string }) => {
      this.zone.run(() => this.chatTyping$.next(d));
    });
    this.conn.on('VoiceStateChanged', (d: VoiceStateEvent) => {
      this.zone.run(() => {
        this.applyVoiceState(d);
        this.voiceState$.next(d);
      });
    });
    this.conn.on('VoiceSignal', (d: VoiceSignalEvent) => {
      this.zone.run(() => this.voiceSignal$.next(d));
    });

    this.conn.onreconnected(() => this.zone.run(() => this.reconnected$.next()));

    await this.conn.start();
    const joined = (await this.conn.invoke('Join', whiteboardId)) as JoinOutcome;

    this.boardId.set(whiteboardId);
    this.me.set({ userId: joined.myUserId, name: joined.myName, color: joined.myColor });
    this.voicePeople.set(
      (joined.onlineMembers || [])
        .filter((m) => m.inVoice)
        .map((m) => ({ userId: m.userId, userName: m.userName, color: m.color, muted: !!m.muted })),
    );
    this.selfInVoice.set(false);
    this.selfMuted.set(false);
    this.chatHistory.set(joined.chatHistory || []);

    return joined;
  }

  private applyVoiceState(d: VoiceStateEvent): void {
    if (!d.inVoice) {
      this.voicePeople.update((list) => list.filter((p) => p.userId !== d.userId));
      if (d.userId === this.me()?.userId) this.selfInVoice.set(false);
      return;
    }

    this.voicePeople.update((list) => {
      const color = list.find((p) => p.userId === d.userId)?.color || '#2f6fed';
      const next = list.filter((p) => p.userId !== d.userId);
      next.push({ userId: d.userId, userName: d.userName, color, muted: d.muted });
      return next;
    });

    if (d.userId === this.me()?.userId) {
      this.selfInVoice.set(true);
      this.selfMuted.set(d.muted);
    }
  }

  /** 元素 upsert（新增或更新同一 key） */
  async upsertElement(whiteboardId: string, key: string, elementType: number, el: unknown): Promise<void> {
    if (!this.connected) return;
    await this.conn!.invoke('UpsertElement', whiteboardId, key, elementType, JSON.stringify(el));
  }

  async deleteElement(whiteboardId: string, key: string): Promise<void> {
    if (!this.connected) return;
    await this.conn!.invoke('DeleteElement', whiteboardId, key);
  }

  /** 光标（高频，节流由调用方控制） */
  async cursor(whiteboardId: string, x: number, y: number): Promise<void> {
    if (!this.connected) return;
    try {
      await this.conn!.invoke('Cursor', whiteboardId, x, y);
    } catch {
      /* 光标丢了不影响正确性，静默 */
    }
  }

  async viewport(whiteboardId: string, x: number, y: number, scale: number): Promise<void> {
    if (!this.connected) return;
    try {
      await this.conn!.invoke('Viewport', whiteboardId, x, y, scale);
    } catch {
      /* ignore */
    }
  }

  // ── 文字聊天 ────────────────────────────────────────────────

  /**
   * 发送消息。
   *
   * <p>服务端会同时广播（含自己，多标签页同步）并返回落库版本，
   * 前端按 id / clientId 去重，不会出现重复气泡。</p>
   */
  async sendChat(text: string, clientId: string): Promise<ChatMessage | null> {
    const boardId = this.boardId();
    if (!this.connected || !boardId) return null;
    try {
      return (await this.conn!.invoke('SendChatMessage', boardId, text, clientId)) as ChatMessage;
    } catch {
      return null;
    }
  }

  /** 「正在输入」提示（组件侧节流） */
  async typing(): Promise<void> {
    const boardId = this.boardId();
    if (!this.connected || !boardId) return;
    try {
      await this.conn!.invoke('Typing', boardId);
    } catch {
      /* 提示类消息丢了不影响正确性 */
    }
  }

  // ── 语音（WebRTC 信令）───────────────────────────────────────

  /** 加入语音：返回已经在麦上的人，由调用方向他们逐个发 offer */
  async joinVoice(): Promise<VoiceParticipant[]> {
    const boardId = this.boardId();
    if (!this.connected || !boardId) return [];
    const others = (await this.conn!.invoke('JoinVoice', boardId)) as VoiceParticipant[];
    const me = this.me();
    if (me) {
      this.applyVoiceState({ userId: me.userId, userName: me.name, inVoice: true, muted: false });
    }
    return others;
  }

  async leaveVoice(): Promise<void> {
    const boardId = this.boardId();
    const me = this.me();
    if (me) {
      this.applyVoiceState({ userId: me.userId, userName: me.name, inVoice: false, muted: false });
    }
    if (!this.connected || !boardId) return;
    try {
      await this.conn!.invoke('LeaveVoice', boardId);
    } catch {
      /* ignore */
    }
  }

  async setMic(muted: boolean): Promise<void> {
    const boardId = this.boardId();
    this.selfMuted.set(muted);
    const me = this.me();
    if (me) this.applyVoiceState({ userId: me.userId, userName: me.name, inVoice: true, muted });
    if (!this.connected || !boardId) return;
    try {
      await this.conn!.invoke('SetMic', boardId, muted);
    } catch {
      /* ignore */
    }
  }

  async voiceSignal(
    targetUserId: string,
    type: 'offer' | 'answer' | 'candidate',
    payload: string,
  ): Promise<void> {
    const boardId = this.boardId();
    if (!this.connected || !boardId) return;
    try {
      await this.conn!.invoke('VoiceSignal', boardId, targetUserId, type, payload);
    } catch {
      /* 信令失败由 ICE 超时兜底 */
    }
  }

  async disconnect(): Promise<void> {
    this.boardId.set(null);
    this.voicePeople.set([]);
    this.chatHistory.set([]);
    this.selfInVoice.set(false);
    this.selfMuted.set(false);

    if (!this.conn) return;
    try {
      await this.conn.stop();
    } catch {
      /* ignore */
    }
    this.conn = null;
  }
}
