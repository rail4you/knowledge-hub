export enum RecruitmentLiveStatus {
  Waiting = 0,
  Active = 1,
  Ended = 2,
  Cancelled = 3,
}

/** 直播类型：招聘直播 / 课程直播（共用同一套直播间能力） */
export enum LiveType {
  Recruitment = 0,
  Course = 1,
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
  type: LiveType;
  courseId?: string;
  courseTitle?: string;
  isParticipant: boolean;
  creationTime: string;
}

export interface CreateRecruitmentLiveDto {
  type?: LiveType;
  /** type=Course 时必填：关联租户内已存在的课程 */
  courseId?: string;
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
  courseId?: string;
  scheduledAt?: string;
  scheduledEndAt?: string;
}

/** 课程简要信息（课程直播关联课程用） */
export interface CourseBriefDto {
  id: string;
  title: string;
  semester?: string;
  statusText: string;
}

export interface UserBriefDto {
  id: string;
  userName: string;
  name: string;
}

export interface PagedRecruitmentLiveRequestDto {
  filter?: string;
  status?: RecruitmentLiveStatus;
  /** 按直播类型隔离（招聘直播 / 课程直播） */
  type?: LiveType;
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
