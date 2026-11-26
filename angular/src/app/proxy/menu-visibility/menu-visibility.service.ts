import type { GetMenuVisibilityTeachersInput, MenuVisibilityDto, MenuVisibilityListItemDto, SetMenuVisibilityInput } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class MenuVisibilityService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getMyHiddenMenus = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, string[]>({
      method: 'GET',
      url: '/api/app/menu-visibility/my-hidden-menus',
    },
    { apiName: this.apiName,...config });
  

  getTeachers = (input: GetMenuVisibilityTeachersInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MenuVisibilityListItemDto>>({
      method: 'GET',
      url: '/api/app/menu-visibility/teachers',
      params: { filter: input.filter, tenantId: input.tenantId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getUserMenus = (userId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MenuVisibilityDto>({
      method: 'GET',
      url: `/api/app/menu-visibility/user-menus/${userId}`,
    },
    { apiName: this.apiName,...config });
  

  resetUserMenus = (userId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/menu-visibility/reset-user-menus/${userId}`,
    },
    { apiName: this.apiName,...config });
  

  setUserMenus = (input: SetMenuVisibilityInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/menu-visibility/set-user-menus',
      body: input,
    },
    { apiName: this.apiName,...config });
}