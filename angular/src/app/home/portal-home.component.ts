import {
  Component,
  ChangeDetectionStrategy,
  inject,
  OnInit,
  signal,
  OnDestroy,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { AuthService, ConfigStateService, EnvironmentService } from '@abp/ng.core';
import { DOCUMENT } from '@angular/common';
import { hasRole } from '../auth/current-user.utils';
import { PortalService } from '../proxy/portal/portal.service';
import {
  TenantResourceSummaryDto,
  PublicHomeStatsDto,
  CourseBriefDto,
  MaterialBriefDto,
  PartnerBriefDto,
} from '../proxy/portal/models';

interface HeroSlide {
  title: string;
  subtitle: string;
  description: string;
  bg: string;
  icon: string;
  primaryText: string;
  primaryLink: string;
  secondaryText?: string;
  secondaryLink?: string;
}

interface StatItem {
  value: string;
  label: string;
}

interface SubjectCategory {
  icon: string;
  name: string;
  link: string;
}

@Component({
  selector: 'app-portal-home',
  standalone: true,
  imports: [CommonModule, RouterLink, NzSpinModule, NzAvatarModule],
  templateUrl: './portal-home.component.html',
  styleUrls: ['./portal-home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortalHomeComponent implements OnInit, OnDestroy {
  private authService = inject(AuthService);
  private configService = inject(ConfigStateService);
  private environmentService = inject(EnvironmentService);
  private document = inject(DOCUMENT);
  private router = inject(Router);
  private portalService = inject(PortalService);

  // ====== 认证状态 ======
  currentUserName = signal('用户');
  currentUser = signal<Record<string, unknown> | null>(null);
  userRoleLabel = signal('学生');
  userInitial = computed(() => {
    const name = this.currentUserName();
    return name?.charAt(0)?.toUpperCase() || 'U';
  });

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  get isStudent(): boolean {
    return hasRole(this.configService, 'Student');
  }

  get isAdmin(): boolean {
    return !this.isStudent;
  }

  // Features filtered by role
  studentFeatures = [
    {
      title: '资源管理',
      description: '浏览和下载教学资源，支持多种文档格式',
      icon: 'folder-open',
      route: '/resources',
      color: '#3b82f6'
    },
    {
      title: '智能搜索',
      description: '基于 AI 的全文搜索，快速定位所需知识内容',
      icon: 'search',
      route: '/search',
      color: '#10b981'
    },
    {
      title: 'AI 助手',
      description: '智能对话问答，辅助学习与知识探索',
      icon: 'comment-dots',
      route: '/ai/chat',
      color: '#8b5cf6'
    },
    {
      title: '课程学习',
      description: '系统化课程管理，跟踪学习进度',
      icon: 'book',
      route: '/learning/my-courses',
      color: '#ec4899'
    },
    {
      title: '知识图谱',
      description: '可视化知识结构，构建完整知识体系',
      icon: 'project-diagram',
      route: '/learning/course-list',
      color: '#06b6d4'
    },
    {
      title: '案例分析',
      description: '深度分析典型案例，提升实践能力',
      icon: 'briefcase',
      route: '/ai/case-analysis',
      color: '#ef4444'
    },
    {
      title: '职业指导',
      description: '个性化职业规划建议，助力职业发展',
      icon: 'graduation-cap',
      route: '/ai/career-guidance',
      color: '#6366f1'
    }
  ];

  adminFeatures = [
    {
      title: '资源管理',
      description: '上传、管理教学资源，支持多种文档格式，自动建立索引',
      icon: 'folder-open',
      route: '/resources',
      color: '#3b82f6'
    },
    {
      title: '智能搜索',
      description: '基于 AI 的全文搜索，快速定位所需知识内容',
      icon: 'search',
      route: '/search',
      color: '#10b981'
    },
    {
      title: 'AI 助手',
      description: '智能对话问答，辅助学习与知识探索',
      icon: 'comment-dots',
      route: '/ai/chat',
      color: '#8b5cf6'
    },
    {
      title: '教案生成',
      description: 'AI 辅助生成专业教案，提升备课效率',
      icon: 'lightbulb',
      route: '/ai/lesson-plan',
      color: '#f59e0b'
    },
    {
      title: '课程管理',
      description: '系统化课程管理，跟踪学习进度',
      icon: 'book',
      route: '/learning/my-courses',
      color: '#ec4899'
    },
    {
      title: '知识图谱',
      description: '可视化知识结构，构建完整知识体系',
      icon: 'project-diagram',
      route: '/learning/course-list',
      color: '#06b6d4'
    },
    {
      title: '案例分析',
      description: '深度分析典型案例，提升实践能力',
      icon: 'briefcase',
      route: '/ai/case-analysis',
      color: '#ef4444'
    },
    {
      title: '职业指导',
      description: '个性化职业规划建议，助力职业发展',
      icon: 'graduation-cap',
      route: '/ai/career-guidance',
      color: '#6366f1'
    }
  ];

  get activeFeatures() {
    return this.isStudent ? this.studentFeatures : this.adminFeatures;
  }

  // ====== Hero 轮播 ======
  heroIndex = signal(0);
  private heroTimer: ReturnType<typeof setInterval> | null = null;

  heroSlides: HeroSlide[] = [
    {
      title: '易课通资源库',
      subtitle: '智能化知识管理与学习平台',
      description: '集成 AI 技术的新一代知识管理系统，为教育机构构建完整的知识体系，提升教学效率与学习体验',
      bg: `linear-gradient(90deg, rgba(8, 26, 54, 0.82) 0%, rgba(18, 72, 154, 0.66) 43%, rgba(18, 72, 154, 0.18) 100%), url('assets/images/home/resource-library-hero.png') center right / cover no-repeat`,
      icon: 'fa fa-graduation-cap',
      primaryText: '浏览资源库',
      primaryLink: '/resources',
      secondaryText: '了解更多',
      secondaryLink: '/',
    },
    {
      title: 'AI 智能驱动',
      subtitle: '让知识触手可及',
      description: '智能搜索、AI 辅助教学、教案自动生成，用前沿技术赋能教育',
      bg: `linear-gradient(90deg, rgba(8, 26, 54, 0.84) 0%, rgba(30, 108, 232, 0.62) 48%, rgba(0, 183, 255, 0.16) 100%), url('assets/images/home/resource-library-hero.png') center right / cover no-repeat`,
      icon: 'fa fa-robot',
      primaryText: '体验 AI 助手',
      primaryLink: '/ai/chat',
      secondaryText: '智能搜索',
      secondaryLink: '/search',
    },
    {
      title: '资源库广场',
      subtitle: '汇聚优质教育资源',
      description: '多院校资源库共建共享，发现更多优质课程与学习材料',
      bg: `linear-gradient(90deg, rgba(8, 26, 54, 0.84) 0%, rgba(16, 185, 129, 0.54) 48%, rgba(30, 108, 232, 0.14) 100%), url('assets/images/home/resource-library-hero.png') center right / cover no-repeat`,
      icon: 'fa fa-layer-group',
      primaryText: '探索资源库',
      primaryLink: '/',
      secondaryText: '课程学习',
      secondaryLink: '/learning/course-list',
    },
  ];

  setHeroSlide(index: number) {
    this.heroIndex.set(index);
    this.restartHeroTimer();
  }

  private startHeroTimer() {
    this.stopHeroTimer();
    this.heroTimer = setInterval(() => {
      this.heroIndex.update((i) => (i + 1) % this.heroSlides.length);
    }, 5000);
  }

  private stopHeroTimer() {
    if (this.heroTimer) {
      clearInterval(this.heroTimer);
      this.heroTimer = null;
    }
  }

  private restartHeroTimer() {
    this.stopHeroTimer();
    this.startHeroTimer();
  }

  // ====== 资源库列表 ======
  tenantList = signal<TenantResourceSummaryDto[]>([]);
  loadingTenants = signal(false);
  tenantsError = signal(false);
  resourceFilter = signal<'all' | 'national' | 'provincial' | 'school'>('all');

  filteredTenants = computed(() => {
    const filter = this.resourceFilter();
    const tenants = this.tenantList();
    if (filter === 'all') return tenants;
    // 当前数据中没有级别字段，暂时全部返回
    // 后续可根据 industryField 或新增字段做筛选
    return tenants;
  });

  // ====== 统计数据 ======
  statsItems: StatItem[] = [
    { value: '--', label: '资源库' },
    { value: '--', label: '课程资源' },
    { value: '--', label: '学习资源' },
    { value: '--', label: '微专业' },
  ];

  // ====== 精选内容 Tab ======
  contentTab = signal<'courses' | 'materials'>('courses');
  featuredCourses = signal<CourseBriefDto[]>([]);
  latestMaterials = signal<MaterialBriefDto[]>([]);
  loadingContent = signal(false);

  // ====== 合作院校 ======
  partners = signal<PartnerBriefDto[]>([]);
  partnersPaused = signal(false);

  // ====== 学科分类 ======
  subjectCategories: SubjectCategory[] = [
    { icon: '📚', name: '公共基础', link: '/search' },
    { icon: '💰', name: '财经商贸', link: '/search' },
    { icon: '🏥', name: '医药卫生', link: '/search' },
    { icon: '💻', name: '电子与信息', link: '/search' },
    { icon: '⚙️', name: '装备制造', link: '/search' },
    { icon: '🎓', name: '教育与体育', link: '/search' },
    { icon: '🏗️', name: '土木建筑', link: '/search' },
    { icon: '🎨', name: '文化艺术', link: '/search' },
  ];

  // ====== 用户下拉菜单 ======
  userDropdownOpen = signal(false);

  toggleUserDropdown() {
    this.userDropdownOpen.update(v => !v);
  }

  get userRoleDisplay(): string {
    return this.userRoleLabel();
  }

  goToStudentPortal() {
    this.router.navigate(['/student']);
  }

  goToAdminPanel() {
    this.router.navigate(['/resources']);
  }

  // ====== 生命周期 ======
  ngOnInit() {
    this.loadCurrentUser();
    this.loadAllData();
    this.startHeroTimer();
    // NOTE: Removed auto-redirect to /student so users stay on home after login.
    // Users can click "进入学生门户" button to navigate manually.
  }

  ngOnDestroy() {
    this.stopHeroTimer();
  }

  // ====== 数据加载 ======
  private loadCurrentUser() {
    const currentUser = this.configService.getDeep('currentUser') as Record<string, unknown> | undefined;
    const userName = currentUser?.['userName'];
    if (typeof userName === 'string' && userName.trim()) {
      this.currentUserName.set(userName);
      this.currentUser.set(currentUser ?? null);
    }
    // Set role label
    if (hasRole(this.configService, 'Teacher')) {
      this.userRoleLabel.set('教师');
    } else if (hasRole(this.configService, 'Student')) {
      this.userRoleLabel.set('学生');
    } else {
      this.userRoleLabel.set('管理员');
    }
  }

  private loadAllData() {
    this.loadTenants();
    this.loadStats();
    this.loadFeaturedContent();
  }

  loadTenants() {
    this.loadingTenants.set(true);
    this.tenantsError.set(false);
    this.portalService.getPublicTenantList().subscribe({
      next: (tenants) => {
        this.tenantList.set(tenants || []);
        this.loadingTenants.set(false);
      },
      error: (err) => {
        console.error('Failed to load tenants:', err);
        this.tenantList.set([]);
        this.loadingTenants.set(false);
        this.tenantsError.set(true);
      },
    });
  }

  private loadStats() {
    this.portalService.getPublicHomeStats().subscribe({
      next: (stats: PublicHomeStatsDto) => {
        this.statsItems = [
          { value: this.formatNumber(stats.tenantCount), label: '资源库' },
          { value: this.formatNumber(stats.totalCourseCount), label: '课程资源' },
          { value: this.formatNumber(stats.totalResourceCount), label: '学习资源' },
          { value: this.formatNumber(stats.totalMicroMajorCount), label: '微专业' },
        ];
      },
      error: (err) => {
        console.error('Failed to load stats:', err);
        // 从租户列表计算粗略统计
        const tenants = this.tenantList();
        if (tenants.length > 0) {
          const totalCourses = tenants.reduce((sum, t) => sum + t.courseCount, 0);
          const totalResources = tenants.reduce((sum, t) => sum + t.resourceCount, 0);
          const totalMicros = tenants.reduce((sum, t) => sum + t.microMajorCount, 0);
          this.statsItems = [
            { value: this.formatNumber(tenants.length), label: '资源库' },
            { value: this.formatNumber(totalCourses), label: '课程资源' },
            { value: this.formatNumber(totalResources), label: '学习资源' },
            { value: this.formatNumber(totalMicros), label: '微专业' },
          ];
        }
      },
    });
  }

  private loadFeaturedContent() {
    this.loadingContent.set(true);
    // 尝试从第一个租户加载首页数据作为精选内容展示
    this.portalService.getPublicTenantList().subscribe({
      next: (tenants) => {
        if (tenants.length > 0) {
          // 使用第一个租户的数据
          const firstTenantId = tenants[0].id;
          this.portalService.getHomeData(firstTenantId).subscribe({
            next: (data) => {
              this.featuredCourses.set(data.featuredCourses || []);
              this.latestMaterials.set(data.latestMaterials || []);
              this.partners.set(data.partners || []);
              this.loadingContent.set(false);
            },
            error: () => {
              this.featuredCourses.set([]);
              this.latestMaterials.set([]);
              this.partners.set([]);
              this.loadingContent.set(false);
            },
          });
        } else {
          this.featuredCourses.set([]);
          this.latestMaterials.set([]);
          this.partners.set([]);
          this.loadingContent.set(false);
        }
      },
      error: () => {
        this.featuredCourses.set([]);
        this.latestMaterials.set([]);
        this.partners.set([]);
        this.loadingContent.set(false);
      },
    });
  }

  // ====== 事件处理 ======
  onTenantClick(tenant: TenantResourceSummaryDto) {
    if (this.hasLoggedIn && hasRole(this.configService, 'Student')) {
      this.router.navigate(['/student'], { queryParams: { tenantId: tenant.id } });
    } else {
      this.login();
    }
  }

  login() {
    this.authService.navigateToLogin();
  }

  logout() {
    // 清除本地 OAuth token storage
    const oauthKeys = ['access_token', 'id_token', 'refresh_token', 'expires_at', 'session_state', 'granted_scopes', 'Abp.AuthToken'];
    oauthKeys.forEach(key => {
      localStorage.removeItem(key);
      sessionStorage.removeItem(key);
    });

    this.authService.logout().subscribe(() => {
      // 调用 API 清除 IdP session cookie（cookie 在 API 域名下）
      fetch('/api/app/logout/clear-session', {
        method: 'GET',
        credentials: 'include'  // 带上 cookies
      }).then(() => {
        // 跳转到 end session 端点，清除 IdP 的 session cookie
        const issuer = this.environmentService.getEnvironment().oAuthConfig?.issuer || '';
        const postLogoutUri = window.location.origin; // http://localhost:4200
        const endSessionUrl = issuer.replace(/\/$/, '') + '/connect/endsession?post_logout_redirect_uri=' + encodeURIComponent(postLogoutUri);
        window.location.href = endSessionUrl;
      }).catch(() => {
        // 即使 API 失败也尝试跳转
        window.location.href = '/';
      });
    });
  }

  pausePartners() {
    this.partnersPaused.set(true);
  }

  resumePartners() {
    this.partnersPaused.set(false);
  }

  // ====== 工具方法 ======
  private formatNumber(num: number): string {
    if (num >= 10000) {
      return (num / 10000).toFixed(1).replace(/\.0$/, '') + '万+';
    }
    if (num >= 1000) {
      return (num / 1000).toFixed(1).replace(/\.0$/, '') + 'k+';
    }
    return String(num);
  }
}
