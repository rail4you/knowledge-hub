import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ResourceService } from '../../proxy/resources/resource.service';
import { ResourceStatus } from '../../proxy/resources/enums/resource-status.enum';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import { ResourceMediaStatus } from '../../proxy/resources/enums/resource-media-status.enum';
import type { ResourceDto, ResourceCategoryDto, ResourceListQueryDto } from '../../proxy/resources/models';
import { TenantInfoService } from '../../proxy/tenant-infos/tenant-info.service';
import { AuthErrorService } from '../../core/auth/auth-error.service';
import { StudentResourceCollectionService } from '../resource-collection.service';
import { ResourceCoverComponent } from '../../shared/resource-cover/resource-cover.component';

/** 分类筛选 chip（对齐原型 .chips：全部 + 各分类） */
interface CategoryChip {
  id: string | null;
  name: string;
}

/** 排序方式（对齐原型 sort select：综合 / 最新更新 / 名称） */
type SortKey = 'default' | 'updated' | 'name';

/** 服务端 sorting 参数映射（ResourceListQueryDto 继承 PagedAndSortedResultRequestDto） */
const SORTING: Record<SortKey, string | undefined> = {
  default: undefined,
  updated: 'LastModificationTime desc',
  name: 'Name asc',
};

@Component({
  selector: 'app-student-resources',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    FormsModule,
    NzIconModule,
    NzSpinModule,
    NzPaginationModule,
    ResourceCoverComponent,
  ],
  templateUrl: './student-resources.component.html',
  styleUrls: ['./student-resources.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentResourcesComponent implements OnInit, OnDestroy {
  private readonly resourceService = inject(ResourceService);
  private readonly authService = inject(AuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly collectionService = inject(StudentResourceCollectionService);
  private readonly authErrorService = inject(AuthErrorService);
  private readonly tenantInfoService = inject(TenantInfoService);

  /** 媒体生成中的轮询（有限次数），处理完成后自动刷新封面 */
  private mediaPollTimer?: ReturnType<typeof setTimeout>;
  private mediaPollCount = 0;

  resources = signal<ResourceDto[]>([]);
  loading = signal(false);
  /** 资源列表加载是否失败（用于区分“加载失败”与“无数据”空态） */
  loadFailed = signal(false);
  filterText = signal('');
  selectedCategoryId = signal<string | null>(null);
  sortKey = signal<SortKey>('default');
  categories = signal<ResourceCategoryDto[]>([]);

  totalCount = signal(0);
  pageIndex = signal(1);
  pageSize = signal(12);

  collectedResourceIds = signal<Record<string, boolean>>({});

  /** 分类筛选 chips（对齐原型：全部 + 分类目录） */
  readonly chips = computed<CategoryChip[]>(() => [
    { id: null, name: '全部' },
    ...(this.categories() || []).map(c => ({ id: c.id ?? null, name: c.name || '未命名分类' })),
  ]);

  /**
   * 空态类型：区分“加载失败 / 筛选无命中 / 租户没有资源”，
   * 文案对齐原型 empty（“暂时没有找到相关资源” + 重置筛选）。
   */
  readonly emptyState = computed<'error' | 'search' | 'empty'>(() => {
    if (this.loadFailed()) return 'error';
    const hasFilter = !!this.filterText().trim() || !!this.selectedCategoryId();
    return hasFilter ? 'search' : 'empty';
  });

  ngOnInit() {
    this.loadCategories();
    this.loadResources();
    this.loadCurrentTenantName();
  }

  loadResources(force = false) {
    this.loading.set(true);
    this.loadFailed.set(false);
    // 轮询刷新时带时间戳绕过浏览器 60s 缓存，及时拿到媒体处理状态
    const bust = force ? { _t: Date.now() } : {};

    const input: ResourceListQueryDto = {
      status: ResourceStatus.LeagueApproved,
      filter: this.filterText() || undefined,
      categoryId: this.selectedCategoryId() || undefined,
      sorting: SORTING[this.sortKey()],
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
      ...bust,
    } as ResourceListQueryDto;

    this.resourceService.getFilteredList(input).subscribe({
      next: (result) => {
        this.resources.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
        this.loadFailed.set(false);
        this.loadCollectionStatus(result.items || []);
        this.scheduleMediaPoll(result.items || []);
      },
      error: (err) => {
        this.loading.set(false);
        this.loadFailed.set(true);
        if (err.status === 401 || err.status === 403) {
          this.authErrorService.setAuthError(
            err.status,
            err.error?.error?.message || err.error?.message || '您未获得授权！'
          );
        }
      }
    });
  }

  /** 列表中有资源正在生成媒体时，每 5s 轮询刷新（最多 24 次，约 2 分钟） */
  private scheduleMediaPoll(items: ResourceDto[]): void {
    if (this.mediaPollTimer) {
      clearTimeout(this.mediaPollTimer);
      this.mediaPollTimer = undefined;
    }

    const anyProcessing = items.some(r => r.mediaStatus === ResourceMediaStatus.Processing);
    if (!anyProcessing) {
      this.mediaPollCount = 0;
      return;
    }

    if (this.mediaPollCount >= 24) {
      return;
    }

    this.mediaPollCount++;
    this.mediaPollTimer = setTimeout(() => this.loadResources(true), 5000);
  }

  ngOnDestroy(): void {
    if (this.mediaPollTimer) {
      clearTimeout(this.mediaPollTimer);
      this.mediaPollTimer = undefined;
    }
  }

  loadCategories() {
    this.resourceService.getCategories().subscribe({
      next: (cats) => this.categories.set(cats || [])
    });
  }

  loadCollectionStatus(items: ResourceDto[]) {
    // 游客无法收藏，跳过收藏状态查询
    if (!this.authService.isAuthenticated) return;

    const current = this.collectedResourceIds();
    const pendingIds = items
      .map(r => r.id)
      .filter((id): id is string => !!id && current[id] === undefined);

    if (pendingIds.length === 0) return;

    // 一次性批量查询已收藏的资源 Id
    this.collectionService.checkCollectedStatus(pendingIds).subscribe({
      next: collectedIds => {
        const map = { ...this.collectedResourceIds() };
        pendingIds.forEach(id => { map[id] = false; });
        (collectedIds || []).forEach(id => { map[id] = true; });
        this.collectedResourceIds.set(map);
      }
    });
  }

  selectCategory(categoryId: string | null) {
    if (this.selectedCategoryId() === categoryId) return;
    this.selectedCategoryId.set(categoryId);
    this.pageIndex.set(1);
    this.loadResources();
  }

  setSort(value: SortKey) {
    if (this.sortKey() === value) return;
    this.sortKey.set(value);
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

  onReset() {
    this.filterText.set('');
    this.selectedCategoryId.set(null);
    this.sortKey.set('default');
    this.pageIndex.set(1);
    this.loadResources();
  }

  openDetail(resource: ResourceDto) {
    if (!resource.id) return;
    this.router.navigate(['/student/resources', resource.id]);
  }

  /** 原型卡片右上角收藏星标（对齐原型 data-fav 按钮） */
  toggleCollection(event: Event, resource: ResourceDto) {
    event.stopPropagation();
    if (!resource.id) return;

    if (!this.authService.isAuthenticated) {
      this.message.info('登录后即可收藏资源');
      return;
    }

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

  /** 当前登录用户的租户 ID（host 为 null） */
  private get currentTenantId(): string | null {
    const cu = this.configState.getDeep('currentUser') as Record<string, unknown> | undefined;
    return (cu?.['tenantId'] as string | null | undefined) ?? null;
  }

  /** 当前租户展示名（从 TenantInfo 接口加载；host 用户为 null） */
  readonly currentTenantName = signal<string | null>(null);

  /** 从后端加载当前租户展示名（本租户资源的来源兜底） */
  private loadCurrentTenantName(): void {
    if (!this.currentTenantId) return;
    this.tenantInfoService.getCurrent().subscribe({
      next: info => {
        // 优先 TenantInfo.Name（专业库展示名，如“启迪”），回退 tenantId
        const name = (info?.name || '').trim();
        this.currentTenantName.set(name || null);
      },
      error: () => {
        // 静默失败：不影响列表主体
        this.currentTenantName.set(null);
      },
    });
  }

  /** 获取资源的来源名称：优先用后端返回的 sourceTenantName，本租户资源则取当前用户所在租户名 */
  getResourceSourceName(resource: ResourceDto | null | undefined): string | null {
    return resource?.sourceTenantName || this.currentTenantName();
  }

  /** 卡片描述行（对齐原型 “中医专业资源库 · 精选教学资源”，用真实来源院校名） */
  resourceDesc(resource: ResourceDto): string {
    const source = this.getResourceSourceName(resource);
    return source ? `${source}资源库 · 精选教学资源` : '精选教学资源';
  }

  /** 卡片文件类型 tag（对齐原型 tag gray：PPTX / MP4 / PDF…） */
  extensionTag(resource: ResourceDto): string {
    let ext = (resource.fileExtension || '').replace(/^\./, '').toLowerCase();
    if (!ext) {
      const fileName = resource.originalFileName || resource.name || '';
      const dot = fileName.lastIndexOf('.');
      if (dot >= 0) ext = fileName.substring(dot + 1).toLowerCase();
    }
    if (!ext) return this.resourceTypeTag(resource.resourceType);
    return ext.toUpperCase();
  }

  private resourceTypeTag(type?: ResourceType): string {
    switch (type) {
      case ResourceType.Video: return 'MP4';
      case ResourceType.Audio: return 'AUDIO';
      case ResourceType.Image: return 'IMG';
      default: return 'PDF';
    }
  }

  /** 媒体兜底图标（缩略图未生成/失败时展示在封面中央） */
  fallbackIcon(type?: ResourceType): string {
    switch (type) {
      case ResourceType.Video: return 'play-circle';
      case ResourceType.Audio: return 'sound';
      case ResourceType.Image: return 'picture';
      default: return 'file-text';
    }
  }

  /** 封面兜底底色 class：视频/图片偏深，文档偏浅 */
  fallbackClass(type?: ResourceType): string {
    switch (type) {
      case ResourceType.Video: return 'is-video';
      case ResourceType.Audio: return 'is-audio';
      case ResourceType.Image: return 'is-image';
      default: return 'is-doc';
    }
  }
}
