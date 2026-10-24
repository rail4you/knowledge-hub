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
  RestService,
} from '@abp/ng.core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AuthShellComponent } from '../shared/auth/auth-shell.component';
import { TenantListService } from '../proxy/controllers/tenant-list.service';
import { SchoolDomainContextService } from '../core/tenant/school-domain-context.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { DOCUMENT } from '@angular/common';
import { AUTH_RETURN_KEY } from '../auth/post-login.guard';
import { catchError, map, tap } from 'rxjs/operators';
import { EMPTY, Observable, Subscription } from 'rxjs';

/**
 * 登录页机构下拉项。
 *
 * <para><b>displayName</b> 是给人看的中文机构名（学校档案名 → 专业库展示名 → 租户名），
 * <b>name</b> 是租户原始名（slug），仅用于兜底匹配与回显。</para>
 */
export interface TenantOption {
  id: string;
  /** 租户原始名（slug，例如 qidi） */
  name: string;
  /** 中文机构名 */
  displayName: string;
  /** 学校徽标 */
  logoUrl?: string | null;
  /** 是否平台唯一「社会学校」（公开注册用户的归属机构） */
  isPublicSchool?: boolean;
}

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, RouterLinkActive, LocalizationPipe, NzIconModule, AuthShellComponent],
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
  private readonly restService = inject(RestService);
  private readonly schoolDomain = inject(SchoolDomainContextService);

  /** 当前是否处于某所学校的专属域名（域名绑定多租户） */
  isSchoolDomain = computed(() => this.schoolDomain.isSchoolDomain());
  /** 学校域名下的学校名称 */
  schoolName = computed(() => this.schoolDomain.schoolName());
  /** 学校域名下的学校徽标 */
  schoolLogo = computed(() => this.schoolDomain.schoolLogo());

  form!: UntypedFormGroup;
  inProgress = false;
  private readonly queryParamSub = new Subscription();
  /** 登录成功后的导航兜底计时器：超时仍未卸载说明跳转失败，需解除成功态。 */
  private successFallbackTimer: ReturnType<typeof setTimeout> | null = null;
  /** 提交序号：每次提交自增；晚到的旧提交结果（成功/失败）一律丢弃。 */
  private attemptSeq = 0;
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
  tenantList = signal<TenantOption[]>([]);
  tenantsLoaded = signal(false);
  tenantsLoadError = signal(false);
  /** 机构下拉的搜索关键字（租户较多时按中文机构名过滤） */
  tenantKeyword = signal('');
  /** 兜底机构名：租户列表尚未加载完成时，用租户档案/租户名先顶上 */
  private readonly fallbackTenantName = signal<string | null>(null);
  currentTenantId = signal<string | null>(null);
  /**
   * 当前机构显示名：优先用列表里的中文机构名（学校档案名 → 专业库展示名 → 租户名），
   * 列表未就绪时回退到兜底名。需求「机构名称要用中文」，不能直接展示租户 slug。
   */
  readonly currentTenantName = computed<string | null>(() => {
    const id = this.currentTenantId();
    if (!id) return null;
    const hit = this.tenantList().find(t => t.id === id);
    return hit?.displayName || this.fallbackTenantName();
  });
  /** 下拉里按关键字过滤后的机构列表 */
  readonly filteredTenants = computed<TenantOption[]>(() => {
    const keyword = (this.tenantKeyword() || '').trim().toLowerCase();
    const all = this.tenantList();
    if (!keyword) return all;
    return all.filter(t =>
      (t.displayName || '').toLowerCase().includes(keyword) ||
      (t.name || '').toLowerCase().includes(keyword)
    );
  });
  /** 当前机构的徽标（学校有上传徽标时展示，没有则回退到首字色块） */
  readonly currentTenantLogo = computed<string | null>(() => {
    const id = this.currentTenantId();
    if (!id) return null;
    return this.tenantList().find(t => t.id === id)?.logoUrl || null;
  });

  /**
   * 社会学校提示。
   *
   * <para>需求「社会学校的学生登录怎么处理」：公开注册的用户归属平台唯一「社会学校」，
   * 登录时必须在机构下拉里选中它，否则会落到别的学校上下文导致账号密码校验失败。
   * 这里在注册按钮下方明示该选哪一项。</para>
   */
  readonly publicSchoolHint = computed<string | null>(() => {
    const ps = this.tenantList().find(t => t.isPublicSchool);
    if (!ps) return null;
    if (this.currentTenantId() === ps.id) return null;
    return `公开注册的用户请选择「${ps.displayName}」登录`;
  });
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
    // 域名绑定：学校专属域名下机构已由域名锁定，表单始终可提交
    if (this.isSchoolDomain()) return true;
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
      this.fallbackTenantName.set(null);
    } else {
      this.loadTenants();
      this.loadCurrentTenant();
    }
  }

  private loadTenants() {
    // 域名绑定：学校专属域名下机构已被域名锁定，不再拉取全量租户列表，
    // 直接采用域名解析出的学校作为当前机构。
    if (this.isSchoolDomain()) {
      const ctx = this.schoolDomain.context();
      const tenantId = ctx?.tenantId || null;
      const tenantName = ctx?.schoolName || ctx?.tenantName || null;
      this.tenantList.set(
        tenantId
          ? [{ id: tenantId, name: tenantName || tenantId, displayName: tenantName || tenantId, logoUrl: ctx?.logoUrl || null }]
          : []
      );
      this.tenantsLoaded.set(true);
      this.tenantsLoadError.set(false);
      this.currentTenantId.set(tenantId);
      this.tenantKeyword.set('');
      return;
    }

    this.tenantListService.getTenants().subscribe({
      next: (list: any) => {
        const items = Array.isArray(list) ? list : [];
        // 过滤掉后端返回的 { Id: null, Name: '全局' } 占位项，只保留真实租户
        const realTenants: TenantOption[] = items
          .filter((t: any) => t?.id != null && t?.name)
          .map((t: any) => ({
            id: t.id,
            name: t.name,
            // 中文机构名：后端已把学校档案名放进 schoolName；没有则退回专业库展示名 / 租户名
            displayName: t.schoolName || t.tenantName || t.name,
            logoUrl: t.logoUrl || null,
            isPublicSchool: t.isPublicSchool === true,
          }))
          // 社会学校排最前：公开注册用户一眼就能在列表里找到自己的机构
          .sort((a, b) => Number(!!b.isPublicSchool) - Number(!!a.isPublicSchool));
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
          this.fallbackTenantName.set(null);
          return;
        }
        // 普通登录页默认不再是宿主：无 cookie，或 cookie 指向已不存在的租户时，
        // 自动选中一个租户，避免误以宿主身份登录失败。
        const current = this.currentTenantId();
        // 注册页跳转过来时带了 ?tenant=<社会学校 Id>，优先按它选中，
        // 免得社会用户注册完还要自己找机构。
        const wanted = this.route.snapshot.queryParamMap.get('tenant');
        const wantedTenant = wanted ? realTenants.find(t => t.id === wanted) : undefined;
        if (wantedTenant) {
          this.selectTenant(wantedTenant);
        } else if (!current || !realTenants.some(t => t.id === current)) {
          // 默认不选「社会学校」：它是公开注册用户的归属机构，
          // 各校师生登录时应默认落在自己学校（社会学校在下拉里置顶，仍可手动选）。
          const fallback = realTenants.find(t => !t.isPublicSchool) || realTenants[0];
          this.selectTenant(fallback);
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
        // 兜底名：租户列表尚未就绪时先用租户档案名顶上；
        // 列表到位后 currentTenantName（computed）会自动改用中文机构名。
        this.abpTenantService.findTenantById(tenantId).subscribe({
          next: (res: any) => {
            if (res?.success && res?.name) {
              this.fallbackTenantName.set(res.name);
            } else {
              this.fallbackTenantName.set(tenantId);
            }
          },
          error: () => this.fallbackTenantName.set(tenantId),
        });
      }
    } catch {}
  }

  selectTenant(tenant: TenantOption) {
    if (!tenant?.id) return;
    this.document.cookie = `__tenant=${encodeURIComponent(tenant.id)}; path=/; SameSite=Lax`;
    // 关键：同步会话级租户。密码授权的 token 请求从 sessionState 取租户发
    // __tenant header（OAuthApiInterceptor 同理），只写 cookie 会导致本次登录
    // 仍用旧租户（应用启动时种下的值）请求 /connect/token，进错租户且无任何提示。
    this.sessionState.setTenant({ id: tenant.id, name: tenant.displayName || tenant.name, isAvailable: true });
    this.currentTenantId.set(tenant.id);
    // 选中即收起下拉，不再弹 toast 打扰（与后端登录页行为一致）。
    this.tenantDropdownOpen.set(false);
    this.tenantKeyword.set('');
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

  /**
   * 账号输入错误提示。
   *
   * <para>需求「在登陆界面如果没输入账号密码也应该有提醒」：原来
   * <c>onSubmit()</c> 在表单非法时直接静默 return，用户点了登录毫无反馈，
   * 以为是按钮坏了。这里改成失焦/提交后行内提示 + 顶部错误条。</para>
   */
  readonly usernameError = computed<string | null>(() => {
    const control = this.form?.get('username');
    if (!control || !control.touched) return null;
    if (!String(control.value || '').trim()) return '请输入账号';
    return null;
  });

  /** 密码输入错误提示（同 usernameError）。 */
  readonly passwordError = computed<string | null>(() => {
    const control = this.form?.get('password');
    if (!control || !control.touched) return null;
    if (!control.value) return '请输入密码';
    return null;
  });

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
  onDocumentClick(target: EventTarget | null) {
    // $event.target 是 EventTarget，文本节点/非元素节点都可能进来，先收窄
    if (!(target instanceof HTMLElement)) return;
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
    if (this.form.invalid) {
      // 需求：账号/密码没填也要给提醒，不能静默 return 让用户以为按钮坏了。
      this.form.markAllAsTouched();
      const msg = !String(this.form.value.username || '').trim() ? '请输入账号' : '请输入密码';
      this.loginError.set(msg);
      this.toasterService.error(msg);
      return;
    }

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
    // 本次提交序号：刷新登录后，上一次失败的异步查询结果不得再覆盖页面提示
    const attempt = ++this.attemptSeq;
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
          // 已有更新的提交（用户重试）时，丢弃本次迟到的成功结果
          if (attempt !== this.attemptSeq) return;
          this.loginSucceeded.set(true);
          // 登录成功即清空旧失败提示，避免上一次的错误残留到已登录页面
          this.loginError.set(null);
          // 兜底：登录成功但后续导航/目标页 chunk 加载失败时，超时后解除"正在进入系统"，
          // 允许用户重试。导航由 pipeToLogin 内部触发，其失败无法在此 observable 捕获。
          this.scheduleSuccessFallback();
        }),
        catchError((err: any) => {
          // 过期提交（用户已重试或已成功登录）：丢弃，不弹旧提示
          if (attempt !== this.attemptSeq || this.loginSucceeded()) {
            return EMPTY;
          }
          return this.resolveLoginError$(err).pipe(
            tap(message => {
              if (attempt !== this.attemptSeq || this.loginSucceeded()) return;
              // 行内错误为主（empty 布局下 toast 容器可能不可见），toast 为辅。
              this.loginError.set(message);
              this.toasterService.error(message, '', { life: 7000 });
              this.resetProgress();
            }),
            catchError(() => {
              if (attempt !== this.attemptSeq || this.loginSucceeded()) {
                return EMPTY;
              }
              // 查询锁定信息失败时兜底，仍按通用错误处理。
              const fallback = this.resolveLoginError(err);
              this.loginError.set(fallback);
              this.toasterService.error(fallback, '', { life: 7000 });
              this.resetProgress();
              return EMPTY;
            })
          );
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
   * 登录失败时解析提示信息。
   * invalid_grant（密码错误/账号被锁）会额外向后端查询该账号的锁定状态与剩余尝试次数，
   * 以便提示"还剩 N 次机会"或"账号已被锁定，请联系管理员"。
   * 查询本身失败时返回通用"用户名或密码错误"。
   */
  private resolveLoginError$(err: any): Observable<string> {
    // 网络层失败（后端未启动/重启中、代理不通）：status 为 0 且没有业务错误体。
    if (!err || err.status === 0) {
      return new Observable(sub => {
        sub.next('无法连接服务器，请确认后端服务已启动后再试');
        sub.complete();
      });
    }
    const data = err?.error;
    const grantError = typeof data?.error === 'string' ? data.error : '';
    if (grantError === 'invalid_grant') {
      const userName = this.form?.value?.username;
      return this.queryLoginAttemptInfo(userName).pipe(
        map(message => this.withSchoolDomainHint(message)),
        catchError(() => new Observable<string>(sub => {
          sub.next(this.withSchoolDomainHint(this.localization.instant('AbpAccount::InvalidUserNameOrPassword')));
          sub.complete();
        }))
      );
    }
    return new Observable<string>(sub => {
      sub.next(this.resolveLoginError(err));
      sub.complete();
    });
  }

  /**
   * 学校专属域名下补充提示。
   *
   * <p>域名绑定后，账号必须属于本校才能登录；用别的学校的账号会得到 invalid_grant，
   * 通用文案「用户名或密码错误」会误导用户。追加一句归属说明即可，
   * 且不泄露任何账号信息（不判断账号是否存在）。</p>
   */
  private withSchoolDomainHint(message: string): string {
    if (!this.isSchoolDomain()) {
      return message;
    }
    const school = this.schoolName() || '当前学校';
    return `${message}（当前站点为「${school}」的专属域名，请使用该学校的账号登录）`;
  }

  /**
   * 查询当前租户下指定账号的锁定状态与剩余尝试次数。
   * 接口地址见 KnowledgeHub.HttpApi 的 TenantListController.GetLoginAttemptInfo。
   */
  private queryLoginAttemptInfo(userName: string): Observable<string> {
    if (!userName) {
      return new Observable<string>(sub => {
        sub.next(this.localization.instant('AbpAccount::InvalidUserNameOrPassword'));
        sub.complete();
      });
    }
    return this.restService.request<any, LoginAttemptInfoDto>({
      method: 'GET',
      url: '/api/public/login-attempt-info',
      params: { userName },
    }).pipe(
      map(info => this.buildLoginAttemptMessage(info)),
    );
  }

  /** 根据后端返回的锁定状态构造登录失败提示。 */
  private buildLoginAttemptMessage(info: LoginAttemptInfoDto): string {
    // 账号已锁定：明确提示联系管理员（管理员在用户管理中可解除锁定）。
    if (info?.isLockedOut) {
      return '您的账号已被锁定，请联系管理员解除锁定后重新登录';
    }
    // 用户存在但密码错误：提示已失败次数与剩余次数。
    if (info?.userExists) {
      const remaining = Math.max(0, info.remainingAttempts);
      if (remaining <= 0) {
        return '您的账号已被锁定，请联系管理员解除锁定后重新登录';
      }
      // 失败次数为 0 却收到 invalid_grant：多为租户/上下文不匹配或失败未计入当前账号，
      // "已失败 0 次"会误导用户（也会在成功登录后残留），回退通用文案。
      if (info.accessFailedCount <= 0) {
        return this.localization.instant('AbpAccount::InvalidUserNameOrPassword');
      }
      return `用户名或密码错误，已失败 ${info.accessFailedCount} 次，连续失败 ${info.maxFailedAccessAttempts} 次将锁定账号，剩余 ${remaining} 次机会`;
    }
    // 用户不存在或未查询到：保持通用错误，不暴露账号是否存在。
    return this.localization.instant('AbpAccount::InvalidUserNameOrPassword');
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

export interface LoginAttemptInfoDto {
  userExists: boolean;
  maxFailedAccessAttempts: number;
  accessFailedCount: number;
  remainingAttempts: number;
  isLockedOut: boolean;
  lockoutEnd: string | null;
}
