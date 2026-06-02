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
      method: 'GET', url: `/api/app/recruitment-live/${id}`,
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
    this.restService.request<any, RecruitmentLiveDto>({
      method: 'POST', url: '/api/app/recruitment-live', body: input,
    }, { apiName: this.apiName });

  updateLive = (id: string, input: UpdateRecruitmentLiveDto) =>
    this.restService.request<any, RecruitmentLiveDto>({
      method: 'PUT', url: `/api/app/recruitment-live/${id}`, body: input,
    }, { apiName: this.apiName });

  cancelLive = (id: string) =>
    this.restService.request<any, void>({
      method: 'POST', url: `/api/app/recruitment-live/${id}/cancel-live`,
    }, { apiName: this.apiName });

  deleteLive = (id: string) =>
    this.restService.request<any, void>({
      method: 'DELETE', url: `/api/app/recruitment-live/${id}`,
    }, { apiName: this.apiName });

  getWebSocketToken = (liveId: string) =>
    this.restService.request<any, WsTokenDto>({
      method: 'GET', url: `/api/app/recruitment-live/${liveId}/web-socket-token`,
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

  // ── WebRTC State ──

  readonly liveState = signal<LiveState>('idle');
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

    // 获取本地媒体
    try {
      this.localStream = await navigator.mediaDevices.getUserMedia({
        video: { width: { ideal: 1280 }, height: { ideal: 720 }, facingMode: 'user' },
        audio: true,
      });
    } catch (e: any) {
      // 回退到基本约束
      try {
        this.localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
      } catch (e2: any) {
        throw new Error('无法访问摄像头/麦克风: ' + (e2.message || e2.name));
      }
    }

    this.liveState.set('waiting');

    // 连接 WebSocket
    await this.connectWebSocket(wsUrl, wsToken);

    // 创建 PeerConnection
    await this.createPeerConnection();

    // 如果是教师端，等待学生加入后发 offer
    // 如果是学生端，也创建好 pc 等待对方 offer
    if (role === 'teacher') {
      this.connectionLabel.set('等待学生加入...');
    } else {
      this.connectionLabel.set('等待教师发起连接...');
    }
  }

  private connectWebSocket(wsUrl: string, wsToken: string): Promise<void> {
    return new Promise((resolve, reject) => {
      const url = `${wsUrl}?token=${encodeURIComponent(wsToken)}&liveId=${this.liveId}`;
      this.ws = new WebSocket(url);

      this.ws.onopen = () => {
        resolve();
      };

      this.ws.onerror = () => reject(new Error('无法连接信令服务器'));

      this.ws.onclose = () => {
        if (this.liveState() !== 'ended') {
          this.liveState.set('disconnected');
        }
      };

      this.ws.onmessage = (event) => this.handleSignalMessage(event.data);
    });
  }

  private async createPeerConnection(): Promise<void> {
    const iceServers = await this.getIceServersConfig();
    this.pc = new RTCPeerConnection({ iceServers });

    this.localStream?.getTracks().forEach(track => this.pc!.addTrack(track, this.localStream!));

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
          this.startTimer();
          break;
        case 'connecting':
          this.liveState.set('signaling');
          this.connectionLabel.set('连接中...');
          break;
        case 'disconnected':
        case 'failed':
          this.connectionLabel.set('已断开');
          this.handleConnectionFailure();
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
        if (!this.pc) await this.createPeerConnection();
        await this.pc!.setRemoteDescription(new RTCSessionDescription(msg.data));
        const answer = await this.pc!.createAnswer();
        await this.pc!.setLocalDescription(answer);
        this.sendWs({ type: 'answer', data: answer });
        this.liveState.set('signaling');
        break;

      case 'answer':
        if (this.pc) {
          await this.pc.setRemoteDescription(new RTCSessionDescription(msg.data));
        }
        break;

      case 'ice-candidate':
        if (this.pc && msg.data) {
          try { await this.pc.addIceCandidate(new RTCIceCandidate(msg.data)); } catch {}
        }
        break;

      case 'chat':
        this.chatMessages.update(msgs => [...msgs, {
          text: msg.data,
          from: msg.from || '',
          self: !!msg.self,
          time: Date.now(),
        }]);
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

  private async createAndSendOffer() {
    if (!this.pc) return;
    const offer = await this.pc.createOffer();
    await this.pc.setLocalDescription(offer);
    this.sendWs({ type: 'offer', data: offer });
  }

  private sendWs(obj: any) {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(obj));
    }
  }

  toggleMic() {
    this.micEnabled.update(v => !v);
    this.localStream?.getAudioTracks().forEach(t => (t.enabled = this.micEnabled()));
  }

  toggleCam() {
    this.camEnabled.update(v => !v);
    this.localStream?.getVideoTracks().forEach(t => (t.enabled = this.camEnabled()));
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
    this.sendWs({ type: 'chat', data: text.trim() });
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
    this.remoteStream.set(null);
    this.chatMessages.set([]);
    this.retryCount = 0;
  }

  private handleConnectionFailure() {
    if (this.liveState() === 'ended') return;
    this.retryCount++;
    if (this.retryCount <= 2) {
      this.connectionLabel.set(`连接失败，正在重试(${this.retryCount}/2)...`);
      setTimeout(() => {
        if (this.pc?.iceConnectionState === 'failed') {
          if (this.myRole === 'teacher') this.createAndSendOffer();
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
