import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface GetMenuVisibilityTeachersInput extends PagedAndSortedResultRequestDto {
  filter?: string | null;
  tenantId?: string | null;
}

export interface MenuVisibilityDto {
  userId?: string;
  userName?: string;
  displayName?: string | null;
  hiddenMenus?: string[];
}

export interface MenuVisibilityListItemDto {
  userId?: string;
  userName?: string;
  displayName?: string | null;
  tenantId?: string | null;
  tenantName?: string | null;
  hiddenMenus?: string[];
  hiddenCount?: number;
}

export interface SetMenuVisibilityInput {
  userId?: string;
  hiddenMenus?: string[];
}
