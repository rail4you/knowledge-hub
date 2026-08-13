import { Component, ChangeDetectionStrategy, inject, signal, OnInit, AfterViewInit, OnDestroy, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasRole } from '../../auth/current-user.utils';
import { AuthErrorModalComponent } from '../../core/auth/auth-error-modal.component';
import { Subscription, filter } from 'rxjs';

interface StudentNavEntry {
  key: string;
  label: string;
  icon: string;
  route?: string;
  activePrefixes?: string[];
  children?: StudentNavEntry[];
}

@Component({
  selector: 'app-student-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzDropDownModule,
    AuthErrorModalComponent,
  ],
  templateUrl: './student-layout.component.html',
  styleUrls: ['./student-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentLayoutComponent implements OnInit, AfterViewInit, OnDestroy {
  private authService = inject(AuthService);
  private router = inject(Router);
  private configState = inject(ConfigStateService);

  userName = signal('用户');
  userRoleLabel = signal('学生');
  menuOpen = signal(false);

  private readonly allItems: StudentNavEntry[] = [
    { key: 'micro-majors', label: '微专业', icon: 'trophy', route: '/student/micro-majors' },
    { key: 'my-micro-majors', label: '我的微专业', icon: 'audit', route: '/student/my-micro-majors' },
    { key: 'courses', label: '课程中心', icon: 'read', route: '/student/courses' },
    { key: 'resources', label: '资源库', icon: 'database', route: '/student/resources' },
    { key: 'news', label: '资讯中心', icon: 'file-text', route: '/student/news' },
    { key: 'my-learning', label: '我的学习', icon: 'line-chart', route: '/student/my-learning' },
    { key: 'favorites', label: '我的收藏', icon: 'star', route: '/student/favorites' },
    { key: 'agent-tasks', label: '课堂任务', icon: 'robot', route: '/student/agent-tasks' },
    {
      key: 'practicums',
      label: '实训',
      icon: 'experiment',
      activePrefixes: ['/student/practicums'],
      children: [
        { key: 'practicum-projects', label: '实训项目', icon: 'experiment', route: '/student/practicums' },
        { key: 'my-practicums', label: '我的实训', icon: 'unordered-list', route: '/student/practicums/my' },
      ],
    },
    {
      key: 'employment',
      label: '就业服务',
      icon: 'idcard',
      activePrefixes: ['/student/employment', '/student/recruitment-live'],
      children: [
        { key: 'employment-live', label: '招聘直播', icon: 'video-camera', route: '/student/recruitment-live' },
        { key: 'employment-jobs', label: '就业大厅', icon: 'idcard', route: '/student/employment/jobs' },
        { key: 'employment-resumes', label: '我的简历', icon: 'file-text', route: '/student/employment/my-resumes' },
        { key: 'employment-applications', label: '我的投递', icon: 'send', route: '/student/employment/my-applications' },
        { key: 'employment-outcomes', label: '我的就业去向', icon: 'compass', route: '/student/employment/my-outcomes' },
        { key: 'employment-guidance', label: '就业指导', icon: 'message', route: '/student/employment/guidance' },
      ],
    },
    {
      key: 'ai',
      label: 'AI 功能',
      icon: 'robot',
      activePrefixes: ['/student/ai', '/student/search'],
      children: [
        { key: 'ai-chat', label: 'AI 助手', icon: 'robot', route: '/student/ai/chat' },
        { key: 'ai-search', label: '智能搜索', icon: 'search', route: '/student/search' },
      ],
    },
  ];

  /** 当前显示在导航栏的项 */
  readonly visibleItems = signal<StudentNavEntry[]>(this.allItems);
  /** 折叠进「更多」的项 */
  readonly overflowItems = signal<StudentNavEntry[]>([]);

  private readonly widthCache: Record<string, number> = {};
  private moreBtnWidth = 108;
  private resizeObserver?: ResizeObserver;
  private routerSub?: Subscription;

  @ViewChild('tabTabs') tabTabsEl?: ElementRef<HTMLElement>;

  ngOnInit() {
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
  }

  ngAfterViewInit(): void {
    this.routerSub = this.router.events
      .pipe(filter(e => e instanceof NavigationEnd))
      .subscribe(() => this.scheduleReflow());
    this.resizeObserver = new ResizeObserver(() => this.scheduleReflow());
    if (this.tabTabsEl) {
      this.resizeObserver.observe(this.tabTabsEl.nativeElement);
    }
    this.reflow();
    if (document.fonts?.ready) {
      document.fonts.ready.then(() => this.reflow());
    }
  }

  ngOnDestroy(): void {
    this.routerSub?.unsubscribe();
    this.resizeObserver?.disconnect();
  }

  /** 计算哪些 tab 放得下、哪些折叠进「更多」 */
  private scheduleReflow(): void {
    requestAnimationFrame(() => this.reflow());
  }

  private reflow(): void {
    const container = this.tabTabsEl?.nativeElement;
    if (!container) return;

    // 移动端整个导航以纵向下拉呈现，不做溢出折叠
    if (window.innerWidth <= 768) {
      this.visibleItems.set(this.allItems);
      this.overflowItems.set([]);
      return;
    }

    container.querySelectorAll<HTMLElement>('[data-nav-key]').forEach(el => {
      const key = el.getAttribute('data-nav-key');
      const w = el.offsetWidth;
      if (key && w > 0) this.widthCache[key] = w;
    });

    const moreBtn = container.querySelector<HTMLElement>('[data-nav-more]');
    if (moreBtn && moreBtn.offsetWidth > 0) this.moreBtnWidth = moreBtn.offsetWidth;

    const gap = 4;
    // 先不预留「更多」按钮空间，能全放下就不用折叠
    let result = this.computeFit(container.clientWidth, gap);
    if (result.overflow.length > 0) {
      // 确实溢出：预留「更多」按钮宽度后重算
      result = this.computeFit(container.clientWidth - this.moreBtnWidth, gap);
    }

    this.visibleItems.set(result.visible);
    this.overflowItems.set(result.overflow);
  }

  private computeFit(available: number, gap: number): { visible: StudentNavEntry[]; overflow: StudentNavEntry[] } {
    let used = 0;
    const visible: StudentNavEntry[] = [];
    const overflow: StudentNavEntry[] = [];
    for (const item of this.allItems) {
      const w = this.widthCache[item.key] ?? 0;
      const need = w + (visible.length > 0 ? gap : 0);
      if (overflow.length === 0 && used + need <= available) {
        used += need;
        visible.push(item);
      } else {
        overflow.push(item);
      }
    }
    return { visible, overflow };
  }

  /** 当前 tab 是否处于激活态（含子项前缀匹配） */
  isActive(item: StudentNavEntry): boolean {
    const url = this.router.url || '';
    if (item.activePrefixes?.some(p => url.startsWith(p))) return true;
    if (item.route && url.startsWith(item.route)) return true;
    return false;
  }

  /** 「更多」菜单内是否有当前激活项 */
  moreActive(): boolean {
    return this.overflowItems().some(item => this.isActive(item));
  }

  get isLoggedIn() { return this.authService.isAuthenticated; }

  get userInitial(): string {
    return this.userName()?.charAt(0)?.toUpperCase() || 'U';
  }

  /** 头像渐变背景色（基于用户名稳定生成） */
  avatarGradient(): string {
    const name = this.userName() || 'U';
    const palettes = [
      '#1e6ce8',
      '#0891b2',
      '#059669',
      '#10b981',
      '#0284c7',
      '#0c4cb8',
    ];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = (hash * 31 + name.charCodeAt(i)) | 0;
    }
    return palettes[Math.abs(hash) % palettes.length];
  }

  logout() {
    // ABP authService.logout() 会清除本地 token 并重定向到 IdP 的 end_session_endpoint
    // IdP 清除 session cookie 后会自动重定向回 postLogoutRedirectUri
    // 不要在 subscribe 回调中手动 window.location.href，这会覆盖 OAuth end_session 重定向
    this.authService.logout().subscribe();
  }

  login() { this.authService.navigateToLogin(); }

  toggleMenu() {
    this.menuOpen.update(v => !v);
  }

  closeMenu() {
    this.menuOpen.set(false);
  }
}
