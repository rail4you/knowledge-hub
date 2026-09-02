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
import { RecruitmentLiveDto, RemoteParticipantStream, ParticipantBriefDto } from './recruitment-live.models';
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
  liveService = inject(RecruitmentLiveService);
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
    this.liveService.disconnect();
  }

  private joinLive(userId: string, live: RecruitmentLiveDto) {
    const userName = live.teacherId === userId
      ? (live.teacherName || '教师')
      : (live.participants?.find(p => p.userId === userId)?.userName || '学生');

    this.liveService.getWebSocketToken(this.liveId).subscribe({
      next: (tokenRes) => {
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