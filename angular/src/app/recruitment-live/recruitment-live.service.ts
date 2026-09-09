import { Injectable, inject, signal, computed } from '@angular/core';
import { RestService, PagedResultDto } from '@abp/ng.core';
import { BehaviorSubject, Subject, firstValueFrom } from 'rxjs';
import {
  RecruitmentLiveDto, CreateRecruitmentLiveDto, UpdateRecruitmentLiveDto,
  UserBriefDto, PagedRecruitmentLiveRequestDto, IceServerDto, WsTokenDto, ChatMessage,
  RemoteParticipantStream, ParticipantBriefDto
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

  endLive = (id: string) =>
    this.restService.request<any, void>({
      method: 'POST', url: `/api/app/recruitment-live/${id}/end-live`,
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
    this.restService.request<any, { senderRole: string; content: string; sentAt: string; senderId?: string }[]>({
      method: 'GET', url: `/api/app/recruitment-live/chat-messages/${liveId}`,
    }, { apiName: this.apiName });

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
  /**
   * 远端音频输出（扬声器）开关。
   * 与 micEnabled 互不影响：关闭麦克风不会关闭扬声器，反之亦然。
   */
  readonly speakerEnabled = signal(true);
  readonly chatOpen = signal(false);
  readonly chatMessages = signal<ChatMessage[]>([]);
  readonly callDurationSec = signal(0);
  readonly connectionLabel = signal('');

  /** 所有远程参与者的视频流列表（除自己以外的所有人） */
  readonly remoteStreams = signal<RemoteParticipantStream[]>([]);

  /** 参与者列表（由 WebSocket 发送） */
  readonly participants = signal<ParticipantBriefDto[]>([]);

  /** 本地用户的信息 */
  myUserId = '';
  myUserName = '';
  myRole: 'teacher' | 'student' = 'student';

  private ws: WebSocket | null = null;
  /** 每个远程参与者对应一个 PeerConnection */
  private peerConnections = new Map<string, RTCPeerConnection>();
  private localStream: MediaStream | null = null;
  private timerInterval: ReturnType<typeof setInterval> | null = null;
  private retryCount = 0;
  private currentFacingMode: 'user' | 'environment' = 'user';
  private liveId = '';
  private iceServers: RTCIceServer[] = [];
  /**
   * 在 PC 还未设置 remoteDescription 时缓存远端 ICE candidates，
   * 等 setRemoteDescription 完成后再回放。
   * 修复“ICE candidates 在 handshake 完成前到达被丢弃”导致的连接失败。
   */
  private pendingIceCandidates = new Map<string, RTCIceCandidate[]>();

  /**
   * 会话代际：每次 connect()/disconnect() 递增。
   * 旧会话的 WS 回调（onclose/onmessage 定时器）到达时直接丢弃，
   * 避免“重进后旧 socket 的 onclose 把新会话状态复位为 disconnected”。
   */
  private sessionSeq = 0;
  private activeSession = 0;

  /** offer 自愈：每个远端用户一个超时定时器，offer 丢失时自动补救 */
  private offerWatchTimers = new Map<string, ReturnType<typeof setTimeout>>();
  private offerWatchRetries = new Map<string, number>();
  /** 已从该远端收到过 offer（无需再请求补发） */
  private offerReceivedFrom = new Set<string>();

  async connect(liveId: string, wsToken: string, wsUrl: string, role: 'teacher' | 'student', userId: string, userName: string): Promise<void> {
    // 新会话：让旧会话残留的 WS 回调/定时器失效
    this.activeSession = ++this.sessionSeq;
    this.liveId = liveId;
    this.myRole = role;
    this.myUserId = userId;
    this.myUserName = userName;
    this.liveState.set('connecting');
    this.callDurationSec.set(0);
    this.connectionLabel.set('正在连接...');
    this.offerReceivedFrom.clear();
    this.offerWatchRetries.clear();

    // ICE 配置与本地媒体并行获取，缩短进房等待
    const [iceServers, stream] = await Promise.all([
      this.getIceServersConfig(),
      this.acquireMediaStream(),
    ]);
    if (this.activeSession !== this.sessionSeq) return; // 连接过程中被断开，直接放弃
    this.iceServers = iceServers;
    this.localStream = stream;
    this.localStreamReady.set(this.localStream !== null);

    this.liveState.set('waiting');

    // 连接 WebSocket
    await this.connectWebSocket(wsUrl, wsToken);

    if (this.liveState() === 'waiting') {
      this.connectionLabel.set(role === 'teacher' ? '等待学生加入...' : '等待教师发起连接...');
    }
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

  private async acquireMediaStream(): Promise<MediaStream | null> {
    const constraints: MediaStreamConstraints[] = [
      { video: { width: { ideal: 1280 }, height: { ideal: 720 }, facingMode: 'user' }, audio: true },
      { video: { width: { ideal: 1280 }, height: { ideal: 720 }, facingMode: 'user' }, audio: false },
      { audio: true },
    ];
    for (const c of constraints) {
      try {
        return await navigator.mediaDevices.getUserMedia(c);
      } catch { /* try next */ }
    }
    console.warn('[Live] No camera/mic available, text-only mode');
    return null;
  }

  /**
   * 解析信令 WS 地址。
   * 后端下发的 wsUrl 基于服务端视角的 SelfUrl（开发环境是 localhost:44305）。
   * 当它指向 localhost/127.0.0.1 时，改用当前页面 origin（protocol + host），
   * 这样手机经局域网 IP / 端口转发打开页面时，信令也能连上同一 dev server 代理。
   */
  private resolveWsUrl(wsUrl: string): string {
    try {
      const u = new URL(wsUrl);
      const isLoopback = u.hostname === 'localhost' || u.hostname === '127.0.0.1' || u.hostname === '[::1]';
      if (isLoopback && typeof window !== 'undefined' && window.location?.host) {
        const proto = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
        return `${proto}//${window.location.host}${u.pathname}`;
      }
    } catch { /* 非法 URL 则保持原样 */ }
    return wsUrl;
  }

  private connectWebSocket(wsUrl: string, wsToken: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const session = this.activeSession;
      wsUrl = this.resolveWsUrl(wsUrl);
      const url = `${wsUrl}?token=${encodeURIComponent(wsToken)}&liveId=${this.liveId}`;
      console.log('[LiveWS] Connecting to:', url);
      const wsRef = new WebSocket(url);
      this.ws = wsRef;

      let opened = false;

      const isStale = () => session !== this.activeSession || this.ws !== wsRef;

      wsRef.onopen = () => {
        if (isStale()) { try { wsRef.close(); } catch { /* ignore */ } return; }
        opened = true;
        console.log('[LiveWS] Connected successfully');
        this.flushPendingMessages();
        resolve();
      };

      wsRef.onerror = () => {
        if (isStale()) return;
        reject(new Error('无法连接信令服务器'));
      };

      wsRef.onclose = (e) => {
        // 旧会话残留的 close 事件不得覆盖新会话状态；主动 disconnect 也不再弹“断开”态
        if (isStale()) {
          console.log('[LiveWS] Stale socket closed. code=', e.code);
          return;
        }
        console.log('[LiveWS] Closed. code=', e.code);
        if (!opened) {
          reject(new Error(`信令连接关闭 (code=${e.code})`));
        } else if (this.liveState() !== 'ended' && this.liveState() !== 'idle') {
          this.liveState.set('disconnected');
        }
      };

      wsRef.onmessage = (event) => {
        if (isStale()) return;
        this.handleSignalMessage(event.data).catch(err =>
          console.error('[LiveWS] handleSignalMessage error:', err));
      };
    });
  }

  private async handleSignalMessage(raw: string) {
    let msg: any;
    try { msg = JSON.parse(raw); } catch { return; }

    switch (msg.type) {
      case 'participant-list':
        // 收到完整的参与者列表（在刚连接时由服务器发送）
        const list = (msg.participants || []) as Array<{
          userId: string; userName: string; role: string; you: boolean;
        }>;
        this.participants.set(list
          .filter(p => !p.you)
          .map(p => ({ userId: p.userId, userName: p.userName, role: p.role })));
        // 为列表中的每个人创建 PC（如果是教师则主动发起 offer，学生侧靠 offer-watch 自愈）
        for (const p of list) {
          if (!p.you && !this.peerConnections.has(p.userId)) {
            await this.createPeerConnectionFor(p.userId, p.role);
            if (this.myRole === 'teacher') {
              await this.sendOfferResilient(p.userId);
            } else {
              this.scheduleOfferWatch(p.userId);
            }
          }
        }
        break;

      case 'user-joined':
        // 新用户加入
        const newUserId = msg.userId;
        const newUserName = msg.userName;
        const newRole = msg.role;
        if (newUserId !== this.myUserId && !this.peerConnections.has(newUserId)) {
          // 更新参与者列表
          this.participants.update(list => [...list, { userId: newUserId, userName: newUserName, role: newRole }]);
          // 为此用户创建 PC
          await this.createPeerConnectionFor(newUserId, newRole);
          // 教师主动发起 offer；学生侧靠 offer-watch 自愈（offer 丢失时补发 request-offer）
          if (this.myRole === 'teacher') {
            await this.sendOfferResilient(newUserId);
          } else {
            this.scheduleOfferWatch(newUserId);
          }
          this.connectionLabel.set(`${newUserName} 已加入`);
          this.liveState.set('signaling');
        }
        break;

      case 'user-left':
        // 用户离开
        const leftUserId = msg.userId;
        if (leftUserId && leftUserId !== this.myUserId) {
          this.participants.update(list => list.filter(p => p.userId !== leftUserId));
          this.cancelOfferWatch(leftUserId);
          this.offerReceivedFrom.delete(leftUserId);
          this.closePeerConnection(leftUserId);
          // 移除对应的流
          this.remoteStreams.update(streams => streams.filter(s => s.userId !== leftUserId));
          this.connectionLabel.set(`${msg.reason || '对方已断开连接'}`);
        }
        break;

      case 'offer':
        await this.handleOffer(msg);
        break;

      case 'answer':
        await this.handleAnswer(msg);
        break;

      case 'ice-candidate':
        await this.handleIceCandidate(msg);
        break;

      case 'request-offer': {
        // 对方没收到 offer（offer 在传输中丢失 / 老 PC 已失效），为其（重）发一份
        const fromUserId = msg.fromUserId;
        if (!fromUserId || fromUserId === this.myUserId) break;
        console.log('[LiveWS] Received request-offer from', fromUserId);
        if (!this.peerConnections.has(fromUserId)) {
          await this.createPeerConnectionFor(fromUserId, msg.fromRole || 'student');
        }
        await this.sendOfferResilient(fromUserId);
        break;
      }

      case 'chat':
        if (!!msg.self) break;
        this.chatMessages.update(msgs => [...msgs, {
          text: msg.data,
          from: msg.from || '',
          self: false,
          time: Date.now(),
          fromUserName: msg.fromUserName || msg.from,
        }]);
        if (!this.chatOpen()) this.chatOpen.set(true);
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

  private async handleOffer(msg: any) {
    const fromUserId = msg.fromUserId;
    const fromUserName = msg.fromUserName || fromUserId;
    console.log('[LiveWS] Received offer from', fromUserId);

    if (fromUserId === this.myUserId) return;
    this.offerReceivedFrom.add(fromUserId);
    this.cancelOfferWatch(fromUserId);

    // 如果还没有此用户的 PC，先创建
    if (!this.peerConnections.has(fromUserId)) {
      await this.createPeerConnectionFor(fromUserId, msg.fromRole || 'student');
    }

    const pc = this.peerConnections.get(fromUserId);
    if (!pc) return;

    try {
      // Glare 处理：双方同时发 offer 时，只有一方（userId 较大者，polite）回退并应答，
      // 另一方忽略该 offer、等待自己的 offer 被应答，避免两边同时报错卡死。
      if (pc.signalingState !== 'stable') {
        const polite = this.myUserId > fromUserId;
        if (!polite) {
          console.log('[LiveWS] Glare: impolite side, ignore offer from', fromUserId,
            'state=', pc.signalingState);
          return;
        }
        console.log('[LiveWS] Glare: polite side, rollback for', fromUserId);
        await pc.setLocalDescription({ type: 'rollback' });
      }
      await pc.setRemoteDescription(new RTCSessionDescription(msg.data));
      // 回放在此期间累积的 ICE candidates
      await this.flushPendingIceCandidates(fromUserId, pc);
      const answer = await pc.createAnswer();
      await pc.setLocalDescription(answer);
      this.sendWs({ type: 'answer', data: answer, targetUserId: fromUserId });
      this.liveState.set('signaling');
      console.log('[LiveWS] Answer sent to', fromUserId);
    } catch (e) {
      console.error('[LiveWS] Failed to handle offer from', fromUserId, e);
    }
  }

  private async handleAnswer(msg: any) {
    const fromUserId = msg.fromUserId;
    if (fromUserId === this.myUserId || !fromUserId) return;
    console.log('[LiveWS] Received answer from', fromUserId);
    let pc = this.peerConnections.get(fromUserId);
    if (!pc) {
      // PC 已被关闭（如收到过期 user-left 后重建前），重建后若我是 offer 方则补发 offer
      console.log('[LiveWS] No PC for answer from', fromUserId, ', rebuild');
      await this.createPeerConnectionFor(fromUserId, msg.fromRole || 'student');
      pc = this.peerConnections.get(fromUserId);
      if (pc && this.myRole === 'teacher') {
        await this.sendOfferResilient(fromUserId);
        return;
      }
    }
    if (pc) {
      try {
        // 已 stable 说明是重复/过期 answer，直接忽略
        if (pc.signalingState === 'stable') {
          console.log('[LiveWS] Ignore duplicate answer from', fromUserId);
          return;
        }
        await pc.setRemoteDescription(new RTCSessionDescription(msg.data));
        // 回放在 setRemoteDescription 之前到达的 ICE candidates
        await this.flushPendingIceCandidates(fromUserId, pc);
        console.log('[LiveWS] Remote description set from answer', fromUserId);
      } catch (e) {
        console.error('[LiveWS] Failed to set remote description from answer:', e);
      }
    }
  }

  private async handleIceCandidate(msg: any) {
    const fromUserId = msg.fromUserId;
    if (fromUserId === this.myUserId || !fromUserId) return;
    if (!msg.data) return;
    let pc = this.peerConnections.get(fromUserId);
    if (!pc) {
      // candidate 比 offer 先到时先建 PC 再缓存，避免 candidate 被丢弃
      await this.createPeerConnectionFor(fromUserId, msg.fromRole || 'student');
      pc = this.peerConnections.get(fromUserId);
      if (!pc) return;
    }
    const candidate = new RTCIceCandidate(msg.data);
    // 如果 PC 还未设置 remoteDescription，则暂存 candidates，避免被丢弃导致连接失败
    if (!pc.remoteDescription) {
      const queue = this.pendingIceCandidates.get(fromUserId) ?? [];
      queue.push(candidate);
      this.pendingIceCandidates.set(fromUserId, queue);
      return;
    }
    try {
      await pc.addIceCandidate(candidate);
    } catch (e) {
      console.warn('[LiveWS] Failed to add ICE candidate from', fromUserId, e);
    }
  }

  /** 在 setRemoteDescription 完成后，回放缓存的 ICE candidates */
  private async flushPendingIceCandidates(fromUserId: string, pc: RTCPeerConnection): Promise<void> {
    const queue = this.pendingIceCandidates.get(fromUserId);
    if (!queue || queue.length === 0) return;
    this.pendingIceCandidates.delete(fromUserId);
    for (const c of queue) {
      try {
        await pc.addIceCandidate(c);
      } catch (e) {
        console.warn('[LiveWS] Failed to flush queued ICE candidate from', fromUserId, e);
      }
    }
  }

  private async createPeerConnectionFor(remoteUserId: string, remoteRole: string) {
    // 如果已经存在，先关闭
    this.closePeerConnection(remoteUserId);

    const pc = new RTCPeerConnection({ iceServers: this.iceServers });
    this.peerConnections.set(remoteUserId, pc);

    // 添加本地音视频轨（若有）。记录已发送的媒体类型，便于后续补齐 recvonly transceiver
    const sentKinds = new Set<string>();
    if (this.localStream) {
      this.localStream.getTracks().forEach(track => {
        pc.addTrack(track, this.localStream!);
        sentKinds.add(track.kind);
      });
    }
    // 若没有本地媒体，或本地缺少某种媒体，显式添加 recvonly transceiver，
    // 确保 PC 在协商时能正确接收对方的音频/视频，避免“学生看不到教师”这类问题
    if (!sentKinds.has('audio')) {
      pc.addTransceiver('audio', { direction: 'recvonly' });
    }
    if (!sentKinds.has('video')) {
      pc.addTransceiver('video', { direction: 'recvonly' });
    }

    pc.onicecandidate = (e) => {
      if (e.candidate) {
        this.sendWs({ type: 'ice-candidate', data: e.candidate, targetUserId: remoteUserId });
      }
    };

    pc.ontrack = (e) => {
      console.log('[LiveWS] Received track from', remoteUserId, e.streams[0]);
      const stream = e.streams[0];
      if (!stream) return;
      this.cancelOfferWatch(remoteUserId);
      // 同步扬声器状态：若用户已静音扬声器，新接收到的远端音频轨也应静音
      stream.getAudioTracks().forEach(t => (t.enabled = this.speakerEnabled()));
      this.remoteStreams.update(streams => {
        const existing = streams.find(s => s.userId === remoteUserId);
        if (existing) {
          existing.stream = stream;
          existing.connectionState = 'connected';
          return [...streams];
        }
        const participant = this.participants().find(p => p.userId === remoteUserId);
        return [...streams, {
          userId: remoteUserId,
          userName: participant?.userName || remoteRole,
          role: participant?.role || remoteRole,
          stream,
          connectionState: 'connected',
        }];
      });
      this.liveState.set('connected');
      this.connectionLabel.set('已连接');
      this.retryCount = 0;
      this.startTimer();
    };

    pc.onconnectionstatechange = () => {
      const state = pc.connectionState;
      console.log('[LiveWS] PC state for', remoteUserId, ':', state);
      this.remoteStreams.update(streams => {
        const s = streams.find(st => st.userId === remoteUserId);
        if (s) s.connectionState = state;
        return [...streams];
      });

      switch (state) {
        case 'connected':
          this.cancelOfferWatch(remoteUserId);
          if (this.liveState() !== 'connected') {
            this.liveState.set('connected');
            this.connectionLabel.set('已连接');
            this.retryCount = 0;
            this.startTimer();
          }
          break;
        case 'failed':
        case 'disconnected':
          this.handleConnectionFailure();
          break;
      }
    };
  }

  /**
   * 有韧性的 offer 发送：signalingState 非 stable（上一轮 offer 还在途）时
   * 先重建 PC 再发，避免静默丢弃导致对方永远等不到 offer。
   */
  private async sendOfferResilient(remoteUserId: string) {
    let pc = this.peerConnections.get(remoteUserId);
    if (!pc) {
      const role = this.participants().find(p => p.userId === remoteUserId)?.role || 'student';
      await this.createPeerConnectionFor(remoteUserId, role);
      pc = this.peerConnections.get(remoteUserId);
    }
    if (!pc) {
      console.warn('[LiveWS] sendOfferResilient: PC not found for', remoteUserId);
      return;
    }
    if (pc.signalingState !== 'stable') {
      console.log('[LiveWS] sendOfferResilient: recreate PC (state=', pc.signalingState, ') for', remoteUserId);
      const role = this.participants().find(p => p.userId === remoteUserId)?.role || 'student';
      await this.createPeerConnectionFor(remoteUserId, role);
      pc = this.peerConnections.get(remoteUserId);
      if (!pc) return;
    }
    console.log('[LiveWS] Creating offer for', remoteUserId);
    try {
      const offer = await pc.createOffer();
      await pc.setLocalDescription(offer);
      this.sendWs({ type: 'offer', data: offer, targetUserId: remoteUserId });
      console.log('[LiveWS] Offer sent to', remoteUserId);
    } catch (e) {
      console.error('[LiveWS] Failed to create/send offer for', remoteUserId, e);
    }
  }

  /**
   * offer-watch：建连后 2s 仍没收到远端媒体，则主动补救，最多补救 2 次。
   * 教师侧补发 offer，学生侧发送 request-offer 请对方补发。
   * 覆盖“offer/answer 在传输中丢失、对方 PC 已失效”等情况，无需手动重进。
   */
  private scheduleOfferWatch(remoteUserId: string) {
    this.cancelOfferWatch(remoteUserId);
    this.offerWatchRetries.set(remoteUserId, 0);
    this.offerWatchTimers.set(remoteUserId, setTimeout(() => this.runOfferWatch(remoteUserId), 2000));
  }

  private runOfferWatch(remoteUserId: string) {
    this.offerWatchTimers.delete(remoteUserId);
    if (this.liveState() === 'ended' || this.liveState() === 'idle') return;
    if (!this.peerConnections.has(remoteUserId)) return;
    const hasMedia = this.remoteStreams().some(s => s.userId === remoteUserId);
    if (hasMedia || this.offerReceivedFrom.has(remoteUserId)) return;
    const retries = this.offerWatchRetries.get(remoteUserId) ?? 0;
    if (retries >= 2) {
      this.connectionLabel.set('连接超时，请检查网络后重进');
      return;
    }
    this.offerWatchRetries.set(remoteUserId, retries + 1);
    console.log('[LiveWS] offer-watch retry', retries + 1, 'for', remoteUserId);
    if (this.myRole === 'teacher') {
      this.sendOfferResilient(remoteUserId).finally(() => {
        if (this.peerConnections.has(remoteUserId)) {
          this.offerWatchTimers.set(remoteUserId, setTimeout(() => this.runOfferWatch(remoteUserId), 4000));
        }
      });
    } else {
      this.sendWs({ type: 'request-offer', targetUserId: remoteUserId });
      this.connectionLabel.set('正在建立连接...');
      this.offerWatchTimers.set(remoteUserId, setTimeout(() => this.runOfferWatch(remoteUserId), 4000));
    }
  }

  private cancelOfferWatch(remoteUserId: string) {
    const t = this.offerWatchTimers.get(remoteUserId);
    if (t) {
      clearTimeout(t);
      this.offerWatchTimers.delete(remoteUserId);
    }
  }

  private cancelAllOfferWatches() {
    for (const t of this.offerWatchTimers.values()) clearTimeout(t);
    this.offerWatchTimers.clear();
    this.offerWatchRetries.clear();
  }

  private closePeerConnection(userId: string) {
    const pc = this.peerConnections.get(userId);
    if (pc) {
      try { pc.close(); } catch { /* ignore */ }
      this.peerConnections.delete(userId);
    }
    this.cancelOfferWatch(userId);
    this.pendingIceCandidates.delete(userId);
  }

  private pendingMessages: any[] = [];

  private flushPendingMessages() {
    if (this.ws?.readyState !== WebSocket.OPEN) return;
    for (const msg of this.pendingMessages) {
      this.ws.send(JSON.stringify(msg));
    }
    this.pendingMessages = [];
  }

  private sendWs(obj: any) {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(obj));
      this.flushPendingMessages();
    } else {
      this.pendingMessages.push(obj);
    }
  }

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

  /**
   * 切换扬声器（远端音频输出）开关。
   * 与 toggleMic 互不影响：关闭麦克风不会关闭扬声器，反之亦然。
   * 仅控制远端媒体流的 audio track 的 enabled 状态，
   * 不影响本地音频采集与发送，避免麦克风/扬声器互相干扰。
   */
  toggleSpeaker() {
    this.speakerEnabled.update(v => !v);
    const enabled = this.speakerEnabled();
    for (const stream of this.remoteStreams()) {
      stream.stream?.getAudioTracks().forEach(t => (t.enabled = enabled));
    }
  }

  /**
   * 切换摄像头（前后摄）。返回是否成功，调用方据此给用户提示。
   * 策略：优先用 enumerateDevices 按 deviceId 精确切换（桌面 + 手机都可靠）；
   * 只有一个摄像头或取不到设备列表时，用 facingMode 切换；exact 失败则降级 ideal。
   * 切换后保留当前摄像头开关状态；若原先没有视频轨（纯音频加入），则 addTrack 并重新协商。
   */
  async switchCamera(): Promise<boolean> {
    try {
      const currentTrack = this.localStream?.getVideoTracks()[0];
      const currentDeviceId = currentTrack?.getSettings()?.deviceId;

      let devices: MediaDeviceInfo[] = [];
      try {
        devices = (await navigator.mediaDevices.enumerateDevices())
          .filter(d => d.kind === 'videoinput');
      } catch { /* 忽略，走 facingMode 路径 */ }

      let targetDeviceId: string | undefined;
      if (devices.length > 1) {
        const idx = devices.findIndex(d => d.deviceId && d.deviceId === currentDeviceId);
        targetDeviceId = devices[(idx + 1 + devices.length) % devices.length]?.deviceId || undefined;
      }

      const nextFacing = this.currentFacingMode === 'user' ? 'environment' : 'user';
      let newStream: MediaStream | null = null;
      const attempts: MediaStreamConstraints[] = [];
      if (targetDeviceId) {
        attempts.push({
          video: { deviceId: { exact: targetDeviceId }, width: { ideal: 1280 }, height: { ideal: 720 } },
          audio: false,
        });
      }
      attempts.push(
        { video: { facingMode: { exact: nextFacing }, width: { ideal: 1280 }, height: { ideal: 720 } }, audio: false },
        { video: { facingMode: nextFacing, width: { ideal: 1280 }, height: { ideal: 720 } }, audio: false },
        { video: true, audio: false },
      );
      for (const c of attempts) {
        try {
          newStream = await navigator.mediaDevices.getUserMedia(c);
          break;
        } catch { /* 试下一个约束 */ }
      }
      if (!newStream) return false;

      const newTrack = newStream.getVideoTracks()[0];
      if (!newTrack) {
        newStream.getTracks().forEach(t => t.stop());
        return false;
      }
      // 保持与切换前一致的摄像头开关状态
      newTrack.enabled = this.camEnabled();
      this.currentFacingMode = nextFacing;

      if (!this.localStream) {
        // 之前是纯文本/纯音频加入：新建本地流并重新协商
        this.localStream = new MediaStream([newTrack]);
        this.localStreamReady.set(true);
        for (const [, pc] of this.peerConnections) {
          try { pc.addTrack(newTrack, this.localStream); } catch { /* ignore */ }
        }
        await this.renegotiateAll();
        return true;
      }

      let addedNewTrack = false;
      for (const [, pc] of this.peerConnections) {
        const sender = pc.getSenders().find(s => s.track?.kind === 'video');
        if (sender) {
          await sender.replaceTrack(newTrack);
        } else {
          try {
            pc.addTrack(newTrack, this.localStream);
            addedNewTrack = true;
          } catch { /* ignore */ }
        }
      }
      const oldTrack = this.localStream.getVideoTracks()[0];
      if (oldTrack) {
        try { oldTrack.stop(); } catch { /* ignore */ }
        try { this.localStream.removeTrack(oldTrack); } catch { /* ignore */ }
      }
      try { this.localStream.addTrack(newTrack); } catch { /* ignore */ }
      // 新增轨道的 PC 需要重新协商，对端才能看到新画面
      if (addedNewTrack) await this.renegotiateAll();
      return true;
    } catch {
      return false;
    }
  }

  /** 触发全量重新协商：教师补发 offer，学生请求对方补发 */
  private async renegotiateAll(): Promise<void> {
    if (this.myRole === 'teacher') {
      for (const userId of [...this.peerConnections.keys()]) {
        await this.sendOfferResilient(userId);
      }
    } else {
      for (const userId of [...this.peerConnections.keys()]) {
        this.sendWs({ type: 'request-offer', targetUserId: userId });
      }
    }
  }

  toggleChat() {
    this.chatOpen.update(v => !v);
  }

  sendChat(text: string) {
    if (!text.trim()) return;
    const msg = text.trim();
    this.chatMessages.update(msgs => [...msgs, {
      text: msg,
      from: this.myRole,
      self: true,
      time: Date.now(),
      fromUserName: this.myUserName,
    }]);
    this.sendWs({ type: 'chat', data: msg });
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
    // 作废当前会话：旧 WS 的 onclose/onmessage 不得再改状态
    this.sessionSeq++;
    this.cancelAllOfferWatches();
    this.offerReceivedFrom.clear();
    this.stopTimer();
    // 关闭所有 PC
    for (const [, pc] of this.peerConnections) {
      try { pc.close(); } catch { /* ignore */ }
    }
    this.peerConnections.clear();
    if (this.ws) {
      const wsRef = this.ws;
      this.ws = null;
      try {
        // 移除回调后再关，避免残留 onclose 覆盖新会话状态
        wsRef.onopen = wsRef.onclose = wsRef.onerror = wsRef.onmessage = null;
        wsRef.close();
      } catch { /* ignore */ }
    }
    if (this.localStream) {
      this.localStream.getTracks().forEach(t => { try { t.stop(); } catch { /* ignore */ } });
      this.localStream = null;
    }
    this.localStreamReady.set(false);
    this.remoteStreams.set([]);
    this.participants.set([]);
    this.chatMessages.set([]);
    this.retryCount = 0;
    this.pendingMessages = [];
    this.pendingIceCandidates.clear();
    this.callDurationSec.set(0);
    this.connectionLabel.set('');
    // 保留 'ended'（对端挂断页/主动结束页需要），其余回到 idle
    if (this.liveState() !== 'ended') this.liveState.set('idle');
    // 重置音视频状态，避免下次进入遗留上次的设置
    this.micEnabled.set(true);
    this.camEnabled.set(true);
    this.speakerEnabled.set(true);
  }

  private handleConnectionFailure() {
    if (this.liveState() === 'ended' || this.liveState() === 'idle') return;
    this.retryCount++;
    if (this.retryCount <= 2) {
      this.connectionLabel.set(`连接失败，正在重试(${this.retryCount}/2)...`);
      setTimeout(() => {
        if (this.liveState() === 'ended' || this.liveState() === 'idle') return;
        // 重建所有失败的 PC；教师补发 offer，学生请求对方补发（对方重发 offer 即可恢复）
        for (const [userId, pc] of [...this.peerConnections]) {
          if (pc.connectionState === 'failed' || pc.connectionState === 'disconnected' || pc.connectionState === 'closed') {
            const role = this.participants().find(p => p.userId === userId)?.role || 'student';
            this.createPeerConnectionFor(userId, role);
            if (this.myRole === 'teacher') {
              this.sendOfferResilient(userId);
            } else {
              this.sendWs({ type: 'request-offer', targetUserId: userId });
              this.scheduleOfferWatch(userId);
            }
          }
        }
      }, 2000);
    } else {
      this.liveState.set('disconnected');
      this.connectionLabel.set('连接失败，请挂断后重试');
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