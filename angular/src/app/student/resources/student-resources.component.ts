import { ChangeDetectionStrategy, Component, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
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
import { PortalService } from '../../proxy/portal/portal.service';
import { MajorService } from '../../proxy/majors/major.service';
import type { MajorLookupDto } from '../../proxy/majors/dtos/models';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { buildDownloadFileName } from '../../shared/download/download-file.util';
import { ResourceReviewService, type ResourceRatingSummaryDto } from '../../search/resource-review/resource-review.service';
import { RecommendationService, type RecommendedResourceDto } from '../../search/recommendation/recommendation.service';
import { AuthErrorService } from '../../core/auth/auth-error.service';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';
import { StudentResourceCollectionService } from '../resource-collection.service';
import { ResourceCoverComponent } from '../../shared/resource-cover/resource-cover.component';

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
    StudentHeroComponent,
    ResourceCoverComponent,
  ],
  templateUrl: './student-resources.component.html',
  styleUrls: ['./student-resources.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentResourcesComponent implements OnInit {
  private readonly resourceService = inject(ResourceService);
  private readonly portalService = inject(PortalService);
  private readonly majorService = inject(MajorService);
  private readonly reviewService = inject(ResourceReviewService);
  private readonly recommendationService = inject(RecommendationService);
  private readonly authErrorService = inject(AuthErrorService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly collectionService = inject(StudentResourceCollectionService);

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  resources = signal<ResourceDto[]>([]);
  loading = signal(false);
  filterText = signal('');
  selectedType = signal<ResourceType | null>(null);
  selectedCategoryId = signal<string | null>(null);
  selectedMajorId = signal<string | null>(null);
  categories = signal<ResourceCategoryDto[]>([]);
  majors = signal<MajorLookupDto[]>([]);

  totalCount = signal(0);
  pageIndex = signal(1);
  pageSize = signal(12);

  ratingSummaries = signal<Record<string, ResourceRatingSummaryDto>>({});
  collectedResourceIds = signal<Record<string, boolean>>({});
  showFavorites = signal(false);
  favoritesCount = signal(0);
  /** 当前视图（普通分类 / 我的收藏）下的资源总条数 */
  readonly viewTotal = computed(() => this.showFavorites() ? this.favoritesCount() : this.totalCount());

  recommendedResources = signal<RecommendedResourceDto[]>([]);
  recommendationsLoading = signal(false);

  readonly ResourceType = ResourceType;

  // 站点统计（演示数据，实际可从后端获取）
  stats = signal<StatItem[]>([]);

  /** 加载首页数据总览：从 PortalService 拿真实数据，避免显示硬编码占位 */
  private loadHomeStats(): void {
    this.portalService.getPublicHomeStats().subscribe({
      next: data => {
        this.stats.set([
          { label: '课程数', value: data.totalCourseCount ?? 0, suffix: '门', icon: 'book', color: '#1e6ce8' },
          { label: '资源数', value: data.totalResourceCount ?? 0, suffix: '份', icon: 'play-circle', color: '#00b7ff' },
          { label: '入驻租户', value: data.tenantCount ?? 0, suffix: '家', icon: 'team', color: '#f59e0b' },
        ]);
      },
      error: () => {
        // 静默失败时显示 0 占位
        this.stats.set([
          { label: '课程数', value: 0, suffix: '门', icon: 'book', color: '#1e6ce8' },
          { label: '资源数', value: 0, suffix: '份', icon: 'play-circle', color: '#00b7ff' },
          { label: '入驻租户', value: 0, suffix: '家', icon: 'team', color: '#f59e0b' },
        ]);
      },
    });
  }

  resourceTypes = [
    { label: '全部', value: null as ResourceType | null, icon: 'appstore' },
    { label: '文档', value: ResourceType.Document, icon: 'file-text' },
    { label: '视频', value: ResourceType.Video, icon: 'video-camera' },
    { label: '音频', value: ResourceType.Audio, icon: 'sound' },
    { label: '图片', value: ResourceType.Image, icon: 'picture' },
  ];

  // 热门目录：从真实分类数据中取所有有效分类，按资源数量降序排列
  /** 从 MajorService 加载可用专业列表 */
  majorChips = computed(() => {
    return this.majors().map(m => ({
      id: m.id!,
      name: m.name!,
      count: 0
    }));
  });

  hotCategories = computed(() => {
    const cats = this.categories();
    // 展平所有分类（根节点 + 子节点）
    const flat: { id: string; name: string; count: number }[] = [];
    const flatten = (list: ResourceCategoryDto[]) => {
      for (const c of list) {
        if (c.id && c.name) {
          flat.push({ id: c.id, name: c.name, count: c.resourceCount ?? 0 });
        }
        if (c.children && c.children.length > 0) {
          flatten(c.children);
        }
      }
    };
    flatten(cats);
    // 按资源数量降序排列
    flat.sort((a, b) => b.count - a.count);
    return flat;
  });

  ngOnInit() {
    this.loadCategories();
    this.loadMajors();
    this.loadResources();
    this.loadRecommendations();
    this.loadHomeStats();
    this.loadFavoritesCount();
  }

  loadResources() {
    this.loading.set(true);

    if (this.showFavorites()) {
      this.collectionService.getCollectedList({
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      }).subscribe({
        next: (result) => {
          const items = result.items || [];
          this.resources.set(items);
          this.favoritesCount.set(result.totalCount || 0);
          this.loading.set(false);
          this.loadRatingSummaries(items);
          // “我的收藏”里的资源全部视为已收藏
          const map: Record<string, boolean> = {};
          items.forEach(r => { if (r.id) map[r.id] = true; });
          this.collectedResourceIds.set(map);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('收藏列表加载失败');
        }
      });
      return;
    }

    const input: any = {
      status: ResourceStatus.LeagueApproved,
      filter: this.filterText() || undefined,
      resourceType: this.selectedType() ?? undefined,
      categoryId: this.selectedCategoryId() || undefined,
      majorId: this.selectedMajorId() || undefined,
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

  loadMajors() {
    this.majorService.getLookupList().subscribe({
      next: (list) => this.majors.set(list || []),
      error: () => this.majors.set([])
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

  selectFavorites() {
    this.showFavorites.set(true);
    this.pageIndex.set(1);
    this.loadResources();
  }

  private loadFavoritesCount(): void {
    this.collectionService.getCollectedList({ skipCount: 0, maxResultCount: 1 }).subscribe({
      next: (result) => this.favoritesCount.set(result.totalCount || 0),
      error: () => this.favoritesCount.set(0),
    });
  }

  selectCategory(categoryId: string | null) {
    this.showFavorites.set(false);
    this.selectedCategoryId.set(categoryId);
    this.pageIndex.set(1);
    this.loadResources();
  }

  selectMajor(majorId: string | null) {
    this.showFavorites.set(false);
    this.selectedMajorId.set(majorId);
    this.pageIndex.set(1);
    this.loadResources();
  }

  /**
   * 点击"热门目录"chip：直接使用分类 id 走 selectCategory。
   */
  selectHotCategory(categoryId: string) {
    this.selectCategory(categoryId);
  }

  /**
   * 热门目录 chip 的高亮判断。
   */
  isHotCategoryActive(categoryId: string): boolean {
    return this.selectedCategoryId() === categoryId;
  }

  selectType(type: ResourceType | null) {
    this.showFavorites.set(false);
    this.selectedType.set(type);
    this.pageIndex.set(1);
    this.loadResources();
  }

  onPageChange(index: number) {
    this.pageIndex.set(index);
    this.loadResources();
  }

  onSearch() {
    this.showFavorites.set(false);
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

    // 若 fileExtension 为空，尝试从 originalFileName 或 name 提取扩展名
    let ext = resource.fileExtension;
    if (!ext || ext === '') {
      const fileName = resource.originalFileName || resource.name || '';
      const dot = fileName.lastIndexOf('.');
      if (dot >= 0) {
        ext = fileName.substring(dot); // 包含 . 前缀
      }
    }

    this.filePreview.open(
      resource.id,
      resource.originalFileName || resource.name || '未命名',
      ext || '',
      resource.fileSize || 0,
      resource.isDownloadable !== false
    );

    // 本地乐观更新 viewCount
    this.resources.update(list =>
      list.map(r => r.id === resource.id ? { ...r, viewCount: (r.viewCount || 0) + 1 } : r)
    );
  }

  downloadResource(event: Event, resource: ResourceDto) {
    event.stopPropagation();
    if (!resource?.id) return;
    if (!resource.isDownloadable) {
      this.message.warning('该资源不允许下载，仅支持在线预览');
      return;
    }
    const url = `/api/resource-file/${resource.id}/download`;
    const a = document.createElement('a');
    a.href = url;
    const downloadName = buildDownloadFileName(resource.originalFileName, resource.name, resource.fileExtension);
    if (downloadName) a.download = downloadName;
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
        // 在“我的收藏”分类下取消收藏时，直接将该资源从列表移除
        if (this.showFavorites() && !nextValue) {
          this.resources.update(list => list.filter(r => r.id !== resource.id));
          this.totalCount.update(c => Math.max(0, c - 1));
          this.favoritesCount.update(c => Math.max(0, c - 1));
        }
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
      error: () => {
        // 推荐是辅助功能，静默失败，不干扰主内容加载
        this.recommendationsLoading.set(false);
      }
    });
  }

  openRecommended(event: Event, rec: RecommendedResourceDto) {
    event.preventDefault();
    if (!rec.resourceId) return;
    this.router.navigate(['/student/resources', rec.resourceId]);
  }
}
