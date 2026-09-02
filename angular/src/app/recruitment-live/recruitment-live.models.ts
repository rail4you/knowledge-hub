export enum RecruitmentLiveStatus {
  Waiting = 0,
  Active = 1,
  Ended = 2,
  Cancelled = 3,
}

export interface RecruitmentLiveDto {
  id: string;
  title: string;
  description?: string;
  teacherId: string;
  teacherName: string;
  teacherUserName?: string;
  studentId?: string;
  studentName?: string;
  studentUserName?: string;
  participants: ParticipantBriefDto[];
  roomCode: string;
  status: RecruitmentLiveStatus;
  statusText: string;
  scheduledAt?: string;
  scheduledEndAt?: string;
  startedAt?: string;
  endedAt?: string;
  durationSeconds: number;
  interviewScheduleId?: string;
  isParticipant: boolean;
  creationTime: string;
}

export interface CreateRecruitmentLiveDto {
  title: string;
  description?: string;
  studentIds?: string[];  // 可多选，创建多人直播
  scheduledAt?: string;
  scheduledEndAt?: string;
}

export interface UpdateRecruitmentLiveDto {
  title: string;
  description?: string;
  studentId?: string;
  scheduledAt?: string;
  scheduledEndAt?: string;
}

export interface UserBriefDto {
  id: string;
  userName: string;
  name: string;
}

export interface PagedRecruitmentLiveRequestDto {
  filter?: string;
  status?: RecruitmentLiveStatus;
  sorting?: string;
  skipCount: number;
  maxResultCount: number;
}

export interface IceServerDto {
  urls: string[];
  username?: string;
  credential?: string;
}

export interface WsTokenDto {
  token: string;
  wsUrl: string;
}

export interface ChatMessage {
  text: string;
  from: string;
  self: boolean;
  time: number;
  fromUserName?: string;
}

export interface ParticipantBriefDto {
  userId: string;
  userName: string;
  role: string;
}

/** 远程视频流，关联到特定参与者 */
export interface RemoteParticipantStream {
  userId: string;
  userName: string;
  role: string;
  stream: MediaStream;
  connectionState: string;
  muted?: boolean;
}
