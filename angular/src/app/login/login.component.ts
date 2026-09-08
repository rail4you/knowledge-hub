import { Component, inject, OnInit, signal, HostListener, computed } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, ActivatedRoute, Router } from '@angular/router';
import {
  AuthService,
  ConfigStateService,
  SessionStateService,
  LocalizationPipe,
  EnvironmentService,
  AbpTenantService,
} from '@abp/ng.core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { SiteBrandComponent } from '../shared/branding/site-brand.component';
import { SiteFooterComponent } from '../shared/branding/site-footer.component';
import { TenantListService } from '../proxy/controllers/tenant-list.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { DOCUMENT } from '@angular/common';
import { catchError, finalize } from 'rxjs/operators';
import { throwError } from 'rxjs';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, RouterLinkActive, LocalizationPipe, NzIconModule, SiteBrandComponent, SiteFooterComponent],
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(UntypedFormBuilder);
  private readonly authService = inject(AuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly sessionState = inject(SessionStateService);
  private readonly toasterService = inject(ToasterService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly environmentService = inject(EnvironmentService);
  private readonly abpTenantService = inject(AbpTenantService);
  private readonly tenantListService = inject(TenantListService);

  form!: UntypedFormGroup;
  inProgress = false;
  showPassword = signal(false);
  langDropdownOpen = signal(false);
  languages: { cultureName: string; displayName: string; flagIcon: string }[] = [];
  currentLang = '';

  // 租户（Tab 列表方式，仅展示真实租户；宿主/全局入口已隐藏，见 isHostLogin）
  tenantList = signal<{ id: string; name: string }[]>([]);
  tenantsLoaded = signal(false);
  tenantsLoadError = signal(false);
  currentTenantName = signal<string | null>(null);
  currentTenantId = signal<string | null>(null);
  // 宿主管理员专用通道：通过隐藏网址 /admin-login（或 /account/login?host=true）进入，
  // 页面上不显示任何入口。普通登录页此值为 false。
  isHostLogin = signal(false);
  // 是否展示账号密码登录表单：
  // - 宿主通道：始终展示
  // - 普通页加载中：先隐藏，避免租户 cookie 就绪前误以宿主身份提交
  // - 普通页加载失败：仍展示（用户可能已有有效租户 cookie，允许尝试登录）
  // - 普通页无租户：隐藏，等管理员分配租户后再登录
  canShowForm = computed(() => {
    if (this.isHostLogin()) return true;
    if (!this.tenantsLoaded()) return false;
    if (this.tenantsLoadError()) return true;
    return this.tenantList().length > 0;
  });

  ngOnInit() {
    this.clearSession();
    this.buildForm();
    this.loadLanguages();
    const dataHostMode = this.route.snapshot.data?.['hostMode'] === true;
    const queryHost = this.route.snapshot.queryParams?.['host'];
    const pathHost = this.router.url.split('?')[0].includes('admin-login');
    const hostMode = dataHostMode || pathHost || queryHost === 'true' || queryHost === '1';
    this.isHostLogin.set(hostMode);
    if (hostMode) {
      // 强制宿主上下文：清除残留租户 cookie，不拉取租户列表。
      // 同时写 __host_login 标记：OAuth 跳转到后端 /Account/Login 时 query 会丢失，
      // 后端靠此标记（且无租户 cookie）识别宿主模式，避免渲染租户页拦截 admin。
      this.clearTenantCookie();
      this.document.cookie = `__host_login=1; path=/; SameSite=Lax`;
      // 关键：同步清除 ABP 会话级租户（内存）。
      // SPA 的 SessionState.tenant 在应用启动时已由 application-configuration 种下
      // （残留 __tenant cookie 会让匿名配置请求也解析出租户），只清 cookie 不够；
      // password-flow 的 token 请求会把该值当 __tenant header 发出，导致宿主 admin
      // 被签发成租户 token，登录后掉进租户上下文、打不开 host 专属管理页。
      this.sessionState.setTenant(null);
      this.currentTenantId.set(null);
      this.currentTenantName.set(null);
    } else {
      // 普通页清除宿主标记，避免陈旧标记影响后端判断
      this.document.cookie = `__host_login=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax`;
      this.loadTenants();
      this.loadCurrentTenant();
    }
  }

  private loadTenants() {
    this.tenantListService.getTenants().subscribe({
      next: (list: any) => {
        const items = Array.isArray(list) ? list : [];
        // 过滤掉后端返回的 { Id: null, Name: '全局' } 占位项，只保留真实租户
        const realTenants = items
          .filter((t: any) => t?.id != null && t?.name)
          .map((t: any) => ({ id: t.id, name: t.name }));
        this.tenantList.set(realTenants);
        this.tenantsLoaded.set(true);
        this.tenantsLoadError.set(false);
        if (realTenants.length === 0) {
          // 无租户兜底：系统尚无租户（或接口返回空）时，清除可能残留的租户 cookie，
          // 直接以宿主身份登录，模板侧显示文字提示。
          this.clearTenantCookie();
          this.currentTenantId.set(null);
          this.currentTenantName.set(null);
          return;
        }
        // 普通登录页默认不再是宿主：无 cookie，或 cookie 指向已不存在的租户时，
        // 自动选中第一个租户，避免误以宿主身份登录失败。
        const current = this.currentTenantId();
        if (!current || !realTenants.some(t => t.id === current)) {
          this.selectTenant(realTenants[0], true);
        }
      },
      error: () => {
        this.tenantList.set([]);
        this.tenantsLoaded.set(true);
        this.tenantsLoadError.set(true);
      },
    });
  }

  private loadCurrentTenant() {
    try {
      const match = this.document.cookie.match(/(?:^|; )__tenant=([^;]*)/);
      const tenantId = match ? decodeURIComponent(match[1]) : null;
      if (tenantId) {
        this.currentTenantId.set(tenantId);
        this.abpTenantService.findTenantById(tenantId).subscribe({
          next: (res: any) => {
            if (res?.success && res?.name) {
              this.currentTenantName.set(res.name);
            } else {
              this.currentTenantName.set(tenantId);
            }
          },
          error: () => this.currentTenantName.set(tenantId),
        });
      }
    } catch {}
  }

  selectTenant(tenant: { id: string; name: string }, silent = false) {
    if (!tenant?.id) return;
    this.document.cookie = `__tenant=${encodeURIComponent(tenant.id)}; path=/; SameSite=Lax`;
    this.currentTenantId.set(tenant.id);
    this.currentTenantName.set(tenant.name);
    if (!silent) {
      this.toasterService.success(`已切换到租户：${tenant.name}`);
    }
  }

  private clearTenantCookie() {
    this.document.cookie = `__tenant=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax`;
  }

  clearTenant() {
    this.clearTenantCookie();
    this.currentTenantId.set(null);
    this.currentTenantName.set(null);
    this.toasterService.success('已清除租户，将以系统管理员身份登录');
  }

  /**
   * 清除 IdP session 和 OAuth token
   * 关键：ABP Identity Server 使用 .AspNetCore.Identity.Application cookie 存储 session
   * 必须清除这个 cookie 才能在登录页显示账号选择而不是自动登录上一个用户
   */
  private clearSession() {
    const cookieNames = [
      'idsrv',
      'idsrv.session',
      '.AspNetCore.Identity.Application',
      '.AspNetCore.Session',
      'IdentityServer',
      'Abp.AuthToken',
      'refreshToken',
    ];

    cookieNames.forEach(name => {
      try {
        const paths = ['/', '/connect', '/account'];
        const domains = [window.location.hostname, '.' + window.location.hostname];
        paths.forEach(path => {
          domains.forEach(domain => {
            try {
              this.document.cookie = `${name}=; path=${path}; domain=${domain}; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax`;
            } catch (e) { /* ignore cross-origin errors */ }
          });
          try {
            this.document.cookie = `${name}=; path=${path}; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax`;
          } catch (e) { /* ignore */ }
        });
      } catch (e) {
        // 跨域 cookie 无法访问
      }
    });

    // 清除 OAuth token storage
    try {
      ['access_token', 'id_token', 'refresh_token', 'expires_at', 'session_state', 'granted_scopes', 'Abp.AuthToken'].forEach(key => {
        localStorage.removeItem(key);
        sessionStorage.removeItem(key);
      });
    } catch (e) {
      // 跨域 storage 可能失败
    }
  }

  buildForm() {
    this.form = this.fb.group({
      username: ['', [Validators.required, Validators.maxLength(255)]],
      password: ['', [Validators.required, Validators.maxLength(128)]],
      rememberMe: [false],
    });
  }

  loadLanguages() {
    this.configState
      .getDeep$('localization.languages')
      .subscribe((langs: any[]) => {
        if (langs) {
          this.languages = langs.map((l: any) => ({
            cultureName: l.cultureName,
            displayName: l.displayName,
            flagIcon: l.flagIcon,
          }));
        }
      });
    this.currentLang = this.sessionState.getLanguage() || 'zh-Hans';
  }

  getCurrentLangDisplayName(): string {
    const lang = this.languages.find((l) => l.cultureName === this.currentLang);
    return lang ? lang.displayName : this.currentLang;
  }

  onChangeLang(cultureName: string) {
    this.sessionState.setLanguage(cultureName);
    this.currentLang = cultureName;
    this.langDropdownOpen.set(false);
  }

  toggleLangDropdown() {
    this.langDropdownOpen.update((v) => !v);
  }

  @HostListener('document:click', ['$event.target'])
  onDocumentClick(target: HTMLElement) {
    const selector = target.closest('.language-selector');
    if (!selector) {
      this.langDropdownOpen.set(false);
    }
  }

  togglePassword() {
    this.showPassword.update((v) => !v);
  }

  onSubmit() {
    if (this.form.invalid) return;

    const userName = (this.form.value.username || '').trim();
    // 租户通道拒绝保留账号 admin（与后端 KnowledgeHubLoginModel 守卫一致）：
    // admin 只属于宿主，租户上下文登录必然失败；直接指引到专用通道，
    // 落实“/admin-login 只负责系统管理员账户”。
    if (!this.isHostLogin() && userName.toLowerCase() === 'admin') {
      this.toasterService.error('系统管理员请使用专用通道 /admin-login 登录');
      return;
    }

    this.inProgress = true;
    const { username, password, rememberMe } = this.form.value;
    const redirectUrl = this.isHostLogin()
      // 系统管理员登录成功后直达管理端，不经过门户首页
      ? (this.route.snapshot.queryParams['returnUrl'] || '/resources')
      : (this.route.snapshot.queryParams['returnUrl'] || '/');

    // 登录前再次清除可能的残留 session
    this.clearSession();
    if (this.isHostLogin()) {
      // 宿主登录提交瞬间再清一次会话租户（防初始化后被意外重设），
      // 确保 password-flow 不带 __tenant header，后端签发宿主 token。
      this.clearTenantCookie();
      this.sessionState.setTenant(null);
    }

    this.authService
      .login({ username, password, rememberMe, redirectUrl })
      .pipe(
        catchError((err: any) => {
          this.toasterService.error(
            err?.error?.error_description ||
              err?.error?.error?.message ||
              '::DefaultErrorMessage',
            '',
            { life: 7000 }
          );
          return throwError(() => err);
        }),
        finalize(() => (this.inProgress = false))
      )
      .subscribe();
  }
}
