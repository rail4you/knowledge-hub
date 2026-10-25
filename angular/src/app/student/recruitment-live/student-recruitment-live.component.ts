import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DestroyRef } from '@angular/core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { RecruitmentLiveService } from '../../proxy/recruitment-live-service/recruitment-live.service';
import type { RecruitmentLiveDto } from '../../proxy/recruitment-live/dtos/models';
import { LiveType } from '../../proxy/recruitment-live/live-type.enum';
import { RecruitmentLiveStatus } from '../../proxy/recruitment-live/recruitment-live-status.enum';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import { LIVE_STATUS_LABEL, LIVE_TYPE_LABEL } from './student-recruitment-live-labels';

/** 状态筛选项（写入 URL） */
export type LiveStatusFilter = 'all' | 'waiting' | 'active' | 'ended' | 'cancelled';
/** 类型筛选项（写入 URL） */
export type LiveTypeFilter = 'all' | 'course' | 'recruitment';

/**
 * 学生端 · 课程直播与招聘面试（对齐原型 #live-center 的学生视图）
 *
 * 数据来源（真实接口，生成代理 proxy/recruitment-live-service）：
 *   · GET /api/app/recruitment-live/student-lives
 *     （服务端分页；服务端按「学生参与关系 / 已选课程」过滤，
 *      默认只返回等待中与进行中的安排）
 *
 * 诚实性约束：这里只显示**你被安排参与**或**你已选课程对应**的直播；
 * 其他院校、企业的安排不会自动对你开放（由服务端按参与关系校验）。
 * 页面不提供任何「查看全部直播」的越权入口。
 */
@Component({
  selector: 'app-student-recruitment-live',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    StudentPaginationComponent,
  ],
  templateUrl: './student-recruitment-live.component.html',
  styleUrls: ['./student-recruitment-live.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentRecruitmentLiveComponent implements OnInit {
  private readonly liveService = inject(RecruitmentLiveService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly lives = signal<RecruitmentLiveDto[]>([]);
  readonly totalCount = signal(0);

  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  /** 与 URL 同步的筛选 */
  readonly typeFilter = signal<LiveTypeFilter>('all');
  readonly statusFilter = signal<LiveStatusFilter>('all');

  /** 类型筛选项 */
  readonly typeOptions: { key: LiveTypeFilter; label: string }[] = [
    { key: 'all', label: '全部类型' },
    { key: 'course', label: '课程直播' },
    { key: 'recruitment', label: '招聘直播' },
  ];

  /** 状态筛选项 */
  readonly statusOptions: { key: LiveStatusFilter; label: string }[] = [
    { key: 'all', label: '全部（进行中 / 等待中）' },
    { key: 'waiting', label: '等待中' },
    { key: 'active', label: '进行中' },
    { key: 'ended', label: '已结束' },
    { key: 'cancelled', label: '已取消' },
  ];

  /** 是否处于筛选状态（区分 data / result 空态） */
  readonly hasFilter = computed(() => this.typeFilter() !== 'all' || this.statusFilter() !== 'all');

  /** 页头数据总览（当前页真实数据） */
  readonly stats = computed(() => {
    const page = this.lives();
    return [
      { label: '安排总数', value: this.totalCount(), hint: '你可见的直播安排', icon: 'video-camera' },
      {
        label: '本页进行中',
        value: page.filter(l => l.status === RecruitmentLiveStatus.Active).length,
        hint: '当前这一页正在进行',
        icon: 'play-circle',
      },
      {
        label: '本页等待中',
        value: page.filter(l => l.status === RecruitmentLiveStatus.Waiting).length,
        hint: '当前这一页尚未开始',
        icon: 'clock-circle',
      },
      {
        label: '本页课程直播',
        value: page.filter(l => l.type === LiveType.Course).length,
        hint: '当前这一页的课程直播',
        icon: 'read',
      },
    ];
  });

  ngOnInit(): void {
    // 筛选条件全部来自 URL（刷新、分享、浏览器前进后退都不丢状态）
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const type = params.get('type');
      const status = params.get('status');
      const page = Number(params.get('page') || '1');
      this.typeFilter.set(this.normalizeType(type));
      this.statusFilter.set(this.normalizeStatus(status));
      this.pageIndex.set(Number.isFinite(page) && page > 0 ? page : 1);
      this.load();
    });
  }

  private normalizeType(value: string | null): LiveTypeFilter {
    return value === 'course' || value === 'recruitment' ? value : 'all';
  }

  private normalizeStatus(value: string | null): LiveStatusFilter {
    switch (value) {
      case 'waiting':
      case 'active':
      case 'ended':
      case 'cancelled':
        return value;
      default:
        return 'all';
    }
  }

  /** 服务端分页加载 */
  private load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.liveService
      .getStudentLives({
        type: this.typeFilter() === 'all' ? null : this.typeFilter() === 'course' ? LiveType.Course : LiveType.Recruitment,
        status:
          this.statusFilter() === 'all'
            ? null
            : this.statusFilter() === 'waiting'
              ? RecruitmentLiveStatus.Waiting
              : this.statusFilter() === 'active'
                ? RecruitmentLiveStatus.Active
                : this.statusFilter() === 'ended'
                  ? RecruitmentLiveStatus.Ended
                  : RecruitmentLiveStatus.Cancelled,
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      })
      .subscribe({
        next: res => {
          this.lives.set(res?.items || []);
          this.totalCount.set(res?.totalCount ?? 0);
          this.loading.set(false);
        },
        error: () => {
          this.lives.set([]);
          this.totalCount.set(0);
          this.loadFailed.set(true);
          this.loading.set(false);
        },
      });
  }

  /** 重新加载 */
  reload(): void {
    this.load();
  }

  /** 切换类型筛选 */
  selectType(value: LiveTypeFilter): void {
    this.typeFilter.set(value);
    this.pageIndex.set(1);
    this.writeUrl();
  }

  /** 切换状态筛选 */
  selectStatus(value: LiveStatusFilter): void {
    this.statusFilter.set(value);
    this.pageIndex.set(1);
    this.writeUrl();
  }

  /** 清除筛选 */
  clearFilters(): void {
    this.typeFilter.set('all');
    this.statusFilter.set('all');
    this.pageIndex.set(1);
    this.writeUrl();
  }

  /** 翻页 */
  onPageChange(e: { pageIndex: number; pageSize: number }): void {
    this.pageSize.set(e.pageSize);
    this.pageIndex.set(e.pageIndex);
    this.writeUrl();
  }

  /** 写回 URL */
  private writeUrl(): void {
    const queryParams: Record<string, string> = {};
    if (this.typeFilter() !== 'all') queryParams['type'] = this.typeFilter();
    if (this.statusFilter() !== 'all') queryParams['status'] = this.statusFilter();
    if (this.pageIndex() > 1) queryParams['page'] = String(this.pageIndex());
    this.router.navigate([], { relativeTo: this.route, queryParams, replaceUrl: true });
  }

  /** 进入直播间 */
  enterLive(live: RecruitmentLiveDto): void {
    if (!live.id || !this.canEnter(live)) return;
    this.router.navigate(['/student/recruitment-live', live.id]);
  }

  /** 类型中文 */
  typeText(live: RecruitmentLiveDto): string {
    return LIVE_TYPE_LABEL[live.type ?? LiveType.Recruitment] ?? '直播';
  }

  /** 状态中文 */
  statusText(live: RecruitmentLiveDto): string {
    return LIVE_STATUS_LABEL[live.status ?? RecruitmentLiveStatus.Waiting] ?? '未知状态';
  }

  /** 是否已过期：等待中且已过计划结束时间 */
  isExpired(live: RecruitmentLiveDto): boolean {
    if (live.status !== RecruitmentLiveStatus.Waiting) return false;
    if (!live.scheduledEndAt) return false;
    return new Date(live.scheduledEndAt).getTime() < Date.now();
  }

  /** 是否可进入 */
  canEnter(live: RecruitmentLiveDto): boolean {
    if (live.status === RecruitmentLiveStatus.Active) return true;
    if (live.status === RecruitmentLiveStatus.Waiting) return !this.isExpired(live);
    return false;
  }

  /** 操作按钮文案 */
  actionText(live: RecruitmentLiveDto): string {
    if (live.status === RecruitmentLiveStatus.Active) return '进入直播间';
    if (live.status === RecruitmentLiveStatus.Waiting) {
      return this.isExpired(live) ? '已过期' : '进入直播间';
    }
    return '已结束';
  }

  /** 时间范围文案 */
  scheduleText(live: RecruitmentLiveDto): string {
    if (!live.scheduledAt) return '时间待定';
    const start = new Date(live.scheduledAt);
    const s = `${start.getMonth() + 1} 月 ${start.getDate()} 日 ${`${start.getHours()}`.padStart(2, '0')}:${`${start.getMinutes()}`.padStart(2, '0')}`;
    if (!live.scheduledEndAt) return s;
    const end = new Date(live.scheduledEndAt);
    return `${s} ~ ${`${end.getHours()}`.padStart(2, '0')}:${`${end.getMinutes()}`.padStart(2, '0')}`;
  }
}
