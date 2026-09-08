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

  async connect(liveId: string, wsToken: string, wsUrl: string, role: 'teacher' | 'student', userId: string, userName: string): Promise<void> {
    this.liveId = liveId;
    this.myRole = role;
    this.myUserId = userId;
    this.myUserName = userName;
    this.liveState.set('connecting');

    // 获取 ICE 服务器配置
    this.iceServers = await this.getIceServersConfig();

    // 获取本地媒体
    this.localStream = await this.acquireMediaStream();
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

  private connectWebSocket(wsUrl: string, wsToken: string): Promise<void> {
    return new Promise((resolve, reject) => {
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

      this.ws.onerror = () => {
        reject(new Error('无法连接信令服务器'));
      };

      this.ws.onclose = (e) => {
        console.log('[LiveWS] Closed. code=', e.code);
        if (!opened) {
          reject(new Error(`信令连接关闭 (code=${e.code})`));
        } else if (this.liveState() !== 'ended') {
          this.liveState.set('disconnected');
        }
      };

      this.ws.onmessage = (event) => {
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
        // 为列表中的每个人创建 PC（如果是教师则主动发起 offer）
        for (const p of list) {
          if (!p.you && !this.peerConnections.has(p.userId)) {
            await this.createPeerConnectionFor(p.userId, p.role);
            if (this.myRole === 'teacher') {
              await this.createAndSendOffer(p.userId);
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
          // 教师发起 offer
          if (this.myRole === 'teacher') {
            await this.createAndSendOffer(newUserId);
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

    // 如果还没有此用户的 PC，先创建
    if (!this.peerConnections.has(fromUserId)) {
      await this.createPeerConnectionFor(fromUserId, msg.fromRole || 'student');
    }

    const pc = this.peerConnections.get(fromUserId);
    if (!pc) return;

    try {
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
    const pc = this.peerConnections.get(fromUserId);
    if (pc) {
      try {
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
    const pc = this.peerConnections.get(fromUserId);
    if (!pc || !msg.data) return;
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
      // 同步扬声器状态：若用户已静音扬声器，新接收到的远端音频轨也应静音
      if (stream) {
        stream.getAudioTracks().forEach(t => (t.enabled = this.speakerEnabled()));
      }
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

  private async createAndSendOffer(remoteUserId: string) {
    const pc = this.peerConnections.get(remoteUserId);
    if (!pc) {
      console.warn('[LiveWS] createAndSendOffer: PC not found for', remoteUserId);
      return;
    }
    if (pc.signalingState !== 'stable') {
      console.warn('[LiveWS] createAndSendOffer: signalingState is', pc.signalingState, 'for', remoteUserId);
      return;
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

  private closePeerConnection(userId: string) {
    const pc = this.peerConnections.get(userId);
    if (pc) {
      pc.close();
      this.peerConnections.delete(userId);
    }
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

  async switchCamera() {
    if (!this.localStream) return;
    this.currentFacingMode = this.currentFacingMode === 'user' ? 'environment' : 'user';
    try {
      const newStream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: this.currentFacingMode, width: { ideal: 1280 }, height: { ideal: 720 } },
        audio: false,
      });
      const newTrack = newStream.getVideoTracks()[0];
      // 替换所有 PC 中的视频轨
      for (const [userId, pc] of this.peerConnections) {
        const sender = pc.getSenders().find(s => s.track?.kind === 'video');
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
    this.stopTimer();
    // 关闭所有 PC
    for (const [userId, pc] of this.peerConnections) {
      pc.close();
    }
    this.peerConnections.clear();
    if (this.ws) { this.ws.close(); this.ws = null; }
    if (this.localStream) {
      this.localStream.getTracks().forEach(t => t.stop());
      this.localStream = null;
    }
    this.localStreamReady.set(false);
    this.remoteStreams.set([]);
    this.participants.set([]);
    this.chatMessages.set([]);
    this.retryCount = 0;
    this.pendingMessages = [];
    this.pendingIceCandidates.clear();
    // 重置音视频状态，避免下次进入遗留上次的设置
    this.micEnabled.set(true);
    this.camEnabled.set(true);
    this.speakerEnabled.set(true);
  }

  private handleConnectionFailure() {
    if (this.liveState() === 'ended') return;
    this.retryCount++;
    if (this.retryCount <= 2) {
      this.connectionLabel.set(`连接失败，正在重试(${this.retryCount}/2)...`);
      setTimeout(() => {
        // 重建所有失败的 PC
        for (const [userId, pc] of this.peerConnections) {
          if (pc.connectionState === 'failed' || pc.connectionState === 'disconnected' || pc.connectionState === 'closed') {
            const role = this.participants().find(p => p.userId === userId)?.role || 'student';
            this.createPeerConnectionFor(userId, role);
            if (this.myRole === 'teacher') {
              this.createAndSendOffer(userId);
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