import { ChangeDetectionStrategy, Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { CourseService } from '../../proxy/courses/course.service';
import { CourseShareService } from '../../proxy/courses/course-share.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { MajorService } from '../../proxy/majors/major.service';
import { PortalService } from '../../proxy/portal/portal.service';
import type { CourseDto, SharedCourseDto } from '../../proxy/courses/dtos/models';
import type { MajorLookupDto } from '../../proxy/majors/dtos/models';
import type { StudentCourseListItemDto } from '../../proxy/learning/dtos/models';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import { VoiceContextService } from '../voice/voice-context.service';
import { COURSE_SCOPE_OPTIONS, type CourseScope, isCourseScope } from './student-courses-labels';

/**
 * 学生端 · 课程中心。
 *
 * 对齐原型 `#courses`：Hero + 筛选面板（关键词 / 专业方向 / 筛选范围）+ 课程卡片网格 + 服务端分页。
 *
 * 数据来源（全部真实接口）：
 *   · 本校已发布课程 GET /api/app/course/published（服务端分页）
 *   · 共享到本校课程 GET /api/app/course-share/shared-to-me（服务端分页）
 *   · 我的课程     GET /api/app/learning/my-courses（已选课徽章 + 学习进度，按 courseId 关联）
 *   · 专业字典     GET /api/app/major/lookup-list
 *   · 教师姓名补全 GET /api/app/portal/home-data/{tenantId}（只有该门户负载里出现过的课程才有教师名）
 *
 * 筛选条件与页码全部写进 URL query params（q / major / scope / page / size），刷新不丢。
 */
export interface StudentCourseCard {
  id: string;
  title: string;
  description: string | null;
  coverImageUrl: string | null;
  majorNames: string[];
  /** 学时（共享课数据源不提供，为 null 时不渲染该字段） */
  semesterHours: number | null;
  /** 学分 */
  credits: number | null;
  /** 学习人数 */
  studentCount: number | null;
  /** 章节数 */
  chapterCount: number | null;
  /** 主讲教师：列表接口不返回教师名，只能从门户负载补全；补不到就整块省略 */
  teacherName: string | null;
  /** 共享来源学校（仅共享课） */
  sourceTenantName: string | null;
  isShared: boolean;
  /** 是否已选课 */
  enrolled: boolean;
  /** 学习进度 % */
  progress: number;
}

@Component({
  selector: 'app-student-courses',
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
  ],
  templateUrl: './student-courses.component.html',
  styleUrls: ['./student-courses.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentCoursesComponent implements OnInit, OnDestroy {
  private readonly courseService = inject(CourseService);
  private readonly courseShareService = inject(CourseShareService);
  private readonly learningService = inject(LearningService);
  private readonly majorService = inject(MajorService);
  private readonly portalService = inject(PortalService);
  private readonly authService = inject(AuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly voiceContext = inject(VoiceContextService);

  /** 筛选范围选项（两个真实的服务端分页数据源） */
  readonly scopeOptions = COURSE_SCOPE_OPTIONS;

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(12);

  /** 与 URL 同步的筛选状态 */
  readonly keyword = signal('');
  readonly majorId = signal<string | null>(null);
  readonly scope = signal<CourseScope>('published');

  /** 专业筛选项 */
  readonly majors = signal<MajorLookupDto[]>([]);

  /** 我的课程（用于「已选课」徽章与学习进度，按 courseId 关联） */
  readonly myCourses = signal<StudentCourseListItemDto[]>([]);

  /** 教师姓名补全表：门户负载里出现过的课程才有教师名 */
  private readonly teacherNames = signal<Map<string, string>>(new Map());

  /** 列表接口返回的原始条目（按当前 scope 决定是 CourseDto 还是 SharedCourseDto） */
  private readonly rawItems = signal<CourseDto[]>([]);
  private readonly rawSharedItems = signal<SharedCourseDto[]>([]);

  /** 是否存在筛选条件（决定空态语义与「清除筛选」入口） */
  readonly hasFilters = computed(() => !!this.keyword().trim() || !!this.majorId());

  /** 已选课数量（Hero 右侧统计，全部来自真实接口） */
  readonly enrolledCount = computed(() => this.myCourses().length);

  /**
   * 当前页课程卡。
   *
   * 用 computed 而不是「加载时直接拼好」，是为了让后到的补全数据
   * （我的课程 → 已选课徽章/进度、门户负载 → 教师姓名）能自动反映到卡片上。
   */
  readonly visibleCards = computed<StudentCourseCard[]>(() =>
    this.scope() === 'shared'
      ? this.rawSharedItems().map(c => this.toSharedCard(c))
      : this.rawItems().map(c => this.toCard(c)),
  );

  ngOnInit(): void {
    this.loadMajors();
    this.loadMyCourses();
    this.loadTeacherNames();

    // 筛选 / 范围 / 页码都在 URL 上：订阅 queryParamMap，浏览器前进后退也能正确回显。
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        this.applyQuery(params);
        this.loadCourses();
      });
  }

  ngOnDestroy(): void {
    this.voiceContext.unregister('courses');
  }

  /** 把 URL 上的查询参数同步到信号 */
  private applyQuery(params: ParamMap): void {
    this.keyword.set((params.get('q') || '').trim());

    const major = params.get('major');
    this.majorId.set(major ? major : null);

    const scope = params.get('scope');
    this.scope.set(isCourseScope(scope) ? scope : 'published');

    const page = Number(params.get('page') || '1');
    this.pageIndex.set(Number.isFinite(page) && page > 0 ? Math.floor(page) : 1);

    const size = Number(params.get('size') || '12');
    this.pageSize.set(Number.isFinite(size) && size > 0 && size <= 60 ? Math.floor(size) : 12);
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

  /** 加载课程列表（服务端分页，两个数据源各自独立分页） */
  private loadCourses(): void {
    this.loading.set(true);
    this.loadFailed.set(false);

    const filter = this.keyword().trim() || undefined;
    const majorId = this.majorId() || undefined;
    const skipCount = (this.pageIndex() - 1) * this.pageSize();
    const maxResultCount = this.pageSize();

    if (this.scope() === 'shared') {
      this.courseShareService
        .getSharedToMe({ filter, majorId, skipCount, maxResultCount })
        .subscribe({
          next: result => {
            this.rawSharedItems.set(result?.items || []);
            this.totalCount.set(result?.totalCount || 0);
            this.loading.set(false);
            this.registerVoiceContext();
          },
          error: () => {
            this.rawSharedItems.set([]);
            this.totalCount.set(0);
            this.loading.set(false);
            this.loadFailed.set(true);
          },
        });
      return;
    }

    this.courseService
      .getPublished({ filter, majorId, skipCount, maxResultCount })
      .subscribe({
        next: result => {
          this.rawItems.set(result?.items || []);
          this.totalCount.set(result?.totalCount || 0);
          this.loading.set(false);
          this.registerVoiceContext();
        },
        error: () => {
          this.rawItems.set([]);
          this.totalCount.set(0);
          this.loading.set(false);
          this.loadFailed.set(true);
        },
      });
  }

  /** 重新加载（错误态重试） */
  reload(): void {
    this.loadCourses();
  }

  /** 我的课程：已选课徽章 + 学习进度的唯一来源 */
  private loadMyCourses(): void {
    if (!this.authService.isAuthenticated) return;
    this.learningService.getMyCourses().subscribe({
      next: list => this.myCourses.set(list || []),
      error: () => this.myCourses.set([]),
    });
  }

  /** 专业筛选项 */
  private loadMajors(): void {
    this.majorService.getLookupList().subscribe({
      next: list => this.majors.set(list || []),
      error: () => this.majors.set([]),
    });
  }

  /**
   * 教师姓名补全。
   *
   * 课程列表接口（getPublished）**不返回 teacherName**，只有课程详情接口有。
   * 门户首页负载（portal/home-data）里的课程摘要带 teacherName，可以按课程 id 补全，
   * 但该负载只覆盖门户首页展示的那部分课程 —— 补不到的课程就直接省略教师字段，
   * 绝不写「专业教学团队」之类的占位文案。
   */
  private loadTeacherNames(): void {
    const tenant = this.configState.getDeep('currentTenant') as Record<string, unknown> | undefined;
    const tenantId = (tenant?.['id'] as string) || '';
    if (!tenantId) return;

    this.portalService.getHomeData(tenantId).subscribe({
      next: data => {
        const map = new Map<string, string>();
        const briefs = [...(data?.publishedCourses || []), ...(data?.featuredCourses || [])];
        for (const brief of briefs) {
          if (brief?.id && brief?.teacherName) map.set(brief.id, brief.teacherName);
        }
        this.teacherNames.set(map);
      },
      error: () => undefined,
    });
  }

  /** 已发布课程 DTO → 卡片视图 */
  private toCard(course: CourseDto): StudentCourseCard {
    const majorNames = (course.majorNames || []).filter(n => !!n && n.trim().length > 0);
    if (majorNames.length === 0 && course.majorName) majorNames.push(course.majorName);
    const mine = this.myCourses().find(m => m.courseId === course.id);
    return {
      id: course.id || '',
      title: course.title || '未命名课程',
      description: course.description ?? null,
      coverImageUrl: course.coverImageUrl ?? null,
      majorNames,
      semesterHours: course.semesterHours ?? null,
      credits: course.credits ?? null,
      studentCount: course.studentCount ?? null,
      chapterCount: course.chapterCount ?? null,
      teacherName: this.teacherNames().get(course.id || '') || null,
      sourceTenantName: null,
      isShared: false,
      enrolled: !!mine,
      progress: mine?.progress ?? 0,
    };
  }

  /** 共享课程 DTO → 卡片视图（共享数据源不提供学时/学分/人数/章节数） */
  private toSharedCard(course: SharedCourseDto): StudentCourseCard {
    const majorNames = course.majorName ? [course.majorName] : [];
    const mine = this.myCourses().find(m => m.courseId === course.courseId);
    return {
      id: course.courseId || '',
      title: course.title || '未命名课程',
      description: course.description ?? null,
      coverImageUrl: course.coverImageUrl ?? null,
      majorNames,
      semesterHours: null,
      credits: null,
      studentCount: null,
      chapterCount: null,
      teacherName: this.teacherNames().get(course.courseId || '') || null,
      sourceTenantName: course.sourceTenantName || null,
      isShared: true,
      enrolled: !!mine,
      progress: mine?.progress ?? 0,
    };
  }

  /** 提交检索条 */
  onSearch(): void {
    this.navigate({ q: this.keyword().trim() || null, page: null });
  }

  /** 选择专业方向 */
  setMajor(value: string | null): void {
    const raw = value === null || value === undefined || value === '' ? null : String(value);
    this.majorId.set(raw);
    this.navigate({ major: raw, page: null });
  }

  /** 切换筛选范围 */
  setScope(scope: CourseScope): void {
    if (this.scope() === scope) return;
    // 只写 URL：scope 信号由 queryParamMap 订阅统一更新，
    // 避免「信号先变、列表后到」导致短暂闪出空态。
    this.navigate({ scope, page: null });
  }

  /** 清除筛选条件 */
  resetFilters(): void {
    this.keyword.set('');
    this.majorId.set(null);
    this.navigate({ q: null, major: null, page: null });
  }

  /** 分页 */
  onPageChange(event: { pageIndex: number; pageSize: number }): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.navigate({ page: String(event.pageIndex), size: String(event.pageSize) }, false);
  }

  /** 卡片封面色块：按标题稳定取色（与工作台一致，同一课程颜色恒定） */
  coverClass(card: StudentCourseCard, index: number): number {
    const key = card.title || card.id || `${index}`;
    let sum = 0;
    for (let i = 0; i < key.length; i++) sum = (sum + key.charCodeAt(i)) % 4;
    return sum;
  }

  /** 课程卡的主讲教师（补不到就省略整个字段） */
  teacherNameOf(card: StudentCourseCard): string | null {
    return card.teacherName;
  }

  /** 语音助手上下文：只读摘要（供总结/问答/跳转用） */
  private registerVoiceContext(): void {
    this.voiceContext.register('courses', () => {
      const list = this.visibleCards();
      const head = list.slice(0, 10);
      const lines = head.map(
        (c, i) =>
          `第${i + 1}门：${c.title}${c.majorNames.length ? '，' + c.majorNames.join('、') : ''}` +
          `${c.enrolled ? `，学习进度${c.progress}%` : '，未选课'}。`,
      );
      return {
        key: 'courses',
        route: '/student/courses',
        title: '课程中心',
        summary: `课程中心。当前${this.scope() === 'shared' ? '共享到本校的' : '本校已发布的'}课程共${this.totalCount()}门。${lines.join('')}`,
        items: head.map(c => ({ id: c.id, title: c.title })),
      };
    });
  }
}
