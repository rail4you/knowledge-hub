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

export interface UserDto extends EntityDto<string> {
  name?: string;
  birthDate?: string;
  shortBio?: string;
}
