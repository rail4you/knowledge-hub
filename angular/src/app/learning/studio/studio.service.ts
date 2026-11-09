import { Injectable, inject } from '@angular/core';
import { RestService, ConfigStateService } from '@abp/ng.core';
import { OAuthStorage } from 'angular-oauth2-oidc';
import type { Observable } from 'rxjs';

export interface StudioMember {
  userId: string;
  userName: string;
  role: number;
  roleName: string;
  joinedAt?: string;
}

export interface StudioMaterial {
  id: string;
  studioId: string;
  resourceId: string;
  title: string;
  description?: string | null;
  status: number;
  statusName: string;
  submittedByName: string;
  submittedAt: string;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  reviewComment?: string | null;
  publishedAt?: string | null;
  fileExtension?: string | null;
  fileSize?: number | null;
  originalFileName?: string | null;
  canSubmit: boolean;
  canReview: boolean;
  canPreview: boolean;
}

export interface StudioListItem {
  id: string;
  name: string;
  description?: string | null;
  coverUrl?: string | null;
  leaderUserId?: string | null;
  leaderName?: string | null;
  status: number;
  statusName: string;
  memberCount: number;
  publishedCount: number;
  pendingCount: number;
  isLeader: boolean;
  isMember: boolean;
  canReview: boolean;
  creationTime: string;
}

export interface StudioDetail extends StudioListItem {
  members: StudioMember[];
  materials: StudioMaterial[];
}

const BASE = '/api/app/studio';

/** 名师工作室：成员上传资料 → 领衔人审核 → 发布。文件复用资源中心。 */
@Injectable({ providedIn: 'root' })
export class StudioService {
  private readonly rest = inject(RestService);
  private readonly config = inject(ConfigStateService);
  private readonly oauth = inject(OAuthStorage);

  private cfg = () => ({ apiName: 'KnowledgeHub' });

  getList = (): Observable<StudioListItem[]> =>
    this.rest.request<any, StudioListItem[]>({ method: 'GET', url: BASE }, this.cfg());

  get = (id: string): Observable<StudioDetail> =>
    this.rest.request<any, StudioDetail>({ method: 'GET', url: `${BASE}/${id}` }, this.cfg());

  create = (input: Partial<{ name: string; description: string; coverUrl: string; leaderUserId: string }>) =>
    this.rest.request<any, StudioDetail>({ method: 'POST', url: BASE, body: input }, this.cfg());

  update = (id: string, input: Partial<{ name: string; description: string; coverUrl: string; leaderUserId: string; status: number }>) =>
    this.rest.request<any, StudioDetail>({ method: 'PUT', url: `${BASE}/${id}`, body: input }, this.cfg());

  remove = (id: string) =>
    this.rest.request<any, void>({ method: 'DELETE', url: `${BASE}/${id}` }, this.cfg());

  getCandidates = (id: string): Observable<StudioMember[]> =>
    this.rest.request<any, StudioMember[]>({ method: 'GET', url: `${BASE}/${id}/candidate-teachers` }, this.cfg());

  addMember = (id: string, userId: string, role = 1): Observable<StudioMember[]> =>
    this.rest.request<any, StudioMember[]>({ method: 'POST', url: `${BASE}/${id}/member`, body: { userId, role } }, this.cfg());

  removeMember = (id: string, userId: string) =>
    this.rest.request<any, void>({ method: 'DELETE', url: `${BASE}/${id}/member/${userId}` }, this.cfg());

  createMaterial = (id: string, input: { resourceId: string; title: string; description?: string; submitNow: boolean }) =>
    this.rest.request<any, StudioMaterial>({ method: 'POST', url: `${BASE}/${id}/material`, body: input }, this.cfg());

  submitMaterial = (id: string, materialId: string) =>
    this.rest.request<any, StudioMaterial>({ method: 'POST', url: `${BASE}/${id}/submit-material/${materialId}` }, this.cfg());

  reviewMaterial = (id: string, materialId: string, approved: boolean, comment?: string) =>
    this.rest.request<any, StudioMaterial>(
      { method: 'POST', url: `${BASE}/${id}/review-material/${materialId}`, body: { approved, comment } },
      this.cfg(),
    );

  deleteMaterial = (id: string, materialId: string) =>
    this.rest.request<any, void>({ method: 'DELETE', url: `${BASE}/${id}/material/${materialId}` }, this.cfg());

  /** 我的课程资源（作为工作室资料素材，复用资源中心上传后的记录） */
  getMyResources = (filter?: string) =>
    this.rest.request<any, { items: any[]; totalCount: number }>(
      {
        method: 'GET',
        url: '/api/app/resource/filtered-list',
        params: { filter, creatorId: this.config.getDeep('currentUser.id'), skipCount: 0, maxResultCount: 200 },
      },
      this.cfg(),
    );

  /** 资源预览/下载地址（带上 access_token 供 <img>/<a> 原生请求） */
  resourceUrl(resourceId: string): string {
    let token = '';
    try { token = this.oauth.getItem('access_token') ?? ''; } catch { token = ''; }
    return `/api/resource-file/${resourceId}/preview${token ? `?access_token=${encodeURIComponent(token)}` : ''}`;
  }

  downloadUrl(resourceId: string): string {
    let token = '';
    try { token = this.oauth.getItem('access_token') ?? ''; } catch { token = ''; }
    return `/api/resource-file/${resourceId}/download${token ? `?access_token=${encodeURIComponent(token)}` : ''}`;
  }
}
