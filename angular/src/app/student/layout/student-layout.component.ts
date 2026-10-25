import { Component, ChangeDetectionStrategy, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasRole } from '../../auth/current-user.utils';
import { AuthErrorModalComponent } from '../../core/auth/auth-error-modal.component';
import { VoicePanelComponent } from '../voice/voice-panel.component';
import { SiteFooterComponent } from '../../shared/branding/site-footer.component';
import { BrandingService } from '../../shared/branding/branding.service';
import { SchoolService } from '../../proxy/schools/school.service';
import { Subscription, filter } from 'rxjs';
import {
  buildAllEntries,
  buildGuestItems,
  buildMobileItems,
  buildSidebarGroups,
  StudentNavEntry,
} from '../student-navigation';

@Component({
  selector: 'app-student-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzDropDownModule,
    AuthErrorModalComponent,
    VoicePanelComponent,
    SiteFooterComponent,
  ],
  templateUrl: './student-layout.component.html',
  styleUrls: ['./student-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentLayoutComponent implements OnInit, OnDestroy {
  private authService = inject(AuthService);
  private router = inject(Router);
  private configState = inject(ConfigStateService);
  readonly branding = inject(BrandingService);
  private readonly schoolService = inject(SchoolService);

  userName = signal('用户');
  userRoleLabel = signal('学生');
  /** 当前机构名称（侧边栏机构卡片展示） */
  tenantName = signal('');
  menuOpen = signal(false);

  // ── 导航：全部由 student-navigation.ts 派生，不再手写多份表 ──────
  /** 侧边栏分组（对齐原型 .side-label + .side-link 结构） */
  readonly navGroups = buildSidebarGroups();
  /** 游客可见的公开浏览导航 */
  readonly guestNavItems = buildGuestItems();
  /** 移动端抽屉的功能导航 */
  readonly mobileItems = buildMobileItems();
  /** 全部可导航页面（面包屑标题 + 最具体路由高亮用） */
  private readonly allEntries = buildAllEntries();

  /**
   * 智能体任务详情页（/student/agent-tasks/:id）是全高聊天布局：
   * 聊天窗口占满视口剩余高度、输入框贴页面底部，此时隐藏全局 footer，
   * 否则 footer 会挤占高度、聊天框无法贴底。
   */
  readonly hideFooter = signal(false);
  /**
   * 课程学习页（/student/courses/:id/learn）专用：整页固定高度、文档不滚动。
   * 左侧正文与右侧章节目录各自内部滚动，章节栏因此可以稳稳停在视口内，
   * 不会随着页面滚动被"顶走"、也不会把页面撑出滚动条。
   * 仅桌面端（>1100px）生效，窄屏回退为普通流式页面（见 .scss 的 @media）。
   */
  readonly courseLearnFixed = signal(false);
  /** 语音助手 Feature 开关（KnowledgeHub.VoiceAssistant，按租户控制；取不到值时默认显示） */
  readonly voiceAssistantEnabled = signal(true);

  private routerSub?: Subscription;

  ngOnInit() {
    const flag = this.configState.getFeature('KnowledgeHub.VoiceAssistant');
    this.voiceAssistantEnabled.set((flag ?? 'True').toLowerCase() === 'true');

    // 当前机构名（侧边栏机构卡片）
    const tenant = this.configState.getDeep('currentTenant') as Record<string, unknown> | undefined;
    const tenantDisplay = (tenant?.['name'] as string) || '';
    if (tenantDisplay) this.tenantName.set(tenantDisplay);

    // 机构名改用学校档案里的中文名（会话里的租户名是 slug，如 social / qidi）。
    // 需求「机构名称要用中文」：侧边栏机构卡片不应显示租户 slug。
    const tenantId = (tenant?.['id'] as string) || '';
    if (tenantId) {
      this.schoolService.getProfile(tenantId).subscribe({
        next: (profile) => {
          const name = profile?.school?.name;
          if (name) this.tenantName.set(name);
        },
        error: () => {
          /* 取不到就沿用会话里的租户名 */
        },
      });
    }

    const currentUser = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    const userName = currentUser?.['userName'];

    if (typeof userName === 'string' && userName.trim()) {
      this.userName.set(userName);
    } else {
      const storedUser = localStorage.getItem('abp_session_state');
      if (storedUser) {
        try {
          const session = JSON.parse(storedUser);
          if (session?.username) {
            this.userName.set(session.username);
          }
        } catch {
          // ignore parse errors
        }
      }
    }

    if (hasRole(this.configState, 'Teacher')) {
      this.userRoleLabel.set('教师');
    }

    this.updateFooterVisibility(this.router.url);

    // 路由跳转后：更新 footer 可见性、关闭移动抽屉
    this.routerSub = this.router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe((e) => {
        const url = (e as NavigationEnd).urlAfterRedirects || this.router.url;
        this.updateFooterVisibility(url);
        this.closeMenu();
        this.mobileChildKey.set(null);
      });
  }

  /** 全高聊天页隐藏 footer（智能体任务详情、实训聊天；列表页仍保留 footer）。 */
  private updateFooterVisibility(url: string): void {
    const path = (url || '').split('?')[0].split('#')[0];
    const isChatPage =
      /^\/student\/agent-tasks\/[^/]+$/.test(path) ||
      /^\/student\/practicums\/[^/]+\/chat\/?$/.test(path);
    // 课程学习页：整页不滚动（章节栏固定 + 左栏内部滚动），footer 没有意义，一并隐藏
    const isCourseLearn = /^\/student\/courses\/[^/]+\/learn(\/[^/]+)?\/?$/.test(path);
    this.hideFooter.set(isChatPage || isCourseLearn);
    this.courseLearnFixed.set(isCourseLearn);
  }

  ngOnDestroy(): void {
    this.routerSub?.unsubscribe();
  }

  /** 当前页面标题（面包屑右半部分）：按当前 URL 最长前缀匹配导航项 */
  pageTitle(): string {
    const url = (this.router.url || '').split('?')[0];
    const hit = this.bestMatchEntry();
    if (hit) return hit.label;
    if (url.startsWith('/student/courses/')) return '课程详情';
    if (url.startsWith('/student/practicums/')) return '实训详情';
    if (url.startsWith('/student/employment/jobs/')) return '职位详情';
    if (url.startsWith('/student/resources/')) return '资源详情';
    if (url.startsWith('/student/news/')) return '资讯详情';
    if (url.startsWith('/student/micro-majors/')) return '微专业详情';
    return '学习空间';
  }

  /** 当前 URL 命中的最具体路由项（避免 /student/practicums 同时点亮 /student/practicums/my） */
  private bestMatchEntry(): StudentNavEntry | null {
    const url = (this.router.url || '').split('?')[0];
    let best: StudentNavEntry | null = null;
    for (const entry of this.allEntries) {
      const r = entry.route;
      if (!r) continue;
      if (url === r || url.startsWith(r + '/')) {
        if (!best || r.length > best.route!.length) best = entry;
      }
    }
    return best;
  }

  /** 侧边栏链接激活态 */
  isSideActive(item: StudentNavEntry): boolean {
    const hit = this.bestMatchEntry();
    return !!hit && !!item.route && hit.route === item.route;
  }

  /** 移动端分组项激活态：任一子项命中即激活 */
  isActive(item: StudentNavEntry): boolean {
    if (item.children?.length) {
      return item.children.some(c => this.isActive(c));
    }
    const url = this.router.url || '';
    return !!item.route && url.startsWith(item.route);
  }

  get isLoggedIn() { return this.authService.isAuthenticated; }

  get userInitial(): string {
    return this.userName()?.charAt(0)?.toUpperCase() || 'U';
  }

  /** 机构卡片首字 */
  get tenantInitial(): string {
    return (this.tenantName() || '知').charAt(0);
  }

  userMenuTitle(): string {
    const name = this.userName() || '用户';
    const role = this.userRoleLabel() || '学生';
    return `${name} · ${role}`;
  }

  /** 移动端全屏菜单：当前钻取进入的子菜单 key（null = 根级列表） */
  readonly mobileChildKey = signal<string | null>(null);

  /** 当前钻取的子菜单项 */
  mobileChildEntry(): StudentNavEntry | null {
    const key = this.mobileChildKey();
    if (!key) return null;
    return this.mobileItems.find(i => i.key === key) ?? null;
  }

  /** 进入子菜单层 */
  openMobileChild(key: string) {
    this.mobileChildKey.set(key);
  }

  /** 返回根级列表 */
  closeMobileChild() {
    this.mobileChildKey.set(null);
    this.scrollMobileMenuToActive();
  }

  /** 父项副标题：前两个子项名 + 数量，如「招聘直播 · 就业大厅等6项」 */
  mobileChildHint(item: StudentNavEntry): string {
    const kids = item.children ?? [];
    if (!kids.length) return '';
    const names = kids.slice(0, 2).map(k => k.label).join(' · ');
    return kids.length > 2 ? `${names}等${kids.length}项` : names;
  }

  /** 移动抽屉内点击任意路由后关闭 */
  closeOnNavigate() {
    this.closeMenu();
    this.mobileChildKey.set(null);
  }

  logout() {
    // ABP authService.logout() 会清除本地 token 并重定向到 IdP 的 end_session_endpoint
    // IdP 清除 session cookie 后会自动重定向回 postLogoutRedirectUri
    // 不要在 subscribe 回调中手动 window.location.href，这会覆盖 OAuth end_session 重定向
    this.authService.logout().subscribe();
  }

  login() { this.authService.navigateToLogin(); }

  toggleMenu() {
    const next = !this.menuOpen();
    this.menuOpen.set(next);
    if (!next) {
      this.mobileChildKey.set(null);
    } else {
      this.scrollMobileMenuToActive();
    }
  }

  closeMenu() {
    this.menuOpen.set(false);
    this.mobileChildKey.set(null);
  }

  /** 菜单打开 / 返回根级时，把高亮项滚到可见位置（只滚菜单内部，不动背后页面） */
  private scrollMobileMenuToActive(): void {
    setTimeout(() => {
      const body = document.querySelector('.mobile-drawer__body');
      const el = document.querySelector('.mobile-drawer .mobile-link.active');
      if (!body || !el) return;
      const r = el.getBoundingClientRect();
      const br = body.getBoundingClientRect();
      if (r.top < br.top || r.bottom > br.bottom) {
        body.scrollTo({ top: body.scrollTop + (r.top - br.top) - 72, behavior: 'smooth' });
      }
    }, 90);
  }
}
