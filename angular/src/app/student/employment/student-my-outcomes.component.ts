import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { ActivatedRoute, ParamMap, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { EmploymentService } from '../../proxy/employment/employment.service';
import { EmploymentOutcomeStatus } from '../../proxy/employment/enums';
import type { EmploymentOutcomeDto } from '../../proxy/employment/dtos/models';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import { outcomeStatusLabel, outcomeStatusTone } from './employment-labels';

/**
 * 学生端 · 我的就业去向（对齐原型 `#employment-followup` 的「就业去向填报」）。
 *
 * 数据来源（全部真实接口）：
 *   · 去向列表 GET /api/app/employment/outcome-list
 *     （学生没有「维护去向」权限，后端会自动过滤为本人记录）
 *
 * 与原型的差异（后端事实）：
 *   原型写的是「正式系统由本人提交、就业老师审核」，但后台的
 *   POST /api/app/employment/save-outcome 与 DELETE /api/app/employment/{id}/outcome
 *   只开放给具备 Employment.ManageOutcome 权限的教师 / 管理员，
 *   学生角色没有该权限，且保存时强制要求「去向单位 + 岗位名称」，
 *   无法只提交「求职中」这一状态。因此本页只做「查看 + 状态展示」，
 *   填报入口以 app-student-empty 如实说明，不做假提交。
 */
@Component({
  selector: 'app-student-my-outcomes',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    StudentPaginationComponent,
  ],
  templateUrl: './student-my-outcomes.component.html',
  styleUrls: ['./student-my-outcomes.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyOutcomesComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly items = signal<EmploymentOutcomeDto[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  /** 页头数据总览（数字来自真实接口） */
  readonly stats = computed(() => {
    const list = this.items();
    return [
      { label: '去向记录', value: this.totalCount(), hint: '全部去向记录总数' },
      {
        label: '本页已签约',
        value: list.filter(o => o.status === EmploymentOutcomeStatus.Signed).length,
        hint: '当前页已签约的去向',
      },
      {
        label: '主要去向',
        value: list.filter(o => o.isPrimary).length,
        hint: '标记为主要就业去向',
      },
    ];
  });

  ngOnInit(): void {
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        this.applyQuery(params);
        this.loadItems();
      });
  }

  private applyQuery(params: ParamMap): void {
    const page = Number(params.get('page') || '1');
    this.pageIndex.set(Number.isFinite(page) && page > 0 ? Math.floor(page) : 1);
  }

  private navigate(params: Record<string, string | null>, replaceUrl = true): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: params,
      queryParamsHandling: 'merge',
      replaceUrl,
    });
  }

  /** 加载去向列表（服务端分页；学生仅可见本人记录） */
  private loadItems(): void {
    this.loading.set(true);
    this.loadFailed.set(false);

    this.employmentService
      .getOutcomeList({
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      })
      .subscribe({
        next: result => {
          this.items.set(result?.items || []);
          this.totalCount.set(result?.totalCount || 0);
          this.loading.set(false);
        },
        error: () => {
          this.items.set([]);
          this.loading.set(false);
          this.loadFailed.set(true);
        },
      });
  }

  /** 重新加载 */
  reload(): void {
    this.loadItems();
  }

  /** 分页 */
  onPageChange(event: { pageIndex: number; pageSize: number }): void {
    this.pageIndex.set(event.pageIndex);
    this.navigate({ page: String(event.pageIndex) }, false);
  }

  /** 状态文案 */
  statusLabel(item: EmploymentOutcomeDto): string {
    return outcomeStatusLabel(item.status);
  }

  /** 状态色调 */
  statusTone(item: EmploymentOutcomeDto): string {
    return outcomeStatusTone(item.status);
  }

  /** 岗位行文本 */
  jobText(item: EmploymentOutcomeDto): string {
    const parts = [item.jobTitle || '暂无岗位', item.employmentType].filter(p => !!p?.trim());
    return parts.join(' · ');
  }
}
