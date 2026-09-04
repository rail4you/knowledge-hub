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
import { TenantListService } from '../proxy/controllers/tenant-list.service';
import { ToasterService } from '@abp/ng.theme.shared';
import { DOCUMENT } from '@angular/common';
import { catchError, finalize } from 'rxjs/operators';
import { throwError } from 'rxjs';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, RouterLinkActive, LocalizationPipe, NzIconModule],
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

  // 租户（Tab 列表方式）
  tenantList = signal<{ id: string; name: string }[]>([]);
  currentTenantName = signal<string | null>(null);
  currentTenantId = signal<string | null>(null);

  ngOnInit() {
    this.clearSession();
    this.buildForm();
    this.loadLanguages();
    this.loadTenants();
    this.loadCurrentTenant();
  }

  private loadTenants() {
    this.tenantListService.getTenants().subscribe({
      next: (list: any) => {
        const items = Array.isArray(list) ? list : [];
        this.tenantList.set(items.map((t: any) => ({ id: t.id, name: t.name })));
      },
      error: () => this.tenantList.set([]),
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
    this.document.cookie = `__tenant=${encodeURIComponent(tenant.id)}; path=/; SameSite=Lax`;
    this.currentTenantId.set(tenant.id);
    this.currentTenantName.set(tenant.name);
    this.toasterService.success(`已切换到租户：${tenant.name}`);
  }

  clearTenant() {
    this.document.cookie = `__tenant=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax`;
    this.currentTenantId.set(null);
    this.currentTenantName.set(null);
    this.toasterService.success('已清除租户，将以宿主身份登录');
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

    this.inProgress = true;
    const { username, password, rememberMe } = this.form.value;
    const redirectUrl =
      this.route.snapshot.queryParams['returnUrl'] || '/';

    // 登录前再次清除可能的残留 session
    this.clearSession();

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
