import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '@abp/ng.core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ResourceService } from '../../proxy/resources/resource.service';
import { MajorService } from '../../proxy/majors/major.service';
import { TenantInfoService } from '../../proxy/tenant-infos/tenant-info.service';
import { ResourceStatus } from '../../proxy/resources/enums/resource-status.enum';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import type { ResourceCategoryDto, ResourceDto, ResourceListQueryDto } from '../../proxy/resources/models';
import type { MajorLookupDto } from '../../proxy/majors/dtos/models';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import { StudentResourceRowComponent } from './student-resource-row.component';
import { RESOURCE_TYPE_FILTER_OPTIONS } from './student-resource-labels';

/**
 * 学生端 · 资源中心。
 *
 * 对齐原型 `#resources`：检索条 + 专业/格式/模块 三级筛选 + 资源行列表 + 空态 + 重置筛选。
 *
 * 数据来源（全部真实接口）：
 *   · 列表   GET /api/app/resource/filtered-list（服务端分页）
 *   · 模块   GET /api/app/resource/categories
 *   · 专业   GET /api/app/major/lookup-list
 *   · 租户名 GET /api/app/tenant-info/current
 *   · 收藏   POST /api/app/resource/collect|uncollect、check-collected-status
 *
 * 筛选条件全部写进 URL query params（刷新不丢），页码同样在 URL 里。
 */
@Component({
  selector: 'app-student-resources',
  standalone: true,
  imports: [
    CommonModule,
    DecimalPipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    StudentPaginationComponent,
    StudentResourceRowComponent,
  ],
  templateUrl: './student-resources.component.html',
  styleUrls: ['./student-resources.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentResourcesComponent implements OnInit {
  private readonly resourceService = inject(ResourceService);
  private readonly majorService = inject(MajorService);
  private readonly tenantInfoService = inject(TenantInfoService);
  private readonly authService = inject(AuthService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  /** 资源列表 */
  readonly resources = signal<ResourceDto[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(20);

  /** 筛选项 */
  readonly categories = signal<ResourceCategoryDto[]>([]);
  readonly majors = signal<MajorLookupDto[]>([]);

  /** 与 URL 同步的筛选状态 */
  readonly keyword = signal('');
  readonly majorId = signal<string | null>(null);
  readonly resourceType = signal<ResourceType | null>(null);
  readonly categoryId = signal<string | null>(null);

  /** 当前租户名（本租户资源来源兜底） */
  readonly tenantName = signal<string | null>(null);

  /** 已收藏资源 Id 集合 */
  readonly collectedIds = signal<Record<string, boolean>>({});

  /** 「资源格式」筛选项（对应后端 resourceType 枚举） */
  readonly typeOptions = RESOURCE_TYPE_FILTER_OPTIONS;

  /** 「模块」筛选项：分类树拍平（父 / 子） */
  readonly categoryOptions = computed<{ id: string; label: string }[]>(() => {
    const rows: { id: string; label: string }[] = [];
    (this.categories() || []).forEach(c => {
      if (!c.id) return;
      rows.push({ id: c.id, label: c.name || '未命名模块' });
      (c.children || []).forEach(child => {
        if (!child.id) return;
        rows.push({ id: child.id, label: `${c.name || '未命名模块'} / ${child.name || '未命名模块'}` });
      });
    });
    return rows;
  });

  /** 是否存在筛选条件（决定空态语义与「重置筛选」是否可用） */
  readonly hasFilters = computed(
    () =>
      !!this.keyword().trim() ||
      !!this.majorId() ||
      this.resourceType() !== null ||
      !!this.categoryId(),
  );

  ngOnInit(): void {
    this.loadCategories();
    this.loadMajors();
    this.loadTenantName();

    // 筛选 / 页码都在 URL 上：订阅 queryParamMap，浏览器前进后退也能正确回显。
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        this.applyQuery(params);
        this.loadResources();
      });
  }

  /** 把 URL 上的查询参数同步到信号 */
  private applyQuery(params: ParamMap): void {
    this.keyword.set((params.get('q') || '').trim());

    this.majorId.set(params.get('major') || null);

    const type = params.get('type');
    this.resourceType.set(type !== null && type !== '' ? (Number(type) as ResourceType) : null);

    this.categoryId.set(params.get('category') || null);

    const page = Number(params.get('page') || '1');
    this.pageIndex.set(Number.isFinite(page) && page > 0 ? Math.floor(page) : 1);
  }

  /** 写回 URL（null 表示移除该参数） */
  private navigate(params: Record<string, string | null>, replaceUrl = true): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: params,
      queryParamsHandling: 'merge',
      replaceUrl,
    });
  }

  /** 加载资源列表（服务端分页） */
  private loadResources(): void {
    this.loading.set(true);
    this.loadFailed.set(false);

    // Statuses 是后端新加的可选参数，生成代理尚未重新生成，故此处显式扩展类型。
    const input: ResourceListQueryDto & { statuses?: ResourceStatus[] } = {
      // 与后端其余入口同口径：SchoolApproved（本校审核通过）与 LeagueApproved（联盟审核通过）
      // 都属于「已发布」。只取 LeagueApproved 会让学校审核通过的资源在列表里消失，
      // 却仍然能从「相关推荐」点进去 —— 死角。
      // 注：Statuses 是新加的可选参数，代理尚未重新生成，因此这里走 as any 传入。
      statuses: [ResourceStatus.SchoolApproved, ResourceStatus.LeagueApproved],
      filter: this.keyword() || undefined,
      majorId: this.majorId() || undefined,
      resourceType: this.resourceType() ?? undefined,
      categoryId: this.categoryId() || undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    };

    this.resourceService.getFilteredList(input).subscribe({
      next: result => {
        this.resources.set(result?.items || []);
        this.totalCount.set(result?.totalCount || 0);
        this.loading.set(false);
        this.loadCollectionStatus(result?.items || []);
      },
      error: () => {
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  /** 重新加载（错误态重试） */
  reload(): void {
    this.loadResources();
  }

  /** 模块筛选项 */
  private loadCategories(): void {
    this.resourceService.getCategories().subscribe({
      next: list => this.categories.set(list || []),
      error: () => this.categories.set([]),
    });
  }

  /** 专业筛选项 */
  private loadMajors(): void {
    this.majorService.getLookupList().subscribe({
      next: list => this.majors.set(list || []),
      error: () => this.majors.set([]),
    });
  }

  /** 当前租户名（本租户资源来源兜底） */
  private loadTenantName(): void {
    this.tenantInfoService.getCurrent().subscribe({
      next: info => this.tenantName.set((info?.name || '').trim() || null),
      error: () => this.tenantName.set(null),
    });
  }

  /** 批量查询本页资源的收藏状态 */
  private loadCollectionStatus(items: ResourceDto[]): void {
    if (!this.authService.isAuthenticated) return;

    const current = this.collectedIds();
    const pendingIds = items
      .map(r => r.id)
      .filter((id): id is string => !!id && current[id] === undefined);
    if (pendingIds.length === 0) return;

    this.resourceService.checkCollectedStatus(pendingIds).subscribe({
      next: collected => {
        const map = { ...this.collectedIds() };
        pendingIds.forEach(id => (map[id] = false));
        (collected || []).forEach(id => (map[id] = true));
        this.collectedIds.set(map);
      },
      error: () => undefined,
    });
  }

  /** 提交检索条 */
  onSearch(): void {
    this.navigate({ q: this.keyword().trim() || null, page: null });
  }

  /** 选择筛选项 */
  setFilter(key: 'major' | 'type' | 'category', value: string | number | null): void {
    // 统一转成字符串：resourceType 的枚举值 0（文档）是有效值，不能被当成空值丢掉
    const raw = value === null || value === undefined || value === '' ? null : String(value);
    if (key === 'major') this.majorId.set(raw);
    if (key === 'type') this.resourceType.set(raw === null ? null : (Number(raw) as ResourceType));
    if (key === 'category') this.categoryId.set(raw);
    this.navigate({ [key]: raw, page: null });
  }

  /** 重置筛选（清空 URL 上的筛选参数） */
  resetFilters(): void {
    this.keyword.set('');
    this.majorId.set(null);
    this.resourceType.set(null);
    this.categoryId.set(null);
    this.navigate({ q: null, major: null, type: null, category: null, page: null });
  }

  /** 分页 */
  onPageChange(event: { pageIndex: number; pageSize: number }): void {
    this.pageIndex.set(event.pageIndex);
    this.navigate({ page: String(event.pageIndex) }, false);
  }

  /** 收藏 / 取消收藏 */
  toggleCollect(resourceId: string): void {
    if (!this.authService.isAuthenticated) {
      this.message.info('登录后即可收藏资源');
      return;
    }

    const wasCollected = !!this.collectedIds()[resourceId];
    const request$ = wasCollected
      ? this.resourceService.uncollect(resourceId)
      : this.resourceService.collect(resourceId);

    request$.subscribe({
      next: () => {
        this.collectedIds.set({
          ...this.collectedIds(),
          [resourceId]: !wasCollected,
        });
        this.message.success(wasCollected ? '已取消收藏' : '已加入收藏');
      },
      error: () => this.message.error(wasCollected ? '取消收藏失败' : '收藏失败'),
    });
  }

  /** 某资源是否已收藏 */
  isCollected(resourceId?: string | null): boolean {
    return !!resourceId && !!this.collectedIds()[resourceId];
  }

  /** 去智能检索 */
  goSearch(): void {
    this.router.navigate(['/student/search']);
  }
}
