import type { FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { RecruitmentLiveStatus } from '../recruitment-live-status.enum';

export interface CreateRecruitmentLiveDto {
  title?: string;
  description?: string | null;
  studentId?: string | null;
  scheduledAt?: string | null;
}

export interface IceServerDto {
  urls?: string[];
  username?: string | null;
  credential?: string | null;
}

export interface PagedRecruitmentLiveRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  status?: RecruitmentLiveStatus | null;
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
  roomCode?: string;
  status?: RecruitmentLiveStatus;
  statusText?: string;
  scheduledAt?: string | null;
  startedAt?: string | null;
  endedAt?: string | null;
  durationSeconds?: number;
  interviewScheduleId?: string | null;
  isParticipant?: boolean;
}

export interface UpdateRecruitmentLiveDto {
  title?: string;
  description?: string | null;
  studentId?: string | null;
  scheduledAt?: string | null;
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
