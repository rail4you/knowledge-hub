import { Injectable, inject, signal, computed } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable, tap } from 'rxjs';

export interface BrandingDto {
  appTitle: string;
  appSubtitle: string;
  footerText: string;
  logoUrl: string;
}

export interface UpdateBrandingDto {
  appTitle: string;
  appSubtitle?: string;
  footerText?: string;
  logoUrl?: string;
}

const DEFAULT_BRANDING: BrandingDto = {
  appTitle: '易课通',
  appSubtitle: '知识资源库',
  footerText: '© 2026 易课通 · 知识资源库',
  logoUrl: '',
};

/**
 * 站点品牌（全局唯一）：标题 / 副标题 / 页脚文本 / Logo。
 * 默认值起步，后台 GET /api/app/branding 返回后更新；读取公开匿名。
 */
@Injectable({
  providedIn: 'root',
})
export class BrandingService {
  private readonly rest = inject(RestService);
  private readonly apiName = 'KnowledgeHub';
  private readonly url = '/api/app/branding';

  private loading = false;

  readonly branding = signal<BrandingDto>({ ...DEFAULT_BRANDING });
  readonly loaded = signal(false);

  readonly appTitle = computed(() => this.branding().appTitle);
  readonly appSubtitle = computed(() => this.branding().appSubtitle);
  readonly footerText = computed(() => this.branding().footerText);
  readonly logoUrl = computed(() => this.branding().logoUrl);

  /** 首屏各处调用，幂等：只拉取一次，失败静默保留默认值 */
  ensureLoaded(): void {
    if (this.loaded() || this.loading) return;
    this.loading = true;
    this.load().subscribe({
      next: () => (this.loading = false),
      error: () => (this.loading = false),
    });
  }

  load(): Observable<BrandingDto> {
    return this.rest
      .request<void, BrandingDto>(
        { method: 'GET', url: this.url },
        { apiName: this.apiName },
      )
      .pipe(
        tap(res => {
          if (res) {
            this.branding.set({ ...DEFAULT_BRANDING, ...res });
            this.loaded.set(true);
          }
        }),
      );
  }

  update(input: UpdateBrandingDto): Observable<BrandingDto> {
    return this.rest
      .request<UpdateBrandingDto, BrandingDto>(
        { method: 'PUT', url: this.url, body: input },
        { apiName: this.apiName },
      )
      .pipe(
        tap(res => {
          if (res) {
            this.branding.set({ ...DEFAULT_BRANDING, ...res });
            this.loaded.set(true);
          }
        }),
      );
  }
}
