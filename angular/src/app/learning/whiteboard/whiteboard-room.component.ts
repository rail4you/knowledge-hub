import {
  Component, ChangeDetectionStrategy, inject, signal, computed,
  OnInit, OnDestroy, ElementRef, ViewChild, NgZone,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSelectModule } from 'ng-zorro-antd/select';
import Konva from 'konva';
import { Subscription } from 'rxjs';
import {
  WhiteboardService, WhiteboardDetail, BoardElement, InvitableTeacher,
} from './whiteboard.service';
import { WhiteboardRealtimeService, PresenceUser } from './whiteboard-realtime.service';

/**
 * 教研白板画布房间（Konva）。
 *
 * <p>参照原型：拖动空白处平移、滚轮缩放、点击元素选中后可拖动/缩放/旋转；
 * 支持文本、便签、以及**从自己课程资源里选的图片与视频**。</p>
 *
 * <p><b>素材口径</b>：图片/视频只存 `resourceId`，不内联 base64 ——
 * 一是避免消息过大，二是让素材始终跟随资源中心的最新版本与权限。</p>
 *
 * <p><b>实时协同</b>：SignalR 群组，元素 upsert/delete 写库并广播；
 * 光标只广播。远端光标带教师姓名浮标，便于两人同时操作时区分。</p>
 */
@Component({
  selector: 'app-whiteboard-room',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule,
    NzButtonModule, NzIconModule, NzModalModule, NzInputModule,
    NzSpinModule, NzTagModule, NzTooltipModule, NzEmptyModule, NzSelectModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="room">
      <!-- 顶部：标题 + 在线成员 + 操作 -->
      <header class="room__head">
        <button nz-button nzType="text" (click)="back()" nz-tooltip nzTooltipTitle="返回白板列表">
          <span nz-icon nzType="arrow-left"></span>
        </button>
        <div class="room__title">
          <h2>{{ board()?.title || '加载中…' }}</h2>
          <p>
            @if (board()?.subject) { {{ board()!.subject }} · }
            创建人 {{ board()?.creatorName }}
            @if (board()?.topic) { · {{ board()!.topic }} }
          </p>
        </div>

        <div class="room__online">
          @for (m of onlineMembers(); track m.userId) {
            <span class="avatar" [style.background]="m.color" [title]="m.userName + '（在线）'">
              {{ initial(m.userName) }}
            </span>
          }
          <span class="room__conn" [class.is-off]="!connected()">
            <i></i>{{ connected() ? '实时协同中' : '连接中…' }}
          </span>
        </div>

        <div class="room__actions">
          @if (board()?.isOwner) {
            <button nz-button (click)="openInvite()">
              <span nz-icon nzType="user-add"></span> 邀请教师共创
            </button>
          }
          <span class="room__me" [style.borderColor]="myColor()">
            <i [style.background]="myColor()"></i>{{ myName() }}
          </span>
        </div>
      </header>

      <!-- 工具栏 -->
      <div class="room__tools">
        <button nz-button (click)="addText()" [disabled]="!canEdit()">
          <span nz-icon nzType="font-size"></span> 文本
        </button>
        <button nz-button (click)="addNote()" [disabled]="!canEdit()">
          <span nz-icon nzType="sticky"></span> 便签
        </button>
        <button nz-button (click)="openResourcePicker('image')" [disabled]="!canEdit()">
          <span nz-icon nzType="picture"></span> 从课程资源选图片
        </button>
        <button nz-button (click)="openResourcePicker('video')" [disabled]="!canEdit()">
          <span nz-icon nzType="play-circle"></span> 从课程资源选视频
        </button>
        <span class="room__hint">
          拖动空白处平移 · 滚轮缩放 · 点选元素后可拖动/缩放/旋转
        </span>
      </div>

      <!-- 画布 -->
      <div class="room__stage" #stageHost [class.is-locked]="!canEdit()">
        @if (loading()) {
          <div class="room__loading"><nz-spin nzSimple nzTip="正在加载画布…"></nz-spin></div>
        }
      </div>

      <!-- 右侧属性面板 -->
      @if (selected(); as sel) {
        <aside class="room__panel">
          <h4>{{ sel.type === 'note' ? '便签' : sel.type === 'text' ? '文本' : sel.type === 'image' ? '图片' : '视频' }}</h4>

          @if (sel.type === 'text' || sel.type === 'note') {
            <label>
              <span>内容</span>
              <textarea nz-input rows="4" [ngModel]="sel.text" (ngModelChange)="onTextInput($event)"></textarea>
            </label>
            <label>
              <span>字号 {{ sel.fs }}px</span>
              <input type="range" min="12" max="72" [ngModel]="sel.fs" (ngModelChange)="onFontInput($event)" />
            </label>
            @if (sel.by) { <p class="room__by">— {{ sel.by }}</p> }
          }

          @if (sel.type === 'image' || sel.type === 'video') {
            <p class="room__by">素材：{{ sel.resourceName || '课程资源' }}</p>
          }

          <button nz-button nzDanger style="width:100%;margin-top:8px" (click)="deleteSelected()">
            <span nz-icon nzType="delete"></span> 删除所选
          </button>
        </aside>
      }
    </div>

    <!-- 资源选择框 -->
    <nz-modal
      [(nzVisible)]="pickerOpen"
      [nzTitle]="pickerType === 'image' ? '从课程资源选择图片' : '从课程资源选择视频'"
      nzOkText="插入到白板"
      nzCancelText="取消"
      [nzWidth]="820"
      [nzOkDisabled]="!pickedResource"
      (nzOnOk)="insertPickedResource()"
      (nzOnCancel)="pickerOpen = false"
    >
      <ng-container *nzModalContent>
        <div class="picker">
          <div class="picker__filters">
            <nz-select
              style="width:240px"
              nzPlaceHolder="按课程筛选"
              nzAllowClear
              [(ngModel)]="pickerCourseId"
              (ngModelChange)="loadResources()"
            >
              @for (c of myCourses(); track c.id) {
                <nz-option [nzValue]="c.id" [nzLabel]="c.title"></nz-option>
              }
            </nz-select>
            <input nz-input placeholder="搜索资源名称" [(ngModel)]="pickerKeyword"
                   (keyup.enter)="loadResources()" style="width:240px" />
            <button nz-button (click)="loadResources()">查询</button>
          </div>

          @if (resourcesLoading()) {
            <div class="picker__loading"><nz-spin nzSimple></nz-spin></div>
          } @else if (resources().length === 0) {
            <nz-empty nzNotFoundContent="没有符合条件的素材（只列你自己上传的资源）"></nz-empty>
          } @else {
            <div class="picker__grid">
              @for (r of resources(); track r.id) {
                <div class="picker__item"
                     [class.is-picked]="pickedResource?.id === r.id"
                     (click)="pickedResource = r">
                  @if (pickerType === 'image') {
                    <img [src]="resourceThumb(r.id)" [alt]="r.name" loading="lazy" />
                  } @else {
                    <div class="picker__video"><span nz-icon nzType="play-circle" nzTheme="fill"></span></div>
                  }
                  <span class="picker__name" [title]="r.name">{{ r.name }}</span>
                </div>
              }
            </div>
          }
        </div>
      </ng-container>
    </nz-modal>

    <!-- 邀请教师 -->
    <nz-modal
      [(nzVisible)]="inviteOpen"
      nzTitle="邀请同校教师加入共创"
      nzOkText="发送邀请"
      nzCancelText="关闭"
      [nzOkLoading]="inviting()"
      (nzOnOk)="submitInvite()"
      (nzOnCancel)="inviteOpen = false"
    >
      <ng-container *nzModalContent>
        <p class="invite__tip">
          被邀请的教师登录后即可在自己的「教研白板」列表看到此白板，两人同时打开就能实时协作。
        </p>
        @if (teachersLoading()) {
          <div class="picker__loading"><nz-spin nzSimple></nz-spin></div>
        } @else if (teachers().length === 0) {
          <nz-empty nzNotFoundContent="本校没有其他教师可邀请"></nz-empty>
        } @else {
          <div class="invite__list">
            @for (t of teachers(); track t.userId) {
              <label class="invite__row" [class.is-joined]="t.joined">
                <input type="checkbox" [disabled]="t.joined"
                       [checked]="inviteSelected.has(t.userId)"
                       (change)="toggleInvite(t.userId, $event)" />
                <span class="avatar avatar--sm" [style.background]="colorOf(t.userName)">{{ initial(t.userName) }}</span>
                <span class="invite__name">{{ t.name || t.userName }}</span>
                <span class="invite__uid">{{ t.userName }}</span>
                @if (t.joined) { <nz-tag nzColor="green">已加入</nz-tag> }
              </label>
            }
          </div>
        }
      </ng-container>
    </nz-modal>
  `,
  styles: [`
    .room { position: relative; display: flex; flex-direction: column; height: calc(100vh - 76px); background: #f5f8f7; }
    .room__head { display: flex; align-items: center; gap: 12px; padding: 10px 18px; background: #fff; border-bottom: 1px solid #e0e8e5; }
    .room__title h2 { margin: 0; font-size: 16px; font-weight: 650; }
    .room__title p { margin: 2px 0 0; font-size: 12px; color: #64757a; }
    .room__online { display: flex; align-items: center; gap: 6px; margin-left: auto; }
    .avatar { width: 26px; height: 26px; border-radius: 50%; color: #fff; font-size: 11px; display: grid; place-items: center; font-weight: 600; }
    .avatar--sm { width: 22px; height: 22px; font-size: 10px; }
    .room__conn { display: inline-flex; align-items: center; gap: 5px; margin-left: 8px; font-size: 11px; color: #18a058; }
    .room__conn i { width: 6px; height: 6px; border-radius: 50%; background: #18a058; }
    .room__conn.is-off { color: #d6a21e; }
    .room__conn.is-off i { background: #d6a21e; }
    .room__actions { display: flex; align-items: center; gap: 10px; }
    .room__me { display: inline-flex; align-items: center; gap: 6px; padding: 2px 10px; border: 1px solid; border-radius: 999px; font-size: 12px; }
    .room__me i { width: 8px; height: 8px; border-radius: 50%; }
    .room__tools { display: flex; align-items: center; gap: 8px; padding: 8px 18px; background: #fff; border-bottom: 1px solid #e0e8e5; }
    .room__hint { margin-left: auto; font-size: 12px; color: #96a5aa; }
    .room__stage {
      flex: 1 1 auto; position: relative; overflow: hidden;
      background-color: #fbfcfc;
      background-image: radial-gradient(#dbe4e2 1px, transparent 1px);
      background-size: 24px 24px;
    }
    .room__stage.is-locked::after { content: '只读模式'; position: absolute; right: 12px; top: 12px; font-size: 12px; color: #96a5aa; }
    .room__loading { position: absolute; inset: 0; display: grid; place-items: center; }
    .room__panel {
      position: absolute; right: 16px; top: 108px; width: 240px; padding: 14px;
      background: #fff; border: 1px solid #e0e8e5; border-radius: 12px; box-shadow: 0 8px 24px rgba(16,32,48,.1);
      display: flex; flex-direction: column; gap: 10px;
    }
    .room__panel h4 { margin: 0; font-size: 13px; font-weight: 650; }
    .room__panel label { display: flex; flex-direction: column; gap: 6px; font-size: 12px; color: #64757a; }
    .room__by { margin: 0; font-size: 11px; color: #96a5aa; }
    .picker { display: flex; flex-direction: column; gap: 12px; }
    .picker__filters { display: flex; gap: 8px; flex-wrap: wrap; }
    .picker__loading { padding: 40px 0; display: grid; place-items: center; }
    .picker__grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 10px; max-height: 420px; overflow: auto; }
    .picker__item { display: flex; flex-direction: column; gap: 6px; padding: 8px; border: 1px solid #e0e8e5; border-radius: 10px; cursor: pointer; }
    .picker__item.is-picked { border-color: #087f7a; box-shadow: 0 0 0 2px rgba(8,127,122,.15); }
    .picker__item img { width: 100%; height: 86px; object-fit: cover; border-radius: 6px; background: #eef2f1; }
    .picker__video { height: 86px; display: grid; place-items: center; background: #203b40; border-radius: 6px; color: #fff; font-size: 26px; }
    .picker__name { font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .invite__tip { margin: 0 0 12px; font-size: 12px; line-height: 1.7; color: #64757a; }
    .invite__list { display: flex; flex-direction: column; gap: 4px; max-height: 360px; overflow: auto; }
    .invite__row { display: flex; align-items: center; gap: 10px; padding: 8px 10px; border-radius: 8px; cursor: pointer; }
    .invite__row:hover { background: #f5f8f7; }
    .invite__row.is-joined { opacity: .6; cursor: default; }
    .invite__name { font-size: 13px; }
    .invite__uid { font-size: 11px; color: #96a5aa; margin-left: auto; }
  `],
})
export class WhiteboardRoomComponent implements OnInit, OnDestroy {
  @ViewChild('stageHost', { static: true }) stageHost!: ElementRef<HTMLDivElement>;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(WhiteboardService);
  private readonly rt = inject(WhiteboardRealtimeService);
  private readonly message = inject(NzMessageService);
  private readonly zone = inject(NgZone);

  readonly board = signal<WhiteboardDetail | null>(null);
  readonly loading = signal(true);
  readonly connected = signal(false);
  readonly myColor = signal('#2f6fed');
  readonly myName = signal('');
  readonly onlineMembers = signal<PresenceUser[]>([]);
  readonly selected = signal<BoardElement | null>(null);
  readonly canEdit = computed(() => this.board()?.canEdit !== false);

  // 资源选择框
  pickerOpen = false;
  pickerType: 'image' | 'video' = 'image';
  pickerCourseId: string | null = null;
  pickerKeyword = '';
  readonly resources = signal<any[]>([]);
  readonly resourcesLoading = signal(false);
  readonly myCourses = signal<any[]>([]);
  pickedResource: any = null;

  // 邀请
  inviteOpen = false;
  readonly teachers = signal<InvitableTeacher[]>([]);
  readonly teachersLoading = signal(false);
  readonly inviting = signal(false);
  inviteSelected = new Set<string>();

  private boardId = '';
  private stage?: Konva.Stage;
  private layer?: Konva.Layer;
  private cursorLayer?: Konva.Layer;
  private tr?: Konva.Transformer;
  /** 本地元素模型：key → BoardElement */
  private elements = new Map<string, BoardElement>();
  private cursors = new Map<string, Konva.Group>();
  private subs: Subscription[] = [];
  private lastCursorSent = 0;
  private resizeObserver?: ResizeObserver;

  ngOnInit(): void {
    this.boardId = this.route.snapshot.paramMap.get('id') || '';
    if (!this.boardId) { void this.router.navigate(['/learning/whiteboards']); return; }

    this.bindRealtime();
    this.loadBoard();
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
    void this.rt.disconnect();
    this.resizeObserver?.disconnect();
    this.stage?.destroy();
  }

  // ── 加载与实时 ─────────────────────────────────────────────

  private loadBoard(): void {
    this.loading.set(true);
    this.api.get(this.boardId).subscribe({
      next: async (d) => {
        this.board.set(d);
        this.myColor.set(d.myColor || '#2f6fed');
        this.myName.set(d.myName || '');
        this.connected.set(false);

        try {
          const joined = await this.rt.join(this.boardId);
          this.myColor.set(joined.myColor || this.myColor());
          this.myName.set(joined.myName || this.myName());
          this.onlineMembers.set(joined.onlineMembers || []);
          this.connected.set(true);

          this.initStage();
          // 服务端全量优先（断线重连/后来者补齐）
          const fromServer = (joined.elements || []).map((e) => this.parse(e.json)).filter(Boolean) as BoardElement[];
          fromServer.forEach((el) => this.upsertLocal(el, false));
          this.layer?.batchDraw();
        } catch (err: any) {
          this.loading.set(false);
          this.message.error(err?.message || '实时连接失败');
        }
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.message.error('白板加载失败'); },
    });
  }

  private bindRealtime(): void {
    this.subs.push(
      this.rt.elementUpserted$.subscribe((d) => {
        const el = this.parse(d.json || '{}');
        if (el) { this.upsertLocal(el, false); this.layer?.batchDraw(); }
      }),
      this.rt.elementDeleted$.subscribe((d) => {
        this.removeLocal(d.key);
        this.layer?.batchDraw();
      }),
      this.rt.cursorMoved$.subscribe((c) => this.drawCursor(c)),
      this.rt.presenceJoin$.subscribe((u) => {
        this.onlineMembers.update((list) => [...list.filter((x) => x.userId !== u.userId), { ...u, online: true }]);
        this.message.info(`${u.userName} 已加入协同`);
      }),
      this.rt.presenceLeave$.subscribe((u) => {
        this.onlineMembers.update((list) => list.filter((x) => x.userId !== u.userId));
        this.cursors.get(u.userId)?.destroy();
        this.cursors.delete(u.userId);
        this.cursorLayer?.batchDraw();
      }),
      this.rt.reconnected$.subscribe(() => { void this.rejoin(); }),
    );
  }

  private async rejoin(): Promise<void> {
    try {
      const joined = await this.rt.join(this.boardId);
      this.onlineMembers.set(joined.onlineMembers || []);
      this.connected.set(true);
      this.message.success('已重新连接');
    } catch {
      this.connected.set(false);
    }
  }

  // ── Konva 舞台 ─────────────────────────────────────────────

  private initStage(): void {
    const host = this.stageHost.nativeElement;
    const rect = host.getBoundingClientRect();

    this.stage = new Konva.Stage({
      container: host,
      width: Math.max(320, rect.width),
      height: Math.max(240, rect.height),
      draggable: true,   // 拖空白处平移
    });

    this.layer = new Konva.Layer();
    this.cursorLayer = new Konva.Layer({ listening: false });
    this.stage.add(this.layer, this.cursorLayer);

    this.tr = new Konva.Transformer({
      borderStroke: '#087f7a',
      anchorStroke: '#087f7a',
      anchorSize: 9,
      rotationSnaps: [0, 90, 180, 270],
    });
    this.layer.add(this.tr);

    // 滚轮缩放（以指针为锚点）
    this.stage.on('wheel', (e) => {
      e.evt.preventDefault();
      const oldScale = this.stage!.scaleX();
      const pointer = this.stage!.getPointerPosition();
      if (!pointer) return;
      const k = e.evt.deltaY > 0 ? 1 / 1.08 : 1.08;
      const newScale = Math.min(3, Math.max(0.25, oldScale * k));
      const wp = { x: (pointer.x - this.stage!.x()) / oldScale, y: (pointer.y - this.stage!.y()) / oldScale };
      this.stage!.scale({ x: newScale, y: newScale });
      this.stage!.position({ x: pointer.x - wp.x * newScale, y: pointer.y - wp.y * newScale });
      this.syncGridBackground();
    });

    // 点空白处取消选中
    this.stage.on('click tap', (e) => {
      if (e.target === this.stage) this.select(null);
    });

    // 平移/缩放后同步网格底纹 + 广播视口
    this.stage.on('dragend', (e) => {
      if (e.target === this.stage) {
        this.syncGridBackground();
        void this.rt.viewport(this.boardId, this.stage!.x(), this.stage!.y(), this.stage!.scaleX());
      }
    });

    // 光标上报（50ms 节流）
    this.stage.on('mousemove', () => {
      const now = Date.now();
      if (now - this.lastCursorSent < 50) return;
      this.lastCursorSent = now;
      const p = this.stage!.getRelativePointerPosition();
      if (p) void this.rt.cursor(this.boardId, p.x, p.y);
    });

    // 容器尺寸变化
    this.resizeObserver = new ResizeObserver(() => {
      if (!this.stage) return;
      const r = host.getBoundingClientRect();
      this.stage.size({ width: Math.max(320, r.width), height: Math.max(240, r.height) });
    });
    this.resizeObserver.observe(host);

    this.syncGridBackground();
  }

  private syncGridBackground(): void {
    const host = this.stageHost?.nativeElement;
    if (!host || !this.stage) return;
    const s = this.stage.scaleX() * 24;
    host.style.backgroundSize = `${s}px ${s}px`;
    host.style.backgroundPosition = `${this.stage.x()}px ${this.stage.y()}px`;
  }

  // ── 元素模型 ───────────────────────────────────────────────

  private parse(json: string): BoardElement | null {
    try {
      const o = JSON.parse(json);
      if (!o || !o.id || !o.type) return null;
      return o as BoardElement;
    } catch { return null; }
  }

  /** 本地 upsert：更新模型 + 重建/更新 Konva 节点（remote=true 时不回广播） */
  private upsertLocal(el: BoardElement, broadcast: boolean): void {
    this.elements.set(el.id, el);
    const existing = this.layer?.findOne(`#${this.cssId(el.id)}`) as Konva.Group | undefined;
    if (existing) {
      this.applyAttrs(existing, el);
      this.layer?.batchDraw();
      return;
    }
    this.buildNode(el);
    if (broadcast) void this.rt.upsertElement(this.boardId, el.id, this.typeCode(el.type), el);
  }

  private removeLocal(key: string): void {
    this.elements.delete(key);
    this.layer?.findOne(`#${this.cssId(key)}`)?.destroy();
    if (this.selected()?.id === key) this.select(null);
  }

  /** Konva id 不能以数字开头，统一加前缀并用安全字符 */
  private cssId(key: string): string {
    return 'el-' + key.replace(/[^a-zA-Z0-9_-]/g, '');
  }

  private typeCode(t: BoardElement['type']): number {
    return t === 'text' ? 0 : t === 'note' ? 1 : t === 'image' ? 2 : 3;
  }

  private buildNode(el: BoardElement): void {
    if (!this.layer) return;

    const group = new Konva.Group({
      id: this.cssId(el.id),
      x: el.x, y: el.y,
      scaleX: el.sx ?? 1, scaleY: el.sy ?? 1,
      rotation: el.rot ?? 0,
      draggable: this.canEdit(),
      name: 'board-el',
    });

    if (el.type === 'text') {
      group.add(new Konva.Text({
        text: el.text || '',
        fontSize: el.fs || 24,
        fill: el.fill || '#1f2430',
        width: el.w || 320,
        lineHeight: 1.4,
      }));
    } else if (el.type === 'note') {
      const w = el.w || 200, h = el.h || 150;
      group.add(new Konva.Rect({
        width: w, height: h, fill: el.bg || '#ffe58a', cornerRadius: 6,
        shadowColor: '#000', shadowBlur: 8, shadowOpacity: 0.18, shadowOffsetY: 3,
      }));
      group.add(new Konva.Text({
        x: 12, y: 12, width: w - 24, text: el.text || '', fontSize: el.fs || 16,
        fill: '#3a2f00', lineHeight: 1.45,
      }));
      if (el.by) {
        group.add(new Konva.Text({ x: 12, y: h - 20, text: '— ' + el.by, fontSize: 11, fill: 'rgba(58,47,0,.6)' }));
      }
    } else {
      const w = el.w || 260, h = el.h || 170;
      // 占位（图/视频加载前）
      const ph = new Konva.Rect({
        width: w, height: h, fill: 'rgba(136,136,136,.12)', stroke: 'rgba(136,136,136,.35)',
        dash: [6, 4], cornerRadius: 6,
      });
      group.add(ph);

      const src = el.resourceId ? this.api.resourceUrl(el.resourceId) : '';

      if (el.type === 'image' && src) {
        const img = new Image();
        img.onload = () => {
          ph.destroy();
          group.add(new Konva.Image({ image: img, width: w, height: h }));
          this.layer?.batchDraw();
        };
        img.onerror = () => { this.layer?.batchDraw(); };
        img.src = src;
      } else if (el.type === 'video') {
        const v = document.createElement('video');
        v.src = src;
        v.muted = true; v.loop = true; v.playsInline = true; v.preload = 'auto';
        const icon = new Konva.Group({ x: w / 2, y: h / 2 });
        icon.add(new Konva.Circle({ radius: 26, fill: 'rgba(0,0,0,.55)' }));
        icon.add(new Konva.Text({ x: -7, y: -11, text: '▶', fontSize: 22, fill: '#fff' }));

        v.onloadeddata = () => {
          ph.destroy();
          group.add(new Konva.Image({ image: v, width: w, height: h }));
          group.add(icon);
          this.layer?.batchDraw();
        };
        const anim = new Konva.Animation(() => { /* 每帧重绘以播放视频 */ }, this.layer);
        group.on('click tap', (ev) => {
          ev.cancelBubble = true;
          if (v.paused) { void v.play(); icon.hide(); anim.start(); }
          else { v.pause(); icon.show(); anim.stop(); this.layer?.batchDraw(); }
        });
      }
    }

    // 选中 / 拖动 / 变换
    group.on('click tap', (ev) => {
      ev.cancelBubble = true;
      this.select(el.id);
    });
    group.on('dragstart', () => this.select(el.id));
    group.on('dragend', () => this.commitGeometry(el.id));
    group.on('transformend', () => this.commitGeometry(el.id));

    this.layer.add(group);
    this.tr?.moveToTop();
    this.layer.batchDraw();
  }

  private applyAttrs(node: Konva.Group, el: BoardElement): void {
    node.setAttrs({ x: el.x, y: el.y, scaleX: el.sx ?? 1, scaleY: el.sy ?? 1, rotation: el.rot ?? 0 });
    const textNode = node.findOne('Text') as Konva.Text | undefined;
    if (textNode && (el.type === 'text' || el.type === 'note')) {
      textNode.text(el.text || '');
      textNode.fontSize(el.fs || textNode.fontSize());
      if (el.type === 'text') {
        textNode.fill(el.fill || '#1f2430');
        if (el.w) textNode.width(el.w);
      }
    }
    const rect = node.findOne('Rect') as Konva.Rect | undefined;
    if (rect && el.type === 'note' && el.bg) rect.fill(el.bg);
  }

  /** 拖动/缩放/旋转后：回写模型并广播 */
  private commitGeometry(key: string): void {
    const node = this.layer?.findOne(`#${this.cssId(key)}`) as Konva.Group | undefined;
    const el = this.elements.get(key);
    if (!node || !el) return;

    if (el.type === 'text') {
      el.w = Math.max(60, node.width() * node.scaleX());
      node.scaleX(1); node.scaleY(1);
    } else {
      el.sx = node.scaleX();
      el.sy = node.scaleY();
    }
    el.x = node.x();
    el.y = node.y();
    el.rot = node.rotation();

    this.elements.set(key, el);
    if (this.canEdit()) void this.rt.upsertElement(this.boardId, key, this.typeCode(el.type), el);
  }

  // ── 交互：选中 / 添加 / 删除 ────────────────────────────────

  select(key: string | null): void {
    if (!this.tr) return;
    if (!key) {
      this.tr.nodes([]);
      this.selected.set(null);
      this.layer?.batchDraw();
      return;
    }
    const node = this.layer?.findOne(`#${this.cssId(key)}`) as Konva.Group | undefined;
    if (!node) return;
    this.tr.nodes([node]);
    this.layer?.batchDraw();
    this.selected.set({ ...(this.elements.get(key) as BoardElement) });
  }

  /** 屏幕中心对应的画布坐标 */
  private center(): { x: number; y: number } {
    if (!this.stage) return { x: 120, y: 120 };
    const s = this.stage.scaleX();
    return {
      x: (this.stage.width() / 2 - this.stage.x()) / s - 120 + Math.random() * 40,
      y: (this.stage.height() / 2 - this.stage.y()) / s - 80 + Math.random() * 40,
    };
  }

  private newKey(): string {
    return Date.now().toString(36) + Math.floor(Math.random() * 1e6).toString(36);
  }

  addText(): void {
    const p = this.center();
    const el: BoardElement = {
      id: this.newKey(), type: 'text', ...p, w: 320,
      text: '双击右侧面板编辑文本', fs: 24, fill: '#1f2430',
    };
    this.upsertLocal(el, true);
    this.select(el.id);
  }

  addNote(): void {
    const p = this.center();
    const el: BoardElement = {
      id: this.newKey(), type: 'note', ...p, w: 200, h: 150,
      text: '写下教研想法…', fs: 16, bg: '#ffe58a', by: this.myName(),
    };
    this.upsertLocal(el, true);
    this.select(el.id);
  }

  onTextInput(v: string): void {
    const sel = this.selected();
    if (!sel) return;
    sel.text = v;
    this.elements.set(sel.id, sel);
    const node = this.layer?.findOne(`#${this.cssId(sel.id)}`) as Konva.Group | undefined;
    const t = node?.findOne('Text') as Konva.Text | undefined;
    t?.text(v);
    this.layer?.batchDraw();
    this.selected.set({ ...sel });
    void this.rt.upsertElement(this.boardId, sel.id, this.typeCode(sel.type), sel);
  }

  onFontInput(v: number): void {
    const sel = this.selected();
    if (!sel) return;
    sel.fs = Number(v);
    this.elements.set(sel.id, sel);
    const node = this.layer?.findOne(`#${this.cssId(sel.id)}`) as Konva.Group | undefined;
    (node?.findOne('Text') as Konva.Text | undefined)?.fontSize(sel.fs);
    this.layer?.batchDraw();
    this.selected.set({ ...sel });
    void this.rt.upsertElement(this.boardId, sel.id, this.typeCode(sel.type), sel);
  }

  deleteSelected(): void {
    const sel = this.selected();
    if (!sel) return;
    this.removeLocal(sel.id);
    this.select(null);
    this.layer?.batchDraw();
    void this.rt.deleteElement(this.boardId, sel.id);
  }

  // ── 远端光标 ───────────────────────────────────────────────

  private drawCursor(c: { userId: string; name: string; color: string; x: number; y: number }): void {
    if (!this.cursorLayer) return;
    let g = this.cursors.get(c.userId);
    if (!g) {
      g = new Konva.Group();
      g.add(new Konva.Line({
        points: [0, 0, 0, 16, 4, 12, 10, 12], closed: true,
        fill: c.color, stroke: '#fff', strokeWidth: 1,
      }));
      const label = new Konva.Label({ x: 10, y: 14 });
      label.add(new Konva.Tag({ fill: c.color, cornerRadius: 4 }));
      label.add(new Konva.Text({ text: c.name, fill: '#fff', fontSize: 11, padding: 3 }));
      g.add(label);
      g.position({ x: c.x, y: c.y });
      this.cursorLayer.add(g);
      this.cursors.set(c.userId, g);
    } else {
      g.to({ x: c.x, y: c.y, duration: 0.4, easing: Konva.Easings.EaseOut });
    }
    this.cursorLayer.batchDraw();
  }

  // ── 资源选择框 ─────────────────────────────────────────────

  openResourcePicker(type: 'image' | 'video'): void {
    this.pickerType = type;
    this.pickerOpen = true;
    this.pickedResource = null;
    this.loadMyCourses();
    this.loadResources();
  }

  private loadMyCourses(): void {
    if (this.myCourses().length) return;
    this.api.getMyCourses().subscribe({
      next: (r) => this.myCourses.set(r.items || []),
      error: () => this.myCourses.set([]),
    });
  }

  loadResources(): void {
    this.resourcesLoading.set(true);
    // 图片=3 / 视频=1（与 ResourceType 枚举一致）
    const typeCode = this.pickerType === 'image' ? 3 : 1;
    this.api.getMyResources(this.pickerKeyword?.trim() || undefined, typeCode, this.pickerCourseId || undefined)
      .subscribe({
        next: (r) => { this.resources.set(r.items || []); this.resourcesLoading.set(false); },
        error: () => { this.resources.set([]); this.resourcesLoading.set(false); },
      });
  }

  resourceThumb(id: string): string { return this.api.resourceUrl(id); }

  insertPickedResource(): void {
    const r = this.pickedResource;
    if (!r) return;
    const p = this.center();
    const isImage = this.pickerType === 'image';
    const el: BoardElement = {
      id: this.newKey(),
      type: isImage ? 'image' : 'video',
      ...p,
      w: isImage ? 280 : 320,
      h: isImage ? 200 : 180,
      resourceId: r.id,
      resourceName: r.name,
      fileExtension: r.fileExtension,
    };
    this.upsertLocal(el, true);
    this.select(el.id);
    this.pickerOpen = false;
    this.pickedResource = null;
  }

  // ── 邀请 ───────────────────────────────────────────────────

  openInvite(): void {
    this.inviteOpen = true;
    this.inviteSelected.clear();
    this.teachersLoading.set(true);
    this.api.getInvitableTeachers(this.boardId).subscribe({
      next: (list) => { this.teachers.set(list || []); this.teachersLoading.set(false); },
      error: () => { this.teachers.set([]); this.teachersLoading.set(false); },
    });
  }

  toggleInvite(userId: string, ev: Event): void {
    const checked = (ev.target as HTMLInputElement).checked;
    if (checked) this.inviteSelected.add(userId); else this.inviteSelected.delete(userId);
  }

  submitInvite(): void {
    const ids = [...this.inviteSelected];
    if (!ids.length) { this.message.warning('请选择要邀请的教师'); return; }
    this.inviting.set(true);
    this.api.invite(this.boardId, ids).subscribe({
      next: (members) => {
        this.inviting.set(false);
        this.inviteOpen = false;
        this.board.update((b) => (b ? { ...b, members, memberCount: members.length } : b));
        this.message.success(`已邀请 ${ids.length} 位教师，对方登录后即可看到此白板`);
      },
      error: (err) => { this.inviting.set(false); this.message.error(err?.error?.error?.message || '邀请失败'); },
    });
  }

  // ── 小工具 ─────────────────────────────────────────────────

  initial(name: string): string {
    if (!name) return '师';
    return name.trim().slice(0, 1);
  }

  colorOf(name: string): string {
    const palette = ['#2f6fed', '#e0662f', '#18a058', '#a24bd6', '#d6a21e'];
    let h = 0;
    for (let i = 0; i < (name || '').length; i++) h = (h * 31 + name.charCodeAt(i)) % 997;
    return palette[h % palette.length];
  }

  back(): void {
    void this.router.navigate(['/learning/whiteboards']);
  }
}
