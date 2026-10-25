import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import { SpecialIepService } from '../../proxy/application/special-education/special-iep.service';
import { categoryLabel } from './special-edu-labels';
import type { IepPlanDto } from '../../proxy/special-education/dtos/models';

@Component({
  selector: 'app-student-my-iep',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    StudentPaginationComponent,
  ],
  templateUrl: './student-my-iep.component.html',
  styleUrls: ['./student-my-iep.component.scss'],
})
export class StudentMyIepComponent {
  private readonly iepService = inject(SpecialIepService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly items = signal<IepPlanDto[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly total = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  constructor() {
    // 页码存 URL：刷新 / 前进后退都能还原
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const page = Number(params.get('page') ?? '1');
      this.pageIndex.set(isNaN(page) || page < 1 ? 1 : page);
      this.load();
    });
  }

  categoryText(item: IepPlanDto): string {
    return item?.categoryName || categoryLabel(item?.category);
  }

  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.iepService
      .getMyIepList({
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

  onPageChange(e: { pageIndex: number; pageSize: number }): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: e.pageIndex },
      queryParamsHandling: 'merge',
    });
  }

  retry(): void {
    this.load();
  }
}
