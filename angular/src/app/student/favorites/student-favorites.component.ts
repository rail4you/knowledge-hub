import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ResourceService } from '../../proxy/resources/resource.service';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import type { ResourceCategoryDto, ResourceDto, ResourceListQueryDto } from '../../proxy/resources/models';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import { StudentResourceRowComponent } from '../resources/student-resource-row.component';
import { RESOURCE_TYPE_FILTER_OPTIONS } from '../resources/student-resource-labels';

/**
 * 学生端 · 我的收藏。
 *
 * 对齐原型「个人学习中心 · 收藏课程」：收藏资源列表（与资源中心同一套行样式）+ 空态。
 *
 * 数据来源（全部真实接口）：
 *   · 收藏列表 GET  /api/app/resource/collected-list（服务端分页，支持关键词/格式/模块筛选）
 *   · 模块     GET  /api/app/resource/categories
 *   · 取消收藏 POST /api/app/resource/uncollect/{id}
 *
 * 筛选条件写进 URL query params（刷新不丢），页码同样在 URL 里。
 */
@Component({
  selector: 'app-student-favorites',
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
  templateUrl: './student-favorites.component.html',
  styleUrls: ['./student-favorites.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentFavoritesComponent implements OnInit {
  private readonly resourceService = inject(ResourceService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly resources = signal<ResourceDto[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(20);

  /** 模块筛选项 */
  readonly categories = signal<ResourceCategoryDto[]>([]);

  /** 与 URL 同步的筛选状态 */
  readonly keyword = signal('');
  readonly resourceType = signal<ResourceType | null>(null);
  readonly categoryId = signal<string | null>(null);

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

  /** 是否存在筛选条件 */
  readonly hasFilters = computed(
    () => !!this.keyword().trim() || this.resourceType() !== null || !!this.categoryId(),
  );

  ngOnInit(): void {
    this.loadCategories();

    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        this.applyQuery(params);
        this.loadFavorites();
      });
  }

  /** 把 URL 上的查询参数同步到信号 */
  private applyQuery(params: ParamMap): void {
    this.keyword.set((params.get('q') || '').trim());

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

  /** 加载收藏列表（服务端分页） */
  private loadFavorites(): void {
    this.loading.set(true);
    this.loadFailed.set(false);

    const input: ResourceListQueryDto = {
      filter: this.keyword() || undefined,
      resourceType: this.resourceType() ?? undefined,
      categoryId: this.categoryId() || undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    };

    this.resourceService.getCollectedList(input).subscribe({
      next: result => {
        this.resources.set(result?.items || []);
        this.totalCount.set(result?.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  /** 重新加载（错误态重试） */
  reload(): void {
    this.loadFavorites();
  }

  /** 模块筛选项 */
  private loadCategories(): void {
    this.resourceService.getCategories().subscribe({
      next: list => this.categories.set(list || []),
      error: () => this.categories.set([]),
    });
  }

  /** 提交检索条 */
  onSearch(): void {
    this.navigate({ q: this.keyword().trim() || null, page: null });
  }

  /** 选择筛选项 */
  setFilter(key: 'type' | 'category', value: string | number | null): void {
    // 统一转成字符串：resourceType 的枚举值 0（文档）是有效值，不能被当成空值丢掉
    const raw = value === null || value === undefined || value === '' ? null : String(value);
    if (key === 'type') {
      this.resourceType.set(raw === null ? null : (Number(raw) as ResourceType));
    }
    if (key === 'category') this.categoryId.set(raw);
    this.navigate({ [key]: raw, page: null });
  }

  /** 重置筛选 */
  resetFilters(): void {
    this.keyword.set('');
    this.resourceType.set(null);
    this.categoryId.set(null);
    this.navigate({ q: null, type: null, category: null, page: null });
  }

  /** 分页 */
  onPageChange(event: { pageIndex: number; pageSize: number }): void {
    this.pageIndex.set(event.pageIndex);
    this.navigate({ page: String(event.pageIndex) }, false);
  }

  /** 取消收藏（收藏夹里的资源默认都是已收藏状态） */
  toggleCollect(resourceId: string): void {
    this.resourceService.uncollect(resourceId).subscribe({
      next: () => {
        this.message.success('已取消收藏');
        if (this.resources().length === 1 && this.pageIndex() > 1) {
          this.pageIndex.update(v => v - 1);
          this.navigate({ page: String(this.pageIndex()) }, false);
          return;
        }
        this.loadFavorites();
      },
      error: () => this.message.error('取消收藏失败'),
    });
  }

  /** 去资源中心 */
  goResources(): void {
    this.router.navigate(['/student/resources']);
  }
}
