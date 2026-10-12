import { ChangeDetectionStrategy, Component, ElementRef, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MicroMajorService } from '../../proxy/micro-majors/micro-major.service';
import { CourseService } from '../../proxy/courses/course.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { AuthFetchService } from '../../shared/utils/auth-fetch.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { StudentCourseListItemDto } from '../../proxy/learning/dtos/models';
import type { MicroMajorDetailDto, MicroMajorResourceDto } from '../../proxy/micro-majors/dtos/models';
import { ClientCacheService } from '../../shared/cache/client-cache.service';
import { hashGradient } from '../../shared/utils/color.util';

@Component({
  selector: 'app-student-micro-major-detail',
  standalone: true,
  imports: [
    CommonModule, RouterModule,
    NzIconModule, NzSpinModule,
  ],
  templateUrl: './student-micro-major-detail.component.html',
  styleUrls: ['./student-micro-major-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMicroMajorDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly microMajorService = inject(MicroMajorService);
  private readonly courseService = inject(CourseService);
  private readonly learningService = inject(LearningService);
  private readonly message = inject(NzMessageService);
  private readonly cache = inject(ClientCacheService);
  private readonly authFetch = inject(AuthFetchService);
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly detail = signal<MicroMajorDetailDto | null>(null);
  readonly resources = signal<MicroMajorResourceDto[]>([]);
  readonly loading = signal(true);
  readonly coursesLoading = signal(false);

  /** 微专业资料下载链接（浏览器原生下载，附 access_token 认证） */
  downloadUrl(resourceId: string): string {
    return this.authFetch.withAccessToken(`/api/resource-file/${resourceId}/download`);
  }

  /** 微专业中各课程的完整数据（与课程列表同源） */
  readonly courseDetails = signal<CourseDto[]>([]);
  /** 当前用户的选课列表（用于判断已选课 / 学习进度） */
  readonly myCourses = signal<StudentCourseListItemDto[]>([]);
  /** 选课进行中状态 */
  readonly enrolling = signal<string | null>(null);

  /** 课程 Id -> 习题进度（已完成 / 全部，与课程卡片同口径：后端 GetMyCourses 唯一来源） */
  private readonly myCourseProgress = computed(() => {
    const map = new Map<string, { completed: number; total: number }>();
    for (const c of this.myCourses()) {
      if (c.courseId) map.set(c.courseId, {
        completed: c.completedExerciseCount ?? 0,
        total: c.totalExerciseCount ?? 0,
      });
    }
    return map;
  });

  /** 微专业核心课程 Id 集合 */
  private readonly coreCourseIds = computed(() =>
    new Set(
      (this.detail()?.courses || [])
        .filter(c => c.isCore && c.courseId)
        .map(c => c.courseId!)
    )
  );

  /** 你的学习路径：按课程顺序均分为 3 个阶段（对齐原型 PHASE 01-03） */
  readonly phases = computed(() => {
    const courses = this.courseDetails();
    if (courses.length === 0) return [];
    const names = ['专业基础', '方法进阶', '实践应用'];
    const count = courses.length;
    const phaseCount = count >= 3 ? 3 : count;
    const result: { index: number; name: string; courses: CourseDto[] }[] = [];
    for (let i = 0; i < phaseCount; i++) {
      const start = Math.floor((i * count) / phaseCount);
      const end = Math.floor(((i + 1) * count) / phaseCount);
      result.push({
        index: i + 1,
        name: names[i] || `阶段 ${i + 1}`,
        courses: courses.slice(start, end),
      });
    }
    return result;
  });

  /** 从首页微专业模块进入时，返回首页对应模块位置（原路返回） */
  readonly fromHome = signal(false);
  /** 从“我的微专业”进入时，返回我的微专业 */
  readonly fromMyMicroMajors = signal(false);
  /** 首页来源区块，返回首页时原路定位（默认 micro-majors，兼容旧 section=microMajors） */
  readonly homeSection = signal<string | null>(null);

  /** 当前微专业 id，随课程链接透传给课程详情页，实现“返回微专业” */
  readonly microMajorId = signal<string | null>(null);
  /** 原始 from 参数（home / my-micro-majors），随课程链接透传，返回时还原本页 URL */
  readonly fromSource = signal<string | null>(null);

  /** 面包屑 / 返回按钮的目标 */
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
    return '返回微专业列表';
  });

  ngOnInit(): void {
    const from = this.route.snapshot.queryParamMap.get('from');
    this.fromHome.set(from === 'home');
    this.fromMyMicroMajors.set(from === 'my-micro-majors');
    const rawSection = this.route.snapshot.queryParamMap.get('section');
    // 兼容旧链接 section=microMajors；browse 来源的微专业返回底部“全部资源”
    this.homeSection.set(rawSection === 'microMajors' ? 'micro-majors' : (rawSection || 'micro-majors'));
    this.fromSource.set(from || null);

    const id = this.route.snapshot.paramMap.get('id');
    this.microMajorId.set(id || null);
    if (!id) return;
    this.loadDetail(id);
    this.loadResources(id);
    this.loadMyCourses();
  }

  loadDetail(id: string): void {
    this.loading.set(true);
    this.cache.load<MicroMajorDetailDto>('student.micro-majors', `detail:${id}`, () => this.microMajorService.getDetail(id)).subscribe({
      next: result => {
        this.detail.set(result);
        const courseIds = (result?.courses || [])
          .map(c => c.courseId)
          .filter((id): id is string => !!id);
        this.loadCourseDetails(courseIds);
        this.loading.set(false);
      },
      error: (err: any) => {
        this.loading.set(false);
        const msg = err?.error?.error?.message || err?.error?.message;
        this.message.error(msg || '加载微专业详情失败');
      },
    });
  }

  /** 拉取微专业中每门课程的完整 CourseDto（与课程列表同源，1 次并行请求） */
  loadCourseDetails(courseIds: string[]): void {
    if (courseIds.length === 0) {
      this.courseDetails.set([]);
      return;
    }
    this.coursesLoading.set(true);
    // 单个课程失败不应阻断其他课程的加载
    forkJoin(
      courseIds.map(id =>
        this.cache.load<CourseDto>('student.micro-majors', `course:${id}`, () => this.courseService.get(id))
          .pipe(catchError(() => of(null as CourseDto | null)))
      )
    ).subscribe({
      next: results => {
        const courses = (results || []).filter((c): c is CourseDto => !!c);
        // 按微专业中的原始顺序排序
        const order = new Map(courseIds.map((id, idx) => [id, idx]));
        courses.sort((a, b) => (order.get(a.id!) ?? 0) - (order.get(b.id!) ?? 0));
        this.courseDetails.set(courses);
        this.coursesLoading.set(false);
      },
      // forkJoin 上不会走这里，因为每个子流都 catchError 了
      complete: () => this.coursesLoading.set(false),
    });
  }

  /** 拉取当前用户的选课列表（与课程列表同一逻辑） */
  loadMyCourses(): void {
    this.cache.load<StudentCourseListItemDto[]>('student.my-courses', 'list', () => this.learningService.getMyCourses()).subscribe({
      next: list => this.myCourses.set(list || []),
      error: () => this.myCourses.set([]),
    });
  }

  loadResources(id: string): void {
    this.cache.load<MicroMajorResourceDto[]>('student.micro-majors', `resources:${id}`, () => this.microMajorService.getResources(id)).pipe(
      catchError(() => of([] as MicroMajorResourceDto[]))
    ).subscribe({
      next: result => this.resources.set(result || []),
    });
  }

  /** 判断该课程是否被标记为微专业的“核心课” */
  isCoreCourse(courseId: string): boolean {
    return this.coreCourseIds().has(courseId);
  }

  /** 是否已选该课 */
  isEnrolled(courseId: string): boolean {
    return this.myCourseProgress().has(courseId);
  }

  /** 与课程卡片统一口径：已完成习题 / 全部习题（后端计算，前端只展示） */
  getExerciseProgress(courseId: string): { completed: number; total: number } {
    const found = this.myCourseProgress().get(courseId);
    return {
      completed: found?.completed ?? 0,
      total: found?.total ?? 0,
    };
  }

  /** 卡片状态：无习题 / 未开始 / 学习中 / 已完成（与课程卡片一致） */
  getProgressState(courseId: string): 'empty' | 'not-started' | 'in-progress' | 'completed' {
    const { completed, total } = this.getExerciseProgress(courseId);
    if (total <= 0) return 'empty';
    if (completed <= 0) return 'not-started';
    if (completed >= total) return 'completed';
    return 'in-progress';
  }

  getProgressStateLabel(courseId: string): string {
    switch (this.getProgressState(courseId)) {
      case 'empty': return '暂无习题';
      case 'not-started': return '未开始';
      case 'in-progress': return '学习中';
      case 'completed': return '已完成';
    }
  }

  /** 是否有封面图 */
  hasCover(course: CourseDto): boolean {
    return !!course.coverImageUrl && course.coverImageUrl.trim().length > 0;
  }

  /** 课程封面渐变（与课程列表同调色板） */
  courseGradient(course: CourseDto): string {
    const palettes = [
      '#0f766e', '#0d5e56', '#14b8a6', '#16a34a', '#4aa79f', '#059669', '#0d9488', '#d97706',
    ];
    const key = (course.title || course.id || 'x') + (course.majorName || '');
    return hashGradient(key, palettes);
  }

  /** 微专业是否有封面图 */
  hasMajorCover(d: MicroMajorDetailDto): boolean {
    return !!d.coverImageUrl && d.coverImageUrl.trim().length > 0;
  }

  /** 微专业封面渐变（无封面图时的色块） */
  majorGradient(d: MicroMajorDetailDto): string {
    const palettes = ['#087f7a', '#3c5e7c', '#806440', '#0d9488', '#086964'];
    return hashGradient((d.title || d.id || 'x'), palettes);
  }

  /** 点击「查看课程」：滚动到课程区 */
  scrollToCourses(): void {
    const el = this.host.nativeElement.querySelector('#mm-courses') as HTMLElement | null;
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  difficultyLabel(d: number | null | undefined): string {
    const list = ['入门', '初级', '中级', '高级', '专家'];
    return list[(d || 1) - 1] || '未设置';
  }

  difficultyColor(d: number | null | undefined): string {
    const list = ['#34d399', '#22c55e', '#0f766e', '#0d5e56', '#14b8a6'];
    return list[(d || 1) - 1] || '#94a3b8';
  }

  /** 进入课程详情页 */
  openCourse(courseId: string): void {
    const queryParams: Record<string, string> = {};
    if (this.microMajorId()) queryParams['fromMicroMajor'] = this.microMajorId()!;
    if (this.fromSource()) queryParams['from'] = this.fromSource()!;
    // 透传首页来源区块，课程页“返回首页”时仍能定位到原来位置
    if (this.fromHome() && this.homeSection()) queryParams['section'] = this.homeSection()!;
    this.router.navigate(['/student/courses', courseId], { queryParams });
  }

  /** 继续学习（已选课） */
  startLearning(course: CourseDto, event: Event): void {
    event.stopPropagation();
    if (course.id) this.openCourse(course.id);
  }

  /** 选课（未选） */
  enroll(course: CourseDto, event: Event): void {
    event.stopPropagation();
    if (!course.id || this.enrolling()) return;
    this.enrolling.set(course.id);
    this.courseService.enroll(course.id).subscribe({
      next: () => {
        this.enrolling.set(null);
        this.message.success('选课成功');
        this.cache.clear('student.my-courses');
        this.loadMyCourses();
      },
      error: (err: any) => {
        this.enrolling.set(null);
        // 跨租户课程后端会返回“不能跨租户选课”，直接展示服务端信息
        const msg = err?.error?.error?.message || err?.error?.message;
        this.message.error(msg || '选课失败');
      },
    });
  }

  /** 报名微专业 */
  enrollMicroMajor(): void {
    const id = this.detail()?.id;
    if (!id) return;
    this.microMajorService.enroll(id).subscribe({
      next: () => {
        this.message.success('报名成功');
        // 报名状态变化：清空微专业缓存后重新拉取详情
        this.cache.clear('student.micro-majors');
        this.loadDetail(id);
      },
      error: (err: any) => {
        // 跨租户微专业后端会返回“不能跨院校报名…仅支持浏览”，直接展示服务端信息
        const msg = err?.error?.error?.message || err?.error?.message;
        this.message.error(msg || '报名失败');
      },
    });
  }
}
