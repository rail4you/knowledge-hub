import type { ReviewDecision } from '../review-decision.enum';
import type { ReviewStage } from '../review-stage.enum';
import type { ReviewOperation } from '../review-operation.enum';
import type { ReviewTargetType } from '../review-target-type.enum';
import type { ReviewActionType } from '../review-action-type.enum';
import type { ReviewTicketStatus } from '../review-ticket-status.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface DecideReviewInput {
  ticketId?: string;
  decision?: ReviewDecision;
  reason?: string | null;
  expectedRoundNo?: number | null;
}

export interface ReviewActionDto {
  id?: string;
  stage?: ReviewStage;
  stageName?: string;
  operation?: ReviewOperation;
  operationName?: string;
  decision?: ReviewDecision | null;
  actorId?: string;
  actorName?: string | null;
  actorTenantId?: string | null;
  reason?: string | null;
  roundNo?: number;
  creationTime?: string;
  snapshotJson?: string | null;
}

export interface ReviewTicketDto {
  id?: string;
  targetType?: ReviewTargetType;
  targetTypeName?: string;
  targetId?: string;
  targetName?: string | null;
  actionType?: ReviewActionType;
  actionTypeName?: string;
  libraryId?: string | null;
  libraryName?: string | null;
  contentTenantId?: string | null;
  contentTenantName?: string | null;
  stage?: ReviewStage;
  stageName?: string;
  roundNo?: number;
  status?: ReviewTicketStatus;
  statusName?: string;
  submittedBy?: string;
  submittedByName?: string | null;
  submittedAt?: string;
  reviewerId?: string | null;
  reviewerName?: string | null;
  reviewedAt?: string | null;
  reason?: string | null;
  payloadJson?: string;
  totalStages?: number;
  canDecide?: boolean;
  canWithdraw?: boolean;
  canResubmit?: boolean;
  actions?: ReviewActionDto[];
}

export interface ReviewTicketListRequestDto extends PagedAndSortedResultRequestDto {
  targetType?: ReviewTargetType | null;
  stage?: ReviewStage | null;
  status?: ReviewTicketStatus | null;
  libraryId?: string | null;
  targetId?: string | null;
  onlyMine?: boolean;
  filter?: string | null;
}
