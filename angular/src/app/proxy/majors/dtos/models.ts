import type { EntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CreateUpdateMajorDto {
  name: string;
  code?: string | null;
  description?: string | null;
  trainingObjectives?: string | null;
}

export interface MajorDto extends FullAuditedEntityDto<string> {
  name?: string;
  code?: string | null;
  description?: string | null;
  trainingObjectives?: string | null;
}

export interface MajorLookupDto extends EntityDto<string> {
  name?: string;
  code?: string | null;
}

export interface PagedMajorRequestDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}
