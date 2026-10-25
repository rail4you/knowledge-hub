import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import { SpecialEduResourceService } from '../../proxy/application/special-education/special-edu-resource.service';
import { BraillePair, BrailleViewerComponent } from '../../special-edu/braille-viewer/braille-viewer.component';
import { SPECIAL_EDU_CATEGORY_LABELS, modalityLabel } from './special-edu-labels';
import type { SpecialEduResourceDto } from '../../proxy/special-education/dtos/models';

@Component({
  selector: 'app-student-special-resources',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    NzSelectModule,
    NzIconModule,
    BrailleViewerComponent,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    StudentPaginationComponent,
  ],
  templateUrl: './student-special-resources.component.html',
  styleUrls: ['./student-special-resources.component.scss'],
})
export class StudentSpecialResourcesComponent {
  private readonly resourceService = inject(SpecialEduResourceService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly categories = Object.entries(SPECIAL_EDU_CATEGORY_LABELS).map(([value, label]) => ({
    value: Number(value),
    label,
  }));

  readonly items = signal<SpecialEduResourceDto[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly total = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(20);

  /** 当前类别筛选（null = 全部）；来自 URL query `cat` */
  readonly category = signal<number | null>(null);

  /** 是否有筛选条件（决定空态用 result 还是 data） */
  readonly hasFilter = computed(() => this.category() !== null);

  /**
   * 盲文对照点：随资源一次性解析并缓存，
   * 避免模板每轮变更检测都重复 JSON.parse。
   */
  private readonly pairsCache = computed(() => {
    const map = new Map<SpecialEduResourceDto, BraillePair[]>();
    for (const item of this.items()) {
      let pairs: BraillePair[] = [];
      try {
        const raw = JSON.parse(item?.rawJson ?? '{}');
        if (Array.isArray(raw?.pairs)) pairs = raw.pairs as BraillePair[];
      } catch {
        pairs = [];
      }
      map.set(item, pairs);
    }
    return map;
  });

  constructor() {
    // 筛选与页码都存在 URL 上，刷新 / 前进后退都能还原
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const cat = params.get('cat');
      const category = cat === null || cat === '' ? null : Number(cat);
      const page = Number(params.get('page') ?? '1');
      this.category.set(category !== null && !isNaN(category) ? category : null);
      this.pageIndex.set(isNaN(page) || page < 1 ? 1 : page);
      this.load();
    });
  }

  pairsOf(item: SpecialEduResourceDto): BraillePair[] {
    return this.pairsCache().get(item) ?? [];
  }

  modalityText(item: SpecialEduResourceDto): string {
    return modalityLabel(item?.modality);
  }

  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.resourceService
      .getList({
        category: this.category(),
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.items.set(result?.items ?? []);
          this.total.set(result?.totalCount ?? 0);
          this.loading.set(false);
        },
        error: () => {
          this.items.set([]);
          this.loadFailed.set(true);
          this.loading.set(false);
        },
      });
  }

  /** 切换类别：写回 URL */
  onCategoryChange(value: number | null): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { cat: value ?? null, page: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  onPageChange(e: { pageIndex: number; pageSize: number }): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: e.pageIndex },
      queryParamsHandling: 'merge',
    });
  }

  /** 清除筛选 */
  clearFilter(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { cat: null, page: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  retry(): void {
    this.load();
  }
}
