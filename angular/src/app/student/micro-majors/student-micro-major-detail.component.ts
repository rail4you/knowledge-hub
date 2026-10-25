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
import { NzMessageService } from 'ng-zorro-antd/message';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MicroMajorService } from '../../proxy/micro-majors/micro-major.service';
import { CourseService } from '../../proxy/courses/course.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { AuthFetchService } from '../../shared/utils/auth-fetch.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { StudentCourseListItemDto } from '../../proxy/learning/dtos/models';
import type {
  MicroMajorCourseDto,
  MicroMajorDetailDto,
  MicroMajorEnrollmentDto,
  MicroMajorResourceDto,
} from '../../proxy/micro-majors/dtos/models';
import { MicroMajorEnrollmentStatus } from '../../proxy/micro-majors/enums/micro-major-enrollment-status.enum';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { MicroMajorStageTrackComponent } from './micro-major-stage-track.component';
import {
  MICRO_MAJOR_ENROLLMENT_LABELS,
  microMajorStateFlags,
  microMajorStatusLabel,
} from './micro-major-labels';

/**
 * 学生端 · 微专业详情与报名（原型 #micro-detail）。
 *
 * 结构：返回 → 页头 → 4 步状态条 → 左栏（培养方案 / 培养课程 / 考核规则 / 配套资料）
 *      + 右栏（报名信息 + 报名表单）。
 *
 * 数据来源（全部真实接口）：
 *  · `GET /api/app/micro-major/{id}/detail`  培养方案 + 课程清单
 *  · `GET /api/app/micro-major/resources/{id}` 配套资料
 *  · `GET /api/app/micro-major/my-enrollments`  报名 / 结业 / 发证三态
 *  · `GET /api/app/course/{courseId}`  课程学分与学时
 *  · `GET /api/app/learning/my-courses`  课程学习进度
 *
 * 说明：后端 `EnrollAsync` 没有「已阅读培养方案」这类确认字段，
 * 因此勾选框只是前端的一次性确认（未勾选不允许提交），不代表后端已记录。
 */
@Component({
  selector: 'app-student-micro-major-detail',
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
  templateUrl: './student-micro-major-detail.component.html',
  styleUrls: ['./student-micro-major-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMicroMajorDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly microMajorService = inject(MicroMajorService);
  private readonly courseService = inject(CourseService);
  private readonly learningService = inject(LearningService);
  private readonly message = inject(NzMessageService);
  private readonly authFetch = inject(AuthFetchService);

  readonly detail = signal<MicroMajorDetailDto | null>(null);
  readonly resources = signal<MicroMajorResourceDto[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly coursesLoading = signal(false);

  /** 我的报名列表（用于状态条与报名状态） */
  readonly myEnrollments = signal<MicroMajorEnrollmentDto[]>([]);

  /** 微专业中各课程的完整数据（学分 / 学时 / 简介） */
  readonly courseDetails = signal<CourseDto[]>([]);
  /** 当前用户的选课列表（进度与是否已选课） */
  readonly myCourses = signal<StudentCourseListItemDto[]>([]);
  /** 正在选课的课程 id */
  readonly enrolling = signal<string | null>(null);
  /** 正在报名微专业 */
  readonly enrollingMajor = signal(false);

  /** 已阅读培养方案的前端确认（后端无对应字段） */
  readonly acknowledged = signal(false);

  /** 当前微专业 id */
  readonly microMajorId = signal<string | null>(null);

  /* ── 页面来源（用于「返回」按钮原路返回） ─────────────────── */
  readonly fromHome = signal(false);
  readonly fromMyMicroMajors = signal(false);
  readonly homeSection = signal<string | null>(null);
  readonly fromSource = signal<string | null>(null);

  readonly backLink = computed(() => {
    if (this.fromHome()) return '/';
    if (this.fromMyMicroMajors()) return '/student/my-micro-majors';
    return '/student/micro-majors';
  });

  readonly backQueryParams = computed(() => {
    if (this.fromHome()) return { section: this.homeSection() || 'micro-majors' };
    return {};
  });

  readonly backLabel = computed(() => {
    if (this.fromHome()) return '返回首页';
    if (this.fromMyMicroMajors()) return '返回我的微专业';
    return '返回微专业中心';
  });

  /* ── 报名状态（报名 / 结业 / 发证 三态分开） ───────────────── */

  /** 当前微专业的报名记录 */
  readonly myEnrollment = computed<MicroMajorEnrollmentDto | null>(() => {
    const id = this.microMajorId();
    if (!id) return null;
    const hit = this.myEnrollments().find(e => e.microMajorId === id);
    if (!hit) return null;
    if (hit.status === MicroMajorEnrollmentStatus.Cancelled) return null;
    return hit;
  });

  /** 报名状态值 */
  readonly enrollmentStatus = computed<MicroMajorEnrollmentStatus | null>(
    () => this.myEnrollment()?.status ?? null,
  );

  /** 报名状态文案 */
  readonly enrollmentLabel = computed(() => microMajorStatusLabel(this.enrollmentStatus()));

  /** 报名状态色调 */
  readonly enrollmentTone = computed(() => {
    const status = this.enrollmentStatus();
    if (status === null) return 'default';
    return MICRO_MAJOR_ENROLLMENT_LABELS[status]?.tone ?? 'default';
  });

  /** 三态标记：报名 / 结业 / 发证 */
  readonly stateFlags = computed(() => microMajorStateFlags(this.enrollmentStatus()));

  /* ── 培养方案信息 ────────────────────────────────────────── */

  /** 培养课程清单（来自 detail 接口，按 sortOrder 排序） */
  readonly courses = computed<MicroMajorCourseDto[]>(() =>
    (this.detail()?.courses || []).slice().sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
  );

  /** 培养学时合计（真实课程学时求和；课程未填学时时为 0，不展示） */
  readonly totalHours = computed(() => {
    const list = this.courseDetails();
    const sum = list.reduce((acc, c) => acc + (c.semesterHours ?? 0), 0);
    return sum > 0 ? sum : 0;
  });

  /** 学分合计 */
  readonly totalCredits = computed(() => {
    const list = this.courseDetails();
    const sum = list.reduce((acc, c) => acc + (c.credits ?? 0), 0);
    return sum > 0 ? sum : 0;
  });

  /** 课程数量（后端 courseCount 优先） */
  readonly courseCount = computed(() => this.detail()?.courseCount || this.courses().length || 0);

  /** 课程 id -> 习题进度 */
  private readonly myCourseProgress = computed(() => {
    const map = new Map<string, { completed: number; total: number }>();
    for (const c of this.myCourses()) {
      if (c.courseId) {
        map.set(c.courseId, {
          completed: c.completedExerciseCount ?? 0,
          total: c.totalExerciseCount ?? 0,
        });
      }
    }
    return map;
  });

  /** 核心课程 id 集合 */
  private readonly coreCourseIds = computed(
    () =>
      new Set(
        this.courses()
          .filter(c => c.isCore && c.courseId)
          .map(c => c.courseId!)
      )
  );

  ngOnInit(): void {
    const from = this.route.snapshot.queryParamMap.get('from');
    this.fromHome.set(from === 'home');
    this.fromMyMicroMajors.set(from === 'my-micro-majors');
    const rawSection = this.route.snapshot.queryParamMap.get('section');
    this.homeSection.set(rawSection || 'micro-majors');
    this.fromSource.set(from || null);

    const id = this.route.snapshot.paramMap.get('id');
    this.microMajorId.set(id || null);
    if (!id) {
      this.loading.set(false);
      this.loadFailed.set(true);
      return;
    }

    this.loadDetail(id);
    this.loadResources(id);
    this.loadMyEnrollments();
    this.loadMyCourses();
  }

  /** 拉取培养方案与课程清单 */
  loadDetail(id: string): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.microMajorService.getDetail(id).subscribe({
      next: result => {
        this.detail.set(result);
        const courseIds = (result?.courses || [])
          .map(c => c.courseId)
          .filter((cid): cid is string => !!cid);
        this.loadCourseDetails(courseIds);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  /** 拉取微专业中每门课程的完整 CourseDto（学分 / 学时） */
  private loadCourseDetails(courseIds: string[]): void {
    if (courseIds.length === 0) {
      this.courseDetails.set([]);
      return;
    }
    this.coursesLoading.set(true);
    forkJoin(
      courseIds.map(cid =>
        this.courseService.get(cid).pipe(catchError(() => of(null as CourseDto | null)))
      )
    ).subscribe(results => {
      const list = (results || []).filter((c): c is CourseDto => !!c);
      const order = new Map(courseIds.map((cid, idx) => [cid, idx]));
      list.sort((a, b) => (order.get(a.id!) ?? 0) - (order.get(b.id!) ?? 0));
      this.courseDetails.set(list);
      this.coursesLoading.set(false);
    });
  }

  /** 我的报名（状态条 / 报名状态） */
  private loadMyEnrollments(): void {
    this.microMajorService.getMyEnrollments().subscribe({
      next: list => this.myEnrollments.set(list || []),
      error: () => this.myEnrollments.set([]),
    });
  }

  /** 我的选课列表（课程进度） */
  private loadMyCourses(): void {
    this.learningService.getMyCourses().subscribe({
      next: list => this.myCourses.set(list || []),
      error: () => this.myCourses.set([]),
    });
  }

  /** 配套资料 */
  private loadResources(id: string): void {
    this.microMajorService.getResources(id).subscribe({
      next: list => this.resources.set(list || []),
      error: () => this.resources.set([]),
    });
  }

  /** 重新加载（错误态重试） */
  reload(): void {
    const id = this.microMajorId();
    if (!id) return;
    this.loadDetail(id);
    this.loadResources(id);
    this.loadMyEnrollments();
  }

  /** 返回微专业中心 */
  goToList(): void {
    this.router.navigate(['/student/micro-majors']);
  }

  /** 配套资料下载链接（浏览器原生下载，附 access_token 认证） */
  downloadUrl(resourceId: string): string {
    return this.authFetch.withAccessToken(`/api/resource-file/${resourceId}/download`);
  }

  /** 是否核心课 */
  isCoreCourse(courseId: string): boolean {
    return this.coreCourseIds().has(courseId);
  }

  /** 该课程的完整数据 */
  courseDetail(course: MicroMajorCourseDto): CourseDto | undefined {
    return this.courseDetails().find(c => c.id === course.courseId);
  }

  /** 是否已选该课 */
  isEnrolled(courseId: string): boolean {
    return this.myCourseProgress().has(courseId);
  }

  /** 课程进度（已完成习题 / 全部习题） */
  getExerciseProgress(courseId: string): { completed: number; total: number } {
    const found = this.myCourseProgress().get(courseId);
    return { completed: found?.completed ?? 0, total: found?.total ?? 0 };
  }

  /** 课程状态：未开始 / 学习中 / 已完成 */
  getProgressStateLabel(courseId: string): string {
    const { completed, total } = this.getExerciseProgress(courseId);
    if (total <= 0) return '暂无习题';
    if (completed <= 0) return '未开始';
    if (completed >= total) return '已完成';
    return '学习中';
  }

  /** 课程进度百分比 */
  progressPercent(courseId: string): number {
    const { completed, total } = this.getExerciseProgress(courseId);
    if (total <= 0) return 0;
    return Math.round((completed / total) * 100);
  }

  /** 课程详情 / 继续学习 */
  openCourse(courseId: string): void {
    const queryParams: Record<string, string> = {};
    if (this.microMajorId()) queryParams['fromMicroMajor'] = this.microMajorId()!;
    if (this.fromSource()) queryParams['from'] = this.fromSource()!;
    if (this.fromHome() && this.homeSection()) queryParams['section'] = this.homeSection()!;
    this.router.navigate(['/student/courses', courseId], { queryParams });
  }

  /** 选课（进入微专业后课程会自动加入，本页也可单独选课） */
  enrollCourse(course: MicroMajorCourseDto, event: Event): void {
    event.stopPropagation();
    if (!course.courseId || this.enrolling()) return;
    this.enrolling.set(course.courseId);
    this.courseService.enroll(course.courseId).subscribe({
      next: () => {
        this.enrolling.set(null);
        this.message.success('选课成功');
        this.loadMyCourses();
      },
      error: (err: any) => {
        this.enrolling.set(null);
        const msg = err?.error?.error?.message || err?.error?.message;
        this.message.error(msg || '选课失败');
      },
    });
  }

  /** 勾选「已阅读培养方案」 */
  setAcknowledged(value: boolean): void {
    this.acknowledged.set(value);
  }

  /** 是否允许提交报名：未报名 + 已勾选确认 */
  readonly canEnroll = computed(
    () => !this.myEnrollment() && this.acknowledged() && !this.enrollingMajor()
  );

  /** 提交报名（后端会把状态置为「待审核」） */
  enrollMicroMajor(): void {
    const id = this.detail()?.id;
    if (!id || !this.canEnroll()) return;
    this.enrollingMajor.set(true);
    this.microMajorService.enroll(id).subscribe({
      next: () => {
        this.enrollingMajor.set(false);
        this.acknowledged.set(false);
        this.message.success('报名已提交，等待教师审核');
        this.loadMyEnrollments();
        this.loadDetail(id);
      },
      error: (err: any) => {
        this.enrollingMajor.set(false);
        const msg = err?.error?.error?.message || err?.error?.message;
        this.message.error(msg || '报名失败');
      },
    });
  }
}
