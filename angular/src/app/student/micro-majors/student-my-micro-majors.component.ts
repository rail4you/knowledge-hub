import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { MicroMajorService } from '../../proxy/micro-majors/micro-major.service';
import { LearningService } from '../../proxy/learning/learning.service';
import type {
  MicroMajorCertificateDto,
  MicroMajorCourseDto,
  MicroMajorEnrollmentDto,
  MyMicroMajorDto,
} from '../../proxy/micro-majors/dtos/models';
import { MicroMajorEnrollmentStatus } from '../../proxy/micro-majors/enums/micro-major-enrollment-status.enum';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { MicroMajorStageTrackComponent } from './micro-major-stage-track.component';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  MICRO_MAJOR_ENROLLMENT_LABELS,
  microMajorStateFlags,
  microMajorStatusLabel,
} from './micro-major-labels';

/** 状态页签（写入 URL 的 tab） */
type MyMicroTab = 'all' | 'pending' | 'learning' | 'completed' | 'certified';

/** 页签定义 */
interface TabDef {
  key: MyMicroTab;
  label: string;
}

/** 顶部统计卡 */
interface CenterStat {
  label: string;
  value: number;
  hint: string;
}

/**
 * 学生端 · 我的微专业与证书（原型 #my-micro）。
 *
 * 核心规则：**报名 ≠ 结业 ≠ 发证** 是三个互不等价的状态，页面必须分开呈现：
 *   报名（待审核 / 已通过待学习 / 学习中）→ 结业（教师确认）→ 发证（教师发放证书）。
 *
 * 数据来源（全部真实接口）：
 *  · `GET /api/app/micro-major/my-micro-majors`  微专业 + 培养课程 + 报名信息
 *  · `GET /api/app/micro-major/my-enrollments`    报名状态的权威来源
 *  · `GET /api/app/micro-major/my-certificates`   证书编号与发证信息
 */
@Component({
  selector: 'app-student-my-micro-majors',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    MicroMajorStageTrackComponent,
  ],
  templateUrl: './student-my-micro-majors.component.html',
  styleUrls: ['./student-my-micro-majors.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyMicroMajorsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  /** 统一使用生成的代理服务（proxy/micro-majors） */
  private readonly microMajorService = inject(MicroMajorService);
  /** 已选课程（判断「继续学习」能否直达课程学习页） */
  private readonly learningService = inject(LearningService);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);

  /** 我的微专业（主列表） */
  readonly items = signal<MyMicroMajorDto[]>([]);
  /** 报名状态（权威来源） */
  readonly enrollments = signal<MicroMajorEnrollmentDto[]>([]);
  /** 我的证书（证书编号来源） */
  readonly certificates = signal<MicroMajorCertificateDto[]>([]);
  /** 我已选课的课程 id 集合 —— 决定「继续学习」能否直达课程学习页 */
  readonly myCourseIds = signal<Set<string>>(new Set());
  /** 各微专业的课程清单（按微专业 id 索引，用于挑选可继续学习的课程） */
  readonly courseMap = signal<Record<string, MicroMajorCourseDto[]>>({});

  /** 当前页签 */
  readonly tab = signal<MyMicroTab>('all');

  readonly tabs: TabDef[] = [
    { key: 'all', label: '全部' },
    { key: 'pending', label: '待审核' },
    { key: 'learning', label: '学习中' },
    { key: 'completed', label: '已结业' },
    { key: 'certified', label: '已发证' },
  ];

  ngOnInit(): void {
    this.route.queryParams.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const raw = (params['tab'] as string) ?? 'all';
      this.tab.set(this.tabs.some(t => t.key === raw) ? (raw as MyMicroTab) : 'all');
      this.load();
    });
  }

  /** 三个接口并行拉取：报名状态与证书编号用于补全列表 */
  private load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);

    let pending = 3;
    const done = () => {
      pending -= 1;
      if (pending > 0) return;
      this.loading.set(false);
    };

    this.microMajorService.getMyMicroMajors().subscribe({
      next: list => {
        this.items.set(list || []);
        this.loadLearningCourses(list || []);
        done();
      },
      error: () => {
        this.loadFailed.set(true);
        done();
      },
    });

    this.microMajorService.getMyEnrollments().subscribe({
      next: list => {
        this.enrollments.set(list || []);
        done();
      },
      error: () => {
        this.enrollments.set([]);
        done();
      },
    });

    this.microMajorService.getMyCertificates().subscribe({
      next: list => {
        this.certificates.set(list || []);
        done();
      },
      error: () => {
        this.certificates.set([]);
        done();
      },
    });
  }

  /**
   * 加载「可继续学习」的课程信息。
   *
   * 两个来源：
   *  · `GET /api/app/learning/my-courses` → 我已选课的课程 id（判断能否直接进学习页）
   *  · 各微专业的 detail → 课程清单（按微专业 id 索引，用于挑选要跳的那门课）
   *
   * 已结业 / 已发证的微专业同样保留这个入口：证书发放后课程仍在已选列表里，
   * 允许学员回看学习内容，而不是只能看证书。
   */
  private loadLearningCourses(items: MyMicroMajorDto[]): void {
    // 已选课程 id
    this.learningService.getMyCourses().subscribe({
      next: list => {
        const ids = new Set<string>();
        for (const c of list || []) {
          if (c.courseId) ids.add(c.courseId);
        }
        this.myCourseIds.set(ids);
      },
      error: () => this.myCourseIds.set(new Set()),
    });

    // 各微专业的课程清单
    const targets = items.filter(m => !!m.id);
    if (!targets.length) {
      this.courseMap.set({});
      return;
    }
    forkJoin(
      targets.map(m =>
        this.microMajorService.getDetail(m.id!).pipe(catchError(() => of(null)))
      )
    ).subscribe(results => {
      const map: Record<string, MicroMajorCourseDto[]> = {};
      results.forEach((detail, i) => {
        const id = targets[i].id!;
        if (detail?.courses?.length) map[id] = detail.courses;
      });
      this.courseMap.set(map);
    });
  }

  /**
   * 该微专业下可继续学习的课程（我已选课且属于该微专业的第一门）。
   * 没有可学课程时返回 null（页面据此隐藏入口）。
   */
  continueCourse(item: MyMicroMajorDto): MicroMajorCourseDto | null {
    if (!item.id) return null;
    const courses = this.courseMap()[item.id] || [];
    const mine = this.myCourseIds();
    return courses.find(c => !!c.courseId && mine.has(c.courseId)) || null;
  }

  /** 进入课程学习页（从微专业卡片直达） */
  openLearning(item: MyMicroMajorDto, course: MicroMajorCourseDto, event: Event): void {
    event.stopPropagation();
    if (!course.courseId) return;
    this.router.navigate(['/student/courses', course.courseId, 'learn'], {
      queryParams: { fromMicroMajor: item.id, from: 'my-micro-majors' },
    });
  }

  /** 重新加载（错误态重试） */
  reload(): void {
    this.load();
  }

  /** 切换页签 → 写回 URL */
  setTab(key: MyMicroTab): void {
    this.tab.set(key);
    this.router
      .navigate([], {
        relativeTo: this.route,
        queryParams: { tab: key === 'all' ? null : key },
        queryParamsHandling: 'merge',
        replaceUrl: true,
      })
      .catch(() => undefined);
  }

  /** 报名记录（按 enrollmentId 优先，其次按微专业 id） */
  enrollmentOf(item: MyMicroMajorDto): MicroMajorEnrollmentDto | null {
    const byId = item.enrollmentId
      ? this.enrollments().find(e => e.id === item.enrollmentId)
      : undefined;
    if (byId) return byId;
    return this.enrollments().find(e => e.microMajorId === item.id) || null;
  }

  /** 报名状态值 */
  statusOf(item: MyMicroMajorDto): MicroMajorEnrollmentStatus | null {
    const enrollment = this.enrollmentOf(item);
    if (enrollment?.status !== undefined && enrollment.status !== null) return enrollment.status;
    return item.enrollmentStatus ?? null;
  }

  /** 精确状态文案 */
  statusLabel(item: MyMicroMajorDto): string {
    return microMajorStatusLabel(this.statusOf(item));
  }

  /** 状态色调 */
  statusTone(item: MyMicroMajorDto): string {
    const status = this.statusOf(item);
    if (status === null) return 'default';
    return MICRO_MAJOR_ENROLLMENT_LABELS[status]?.tone ?? 'default';
  }

  /** 三态标记：报名 / 结业 / 发证 */
  flags(item: MyMicroMajorDto): { applied: boolean; completed: boolean; certified: boolean } {
    const f = microMajorStateFlags(this.statusOf(item));
    return { applied: f.applied, completed: f.completed, certified: f.certified };
  }

  /** 证书（按 enrollmentId 关联） */
  certificateOf(item: MyMicroMajorDto): MicroMajorCertificateDto | null {
    const enrollmentId = item.enrollmentId || this.enrollmentOf(item)?.id;
    if (!enrollmentId) return null;
    return this.certificates().find(c => c.enrollmentId === enrollmentId) || null;
  }

  /** 证书编号 */
  certificateNo(item: MyMicroMajorDto): string {
    return this.certificateOf(item)?.certificateNo || '—';
  }

  /** 学习进度（后端计算的平均课程进度） */
  progress(item: MyMicroMajorDto): number {
    const enrollment = this.enrollmentOf(item);
    const value = enrollment?.progress ?? item.progress ?? 0;
    return Math.round(value);
  }

  /** 按页签过滤后的列表 */
  readonly visibleItems = computed(() => {
    const tab = this.tab();
    const list = this.items();
    if (tab === 'all') return list;
    return list.filter(item => {
      const status = this.statusOf(item);
      switch (tab) {
        case 'pending':
          return status === MicroMajorEnrollmentStatus.Pending;
        case 'learning':
          return (
            status === MicroMajorEnrollmentStatus.Enrolled ||
            status === MicroMajorEnrollmentStatus.InProgress
          );
        case 'completed':
          return status === MicroMajorEnrollmentStatus.Completed;
        case 'certified':
          return status === MicroMajorEnrollmentStatus.Certified;
        default:
          return true;
      }
    });
  });

  /** 四项统计：报名 / 学习中 / 已结业 / 已发证（三个状态互不合并） */
  readonly stats = computed<CenterStat[]>(() => {
    const list = this.items();
    const count = (predicate: (item: MyMicroMajorDto) => boolean) =>
      list.filter(item => predicate(item)).length;
    return [
      { label: '我的微专业', value: list.length, hint: '含待审核与已取消的报名记录' },
      {
        label: '待审核',
        value: count(i => this.statusOf(i) === MicroMajorEnrollmentStatus.Pending),
        hint: '等待教师审核',
      },
      {
        label: '学习中',
        value: count(
          i =>
            this.statusOf(i) === MicroMajorEnrollmentStatus.Enrolled ||
            this.statusOf(i) === MicroMajorEnrollmentStatus.InProgress
        ),
        hint: '审核通过后的学习阶段',
      },
      {
        label: '已结业',
        value: count(i => this.statusOf(i) === MicroMajorEnrollmentStatus.Completed),
        hint: '教师已确认结业',
      },
    ];
  });

  /** 是否有任何记录（用于空状态文案区分「没报名」与「该页签无记录」） */
  readonly hasAny = computed(() => this.items().length > 0);

  /** 进入微专业详情 */
  openDetail(item: MyMicroMajorDto): void {
    if (!item.id) return;
    this.router.navigate(['/student/micro-majors', item.id], {
      queryParams: { from: 'my-micro-majors' },
    });
  }

  /** 前往微专业中心 */
  goToCenter(): void {
    this.router.navigate(['/student/micro-majors']);
  }
}
