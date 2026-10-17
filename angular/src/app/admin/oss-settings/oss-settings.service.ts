import { Injectable, inject } from '@angular/core';
import { RestService, Rest } from '@abp/ng.core';

/** 对应后端 OssConfigDto（KnowledgeHub.Application.Contracts/Resources/Oss）。 */
export interface OssConfigDto {
  endpoint: string;
  accessKeyId: string;
  hasAccessKeySecret: boolean;
  bucketName: string;
  uploadPath: string;
  urlPrefix: string;
  isConfigured: boolean;
  /** database=数据库设置，config=配置文件回退，none=未配置 */
  source: string;
  publicHost: string;
}

/** 对应后端 UpdateOssConfigDto。 */
export interface UpdateOssConfigDto {
  endpoint: string;
  accessKeyId: string;
  /** 为空表示保持原密钥不变 */
  accessKeySecret?: string;
  clearAccessKeySecret: boolean;
  bucketName: string;
  uploadPath: string;
  urlPrefix: string;
}

export interface OssTestResultDto {
  success: boolean;
  error?: string;
  probeUrl?: string;
}

/**
 * 对象存储设置服务。
 *
 * 对应后端 IOssConfigAppService 的约定式路由：
 *   GET  /api/app/oss-config
 *   PUT  /api/app/oss-config
 *   POST /api/app/oss-config/test
 *
 * 注：后端接口是标准 ABP AppService，将来执行 `abp generate-proxy -t ng`
 * 会生成 angular/src/app/proxy/resources/oss/oss-config.service.ts；
 * 本文件先手写保证功能可用，生成代理后可整体删除本文件并改用代理。
 */
@Injectable({ providedIn: 'root' })
export class OssSettingsService {
  private restService = inject(RestService);
  apiName = 'KnowledgeHub';

  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, OssConfigDto>(
      { method: 'GET', url: '/api/app/oss-config' },
      { apiName: this.apiName, ...config },
    );

  update = (input: UpdateOssConfigDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OssConfigDto>(
      { method: 'PUT', url: '/api/app/oss-config', body: input },
      { apiName: this.apiName, ...config },
    );

  test = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, OssTestResultDto>(
      { method: 'POST', url: '/api/app/oss-config/test', body: {} },
      { apiName: this.apiName, ...config },
    );
}
