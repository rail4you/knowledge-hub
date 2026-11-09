import { Injectable, inject } from '@angular/core';
import { RestService, ConfigStateService } from '@abp/ng.core';
import { OAuthStorage } from 'angular-oauth2-oidc';
import type { Observable } from 'rxjs';

export interface WhiteboardListItem {
  id: string;
  title: string;
  topic?: string | null;
  subject?: string | null;
  creatorName: string;
  memberCount: number;
  elementCount: number;
  isOwner: boolean;
  creationTime: string;
  lastModificationTime?: string | null;
}

export interface WhiteboardMember {
  userId: string;
  userName: string;
  color: string;
  role: number;
  roleName: string;
  joinedAt: string;
  online?: boolean;
}

export interface WhiteboardElementDto {
  key: string;
  json: string;
  elementType: number;
  lastEditorName?: string | null;
  lastModificationTime?: string | null;
}

export interface WhiteboardDetail extends WhiteboardListItem {
  description?: string | null;
  status: number;
  creatorId?: string | null;
  viewX: number;
  viewY: number;
  viewScale: number;
  members: WhiteboardMember[];
  elements: WhiteboardElementDto[];
  canEdit: boolean;
  myColor: string;
  myName: string;
}

export interface InvitableTeacher {
  userId: string;
  userName: string;
  name?: string | null;
  joined: boolean;
}

/** 画布元素（前端模型，与后端 JSON 一致） */
export interface BoardElement {
  id: string;
  type: 'text' | 'note' | 'image' | 'video';
  x: number;
  y: number;
  w?: number;
  h?: number;
  sx?: number;
  sy?: number;
  rot?: number;
  text?: string;
  fs?: number;
  fill?: string;
  bg?: string;
  by?: string;
  /** 素材来源：课程资源 Id（图片/视频都存这个，不内联 base64） */
  resourceId?: string;
  resourceName?: string;
  fileExtension?: string;
}

const BASE = '/api/app/whiteboard';

/**
 * 教研白板：元信息 / 成员 / 快照。
 *
 * 实时协同（元素增改移、光标、在线成员）走 SignalR，不在本服务里。
 */
@Injectable({ providedIn: 'root' })
export class WhiteboardService {
  private readonly rest = inject(RestService);
  private readonly config = inject(ConfigStateService);
  /** 访问令牌只在内存（ABP MemoryTokenStorageService）；localStorage 里只有 refresh_token。 */
  private readonly oauthStorage = inject(OAuthStorage);
  private readonly apiName = 'KnowledgeHub';

  private cfg = () => ({ apiName: this.apiName });

  getList = (): Observable<WhiteboardListItem[]> =>
    this.rest.request<any, WhiteboardListItem[]>({ method: 'GET', url: BASE }, this.cfg());

  get = (id: string): Observable<WhiteboardDetail> =>
    this.rest.request<any, WhiteboardDetail>({ method: 'GET', url: `${BASE}/${id}` }, this.cfg());

  create = (input: Partial<{ title: string; topic: string; subject: string; description: string }>) =>
    this.rest.request<any, WhiteboardDetail>({ method: 'POST', url: BASE, body: input }, this.cfg());

  update = (id: string, input: Partial<{ title: string; topic: string; subject: string; description: string }>) =>
    this.rest.request<any, WhiteboardDetail>({ method: 'PUT', url: `${BASE}/${id}`, body: input }, this.cfg());

  delete = (id: string) =>
    this.rest.request<any, void>({ method: 'DELETE', url: `${BASE}/${id}` }, this.cfg());

  getInvitableTeachers = (id: string): Observable<InvitableTeacher[]> =>
    this.rest.request<any, InvitableTeacher[]>({ method: 'GET', url: `${BASE}/${id}/invitable-teachers` }, this.cfg());

  invite = (id: string, userIds: string[], role = 1): Observable<WhiteboardMember[]> =>
    this.rest.request<any, WhiteboardMember[]>(
      { method: 'POST', url: `${BASE}/${id}/invite`, body: { userIds, role } },
      this.cfg(),
    );

  removeMember = (id: string, userId: string) =>
    this.rest.request<any, void>({ method: 'DELETE', url: `${BASE}/${id}/member/${userId}` }, this.cfg());

  clearElements = (id: string) =>
    this.rest.request<any, void>({ method: 'POST', url: `${BASE}/${id}/clear-elements` }, this.cfg());

  /**
   * 当前登录人的 access token。
   *
   * <p>用途有两个：① SignalR 连接（WebSocket 不能设请求头，只能 ?access_token=）；
   * ② 资源预览地址（`<img>` / `<video>` 原生请求也设不了头）。</p>
   */
  accessToken(): string {
    try {
      return this.oauthStorage.getItem('access_token') ?? '';
    } catch {
      return '';
    }
  }

  /** 课程资源（图片/视频）用作白板素材的预览地址 */
  resourceUrl(resourceId: string): string {
    const t = this.accessToken();
    return `/api/resource-file/${resourceId}/preview${t ? `?access_token=${encodeURIComponent(t)}` : ''}`;
  }

  /** 我的课程资源（素材来源） */
  getMyResources = (filter?: string, resourceType?: number, courseId?: string) =>
    this.rest.request<any, { items: any[]; totalCount: number }>(
      {
        method: 'GET',
        url: '/api/app/resource/filtered-list',
        params: {
          filter,
          resourceType,
          courseId,
          creatorId: this.config.getDeep('currentUser.id'),
          skipCount: 0,
          maxResultCount: 200,
        },
      },
      this.cfg(),
    );

  /** 我的课程（素材选择框按课程过滤） */
  getMyCourses = () =>
    this.rest.request<any, { items: any[] }>(
      { method: 'GET', url: '/api/app/course', params: { onlyMine: true, skipCount: 0, maxResultCount: 100 } },
      this.cfg(),
    );
}
