import type { EntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CreateUserDto {
  name: string;
  birthDate: string;
  shortBio?: string | null;
}

export interface GetUserListDto extends PagedAndSortedResultRequestDto {
  filter?: string | null;
}

export interface UpdateUserDto {
  name: string;
  birthDate: string;
  shortBio?: string | null;
}

export interface MyProfileDto {
  userName?: string;
  email?: string;
  name?: string;
  surname?: string;
  phoneNumber?: string;
}

export interface UserDto extends EntityDto<string> {
  name?: string;
  birthDate?: string;
  shortBio?: string;
}

export interface UserImportFailItemDto {
  rowNumber?: number;
  userName?: string;
  reason?: string;
}

export interface UserImportResultDto {
  totalCount?: number;
  successCount?: number;
  failCount?: number;
  failItems?: UserImportFailItemDto[];
}
