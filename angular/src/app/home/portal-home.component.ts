import { Component, ChangeDetectionStrategy, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasRole } from '../auth/current-user.utils';
import { PortalService } from '../proxy/portal/portal.service';
import type { PublicHomeStatsDto, PortalHomeDataDto } from '../proxy/portal/models';

interface HeroSlide {
  eyebrow: string;
  title: string;
  highlight: string;
  desc: string;
  primaryCta: { label: string; link: string };
  secondaryCta: { label: string; link: string };
  badge: string;
  /** 主色调，用于 hero 背景渐变与点缀 */
  accent: string;
}

@Component({
  selector: 'app-portal-home',
  standalone: true,
  imports: [CommonModule, DatePipe, DecimalPipe, RouterLink, NzIconModule],
  templateUrl: './portal-home.component.html',
  styleUrls: ['./portal-home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortalHomeComponent implements OnInit, OnDestroy {
  private authService = inject(AuthService);
  private config = inject(ConfigStateService);
  private router = inject(Router);
  private portal = inject(PortalService);

  readonly stats = signal<PublicHomeStatsDto | null>(null);
  readonly homeData = signal<PortalHomeDataDto | null>(null);
  readonly userName = signal('');

  /** Hero 轮播状态 —— 纯 UI 信号，不涉及任何后端数据 */
  readonly heroIndex = signal(0);
  private heroTimer: ReturnType<typeof setInterval> | null = null;

  readonly heroSlides: HeroSlide[] = [
    {
      eyebrow: '海量资源 · 一站尽览',
      title: '智慧资源库',
      highlight: '让学习更高效',
      desc: '汇聚精品课程、教案素材、实训案例，为院校提供完整的教学资源解决方案。',
      primaryCta: { label: '浏览资源库', link: '/student/resources' },
      secondaryCta: { label: '了解更多', link: '/student/courses' },
      badge: '资源库',
      accent: '#1a5fe0',
    },
    {
      eyebrow: '精品课程 · 名师领航',
      title: '在线课程中心',
      highlight: '名师就在身边',
      desc: '覆盖各专业核心课程，名师团队精心打造，支持在线学习、互动答疑与学习追踪。',
      primaryCta: { label: '探索课程', link: '/student/courses' },
      secondaryCta: { label: '我的学习', link: '/student/my-learning' },
      badge: '课程',
      accent: '#0ea5e9',
    },
    {
      eyebrow: 'AI 驱动 · 因材施教',
      title: '智能教学助手',
      highlight: 'AI 让教学更轻松',
      desc: '智能备课、个性化学习路径、AI 答疑，为师生提供全方位的智能教学服务。',
      primaryCta: { label: '体验 AI 助手', link: '/ai/chat' },
      secondaryCta: { label: '智能搜索', link: '/search' },
      badge: 'AI 教学',
      accent: '#0891b2',
    },
  ];

  /** 资源排行：按下载量降序取前 8 */
  readonly rankedMaterials = () => {
    const mats = this.homeData()?.latestMaterials || [];
    return [...mats].sort((a, b) => (b.downloadCount || 0) - (a.downloadCount || 0)).slice(0, 8);
  };

  get isLoggedIn() { return this.authService.isAuthenticated; }
  get isStudent() { return hasRole(this.config, 'Student'); }

  ngOnInit() {
    const cu = this.config.getDeep('currentUser') as Record<string, unknown> | undefined;
    if (typeof cu?.['userName'] === 'string') this.userName.set(cu['userName'] as string);

    // 登录后按角色跳转：教师/管理员 → 管理后台，学生 → 留在首页
    if (this.isLoggedIn && !this.isStudent) {
      this.router.navigate(['/resources']);
      return;
    }

    this.portal.getPublicHomeStats().subscribe(d => this.stats.set(d));
    this.portal.getPublicTenantList().subscribe(ts => {
      const id = ts?.[0]?.id;
      if (id) this.portal.getHomeData(id).subscribe(d => this.homeData.set(d));
    });

    // 启动 hero 自动轮播
    this.startHeroAutoplay();
  }

  ngOnDestroy() {
    this.stopHeroAutoplay();
  }

  setHeroSlide(i: number) {
    this.heroIndex.set(i);
    this.restartHeroAutoplay();
  }

  private startHeroAutoplay() {
    this.heroTimer = setInterval(() => {
      this.heroIndex.update(i => (i + 1) % this.heroSlides.length);
    }, 5500);
  }

  private stopHeroAutoplay() {
    if (this.heroTimer) {
      clearInterval(this.heroTimer);
      this.heroTimer = null;
    }
  }

  private restartHeroAutoplay() {
    this.stopHeroAutoplay();
    this.startHeroAutoplay();
  }

  login() { this.authService.navigateToLogin(); }

  logout() {
    // ABP authService.logout() 会清除本地 token 并重定向到 IdP 的 end_session_endpoint
    // IdP 清除 session cookie 后会自动重定向回 postLogoutRedirectUri
    this.authService.logout().subscribe();
  }
}
