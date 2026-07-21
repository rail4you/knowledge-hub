import { Injectable, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';

/**
 * 仿真实训 WASM 本地镜像服务。
 *
 * 后端实现：KnowledgeHub.Application/Practicums/WasmMirrors/WasmMirrorAppService
 * ABP 路由：
 *   GET /api/app/wasm-mirror/mapping  - 学生页面用的 sourceUrl → publicUrl 精简映射
 *   GET /api/app/wasm-mirror/all      - 管理/中心用的完整信息（含 title/cover/description/fileCount 等）
 *
 * 该 service 放在 shared/ 而非 proxy/ 目录，
 * 因为 ABP 自动生成代码会清空 proxy/，放在 proxy 之外的目录更安全。
 * 用户执行 `abp generate-proxy -t ng` 不会影响此文件。
 */
@Injectable({ providedIn: 'root' })
export class WasmMirrorService {
  private readonly restService = inject(RestService);

  /** 学生实训详情页用的精简映射。返回的 sourceUrl 已规范化为 lookup key。 */
  getMapping() {
    return this.restService.request<unknown, WasmMirrorMappingDto[]>(
      {
        method: 'GET',
        url: '/api/app/wasm-mirror/mapping',
      },
      { apiName: 'KnowledgeHub' },
    );
  }

  /**
   * 获取所有镜像的完整信息。
   * 学生端资源中心、教师端管理页都用此方法展示列表。
   * 后端有 60s 内存缓存，短时间内多次调用实际只读缓存。
   *
   * 注意：ABP 自动生成的 GetAllAsync 路由是 `/api/app/wasm-mirror`（无 /all 后缀），
   * 因为 ABP 约定 GetList/GetAll 这类方法去掉 Async 后缀直接当 URL segment，
   * 进一步「GET 整个 controller」时再 fallback 到 controller 名本身。
   */
  getAll() {
    return this.restService.request<unknown, WasmMirrorInfoDto[]>(
      {
        method: 'GET',
        url: '/api/app/wasm-mirror',
      },
      { apiName: 'KnowledgeHub' },
    );
  }
}

/** 学生页面用的精简映射表：sourceUrl → publicUrl/status。 */
export interface WasmMirrorMappingDto {
  sourceUrl: string;
  slug: string;
  publicUrl: string;
  status: string;
}

/**
 * 管理/资源中心用的完整信息。字段命名与后端 WasmMirrorInfoDto 一一对应（PascalCase 来自 JSON 反序列化）。
 */
export interface WasmMirrorInfoDto extends WasmMirrorMappingDto {
  entryPath?: string;
  fileCount?: number;
  totalBytes?: number;
  lastModifiedTime?: string;
  mirroredAt?: string;
  buildSha?: string;
  coopCoepRequired?: boolean;
  files?: string[];
  /** 展示用标题；后端在 manifest 缺失时用 slug 美化（首字母大写、把 - 改空格）。 */
  title?: string;
  /** 封面图 URL（可空）。 */
  cover?: string | null;
  /** 描述（可空）。 */
  description?: string | null;
}