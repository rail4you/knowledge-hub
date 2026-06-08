import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzRateModule } from 'ng-zorro-antd/rate';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { ResourceService } from '../../proxy/resources/resource.service';
import { ResourceStatus } from '../../proxy/resources/enums/resource-status.enum';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import type { ResourceDto, ResourceCategoryDto } from '../../proxy/resources/models';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { ResourceReviewService, type ResourceRatingSummaryDto } from '../../search/resource-review/resource-review.service';
import { RecommendationService, type RecommendedResourceDto } from '../../search/recommendation/recommendation.service';
import { AuthErrorService } from '../../core/auth/auth-error.service';

interface HeroSlide {
  title: string;
  subtitle: string;
  description: string;
  color: string;
  tag: string;
  icon: string;
  backgroundImage: string;
}

interface StatItem {
  label: string;
  value: number;
  suffix: string;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-student-resources',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    FormsModule,
    NzIconModule,
    NzButtonModule,
    NzInputModule,
    NzSpinModule,
    NzPaginationModule,
    NzRateModule,
    NzDividerModule,
    FilePreviewComponent,
  ],
  templateUrl: './student-resources.component.html',
  styleUrls: ['./student-resources.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentResourcesComponent implements OnInit, OnDestroy {
  private readonly resourceService = inject(ResourceService);
  private readonly reviewService = inject(ResourceReviewService);
  private readonly recommendationService = inject(RecommendationService);
  private readonly authErrorService = inject(AuthErrorService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  resources = signal<ResourceDto[]>([]);
  loading = signal(false);
  filterText = signal('');
  selectedType = signal<ResourceType | null>(null);
  selectedCategoryId = signal<string | null>(null);
  categories = signal<ResourceCategoryDto[]>([]);

  totalCount = signal(0);
  pageIndex = signal(1);
  pageSize = signal(12);

  ratingSummaries = signal<Record<string, ResourceRatingSummaryDto>>({});
  collectedResourceIds = signal<Record<string, boolean>>({});

  recommendedResources = signal<RecommendedResourceDto[]>([]);
  recommendationsLoading = signal(false);

  activeHeroSlide = signal(0);
  heroAutoPlayTimer: ReturnType<typeof setInterval> | null = null;

  readonly ResourceType = ResourceType;

  // 站点统计（演示数据，实际可从后端获取）
  stats = signal<StatItem[]>([
    { label: '课程数', value: 168, suffix: '门', icon: 'book', color: '#1e6ce8' },
    { label: '微课数', value: 928, suffix: '节', icon: 'play-circle', color: '#00b7ff' },
    { label: '素材数', value: 5460, suffix: '份', icon: 'appstore', color: '#7c3aed' },
    { label: '学员数', value: 12, suffix: '万人', icon: 'team', color: '#f59e0b' },
  ]);

  heroSlides = signal<HeroSlide[]>([
    {
      title: '数字资源 · 一站获取',
      subtitle: 'DIGITAL RESOURCE HUB',
      description: '整合课程、微课、素材与文献资料，覆盖专业基础、专业核心与拓展课程，让学习更高效。',
      color: '#1e6ce8',
      tag: '资源库简介',
      icon: 'cloud',
      backgroundImage: '#1e6ce8',
    },
    {
      title: '名师优课 · 在线学习',
      subtitle: 'ONLINE PROFESSIONAL COURSES',
      description: '汇聚国家级精品在线开放课程，专业教学团队系统讲解，支持在线学习与互动交流。',
      color: '#0891b2',
      tag: '学历课程体系',
      icon: 'read',
      backgroundImage: '#0891b2',
    },
    {
      title: '知识图谱 · 体系化认知',
      subtitle: 'KNOWLEDGE GRAPH',
      description: '基于知识图谱构建专业认知体系，节点关系一目了然，助力学习者构建结构化知识网络。',
      color: '#10b981',
      tag: '知识图谱',
      icon: 'apartment',
      backgroundImage: '#10b981',
    },
  ]);

  resourceTypes = [
    { label: '全部', value: null as ResourceType | null, icon: 'appstore' },
    { label: '文档', value: ResourceType.Document, icon: 'file-text' },
    { label: '视频', value: ResourceType.Video, icon: 'video-camera' },
    { label: '音频', value: ResourceType.Audio, icon: 'sound' },
    { label: '图片', value: ResourceType.Image, icon: 'picture' },
    { label: 'PPT', value: ResourceType.PPT, icon: 'file-ppt' },
  ];

  // 热门分类（演示数据）
  hotCategories = signal([
    { name: '专业基础课', icon: 'book', color: '#1e6ce8' },
    { name: '专业核心课', icon: 'trophy', color: '#7c3aed' },
    { name: '专业拓展课', icon: 'rocket', color: '#10b981' },
    { name: '公共基础课', icon: 'read', color: '#f59e0b' },
    { name: '培训课程', icon: 'solution', color: '#ec4899' },
    { name: '知识图谱', icon: 'apartment', color: '#06b6d4' },
  ]);

  ngOnInit() {
    this.loadCategories();
    this.loadResources();
    this.loadRecommendations();
    this.startHeroAutoPlay();
  }

  ngOnDestroy() {
    this.stopHeroAutoPlay();
  }

  startHeroAutoPlay() {
    this.stopHeroAutoPlay();
    this.heroAutoPlayTimer = setInterval(() => {
      this.activeHeroSlide.set((this.activeHeroSlide() + 1) % this.heroSlides().length);
    }, 6000);
  }

  stopHeroAutoPlay() {
    if (this.heroAutoPlayTimer) {
      clearInterval(this.heroAutoPlayTimer);
      this.heroAutoPlayTimer = null;
    }
  }

  selectHeroSlide(index: number) {
    this.activeHeroSlide.set(index);
    this.startHeroAutoPlay();
  }

  loadResources() {
    this.loading.set(true);
    const input: any = {
      status: ResourceStatus.LeagueApproved,
      filter: this.filterText() || undefined,
      resourceType: this.selectedType() ?? undefined,
      categoryId: this.selectedCategoryId() || undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    };

    this.resourceService.getFilteredList(input).subscribe({
      next: (result) => {
        this.resources.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
        this.loadRatingSummaries(result.items || []);
        this.loadCollectionStatus(result.items || []);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 401 || err.status === 403) {
          this.authErrorService.setAuthError(
            err.status,
            err.error?.error?.message || err.error?.message || '您未获得授权！'
          );
        }
      }
    });
  }

  loadCategories() {
    this.resourceService.getCategories().subscribe({
      next: (cats) => this.categories.set(cats || [])
    });
  }

  loadRatingSummaries(items: ResourceDto[]) {
    const summaries = { ...this.ratingSummaries() };
    items.forEach(resource => {
      if (!resource.id) return;
      this.reviewService.getRatingSummary(resource.id).subscribe({
        next: (summary) => {
          summaries[resource.id!] = summary;
          this.ratingSummaries.set({ ...summaries });
        }
      });
    });
  }

  loadCollectionStatus(items: ResourceDto[]) {
    if (items.length === 0) {
      this.collectedResourceIds.set({});
      return;
    }

    const collectedMap: Record<string, boolean> = {};

    items.forEach(resource => {
      if (!resource.id) return;
      this.resourceService.isCollected(resource.id).subscribe({
        next: (isCollected) => {
          collectedMap[resource.id!] = isCollected;
          this.collectedResourceIds.set({ ...this.collectedResourceIds(), ...collectedMap });
        }
      });
    });
  }

  selectCategory(categoryId: string | null) {
    this.selectedCategoryId.set(categoryId);
    this.pageIndex.set(1);
    this.loadResources();
  }

  /**
   * 点击"热门分类"chip：把展示用的名称映射到真实分类 id，再走 selectCategory。
   * 旧实现里所有 hot-chip 都调用 selectCategory(null)，导致点击热门分类永远回到"全部"，
   * 用户体验上看就是"分类 Tab 点击无反应"。
   */
  selectHotCategory(name: string) {
    const match = this.categories().find(c => c.name === name);
    if (match?.id) {
      this.selectCategory(match.id);
    } else {
      // 没有匹配到真实分类（可能是演示数据），回退到"全部"以避免假死
      this.selectCategory(null);
    }
  }

  /**
   * 热门分类 chip 的高亮判断：当且仅当当前选中的真实分类的 name 与 chip name 一致时高亮。
   * 选中"全部"时所有 chip 都不高亮。
   */
  isHotCategoryActive(name: string): boolean {
    const id = this.selectedCategoryId();
    if (!id) return false;
    const current = this.categories().find(c => c.id === id);
    return current?.name === name;
  }

  selectType(type: ResourceType | null) {
    this.selectedType.set(type);
    this.pageIndex.set(1);
    this.loadResources();
  }

  onPageChange(index: number) {
    this.pageIndex.set(index);
    this.loadResources();
  }

  onSearch() {
    this.pageIndex.set(1);
    this.loadResources();
  }

  openDetail(resource: ResourceDto) {
    if (!resource.id) return;
    this.router.navigate(['/student/resources', resource.id]);
  }

  previewResource(event: Event, resource: ResourceDto) {
    event.stopPropagation();
    if (!resource?.id) return;
    this.filePreview.open(
      resource.id,
      resource.originalFileName || resource.name || '未命名',
      resource.fileExtension || '',
      resource.fileSize || 0
    );
  }

  downloadResource(event: Event, resource: ResourceDto) {
    event.stopPropagation();
    if (!resource?.id) return;
    const url = `/api/resource-file/${resource.id}/download`;
    const a = document.createElement('a');
    a.href = url;
    a.download = resource.originalFileName || resource.name || 'download';
    a.click();
    this.message.success('下载已开始');
  }

  toggleCollection(event: Event, resource: ResourceDto) {
    event.stopPropagation();
    if (!resource.id) return;

    const isCollected = this.isCollected(resource.id);
    const request$ = isCollected
      ? this.resourceService.uncollect(resource.id)
      : this.resourceService.collect(resource.id);

    request$.subscribe({
      next: () => {
        const nextValue = !isCollected;
        this.collectedResourceIds.set({
          ...this.collectedResourceIds(),
          [resource.id!]: nextValue,
        });
        this.message.success(nextValue ? '已加入收藏' : '已取消收藏');
      },
      error: () => {
        this.message.error(isCollected ? '取消收藏失败' : '收藏失败');
      }
    });
  }

  isCollected(resourceId?: string) {
    return !!resourceId && !!this.collectedResourceIds()[resourceId];
  }

  getResourceTypeIcon(type?: number): string {
    const icons: Record<number, string> = {
      [ResourceType.Document]: 'file-text',
      [ResourceType.Video]: 'video-camera',
      [ResourceType.Audio]: 'sound',
      [ResourceType.Image]: 'picture',
      [ResourceType.PPT]: 'file-ppt',
    };
    return icons[type ?? 0] || 'file-text';
  }

  getResourceTypeName(type?: number): string {
    const names: Record<number, string> = {
      [ResourceType.Document]: '文档',
      [ResourceType.Video]: '视频',
      [ResourceType.Audio]: '音频',
      [ResourceType.Image]: '图片',
      [ResourceType.PPT]: '演示文稿',
    };
    return names[type ?? 0] || '资料';
  }

  formatFileSize(size?: number): string {
    if (!size) return '未知大小';
    if (size >= 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)} MB`;
    if (size >= 1024) return `${(size / 1024).toFixed(0)} KB`;
    return `${size} B`;
  }

  loadRecommendations() {
    this.recommendationsLoading.set(true);
    this.recommendationService.getPersonalizedRecommendations(8).subscribe({
      next: (result) => {
        this.recommendedResources.set(result || []);
        this.recommendationsLoading.set(false);
      },
      error: (err) => {
        this.recommendationsLoading.set(false);
        if (err.status === 401 || err.status === 403) {
          this.authErrorService.setAuthError(
            err.status,
            err.error?.error?.message || err.error?.message || '您未获得授权！'
          );
        }
      }
    });
  }

  openRecommended(event: Event, rec: RecommendedResourceDto) {
    event.preventDefault();
    if (!rec.resourceId) return;
    this.router.navigate(['/student/resources', rec.resourceId]);
  }
}
