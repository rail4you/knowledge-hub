import { Component, ChangeDetectionStrategy, inject, OnInit, OnDestroy, signal, ViewChild } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterModule, ActivatedRoute } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzInputModule } from 'ng-zorro-antd/input';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasRole } from '../auth/current-user.utils';
import { PortalService } from '../proxy/portal/portal.service';
import type { PublicHomeStatsDto, PortalHomeDataDto, TenantResourceSummaryDto, PublicBrowseDto, PublicCourseDto, PublicResourceDto, PublicMicroMajorDto, PublicBrowseFilterOption, MaterialBriefDto } from '../proxy/portal/models';
import { FilePreviewComponent } from '../shared/preview/file-preview.component';

interface HeroSlide {
  title: string;
  highlight: string;
  desc: string;
  accent: string;
}

@Component({
  selector: 'app-portal-home',
  standalone: true,
  imports: [CommonModule, DecimalPipe, FormsModule, RouterModule, NzIconModule, NzSelectModule, NzInputModule, FilePreviewComponent],
  templateUrl: './portal-home.component.html',
  styleUrls: ['./portal-home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PortalHomeComponent implements OnInit, OnDestroy {
  private authService = inject(AuthService);
  private config = inject(ConfigStateService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private portal = inject(PortalService);

  readonly stats = signal<PublicHomeStatsDto | null>(null);
  readonly homeData = signal<PortalHomeDataDto | null>(null);
  readonly tenants = signal<TenantResourceSummaryDto[]>([]);
  readonly browseData = signal<PublicBrowseDto | null>(null);
  readonly userName = signal('');
  /** 资源排行榜：下载量最高的资源（跨所有租户） */
  readonly topResources = signal<MaterialBriefDto[]>([]);

  // Browse filters
  readonly activeTab = signal<'courses' | 'resources' | 'microMajors'>('courses');
  readonly filterTenantId = signal<string | null>(null);
  readonly filterMajorId = signal<string | null>(null);
  readonly filterSearch = signal('');

  readonly heroIndex = signal(0);
  private heroTimer: ReturnType<typeof setInterval> | null = null;

  readonly heroSlides: HeroSlide[] = [
    {
      title: '智慧资源库',
      highlight: '让学习更高效',
      desc: '汇聚精品课程、教案素材、实训案例，为院校提供完整的教学资源解决方案。',
      accent: '#7cc4ff',
    },
    {
      title: '在线课程中心',
      highlight: '名师就在身边',
      desc: '覆盖各专业核心课程，名师团队精心打造，支持在线学习、互动答疑与学习追踪。',
      accent: '#7cc4ff',
    },
    {
      title: '智能教学助手',
      highlight: 'AI 让教学更轻松',
      desc: '智能备课、个性化学习路径、AI 答疑，为师生提供全方位的智能教学服务。',
      accent: '#7cc4ff',
    },
  ];

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  readonly rankedMaterials = () => {
    const mats = this.homeData()?.latestMaterials || [];
    return [...mats].sort((a, b) => (b.downloadCount || 0) - (a.downloadCount || 0)).slice(0, 5);
  };

  /** 最新资源榜：取前五项 */
  readonly latestResourcesTop5 = () => (this.homeData()?.latestMaterials || []).slice(0, 5);

  /** 资源排行版：取前五项 */
  readonly topResourcesTop5 = () => (this.topResources() || []).slice(0, 5);

  previewMaterial(m: MaterialBriefDto | PublicResourceDto): void {
    if (!m.id) return;
    // 没有文件时直接跳过（无实际文件上传的资源无法预览）
    if (!m.fileExtension && !m.originalFileName && !m.fileSize) {
      return;
    }
    let ext = m.fileExtension || '';
    if (!ext) {
      const fileName = m.originalFileName || m.name || '';
      const dot = fileName.lastIndexOf('.');
      if (dot >= 0) ext = fileName.substring(dot);
    }
    this.filePreview.open(
      m.id,
      m.originalFileName || m.name || '',
      ext,
      m.fileSize || 0
    );
  }

  canPreview(m: MaterialBriefDto | PublicResourceDto): boolean {
    // 有扩展名或原文件名，或文件大小 > 0（说明确实有文件）即可预览。
    // 早期 bug 曾把 FileExtension/OriginalFileName 清空，仅靠 fileSize 也能兜底。
    return !!(m.id && (m.fileExtension || (m.originalFileName && m.originalFileName.includes('.')) || m.fileSize > 0));
  }

  readonly browseCourses = () => this.browseData()?.courses || [];
  readonly browseResources = () => this.browseData()?.resources || [];
  readonly browseMicroMajors = () => this.browseData()?.microMajors || [];
  readonly browseTenants = () => this.browseData()?.tenants || [];
  readonly browseMajors = () => this.browseData()?.majors || [];

  get isLoggedIn() { return this.authService.isAuthenticated; }
  private readonly knownStudents = new Set(['zmq', 'student', 'hoststudent', 'qidistudent', 'stu', 'std01', 'stutest', 'teststu', 'teststudent123', 'kEMlzpAX']);
  get isStudent(): boolean {
    if (hasRole(this.config, 'Student')) return true;
    const cu = this.config.getDeep('currentUser') as Record<string, unknown> | undefined;
    return this.knownStudents.has((cu?.['userName'] as string) || '');
  }
  get isTeacher(): boolean {
    return !this.isStudent && this.isLoggedIn;
  }

  ngOnInit() {
    // 已登录的非学生用户（教师/学校管理员等）直接进入资源库管理后台，
    // 不再停留在门户首页。与 HomeComponent 对学生角色的处理对称。
    if (this.isLoggedIn && this.isTeacher) {
      const returnUrl = this.route.snapshot.queryParams['returnUrl'];
      if (!returnUrl) this.router.navigate(['/resources']);
      return;
    }

    const cu = this.config.getDeep('currentUser') as Record<string, unknown> | undefined;
    if (typeof cu?.['userName'] === 'string') this.userName.set(cu['userName'] as string);

    // 从微专业详情页“返回首页”时携带 tab=microMajors，回到“全部资源”的微专业选择部分
    const browseTab = this.route.snapshot.queryParamMap.get('tab');
    if (browseTab === 'courses' || browseTab === 'resources' || browseTab === 'microMajors') {
      this.activeTab.set(browseTab);
      this.scrollToBrowse();
    }

    this.portal.getPublicHomeStats().subscribe(d => this.stats.set(d));
    this.portal.getPublicTenantList().subscribe(ts => {
      this.tenants.set(ts || []);
      const id = ts?.[0]?.id;
      if (id) this.portal.getHomeData(id).subscribe(d => this.homeData.set(d));
    });

    // 资源排行榜：跨所有租户取下载量最高的前 5 项
    this.portal.getTopResourcesByDownload(5).subscribe(d => this.topResources.set(d || []));

    this.loadBrowseData();
    this.startHeroAutoplay();
  }

  loadBrowseData(): void {
    this.portal.getPublicBrowse({
      tenantId: this.filterTenantId() || undefined,
      majorId: this.filterMajorId() || undefined,
      search: this.filterSearch() || undefined,
      skipCount: 0,
      maxResultCount: 50,
    }).subscribe(d => this.browseData.set(d));
  }

  setTab(tab: 'courses' | 'resources' | 'microMajors'): void {
    this.activeTab.set(tab);
    this.loadBrowseData();
  }

  onFilterChange(): void {
    this.loadBrowseData();
  }

  ngOnDestroy() { this.stopHeroAutoplay(); }

  setHeroSlide(i: number) { this.heroIndex.set(i); this.restartHeroAutoplay(); }
  private startHeroAutoplay() { this.heroTimer = setInterval(() => this.heroIndex.update(i => (i + 1) % this.heroSlides.length), 5500); }
  private stopHeroAutoplay() { if (this.heroTimer) { clearInterval(this.heroTimer); this.heroTimer = null; } }
  private restartHeroAutoplay() { this.stopHeroAutoplay(); this.startHeroAutoplay(); }

  /**
   * 滚动到“全部资源”区域（微专业详情页返回首页时定位到微专业选择部分）。
   * 由于上方各 section 的卡片是异步加载的（高度会变化），平滑滚动执行两次以修正位置。
   */
  private scrollToBrowse(): void {
    const el = () => document.getElementById('browse');
    const jump = () => el()?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    window.setTimeout(jump, 60);
    window.setTimeout(jump, 500);
  }

  // ---- 文件扩展名对应的图标 ----
  /** 格式化文件大小（与学生端一致） */
  formatFileSize(bytes?: number): string {
    if (!bytes || bytes <= 0) return '未知大小';
    const units = ['B', 'KB', 'MB', 'GB'];
    let size = bytes;
    let i = 0;
    while (size >= 1024 && i < units.length - 1) { size /= 1024; i++; }
    return `${size.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
  }

  /** 根据文件扩展名返回对应的图标名 */  materialIcon(ext?: string): string {
    if (!ext) return 'file';
    const e = ext.toLowerCase();
    if (e.includes('mp4') || e.includes('avi') || e.includes('mov') || e.includes('flv')) return 'video-camera';
    if (e.includes('pdf')) return 'file-pdf';
    if (e.includes('ppt')) return 'file-ppt';
    if (e.includes('doc')) return 'file-word';
    if (e.includes('xls')) return 'file-excel';
    if (e.includes('zip') || e.includes('rar') || e.includes('7z')) return 'file-zip';
    if (e.includes('png') || e.includes('jpg') || e.includes('gif') || e.includes('webp')) return 'file-image';
    return 'file-text';
  }

  login() { this.authService.navigateToLogin(); }
  logout() { this.authService.logout().subscribe(); }
}
