import type { CreateUserDto, GetUserListDto, MyProfileDto, UpdateUserDto, UserDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class UserService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  create = (input: CreateUserDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserDto>({
      method: 'POST',
      url: '/api/app/user',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/user/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, UserDto>({
      method: 'GET',
      url: `/api/app/user/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetUserListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<UserDto>>({
      method: 'GET',
      url: '/api/app/user',
      params: { filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateUserDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/app/user/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });

  updateMyProfile = (input: MyProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MyProfileDto>({
      method: 'PUT',
      url: '/api/app/profile',
      body: input,
    },
    { apiName: this.apiName,...config });
}