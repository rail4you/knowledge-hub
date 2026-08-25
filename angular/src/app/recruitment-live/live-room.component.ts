import {
  Component, OnInit, OnDestroy, AfterViewChecked, ChangeDetectionStrategy, inject, signal, computed, ViewChild, ElementRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzInputModule } from 'ng-zorro-antd/input';
import { FormsModule } from '@angular/forms';
import { RecruitmentLiveService, LiveState } from './recruitment-live.service';
import { RecruitmentLiveDto } from './recruitment-live.models';
import { ConfigStateService } from '@abp/ng.core';
import { NzMessageService } from 'ng-zorro-antd/message';

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
  private liveService = inject(RecruitmentLiveService);
  private configState = inject(ConfigStateService);
  private message = inject(NzMessageService);

  liveId = '';
  live: RecruitmentLiveDto | null = null;
  myRole: 'teacher' | 'student' = 'student';
  chatInput = '';

  readonly liveState = this.liveService.liveState;
  readonly micEnabled = this.liveService.micEnabled;
  readonly camEnabled = this.liveService.camEnabled;
  readonly chatOpen = this.liveService.chatOpen;
  readonly chatMessages = this.liveService.chatMessages;
  readonly callDurationSec = this.liveService.callDurationSec;
  readonly remoteStream = this.liveService.remoteStream;
  readonly connectionLabel = this.liveService.connectionLabel;

  readonly localStreamActive = computed(() => this.liveService.localStreamReady());
  readonly isCallActive = computed(() =>
    this.liveState() === 'connected' || this.liveState() === 'signaling' || this.liveState() === 'disconnected'
  );
  readonly canHangUp = computed(() =>
    this.liveState() !== 'idle' && this.liveState() !== 'ended'
  );

  readonly formattedTime = computed(() => {
    const s = this.callDurationSec();
    const m = Math.floor(s / 60);
    return `${String(m).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
  });

  readonly remoteVideoActive = computed(() => this.remoteStream() !== null);

  @ViewChild('chatMessagesContainer', { static: false }) chatMessagesContainer!: ElementRef;
  private previousMsgCount = 0;

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
    return this.myRole === 'teacher' ? '等待学生加入...' : '等待教师发起连接...';
  });

  readonly roomCode = computed(() => this.live?.roomCode || '');

  ngOnInit() {
    this.liveId = this.route.snapshot.paramMap.get('id') || '';
    if (!this.liveId) {
      this.message.error('无效的直播间');
      this.router.navigate(['/']);
      return;
    }

    this.liveService.getLive(this.liveId).subscribe({
      next: (live) => {
        const currentUser = this.configState.getDeep('currentUser') as any;
        const userId = currentUser?.id;

        // 权限校验：参与者本人或管理员均可进入
        const isOwner = live.teacherId === userId || live.studentId === userId;
        if (!isOwner) {
          // 检查是否为管理员（通过 isParticipant 判断，API 在非参与者但管理员时设为 teacher）
          if (!live.isParticipant) {
            this.message.error('您没有权限进入该直播间');
            this.router.navigate(this.myRole === 'teacher'
              ? ['/admin/recruitment-live']
              : ['/student/recruitment-live']);
            return;
          }
        }

        this.live = live;
        this.myRole = live.teacherId === userId ? 'teacher' : 'student';

        if (live.status === 2 || live.status === 3) {
          this.message.warning('该直播已结束或已取消');
          return;
        }

        // 先加载历史聊天消息，完成后再连接 WebSocket，避免历史消息覆盖实时消息
        this.loadChatHistory(() => {
          this.joinLive();
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
        }));
        this.liveService.chatMessages.set(chatMsgs);
        // 有历史消息时自动打开聊天面板并滚动到底部
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
    this.liveService.disconnect();
  }

  private joinLive() {
    this.liveService.getWebSocketToken(this.liveId).subscribe({
      next: (tokenRes) => {
        this.liveService.connect(this.liveId, tokenRes.token, tokenRes.wsUrl, this.myRole)
          .catch(err => this.message.error(err.message || '连接失败'));
      },
      error: () => this.message.error('获取连接令牌失败'),
    });
  }

  hangUp() {
    this.liveService.hangUp();
    this.message.info('已挂断');
  }

  goBack() {
    this.liveService.disconnect();
    this.router.navigate(this.myRole === 'teacher'
      ? ['/admin/recruitment-live']
      : ['/student/recruitment-live']);
  }

  copyRoomCode() {
    navigator.clipboard.writeText(this.live?.roomCode || '').then(
      () => this.message.success('房间码已复制'),
      () => this.message.error('复制失败')
    );
  }

  sendChatMessage() {
    if (!this.chatInput.trim()) return;
    this.liveService.sendChat(this.chatInput.trim());
    this.chatInput = '';
    this.scrollToBottom();
  }

  onChatKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter') {
      this.sendChatMessage();
    }
  }
}
