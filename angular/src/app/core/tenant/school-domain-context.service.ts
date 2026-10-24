import { Injectable, computed, inject, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { SessionStateService } from '@abp/ng.core';
import type { CurrentTenantDto } from '../../proxy/controllers/models';

/**
 * 学校域名上下文。
 *
 * <p>后端在「学校域名」下会把请求强制落入该学校租户（域名优先于令牌），
 * 前端据此把界面收敛到单校形态：</p>
 * <ul>
 *   <li>登录页锁定机构，不再让用户选别的学校；</li>
 *   <li>会话级租户固定为该学校，避免残留 cookie 造成错位；</li>
 *   <li>首页 / 公开内容页只展示本校内容。</li>
 * </ul>
 *
 * <p>平台域名（如 www.iketong.com）下 <code>isSchoolDomain=false</code>，行为与改造前完全一致。</p>
 */
@Injectable({ providedIn: 'root' })
export class SchoolDomainContextService {
  private readonly http = inject(HttpClient);
  private readonly sessionState = inject(SessionStateService);
  private readonly document = inject(DOCUMENT);

  /** 后端探测结果（启动时拉取一次） */
  readonly context = signal<CurrentTenantDto | null>(null);

  /** 当前是否处于某所学校的专属域名 */
  readonly isSchoolDomain = computed(() => this.context()?.isSchoolDomain === true);

  /** 当前学校名称 */
  readonly schoolName = computed(() => this.context()?.schoolName || this.context()?.tenantName || '');

  /** 当前学校徽标 */
  readonly schoolLogo = computed(() => this.context()?.logoUrl || null);

  /**
   * 启动时探测当前域名归属。
   *
   * <p>命中学校域名时：清掉残留的 <code>__tenant</code> cookie（避免跳到平台域名时被旧租户污染），
   * 并把会话级租户固定为该学校 —— 之后所有 API 请求都会带上正确的租户头。</p>
   */
  async initialize(): Promise<void> {
    try {
      // 刻意用 HttpClient 直连相对路径，不走 RestService：
      // 应用初始化器是并行执行的，此时 ABP 的 EnvironmentService 状态尚未就绪，
      // RestService 解析 apiName 会因为 environment.apis 未初始化而抛错。
      // 相对 URL 由浏览器按当前 origin 解析 —— 正好满足「请求要带学校域名 Host」的要求。
      const res = await firstValueFrom(this.http.get<CurrentTenantDto>('/api/public/current-tenant'));
      if (!res) {
        return;
      }

      this.context.set(res);

      if (res.isSchoolDomain && res.tenantId) {
        this.clearTenantCookie();
        this.sessionState.setTenant({
          id: res.tenantId,
          name: res.tenantName || res.schoolName || '',
          isAvailable: true,
        });
      }
    } catch {
      // 探测失败不影响正常启动：平台域名下本就无需此上下文
    }
  }

  private clearTenantCookie(): void {
    try {
      this.document.cookie = `__tenant=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax`;
    } catch {
      // ignore
    }
  }
}
