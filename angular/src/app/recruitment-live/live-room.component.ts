import {
  Component, OnInit, OnDestroy, AfterViewChecked, ChangeDetectionStrategy,
  inject, signal, computed, ViewChild, ElementRef, HostBinding, HostListener, Renderer2
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule, NavigationStart } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { FormsModule } from '@angular/forms';
import { RecruitmentLiveService, LiveState } from './recruitment-live.service';
import { RecruitmentLiveDto, RemoteParticipantStream, ParticipantBriefDto } from './recruitment-live.models';
import { ConfigStateService } from '@abp/ng.core';
import { NzMessageService } from 'ng-zorro-antd/message';
import { filter } from 'rxjs/operators';

@Component({
  selector: 'app-live-room',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzTooltipModule,
    NzInputModule,
    NzModalModule,
    FormsModule,
  ],
  templateUrl: './live-room.component.html',
  styleUrls: ['./live-room.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LiveRoomComponent implements OnInit, OnDestroy, AfterViewChecked {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  liveService = inject(RecruitmentLiveService);
  private configState = inject(ConfigStateService);
  private message = inject(NzMessageService);
  private hostEl = inject(ElementRef<HTMLElement>);
  private renderer = inject(Renderer2);

  liveId = '';
  live: RecruitmentLiveDto | null = null;
  myRole: 'teacher' | 'student' = 'student';
  chatInput = '';

  /** 老师端（管理后台进入）：标题容器 + 视口高度，不再受学生端布局限制 */
  @HostBinding('class.is-teacher') get isTeacherHost(): boolean {
    return this.myRole === 'teacher';
  }

  /** 全屏状态（监听 fullscreenchange 同步） */
  readonly isFullscreen = signal(false);

  /** 结束直播确认弹窗 / 主动结束中（用于跳过 ended 提示页） */
  endConfirmVisible = false;
  endingLive = false;

  readonly liveState = this.liveService.liveState;
  readonly micEnabled = this.liveService.micEnabled;
  readonly camEnabled = this.liveService.camEnabled;
  readonly speakerEnabled = this.liveService.speakerEnabled;
  readonly chatOpen = this.liveService.chatOpen;
  readonly chatMessages = this.liveService.chatMessages;
  readonly callDurationSec = this.liveService.callDurationSec;
  readonly remoteStreams = this.liveService.remoteStreams;
  readonly participants = this.liveService.participants;
  readonly connectionLabel = this.liveService.connectionLabel;

  readonly localStreamActive = computed(() => this.liveService.localStreamReady());
  /** 本地预览流（仅视频轨，不含麦克风音频，避免 Firefox 标签页声音图标误报/本地回声） */
  readonly localPreviewStream = this.liveService.localPreviewStream;
  readonly isCallActive = computed(() =>
    this.liveState() === 'connected' || this.liveState() === 'signaling'
  );
  readonly canHangUp = computed(() =>
    this.liveState() !== 'idle' && this.liveState() !== 'ended'
  );

  readonly formattedTime = computed(() => {
    const s = this.callDurationSec();
    const m = Math.floor(s / 60);
    return `${String(m).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  });

  /**
   * 当前视角下应展示的远端参与者。
   * 星型拓扑：教师看所有学生；学生只看教师（学生之间不建立媒体连接）。
   */
  readonly visibleParticipants = computed(() => {
    const list = this.participants();
    return this.myRole === 'teacher' ? list : list.filter(p => p.role === 'teacher');
  });

  /** 计算远程参与者人数（按当前视角过滤） */
  readonly participantCount = computed(() => this.visibleParticipants().length);

  /** 是否显示 gallery 网格（多人模式） */
  readonly isGroupCall = computed(() => this.participantCount() > 1);

  /** Gallery 布局列数：1~2 人单栏，3 人起直接双栏 */
  readonly galleryCols = computed(() => {
    const count = this.participantCount() + 1; // +1 为自己
    return count <= 2 ? 1 : 2;
  });

  // ── 视频会议式布局：聚焦视图 / 网格视图 ──
  /** 当前布局：'spotlight' 聚焦主画面+侧边条（默认，老师推荐）；'grid' 均衡网格 */
  readonly layoutMode = signal<'spotlight' | 'grid'>('spotlight');
  /** 用户手动固定的参与者 id（点击侧边条固定到主画面）；null 表示跟随发言人 */
  readonly pinnedUserId = signal<string | null>(null);

  /** 聚焦主画面应展示的参与者 */
  readonly spotlightUser = computed(() => {
    const vis = this.visibleParticipants();
    if (!vis.length) return null;
    const pinned = this.pinnedUserId();
    if (pinned && vis.some(p => p.userId === pinned)) {
      return vis.find(p => p.userId === pinned)!;
    }
    const speaker = this.liveService.activeSpeakerId();
    if (speaker && vis.some(p => p.userId === speaker)) {
      return vis.find(p => p.userId === speaker)!;
    }
    return vis[0];
  });

  /** 聚焦主画面参与者 id */
  readonly spotlightId = computed(() => this.spotlightUser()?.userId ?? null);

  /** 聚焦模式下，侧边条展示所有参与者（含主画面中那位），如 Google Meet 的胶片条 */
  readonly railParticipants = computed(() => this.visibleParticipants());

  /** 判断某人当前是否在主画面中（用于侧边条高亮对应） */
  isInSpotlight(userId: string | null): boolean {
    return !!userId && this.spotlightId() === userId;
  }



  @ViewChild('chatMessagesContainer', { static: false }) chatMessagesContainer!: ElementRef;
  private previousMsgCount = 0;

  /** 组件是否已销毁（防止异步回调在销毁后继续操作状态） */
  private destroyed = false;
  /** Router 事件订阅：离开当前直播间路由时主动断开连接 */
  private routerSub = this.router.events
    .pipe(filter((e): e is NavigationStart => e instanceof NavigationStart))
    .subscribe((e) => {
      const target = (e.url || '').split('?')[0];
      const current = this.router.url.split('?')[0];
      // 当前路由以 /student/recruitment-live/<id> 形式存在，离开这一层即视为退出直播
      const inLiveRoute = current.startsWith('/student/recruitment-live/')
        || current.startsWith('/admin/recruitment-live/');
      const leavingLiveRoute = inLiveRoute
        && !target.startsWith('/student/recruitment-live/')
        && !target.startsWith('/admin/recruitment-live/');
      if (leavingLiveRoute) {
        this.liveService.disconnect();
      }
    });

  /** 浏览器关闭 / 刷新时同步断开（避免后台残留摄像头/麦克风占用） */
  @HostListener('window:beforeunload')
  onBeforeUnload() {
    this.liveService.disconnect();
  }

  /** ResizeObserver：观察 footer 高度变化（窗口变窄、链接换行等） */
  private footerObserver?: ResizeObserver;

  /**
   * 根据 viewport - header - footer 计算 :host 的 max-height，避免撑大 main-content。
   * 使用 ResizeObserver 监听 footer、window resize，确保变化时及时更新。
   */
  private updateMaxHeight = () => {
    if (this.destroyed) return;
    const layoutEl = document.querySelector('app-student-layout');
    const headerEl = layoutEl?.querySelector('.app-header') as HTMLElement | null;
    const footerEl = layoutEl?.querySelector('.app-footer') as HTMLElement | null;
    if (!headerEl || !footerEl || !this.hostEl?.nativeElement) return;

    const headerH = headerEl.offsetHeight || 0;
    const footerH = footerEl.offsetHeight || 0;
    const maxHeight = window.innerHeight - headerH - footerH;
    // 限制下限：viewport 很小（手机横屏）时不为 0，保证至少还能看到控件
    if (maxHeight > 200) {
      this.renderer.setStyle(this.hostEl.nativeElement, 'max-height', `${maxHeight}px`);
    }
  };

  ngAfterViewChecked() {
    const count = this.chatMessages().length;
    if (count > this.previousMsgCount) {
      this.previousMsgCount = count;
      this.scrollToBottom();
    }
  }

  private scrollToBottom() {
    setTimeout(() => {
      const el = this.chatMessagesContainer?.nativeElement;
      if (el) el.scrollTop = el.scrollHeight;
    }, 50);
  }

  readonly waitingText = computed(() => {
    return this.myRole === 'teacher' ? '等待学生加入...' : '等待连接...';
  });

  ngOnInit() {
    // 设置 :host max-height，让页面整体不超出 viewport
    // 延迟一帧确保 header / footer 已完成布局
    setTimeout(() => this.updateMaxHeight(), 0);
    window.addEventListener('resize', this.updateMaxHeight);
    document.addEventListener('fullscreenchange', this.onFullscreenChange);

    const layoutEl = document.querySelector('app-student-layout');
    const footerEl = layoutEl?.querySelector('.app-footer') as HTMLElement | null;
    if (footerEl && typeof ResizeObserver !== 'undefined') {
      this.footerObserver = new ResizeObserver(this.updateMaxHeight);
      this.footerObserver.observe(footerEl);
    }

    this.liveId = this.route.snapshot.paramMap.get('id') || '';
    if (!this.liveId) {
      this.message.error('无效的直播间');
      this.router.navigate(['/']);
      return;
    }

    this.liveService.getLive(this.liveId).subscribe({
      next: (live) => {
        if (this.destroyed) return; // 异步回调到达时组件已销毁，跳过
        const currentUser = this.configState.getDeep('currentUser') as any;
        const userId = currentUser?.id;

        // 权限校验
        const isTeacher = live.teacherId === userId;
        const participantUserIds = (live.participants || []).map(p => p.userId);
        const isParticipant = isTeacher || participantUserIds.includes(userId);

        if (!isParticipant && !live.isParticipant) {
          this.message.error('您没有权限进入该直播间');
          this.router.navigate(this.myRole === 'teacher'
            ? ['/admin/recruitment-live']
            : ['/student/recruitment-live']);
          return;
        }

        this.live = live;
        // 角色以后端参与者表为准（兼容 admin 代管等 teacherId 不一致的场景），
        // 取不到时回退到 teacherId 比对。角色不一致会导致双方都不发 offer
        const selfParticipant = (live.participants || []).find(p => p.userId === userId);
        this.myRole = selfParticipant?.role === 'teacher' || live.teacherId === userId
          ? 'teacher'
          : 'student';

        if (live.status === 2 || live.status === 3) {
          this.message.warning('该直播已结束或已取消');
          return;
        }

        this.loadChatHistory(() => {
          this.joinLive(userId, live);
        });
      },
      error: () => {
        this.message.error('直播不存在');
        this.router.navigate(['/']);
      },
    });
  }

  private loadChatHistory(afterLoad?: () => void) {
    this.liveService.getChatHistory(this.liveId).subscribe({
      next: (msgs) => {
        const chatMsgs = msgs.map(m => ({
          text: m.content,
          from: m.senderRole,
          self: m.senderRole === this.myRole,
          time: new Date(m.sentAt).getTime(),
          fromUserName: m.senderRole === 'teacher' ? (this.live?.teacherName || '面试官') : (m as any).senderId !== this.live?.teacherId ? '学生' : '面试官',
        }));
        this.liveService.chatMessages.set(chatMsgs);
        if (chatMsgs.length > 0) {
          this.liveService.chatOpen.set(true);
          this.scrollToBottom();
        }
        afterLoad?.();
      },
      error: () => {
        console.warn('[LiveRoom] Failed to load chat history');
        afterLoad?.();
      },
    });
  }

  setLayout(mode: 'spotlight' | 'grid') {
    this.layoutMode.set(mode);
  }

  /** 点击侧边条固定/取消固定某参与者到主画面 */
  pinParticipant(userId: string | null) {
    this.pinnedUserId.set(this.pinnedUserId() === userId ? null : userId);
  }

  /** 判断某人是否为当前发言人（用于高亮） */
  isActiveSpeaker(userId: string): boolean {
    return this.liveService.activeSpeakerId() === userId;
  }

  ngOnDestroy() {
    this.destroyed = true;
    this.routerSub?.unsubscribe();
    this.footerObserver?.disconnect();
    window.removeEventListener('resize', this.updateMaxHeight);
    document.removeEventListener('fullscreenchange', this.onFullscreenChange);
    if (document.fullscreenElement) {
      document.exitFullscreen().catch(() => undefined);
    }
    // 清除 max-height 内联样式，避免影响其他路由
    if (this.hostEl?.nativeElement) {
      this.renderer.removeStyle(this.hostEl.nativeElement, 'max-height');
    }
    this.liveService.disconnect();
  }

  private joinLive(userId: string, live: RecruitmentLiveDto) {
    const userName = live.teacherId === userId
      ? (live.teacherName || '教师')
      : (live.participants?.find(p => p.userId === userId)?.userName || '学生');

    this.liveService.getWebSocketToken(this.liveId).subscribe({
      next: (tokenRes) => {
        if (this.destroyed) return;
        this.liveService.connect(
          this.liveId, tokenRes.token, tokenRes.wsUrl, this.myRole, userId, userName
        ).catch(err => this.message.error(err.message || '连接失败'));
      },
      error: () => this.message.error('获取连接令牌失败'),
    });
  }

  /** 全屏变化同步按钮状态 */
  private onFullscreenChange = () => {
    if (this.destroyed) return;
    this.isFullscreen.set(!!document.fullscreenElement);
  };

  /** 全屏 / 退出全屏（作用于整个房间，控件保留可见） */
  toggleFullscreen(roomEl: HTMLElement): void {
    if (document.fullscreenElement) {
      document.exitFullscreen().catch(() => undefined);
    } else if (roomEl.requestFullscreen) {
      roomEl.requestFullscreen().catch(() => this.message.warning('当前浏览器不支持全屏'));
    } else {
      this.message.warning('当前浏览器不支持全屏');
    }
  }

  goBack() {
    this.liveService.disconnect();
    this.router.navigate(this.myRole === 'teacher'
      ? ['/admin/recruitment-live']
      : ['/student/recruitment-live']);
  }

  stopLive() {
    // 兼容保留：实际结束走 confirmEndLive（带确认弹窗）
    this.confirmEndLive();
  }

  /** 确认结束直播：解散房间 + 直接回列表，不展示“已结束”提示页 */
  confirmEndLive(): void {
    this.endConfirmVisible = false;
    // 先标记，压住 hangUp() 同步触发的 ended 提示页，直接跳列表
    this.endingLive = true;
    this.liveService.hangUp();
    this.liveService.endLive(this.liveId).subscribe({
      error: () => console.warn('[LiveRoom] endLive failed'),
    });
    this.router.navigate(this.myRole === 'teacher'
      ? ['/admin/recruitment-live']
      : ['/student/recruitment-live']);
  }

  sendChatMessage() {
    if (!this.chatInput.trim()) return;
    this.liveService.sendChat(this.chatInput.trim());
    this.chatInput = '';
    this.scrollToBottom();
  }

  onChatKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter') this.sendChatMessage();
  }

  /** 切换摄像头：失败时给提示（如设备只有一个摄像头或被占用） */
  async onSwitchCamera(): Promise<void> {
    const ok = await this.liveService.switchCamera();
    if (!ok) this.message.warning('切换摄像头失败，当前设备可能只有一个摄像头');
  }

  /**
   * 切换扬声器：服务层负责改远端音频轨的 enabled（真正切断声音），
   * 组件层再显式把 muted / volume 写到媒体元素上，兼容 Firefox 的动态静音判定。
   */
  toggleSpeaker(): void {
    this.liveService.toggleSpeaker();
    this.applySpeakerMuteToMediaElements();
    // 同一帧内可能有元素刚被创建（布局切换 / 新流到达），等渲染完成后再补一次
    setTimeout(() => this.applySpeakerMuteToMediaElements(), 0);
  }

  /**
   * 把扬声器开关状态强制同步到所有**远端**媒体元素。
   * Firefox 对含音频轨的媒体元素存在“已 muted 仍上报正在播放声音”的判定
   * （See Bugzilla 1190023 / 1235612），故除 muted 外再把 volume 置 0，
   * 让标签页声音图标可靠消失。本地预览（.local-preview）不参与，避免误开本地回环。
   */
  private applySpeakerMuteToMediaElements(): void {
    const root = this.hostEl?.nativeElement as HTMLElement | null;
    if (!root) return;
    const enabled = this.speakerEnabled();
    root.querySelectorAll<HTMLVideoElement>('video:not(.local-preview)').forEach(video => {
      if (video.muted !== !enabled) video.muted = !enabled;
      video.volume = enabled ? 1 : 0;
    });
  }

  /** 获取指定参与者的流 */
  getStreamFor(userId: string): MediaStream | null {
    return this.remoteStreams().find(s => s.userId === userId)?.stream || null;
  }

  /** 获取流状态 */
  getStreamState(userId: string): string {
    return this.remoteStreams().find(s => s.userId === userId)?.connectionState || 'connecting';
  }

  /**
   * 远端视频元数据就绪时主动播放。
   * Android 浏览器（尤其默认/厂商浏览器）比 iOS 更严格：带声音的远端视频可能
   * 被自动播放策略拦截而黑屏。这里先尝试正常播放；被拦截时降级为静音播放保证
   * 画面可见，用户可通过“扬声器”按钮（一次手势）恢复声音。
   */
  onRemoteVideoLoaded(event: Event): void {
    const video = event.target as HTMLVideoElement | null;
    if (!video) return;
    video.play().catch(() => {
      console.warn('[LiveRoom] 自动播放被拦截，降级为静音播放，请点击扬声器按钮开启声音');
      video.muted = true;
      this.liveService.speakerEnabled.set(false);
      video.play().catch(() => undefined);
    });
  }
}