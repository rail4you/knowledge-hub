import { Injectable, inject, signal, computed } from '@angular/core';
import { RestService, PagedResultDto } from '@abp/ng.core';
import { BehaviorSubject, Subject, firstValueFrom } from 'rxjs';
import {
  RecruitmentLiveDto, CreateRecruitmentLiveDto, UpdateRecruitmentLiveDto,
  UserBriefDto, PagedRecruitmentLiveRequestDto, IceServerDto, WsTokenDto, ChatMessage
} from './recruitment-live.models';

export type LiveState = 'idle' | 'connecting' | 'waiting' | 'signaling' | 'connected' | 'disconnected' | 'ended';

@Injectable({ providedIn: 'root' })
export class RecruitmentLiveService {
  private readonly restService = inject(RestService);
  private readonly apiName = 'KnowledgeHub';

  // ── REST API ──

  getLive = (id: string) =>
    this.restService.request<any, RecruitmentLiveDto>({
      method: 'GET', url: `/api/app/recruitment-live/${id}/live`,
    }, { apiName: this.apiName });

  getTeacherLives = (input: PagedRecruitmentLiveRequestDto) =>
    this.restService.request<any, PagedResultDto<RecruitmentLiveDto>>({
      method: 'GET', url: '/api/app/recruitment-live/teacher-lives', params: input as any,
    }, { apiName: this.apiName });

  getStudentLives = (input: PagedRecruitmentLiveRequestDto) =>
    this.restService.request<any, PagedResultDto<RecruitmentLiveDto>>({
      method: 'GET', url: '/api/app/recruitment-live/student-lives', params: input as any,
    }, { apiName: this.apiName });

  createLive = (input: CreateRecruitmentLiveDto) =>
    this.restService.request<any, RecruitmentLiveDto[]>({
      method: 'POST', url: '/api/app/recruitment-live/live', body: input,
    }, { apiName: this.apiName });

  updateLive = (id: string, input: UpdateRecruitmentLiveDto) =>
    this.restService.request<any, RecruitmentLiveDto>({
      method: 'PUT', url: `/api/app/recruitment-live/${id}/live`, body: input,
    }, { apiName: this.apiName });

  cancelLive = (id: string) =>
    this.restService.request<any, void>({
      method: 'POST', url: `/api/app/recruitment-live/${id}/cancel-live`,
    }, { apiName: this.apiName });

  deleteLive = (id: string) =>
    this.restService.request<any, void>({
      method: 'DELETE', url: `/api/app/recruitment-live/${id}/live`,
    }, { apiName: this.apiName });

  getWebSocketToken = (liveId: string) =>
    this.restService.request<any, WsTokenDto>({
      method: 'GET', url: `/api/app/recruitment-live/web-socket-token/${liveId}`,
    }, { apiName: this.apiName });

  getTenantStudents = (filter?: string) =>
    this.restService.request<any, UserBriefDto[]>({
      method: 'GET', url: '/api/app/recruitment-live/tenant-students',
      params: { filter: filter || '' },
    }, { apiName: this.apiName });

  getIceServers = () =>
    this.restService.request<any, IceServerDto[]>({
      method: 'GET', url: '/api/app/recruitment-live/ice-servers',
    }, { apiName: this.apiName });

  getChatHistory = (liveId: string) =>
    this.restService.request<any, { senderRole: string; content: string; sentAt: string }[]>({
      method: 'GET', url: `/api/app/recruitment-live/chat-messages/${liveId}`,
    }, { apiName: this.apiName });

  /** REST 持久化聊天消息（唯一持久化路径，WS 只转发不保存） */
  saveChatMessage = (liveId: string, content: string) =>
    this.restService.request<any, void>({
      method: 'POST', url: `/api/app/recruitment-live/save-chat-message/${liveId}`,
      body: { content },
    }, { apiName: this.apiName });

  // ── WebRTC State ──

  readonly liveState = signal<LiveState>('idle');
  readonly localStreamReady = signal(false);
  readonly micEnabled = signal(true);
  readonly camEnabled = signal(true);
  readonly chatOpen = signal(false);
  readonly chatMessages = signal<ChatMessage[]>([]);
  readonly callDurationSec = signal(0);
  readonly remoteStream = signal<MediaStream | null>(null);
  readonly connectionLabel = signal('');

  private ws: WebSocket | null = null;
  private pc: RTCPeerConnection | null = null;
  private localStream: MediaStream | null = null;
  private timerInterval: ReturnType<typeof setInterval> | null = null;
  private retryCount = 0;
  private currentFacingMode: 'user' | 'environment' = 'user';
  private liveId = '';
  private myRole: 'teacher' | 'student' = 'student';

  async connect(liveId: string, wsToken: string, wsUrl: string, role: 'teacher' | 'student'): Promise<void> {
    this.liveId = liveId;
    this.myRole = role;
    this.liveState.set('connecting');

    // 获取本地媒体（摄像头/麦克风），允许降级
    this.localStream = await this.acquireMediaStream();
    this.localStreamReady.set(this.localStream !== null);

    this.liveState.set('waiting');

    // 先创建 PeerConnection（避免 user-joined 到达时 this.pc 还是 null）
    await this.createPeerConnection();
    // 再连接 WebSocket（聊天走 WebSocket，不受媒体影响）
    await this.connectWebSocket(wsUrl, wsToken);

    if (role === 'teacher') {
      if (this.liveState() === 'signaling' || this.liveState() === 'connected') {
        await this.createAndSendOffer();
      }
    }

    if (this.liveState() === 'waiting') {
      if (role === 'teacher') {
        this.connectionLabel.set('等待学生加入...');
      } else {
        this.connectionLabel.set('等待教师发起连接...');
      }
    }
  }

  /** 获取媒体流，逐级降级：video+audio → video only → audio only → null（纯文字聊天） */
  private async acquireMediaStream(): Promise<MediaStream | null> {
    const constraints: MediaStreamConstraints[] = [
      { video: { width: { ideal: 1280 }, height: { ideal: 720 }, facingMode: 'user' }, audio: true },
      // 麦克风被拒但摄像头可用时，降级为只有视频
      { video: { width: { ideal: 1280 }, height: { ideal: 720 }, facingMode: 'user' }, audio: false },
      { audio: true },
    ];
    for (const c of constraints) {
      try {
        return await navigator.mediaDevices.getUserMedia(c);
      } catch {
        // 尝试下一个降级方案
      }
    }
    // 全部失败，返回 null（纯文字聊天模式）
    console.warn('[Live] No camera/mic available, text-only mode');
    return null;
  }

  private connectWebSocket(wsUrl: string, wsToken: string): Promise<void> {
    return new Promise((resolve, reject) => {
      // 开发环境：通过 Angular dev server proxy (HTTP/1.1) 转发 WebSocket，
      // 避免浏览器直接连接 wss:// Kestrel 时 HTTP/2 导致 WebSocket 升级失败
      if (wsUrl.includes('localhost:44305')) {
        wsUrl = wsUrl.replace('wss://localhost:44305', 'ws://localhost:4200');
      }
      const url = `${wsUrl}?token=${encodeURIComponent(wsToken)}&liveId=${this.liveId}`;
      console.log('[LiveWS] Connecting to:', url);
      this.ws = new WebSocket(url);

      let opened = false;

      this.ws.onopen = () => {
        opened = true;
        console.log('[LiveWS] Connected successfully');
        this.flushPendingMessages();
        resolve();
      };

      this.ws.onerror = (e) => {
        console.error('[LiveWS] Connection error:', e);
        reject(new Error('无法连接信令服务器'));
      };

      this.ws.onclose = (e) => {
        console.log('[LiveWS] Closed. code=', e.code, 'reason=', e.reason, 'wasClean=', e.wasClean);
        if (!opened) {
          // 连接从未成功打开（服务器拒绝或网络不通）
          reject(new Error(`信令连接关闭 (code=${e.code}): ${e.reason || '未知原因'}`));
        } else if (this.liveState() !== 'ended') {
          this.liveState.set('disconnected');
        }
      };

      this.ws.onmessage = (event) => {
        console.log('[LiveWS] Received:', event.data);
        // 捕获异步处理中的错误，防止影响后续消息
        this.handleSignalMessage(event.data).catch(err =>
          console.error('[LiveWS] handleSignalMessage error:', err));
      };
    });
  }

  private async createPeerConnection(): Promise<void> {
    const iceServers = await this.getIceServersConfig();
    this.pc = new RTCPeerConnection({ iceServers });

    // 有本地流才加音视频轨；没有则纯 WebSocket 聊天
    if (this.localStream) {
      this.localStream.getTracks().forEach(track => this.pc!.addTrack(track, this.localStream!));
    }

    this.pc.onicecandidate = (e) => {
      if (e.candidate) {
        this.sendWs({ type: 'ice-candidate', data: e.candidate });
      }
    };

    this.pc.ontrack = (e) => {
      this.remoteStream.set(e.streams[0]);
    };

    this.pc.onconnectionstatechange = () => {
      const state = this.pc?.connectionState;
      switch (state) {
        case 'connected':
          this.liveState.set('connected');
          this.connectionLabel.set('已连接');
          this.retryCount = 0;
          this.startTimer();
          break;
        case 'connecting':
          this.liveState.set('signaling');
          this.connectionLabel.set('连接中...');
          break;
        case 'disconnected':
        case 'failed':
          // 纯文字聊天模式不报错
          if (!this.localStream) {
            this.liveState.set('connected');
            this.connectionLabel.set('文字聊天中');
            this.startTimer();
          } else {
            this.connectionLabel.set('已断开');
            this.handleConnectionFailure();
          }
          break;
      }
    };
  }

  private async getIceServersConfig(): Promise<RTCIceServer[]> {
    try {
      const servers = await firstValueFrom(this.getIceServers());
      return servers.map(s => ({
        urls: s.urls,
        username: s.username,
        credential: s.credential,
      }));
    } catch {
      return [{ urls: 'stun:stun.l.google.com:19302' }];
    }
  }

  private async handleSignalMessage(raw: string) {
    let msg: any;
    try { msg = JSON.parse(raw); } catch { return; }

    switch (msg.type) {
      case 'user-joined':
        this.connectionLabel.set('对方已加入，正在建立连接...');
        this.liveState.set('signaling');
        if (this.myRole === 'teacher') {
          await this.createAndSendOffer();
        }
        break;

      case 'offer':
        console.log('[LiveWS] Received offer, creating answer...');
        // 若之前的 PC 已失败/关闭，先重建再应答，避免 setRemoteDescription 抛错导致死局
        if (!this.pc || this.pc.connectionState === 'failed' || this.pc.connectionState === 'closed') {
          this.offerSent = false;
          if (this.pc) {
            try { this.pc.close(); } catch {}
            this.pc = null;
          }
          await this.createPeerConnection();
        }
        try {
          await this.pc!.setRemoteDescription(new RTCSessionDescription(msg.data));
          const answer = await this.pc!.createAnswer();
          await this.pc!.setLocalDescription(answer);
          this.sendWs({ type: 'answer', data: answer });
          this.liveState.set('signaling');
          console.log('[LiveWS] Answer sent');
        } catch (e) {
          console.error('[LiveWS] Failed to handle offer:', e);
        }
        break;

      case 'answer':
        console.log('[LiveWS] Received answer');
        if (this.pc) {
          try {
            await this.pc.setRemoteDescription(new RTCSessionDescription(msg.data));
            console.log('[LiveWS] Remote description set from answer');
          } catch (e) {
            console.error('[LiveWS] Failed to set remote description from answer:', e);
          }
        }
        break;

      case 'ice-candidate':
        if (this.pc && msg.data) {
          try {
            await this.pc.addIceCandidate(new RTCIceCandidate(msg.data));
          } catch (e) {
            console.warn('[LiveWS] Failed to add ICE candidate:', e);
          }
        }
        break;

      case 'chat':
        console.log('[LiveWS] Chat received. self?', !!msg.self, 'from:', msg.from, 'data:', msg.data);
        // 跳过服务端 echo（已通过乐观添加显示）
        if (!!msg.self) {
          console.log('[LiveWS] Skipping self-echo');
          break;
        }
        this.chatMessages.update(msgs => {
          const newMsgs = [...msgs, {
            text: msg.data,
            from: msg.from || '',
            self: false,
            time: Date.now(),
          }];
          console.log('[LiveWS] Chat messages updated, count:', newMsgs.length);
          return newMsgs;
        });
        // 收到对方消息时自动打开聊天面板
        if (!this.chatOpen()) {
          this.chatOpen.set(true);
        }
        break;

      case 'user-left':
        this.liveState.set('disconnected');
        this.connectionLabel.set(msg.reason || '对方已断开连接');
        break;

      case 'hang-up':
        this.liveState.set('ended');
        this.connectionLabel.set(msg.reason || '对方已挂断');
        this.disconnect();
        break;

      case 'error':
        this.connectionLabel.set('错误: ' + msg.message);
        break;
    }
  }

  private offerSent = false;
  /** 待发送消息队列：WS 未就绪时暂存 */
  private pendingMessages: any[] = [];

  private flushPendingMessages() {
    if (this.ws?.readyState !== WebSocket.OPEN) return;
    for (const msg of this.pendingMessages) {
      const json = JSON.stringify(msg);
      this.ws.send(json);
    }
    this.pendingMessages = [];
  }

  private async createAndSendOffer() {
    if (!this.pc) {
      console.warn('[LiveWS] createAndSendOffer: PC not ready yet, will retry when ready');
      return;
    }
    if (this.offerSent) return; // 避免重复发送
    if (this.pc.signalingState !== 'stable') {
      console.warn('[LiveWS] createAndSendOffer: signalingState is', this.pc.signalingState, ', skipping');
      return;
    }
    this.offerSent = true;
    console.log('[LiveWS] Creating offer...');
    try {
      const offer = await this.pc.createOffer();
      await this.pc.setLocalDescription(offer);
      this.sendWs({ type: 'offer', data: offer });
      console.log('[LiveWS] Offer sent');
    } catch (e) {
      console.error('[LiveWS] Failed to create/send offer:', e);
      this.offerSent = false;
    }
  }

  private sendWs(obj: any) {
    if (this.ws?.readyState === WebSocket.OPEN) {
      const json = JSON.stringify(obj);
      console.log('[LiveWS] Sending:', json);
      this.ws.send(json);
      // 发送成功后尝试刷新待发送队列
      this.flushPendingMessages();
    } else {
      console.warn('[LiveWS] Queuing message, WS state:', this.ws?.readyState, 'msg type:', obj.type);
      this.pendingMessages.push(obj);
    }
  }

  private wsUrl = '';
  private wsToken = '';
  private pendingLiveId = '';
  private pendingRole: 'teacher' | 'student' = 'student';

  toggleMic() {
    if (!this.localStream) return;
    this.micEnabled.update(v => !v);
    this.localStream.getAudioTracks().forEach(t => (t.enabled = this.micEnabled()));
  }

  toggleCam() {
    if (!this.localStream) return;
    this.camEnabled.update(v => !v);
    this.localStream.getVideoTracks().forEach(t => (t.enabled = this.camEnabled()));
  }

  async switchCamera() {
    if (!this.localStream) return;
    this.currentFacingMode = this.currentFacingMode === 'user' ? 'environment' : 'user';
    try {
      const newStream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: this.currentFacingMode, width: { ideal: 1280 }, height: { ideal: 720 } },
        audio: false,
      });
      const newTrack = newStream.getVideoTracks()[0];
      if (this.pc) {
        const sender = this.pc.getSenders().find(s => s.track?.kind === 'video');
        if (sender) await sender.replaceTrack(newTrack);
      }
      const oldTrack = this.localStream.getVideoTracks()[0];
      if (oldTrack) {
        oldTrack.stop();
        this.localStream.removeTrack(oldTrack);
      }
      this.localStream.addTrack(newTrack);
    } catch {}
  }

  toggleChat() {
    this.chatOpen.update(v => !v);
  }

  sendChat(text: string) {
    if (!text.trim()) return;
    const msg = text.trim();
    // 乐观添加：立即显示在本地
    this.chatMessages.update(msgs => [...msgs, {
      text: msg,
      from: this.myRole,
      self: true,
      time: Date.now(),
    }]);
    // WebSocket 发送（只负责实时转发，不持久化）
    this.sendWs({ type: 'chat', data: msg });
    // REST 持久化（唯一持久化路径）
    this.saveChatMessage(this.liveId, msg).subscribe({
      error: () => console.warn('[Live] REST save failed'),
    });
  }

  hangUp() {
    this.sendWs({ type: 'hang-up' });
    this.liveState.set('ended');
    this.disconnect();
  }

  disconnect() {
    this.stopTimer();
    if (this.pc) { this.pc.close(); this.pc = null; }
    if (this.ws) { this.ws.close(); this.ws = null; }
    if (this.localStream) {
      this.localStream.getTracks().forEach(t => t.stop());
      this.localStream = null;
    }
    this.localStreamReady.set(false);
    this.remoteStream.set(null);
    this.chatMessages.set([]);
    this.retryCount = 0;
    this.offerSent = false;
  }

  private handleConnectionFailure() {
    if (this.liveState() === 'ended') return;
    this.retryCount++;
    if (this.retryCount <= 2) {
      this.connectionLabel.set(`连接失败，正在重试(${this.retryCount}/2)...`);
      setTimeout(() => {
        // 仅当连接真的未建立时才重试（避免协商过程中误触发）
        if (!this.pc
          || this.pc.connectionState === 'failed'
          || this.pc.connectionState === 'disconnected'
          || this.pc.connectionState === 'closed') {
          this.retryPeerConnection();
        }
      }, 2000);
    } else {
      this.liveState.set('disconnected');
      this.connectionLabel.set('连接失败，请挂断后重试');
    }
  }

  /** 重建 PeerConnection 并重新协商：重置 offerSent、重建 PC、教师侧重新发起 offer */
  private async retryPeerConnection() {
    this.offerSent = false;
    if (this.pc) {
      try { this.pc.close(); } catch {}
      this.pc = null;
    }
    this.remoteStream.set(null);
    this.connectionLabel.set('重新连接中...');
    await this.createPeerConnection();
    if (this.myRole === 'teacher') {
      await this.createAndSendOffer();
    }
  }

  private startTimer() {
    this.stopTimer();
    const start = Date.now();
    this.timerInterval = setInterval(() => {
      this.callDurationSec.set(Math.floor((Date.now() - start) / 1000));
    }, 1000);
  }

  private stopTimer() {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
      this.timerInterval = null;
    }
  }

  getLocalStream(): MediaStream | null {
    return this.localStream;
  }
}
