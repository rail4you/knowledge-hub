import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzMessageService } from 'ng-zorro-antd/message';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { MicroMajorService } from '../../proxy/micro-majors/micro-major.service';
import { CourseService } from '../../proxy/courses/course.service';
import { LearningService } from '../../proxy/learning/learning.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { StudentCourseListItemDto } from '../../proxy/learning/dtos/models';
import type { MicroMajorDetailDto, MicroMajorResourceDto } from '../../proxy/micro-majors/dtos/models';

@Component({
  selector: 'app-student-micro-major-detail',
  standalone: true,
  imports: [
    CommonModule, DecimalPipe, RouterModule,
    NzIconModule, NzSpinModule, NzProgressModule,
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

  readonly detail = signal<MicroMajorDetailDto | null>(null);
  readonly resources = signal<MicroMajorResourceDto[]>([]);
  readonly loading = signal(true);
  readonly coursesLoading = signal(false);
  readonly activeTab = signal<'courses' | 'resources'>('courses');

  /** 微专业中各课程的完整数据（与课程列表同源） */
  readonly courseDetails = signal<CourseDto[]>([]);
  /** 当前用户的选课列表（用于判断已选课 / 学习进度） */
  readonly myCourses = signal<StudentCourseListItemDto[]>([]);
  /** 选课进行中状态 */
  readonly enrolling = signal<string | null>(null);

  /** 从首页“全部资源/微专业”进入时，返回首页对应位置（原路返回） */
  readonly fromHome = signal(false);
  /** 从“我的微专业”进入时，返回我的微专业 */
  readonly fromMyMicroMajors = signal(false);

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
    if (this.fromHome()) return { tab: 'microMajors' };
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
    this.microMajorService.getDetail(id).subscribe({
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
        this.courseService.get(id).pipe(catchError(() => of(null as CourseDto | null)))
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
    this.learningService.getMyCourses().subscribe({
      next: list => this.myCourses.set(list || []),
      error: () => this.myCourses.set([]),
    });
  }

  loadResources(id: string): void {
    this.microMajorService.getResources(id).pipe(
      catchError(() => of([] as MicroMajorResourceDto[]))
    ).subscribe({
      next: result => this.resources.set(result || []),
    });
  }

  /** 判断该课程是否被标记为微专业的“核心课” */
  isCoreCourse(courseId: string): boolean {
    return (this.detail()?.courses || []).some(c => c.courseId === courseId && c.isCore);
  }

  /** 是否已选该课 */
  isEnrolled(courseId: string): boolean {
    return this.myCourses().some(c => c.courseId === courseId);
  }

  /** 获取该课的学习进度 */
  getMyProgress(courseId: string): number {
    return this.myCourses().find(c => c.courseId === courseId)?.progress || 0;
  }

  /** 是否有封面图 */
  hasCover(course: CourseDto): boolean {
    return !!course.coverImageUrl && course.coverImageUrl.trim().length > 0;
  }

  /** 课程封面渐变（与课程列表同调色板） */
  courseGradient(course: CourseDto): string {
    const palettes = [
      '#0f766e', '#0d5e56', '#14b8a6', '#16a34a', '#5b93db', '#059669', '#0d9488', '#d97706',
    ];
    const key = (course.title || course.id || 'x') + (course.majorName || '');
    let hash = 0;
    for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
    return palettes[Math.abs(hash) % palettes.length];
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
      next: () => { this.message.success('报名成功'); this.loadDetail(id); },
      error: (err: any) => {
        // 跨租户微专业后端会返回“不能跨院校报名…仅支持浏览”，直接展示服务端信息
        const msg = err?.error?.error?.message || err?.error?.message;
        this.message.error(msg || '报名失败');
      },
    });
  }

  /** 学习进度保留 2 位小数 */
  roundProgress(value: number | null | undefined): number {
    if (value == null || isNaN(value)) return 0;
    return Math.round(value * 100) / 100;
  }
}
