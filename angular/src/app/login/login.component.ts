import { Component, inject, OnInit, OnDestroy, signal, HostListener, computed } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, ActivatedRoute, Router } from '@angular/router';
import {
  AuthService,
  ConfigStateService,
  SessionStateService,
  LocalizationPipe,
  LocalizationService,
  EnvironmentService,
  AbpTenantService,
} from '@abp/ng.core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { SiteBrandComponent } from '../shared/branding/site-brand.component';
import { SiteFooterComponent } from '../shared/branding/site-footer.component';
import { TenantListService } from '../proxy/controllers/tenant-list.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { DOCUMENT } from '@angular/common';
import { AUTH_RETURN_KEY } from '../auth/post-login.guard';
import { catchError, tap } from 'rxjs/operators';
import { Subscription, throwError } from 'rxjs';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, RouterLinkActive, LocalizationPipe, NzIconModule, SiteBrandComponent, SiteFooterComponent],
})
export class LoginComponent implements OnInit, OnDestroy {
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
  private readonly localization = inject(LocalizationService);

  form!: UntypedFormGroup;
  inProgress = false;
  private readonly queryParamSub = new Subscription();
  /** 登录成功后的导航兜底计时器：超时仍未卸载说明跳转失败，需解除成功态。 */
  private successFallbackTimer: ReturnType<typeof setTimeout> | null = null;
  /** 登录失败的行内错误（已本地化/转义为可直接展示的文本，模板直接插值）。 */
  loginError = signal<string | null>(null);
  /**
   * 登录成功标记：token + 应用配置刷新完成后置 true，按钮切换为"正在进入…"。
   * 故意不在成功时复位 inProgress——pipeToLogin 的导航、守卫、目标页懒加载 chunk
   * 都发生在 observable 完成之后，此时若恢复成可点击的表单，用户会在空白间隙里
   * 以为"没反应"而重复提交。本页被导航卸载时状态自然丢弃，无需手动复位。
   */
  loginSucceeded = signal(false);
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
  // 宿主管理员专用通道：通过 /admin-login（或 /account/login?host=true）进入，
  // 租户登录页底部提供“系统管理员登录”切换入口。
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
    // 跨租户兜底被触发（postLoginGuard 打回）：明示原因，不静默。
    // 必须订阅而不能只读快照：守卫打回的是同一个登录组件实例
    //（/account/login → /account/login?error=…），Angular 会复用组件，
    // ngOnInit 不会重跑，快照写法会导致按钮卡在"正在进入系统…"。
    this.queryParamSub.add(
      this.route.queryParamMap.subscribe(params => {
        if (params.get('error') === 'tenant-mismatch') {
          this.resetProgress();
          this.loginSucceeded.set(false);
          this.loginError.set('登录账号不属于所选机构，已阻止进入，请确认机构后重试');
        }
      })
    );
    const dataHostMode = this.route.snapshot.data?.['hostMode'] === true;
    const queryHost = this.route.snapshot.queryParams?.['host'];
    const pathHost = this.router.url.split('?')[0].includes('admin-login');
    const hostMode = dataHostMode || pathHost || queryHost === 'true' || queryHost === '1';
    this.isHostLogin.set(hostMode);
    if (hostMode) {
      // 强制宿主上下文：清除残留租户 cookie，不拉取租户列表。
      // 纯 Angular 密码授权：凭据直接 POST /connect/token，不经过后端 Razor 登录页，
      // 无需再写 __host_login 标记；只要会话租户为空，后端即按宿主签发 token。
      this.clearTenantCookie();
      // 关键：同步清除 ABP 会话级租户（内存）。
      // SPA 的 SessionState.tenant 在应用启动时已由 application-configuration 种下
      // （残留 __tenant cookie 会让匿名配置请求也解析出租户），只清 cookie 不够；
      // password-flow 的 token 请求会把该值当 __tenant header 发出，导致宿主 admin
      // 被签发成租户 token，登录后掉进租户上下文、打不开 host 专属管理页。
      this.sessionState.setTenant(null);
      this.currentTenantId.set(null);
      this.currentTenantName.set(null);
    } else {
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
          // 同步清空会话级租户：密码授权的 token 请求用该值发 __tenant header，
          // 只清 cookie 会让请求仍带旧租户，宿主登录会被误签成租户 token。
          this.sessionState.setTenant(null);
          this.currentTenantId.set(null);
          this.currentTenantName.set(null);
          return;
        }
        // 普通登录页默认不再是宿主：无 cookie，或 cookie 指向已不存在的租户时，
        // 自动选中第一个租户，避免误以宿主身份登录失败。
        const current = this.currentTenantId();
        if (!current || !realTenants.some(t => t.id === current)) {
          this.selectTenant(realTenants[0]);
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

  selectTenant(tenant: { id: string; name: string }) {
    if (!tenant?.id) return;
    this.document.cookie = `__tenant=${encodeURIComponent(tenant.id)}; path=/; SameSite=Lax`;
    // 关键：同步会话级租户。密码授权的 token 请求从 sessionState 取租户发
    // __tenant header（OAuthApiInterceptor 同理），只写 cookie 会导致本次登录
    // 仍用旧租户（应用启动时种下的值）请求 /connect/token，进错租户且无任何提示。
    this.sessionState.setTenant({ id: tenant.id, name: tenant.name, isAvailable: true });
    this.currentTenantId.set(tenant.id);
    this.currentTenantName.set(tenant.name);
    // 选中即收起下拉，不再弹 toast 打扰（与后端登录页行为一致）。
    this.tenantDropdownOpen.set(false);
  }

  /** 机构下拉开关（样式对齐后端 Login.cshtml 的 tenant-select）。 */
  tenantDropdownOpen = signal(false);

  toggleTenantDropdown(event?: MouseEvent) {
    event?.stopPropagation();
    this.tenantDropdownOpen.update(v => !v);
  }

  private clearTenantCookie() {
    this.document.cookie = `__tenant=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax`;
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
    if (!target.closest('.language-selector')) {
      this.langDropdownOpen.set(false);
    }
    if (!target.closest('.tenant-select')) {
      this.tenantDropdownOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape() {
    this.langDropdownOpen.set(false);
    this.tenantDropdownOpen.set(false);
  }

  ngOnDestroy() {
    this.queryParamSub.unsubscribe();
    this.clearSuccessFallback();
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
      const msg = '系统管理员请使用专用通道 /admin-login 登录';
      this.loginError.set(msg);
      this.toasterService.error(msg);
      return;
    }

    // 租户通道必须有明确机构：机构列表加载失败/未选中时禁止提交。
    // 否则 token 请求会沿用应用启动时残留的租户上下文，进错机构且无法被
    // postLoginGuard 兜底（无 __tenant cookie 时守卫会跳过一致性校验）。
    if (!this.isHostLogin() && !this.currentTenantId()) {
      const msg = this.tenantsLoadError()
        ? '机构列表加载失败，请刷新页面重试'
        : '请先选择机构';
      this.loginError.set(msg);
      return;
    }

    this.inProgress = true;
    this.loginSucceeded.set(false);
    this.loginError.set(null);
    const { username, password, rememberMe } = this.form.value;
    const returnUrl = this.route.snapshot.queryParams['returnUrl'];
    // 登录前拿不到角色，不能直接按 returnUrl 跳转：学生若从管理端链接进登录页
    // （如 /admin/employment/jobs 被 authGuard 打回登录），登录后会带着管理端
    // returnUrl 直达该路由，被权限守卫拒成 403 且不再回到学生门户。
    // 因此普通登录一律先进统一落点 /post-login，由 postLoginGuard 用登录后的
    // 真实角色分发：管理员可继续去 returnUrl，学生一律回 /student。
    //
    // returnUrl 不能拼接在 redirectUrl 上（ABP 会把 redirectUrl 当路由段导航，
    // `?query` 会被 URL 编码成单个 segment → NG04002 404），只能暂存 sessionStorage。
    if (returnUrl) {
      try {
        sessionStorage.setItem(AUTH_RETURN_KEY, returnUrl);
      } catch {
        // 隐私模式等场景可能禁止写入，忽略，不影响登录。
      }
    }
    const redirectUrl = this.isHostLogin()
      // 宿主系统管理员可安全直达 returnUrl / 工作台
      ? (returnUrl || '/admin/workbench')
      : '/post-login';

    // 登录前再次清除可能的残留 session
    this.clearSession();
    if (this.isHostLogin()) {
      // 宿主登录提交瞬间再清一次会话租户（防初始化后被意外重设），
      // 确保 password-flow 不带 __tenant header，后端签发宿主 token。
      this.clearTenantCookie();
      this.sessionState.setTenant(null);
    } else if (this.currentTenantId()) {
      // 租户通道提交前将会话租户与当前所选 Tab 对齐，防 token 请求带错租户。
      this.sessionState.setTenant({
        id: this.currentTenantId()!,
        name: this.currentTenantName() || this.currentTenantId()!,
        isAvailable: true,
      });
    }

    this.authService
      .login({ username, password, rememberMe, redirectUrl })
      .pipe(
        tap(() => {
          this.loginSucceeded.set(true);
          // 兜底：登录成功但后续导航/目标页 chunk 加载失败时，超时后解除"正在进入系统"，
          // 允许用户重试。导航由 pipeToLogin 内部触发，其失败无法在此 observable 捕获。
          this.scheduleSuccessFallback();
        }),
        catchError((err: any) => {
          const message = this.resolveLoginError(err);
          // 行内错误为主（empty 布局下 toast 容器可能不可见），toast 为辅。
          this.loginError.set(message);
          this.toasterService.error(message, '', { life: 7000 });
          this.resetProgress();
          return throwError(() => err);
        })
      )
      .subscribe({ error: () => this.resetProgress() });
  }

  /** 登录成功后启动导航兜底：超时仍未卸载本组件，则解除成功态并提示重试。 */
  private scheduleSuccessFallback(): void {
    this.clearSuccessFallback();
    this.successFallbackTimer = setTimeout(() => {
      if (!this.loginSucceeded()) return;
      this.loginSucceeded.set(false);
      this.inProgress = false;
      this.loginError.set('登录成功，但页面跳转失败，请重试');
    }, 10000);
  }

  private clearSuccessFallback(): void {
    if (this.successFallbackTimer) {
      clearTimeout(this.successFallbackTimer);
      this.successFallbackTimer = null;
    }
  }

  /** 复位登录进度态（失败/被守卫打回时调用），同时取消成功态兜底计时器。 */
  private resetProgress(): void {
    this.clearSuccessFallback();
    this.inProgress = false;
  }

  /**
   * 把登录失败翻译成用户能看懂的提示。
   * 密码授权失败时后端（OpenIddict /connect/token）返回 invalid_grant，
   * error_description 是英文技术文本，直接展示不友好，统一映射到本地化 key。
   */
  private resolveLoginError(err: any): string {
    // 网络层失败（后端未启动/重启中、代理不通）：status 为 0 且没有业务错误体。
    if (!err || err.status === 0) {
      return '无法连接服务器，请确认后端服务已启动后再试';
    }
    const data = err?.error;
    const grantError = typeof data?.error === 'string' ? data.error : '';
    if (grantError === 'invalid_grant') {
      return this.localization.instant('AbpAccount::InvalidUserNameOrPassword');
    }
    const raw =
      (typeof data?.error_description === 'string' && data.error_description) ||
      (typeof data?.error?.message === 'string' && data.error.message) ||
      '';
    if (raw) return raw;
    return this.localization.instant('AbpAccount::InvalidUserNameOrPassword');
  }
}
