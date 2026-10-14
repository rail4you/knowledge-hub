import type { MyProfileDto, UpdateMyProfileDto, ChangeMyPasswordDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class UserService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';
  

  getMyProfile = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, MyProfileDto>({
      method: 'GET',
      url: '/api/app/user/my-profile',
    },
    { apiName: this.apiName,...config });
  

  updateMyProfile = (input: UpdateMyProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: '/api/app/user/my-profile',
      body: input,
    },
    { apiName: this.apiName,...config });
  
  /// 修改当前用户密码（ABP Account ProfileController：POST api/account/my-profile/change-password）
  changePassword = (input: ChangeMyPasswordDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/account/my-profile/change-password',
      body: input,
    },
    { apiName: this.apiName,...config });
}