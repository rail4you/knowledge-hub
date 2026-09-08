import {
  Component, OnInit, OnDestroy, AfterViewChecked, ChangeDetectionStrategy,
  inject, signal, computed, ViewChild, ElementRef, HostListener, Renderer2
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule, NavigationStart } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzInputModule } from 'ng-zorro-antd/input';
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

  /** 计算远程参与者人数 */
  readonly participantCount = computed(() => this.participants().length);

  /** 是否显示 gallery 网格（多人模式） */
  readonly isGroupCall = computed(() => this.participantCount() > 1);

  /** Gallery 布局列数 */
  readonly galleryCols = computed(() => {
    const count = this.participantCount() + 1; // +1 为自己
    if (count <= 2) return 1;
    if (count <= 4) return 2;
    return 3;
  });

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
        this.myRole = live.teacherId === userId ? 'teacher' : 'student';

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

  ngOnDestroy() {
    this.destroyed = true;
    this.routerSub?.unsubscribe();
    this.footerObserver?.disconnect();
    window.removeEventListener('resize', this.updateMaxHeight);
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

  goBack() {
    this.liveService.disconnect();
    this.router.navigate(this.myRole === 'teacher'
      ? ['/admin/recruitment-live']
      : ['/student/recruitment-live']);
  }

  stopLive() {
    this.liveService.hangUp();
    this.liveService.endLive(this.liveId).subscribe({
      error: () => console.warn('[LiveRoom] endLive failed'),
    });
    this.router.navigate(['/admin/recruitment-live']);
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

  /** 获取指定参与者的流 */
  getStreamFor(userId: string): MediaStream | null {
    return this.remoteStreams().find(s => s.userId === userId)?.stream || null;
  }

  /** 获取流状态 */
  getStreamState(userId: string): string {
    return this.remoteStreams().find(s => s.userId === userId)?.connectionState || 'connecting';
  }
}