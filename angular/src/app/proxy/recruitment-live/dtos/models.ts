import type { LiveType } from '../live-type.enum';
import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { RecruitmentLiveStatus } from '../recruitment-live-status.enum';

export interface CourseBriefDto {
  id?: string;
  title?: string;
  semester?: string | null;
  statusText?: string;
}

export interface CreateRecruitmentLiveDto {
  type?: LiveType;
  courseId?: string | null;
  title?: string;
  description?: string | null;
  studentIds?: string[] | null;
  scheduledAt?: string | null;
  scheduledEndAt?: string | null;
}

export interface IceServerDto {
  urls?: string[];
  username?: string | null;
  credential?: string | null;
}

export interface PagedRecruitmentLiveRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  status?: RecruitmentLiveStatus | null;
  type?: LiveType | null;
}

export interface ParticipantBriefDto {
  userId?: string;
  userName?: string;
  role?: string;
}

export interface RecruitmentLiveChatMessageDto {
  senderRole?: string;
  content?: string;
  sentAt?: string;
}

export interface RecruitmentLiveDto extends FullAuditedEntityDto<string> {
  title?: string;
  description?: string | null;
  teacherId?: string;
  teacherName?: string;
  teacherUserName?: string | null;
  studentId?: string | null;
  studentName?: string | null;
  studentUserName?: string | null;
  participants?: ParticipantBriefDto[];
  roomCode?: string;
  status?: RecruitmentLiveStatus;
  statusText?: string;
  scheduledAt?: string | null;
  scheduledEndAt?: string | null;
  startedAt?: string | null;
  endedAt?: string | null;
  durationSeconds?: number;
  interviewScheduleId?: string | null;
  type?: LiveType;
  courseId?: string | null;
  courseTitle?: string | null;
  isParticipant?: boolean;
}

export interface SaveChatMessageInputDto {
  content?: string;
}

export interface UpdateRecruitmentLiveDto {
  title?: string;
  description?: string | null;
  studentId?: string | null;
  courseId?: string | null;
  scheduledAt?: string | null;
  scheduledEndAt?: string | null;
}

export interface UserBriefDto {
  id?: string;
  userName?: string;
  name?: string;
}

export interface WsTokenDto {
  token?: string;
  wsUrl?: string;
}
