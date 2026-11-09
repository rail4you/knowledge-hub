import { Injectable, inject, NgZone } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { WhiteboardService } from './whiteboard.service';

/** 在线成员 */
export interface PresenceUser {
  userId: string;
  userName: string;
  color: string;
  online?: boolean;
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

/**
 * 教研白板实时协同（SignalR）。
 *
 * <p>一个白板 = 一个群组。进入房间时 Join 拿到「在线成员 + 画布全量」，
 * 之后只走增量：元素 upsert / delete、光标、成员上下线。</p>
 *
 * <p><b>为什么本地监听用 Subject 而不是直接改组件状态</b>：Hub 回调在 Angular zone 外，
 * 统一在 zone.run 里 next()，组件侧只订阅、不用关心变更检测。</p>
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

  get connected(): boolean {
    return this.conn?.state === HubConnectionState.Connected;
  }

  /**
   * 连接并加入白板。
   * @returns Join 结果（在线成员 + 画布全量元素）
   */
  async join(whiteboardId: string): Promise<{
    myColor: string;
    myName: string;
    onlineMembers: PresenceUser[];
    elements: { key: string; elementType: number; json: string }[];
  }> {
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
      this.zone.run(() => this.presenceLeave$.next(d));
    });

    this.conn.onreconnected(() => this.zone.run(() => this.reconnected$.next()));

    await this.conn.start();
    return await this.conn.invoke('Join', whiteboardId);
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

  async disconnect(): Promise<void> {
    if (!this.conn) return;
    try {
      await this.conn.stop();
    } catch {
      /* ignore */
    }
    this.conn = null;
  }
}
