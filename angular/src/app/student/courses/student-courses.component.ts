import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { AuthService, Rest, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { MajorService } from '../../proxy/majors/major.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { CourseShareService } from '../../proxy/courses/course-share.service';
import type { SharedCourseDto } from '../../proxy/courses/dtos/models';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { StudentCourseListItemDto } from '../../proxy/learning/dtos/models';
import { VoiceContextService } from '../voice/voice-context.service';

/** 专业筛选 chip（对齐原型 .chips：全部 / 公共课 / 各专业） */
interface MajorChip {
  id: string | null;
  name: string;
}

/** 课程来源：全部课程 / 共享给我的课程 */
type SourceFilter = 'all' | 'shared';

/** 排序方式（对齐原型 sort select：综合 / 最新更新 / 名称） */
type SortKey = 'default' | 'updated' | 'name';

@Component({
  selector: 'app-student-courses',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzPaginationModule,
  ],
  templateUrl: './student-courses.component.html',
  styleUrls: ['./student-courses.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentCoursesComponent implements OnInit, OnDestroy {
  private readonly majorService = inject(MajorService);
  private readonly restService = inject(RestService);
  private readonly learningService = inject(LearningService);
  private readonly courseShareService = inject(CourseShareService);
  private readonly authService = inject(AuthService);
  private readonly message = inject(NzMessageService);
  private readonly voiceContext = inject(VoiceContextService);

  readonly loading = signal(false);

  readonly courses = signal<CourseDto[]>([]);
  readonly myCourses = signal<StudentCourseListItemDto[]>([]);
  /** 共享到本机构的课程（来自其它学校，如「社会学校」的共享课） */
  readonly sharedCourses = signal<SharedCourseDto[]>([]);
  /** 课程来源筛选：全部课程 / 共享给我的课程 */
  readonly sourceFilter = signal<SourceFilter>('all');

  readonly filter = signal('');
  readonly selectedMajor = signal<string | null>(null);
  readonly sortKey = signal<SortKey>('default');
  readonly pageIndex = signal(1);
  readonly pageSize = signal(12);

  /** 专业下拉哨兵值：选中表示只看公共课（无专业归属） */
  readonly majorPublicOnlyValue = '__public__';

  /** 专业筛选 chips（全部 / 公共课 / 专业字典） */
  readonly chips = signal<MajorChip[]>([
    { id: null, name: '全部' },
    { id: this.majorPublicOnlyValue, name: '公共课' },
  ]);

  /**
   * 课程列表：已发布课程（当前租户）+ 补齐缺失的已选课程（跨租户/未发布），
   * 保证已选课程的进度条永远可见。
   */
  readonly visibleCourses = computed<CourseDto[]>(() => {
    // 「共享给我的课程」视图：只列其它学校共享给本机构的课
    if (this.sourceFilter() === 'shared') {
      return this.sharedCourses().map(c => this.toCourseDto(c));
    }
    const publishedIds = new Set(this.courses().map(c => c.id));
    return [...this.courses(), ...this.collectMissingEnrolled(publishedIds)];
  });

  /** 是否有共享课程（决定要不要显示「共享给我的课程」入口） */
  readonly hasSharedCourses = computed(() => this.sharedCourses().length > 0);

  /** 共享课程的来源学校（按课程 Id 索引，列表卡片上展示「来自 XX」） */
  private readonly sharedSourceMap = computed(() => {
    const map = new Map<string, string>();
    for (const c of this.sharedCourses()) {
      if (c.courseId) map.set(c.courseId, c.sourceTenantName || '共建学校');
    }
    return map;
  });

  /** 课程来源学校名（共享课才返回） */
  sharedSourceName(courseId?: string | null): string | null {
    if (!courseId) return null;
    return this.sharedSourceMap().get(courseId) || null;
  }

  /** 共享记录 → 课程卡片视图 */
  private toCourseDto(c: SharedCourseDto): CourseDto {
    return {
      id: c.courseId,
      title: c.title || '未命名课程',
      description: c.description ?? null,
      coverImageUrl: c.coverImageUrl ?? null,
      majorId: c.majorId ?? null,
      majorName: c.majorName ?? null,
      majorIds: c.majorId ? [c.majorId] : [],
      majorNames: c.majorName ? [c.majorName] : [],
      isShared: true,
      studentCount: 0,
      progress: 0,
      isEnrolled: false,
    } as unknown as CourseDto;
  }

  /** 排序后的课程列表（对齐原型 sort select） */
  readonly sortedCourses = computed<CourseDto[]>(() => {
    const list = this.visibleCourses();
    const key = this.sortKey();
    if (key === 'name') {
      return list.slice().sort((a, b) => (a.title || '').localeCompare(b.title || '', 'zh-Hans-CN'));
    }
    if (key === 'updated') {
      const t = (c: CourseDto) => {
        const v = c.lastModificationTime || c.creationTime;
        const ms = v ? new Date(v).getTime() : 0;
        return isNaN(ms) ? 0 : ms;
      };
      return list.slice().sort((a, b) => t(b) - t(a));
    }
    return list;
  });

  /** 当前页展示的课程（客户端分页，每页 12，网格三列） */
  readonly pagedCourses = computed<CourseDto[]>(() => {
    const list = this.sortedCourses();
    const start = (this.pageIndex() - 1) * this.pageSize();
    return list.slice(start, start + this.pageSize());
  });

  /**
   * 从我的选课记录中补齐列表缺失的课程卡片（跨租户 / 未发布），
   * 并叠加当前搜索/专业筛选，保证已选课程不丢失。
   */
  private collectMissingEnrolled(existingIds: Set<string>): CourseDto[] {
    const keyword = (this.filter() || '').trim().toLowerCase();
    const majorSel = this.selectedMajor();
    const missing: CourseDto[] = [];
    for (const m of this.myCourses()) {
      if (!m.courseId || existingIds.has(m.courseId)) continue;
      if (keyword && !(m.courseTitle || '').toLowerCase().includes(keyword)) continue;
      if (majorSel === this.majorPublicOnlyValue) {
        if (m.majorId) continue;
      } else if (majorSel) {
        if (m.majorId !== majorSel) continue;
      }
      missing.push({
        id: m.courseId,
        title: m.courseTitle || '未命名课程',
        coverImageUrl: m.courseCoverImageUrl ?? null,
        majorId: m.majorId ?? null,
        majorName: m.majorName ?? null,
        majorIds: m.majorId ? [m.majorId] : [],
        majorNames: m.majorName ? [m.majorName] : [],
        semester: m.semester ?? null,
        credits: m.credits ?? null,
        semesterHours: m.semesterHours ?? null,
        isEnrolled: true,
        progress: m.progress ?? 0,
        studentCount: 0,
        // 章节数取后端「我的课程」返回的真实值：补齐卡片多为未发布/跨租户已选课，
        // 写死 0 会与课程详情页显示的章节数对不上（列表 0 章节、详情 8 章节）。
        chapterCount: m.chapterCount ?? 0,
      } as unknown as CourseDto);
    }
    return missing;
  }

  ngOnInit(): void {
    this.voiceContext.register('courses', () => {
      const list = this.visibleCourses();
      const head = list.slice(0, 10);
      const lines = head.map(
        (c, i) => `第${i + 1}门：${c.title || '未命名'}，${this.courseMajorText(c)}，${this.isEnrolled(c.id!) ? `学习进度${this.getMyProgress(c.id!)}%` : '未选课'}。`
      );
      return {
        key: 'courses',
        route: '/student/courses',
        title: '课程中心',
        summary: `课程中心。当前列表共${list.length}门课程。${lines.join('')}`,
        items: head.map(c => ({ id: c.id!, title: c.title || '未命名课程' })),
      };
    });
    this.loadCourses();
    this.loadMajors();
    this.loadMyCourses();
    this.loadSharedCourses();
  }

  /**
   * 拉取「共享到本机构的课程」。
   *
   * <para>社会学校的学生登录后，本租户自己可能一门课都没有 —— 内容都来自其它学校
   * 「共享到社会学校」的课。没有这个入口，共享功能等于白做（学生登录后课程中心空空如也）。</para>
   */
  loadSharedCourses(): void {
    this.courseShareService
      .getSharedToMe({ skipCount: 0, maxResultCount: 200 })
      .subscribe({
        next: result => this.sharedCourses.set(result.items || []),
        error: () => this.sharedCourses.set([]),
      });
  }

  /** 切换课程来源（全部 / 共享给我的） */
  setSourceFilter(source: SourceFilter): void {
    this.sourceFilter.set(source);
    this.pageIndex.set(1);
  }

  ngOnDestroy(): void {
    this.voiceContext.unregister('courses');
  }

  loadCourses(): void {
    this.loading.set(true);
    const majorSel = this.selectedMajor();
    const params: Record<string, unknown> = {
      filter: this.filter() || undefined,
      skipCount: 0,
      // 一次取足够大的一页：列表在客户端对 visibleCourses()（含跨租户/未发布已选课补齐）分页
      maxResultCount: 200,
    };
    if (majorSel === this.majorPublicOnlyValue) {
      params['onlyPublicCourses'] = true;
    } else if (majorSel) {
      params['majorId'] = majorSel;
    }
    this.restService
      .request<any, PagedResultDto<CourseDto>>(
        { method: 'GET', url: '/api/app/course/published', params },
        { apiName: 'KnowledgeHub' } as Partial<Rest.Config>
      )
      .subscribe({
        next: result => {
          this.courses.set(result.items || []);
          this.loading.set(false);
        },
        error: () => {
          this.courses.set([]);
          this.loading.set(false);
          this.message.error('课程加载失败');
        },
      });
  }

  loadMyCourses(): void {
    if (!this.authService.isAuthenticated) return;
    this.learningService.getMyCourses().subscribe({
      next: list => this.myCourses.set((list || []) as StudentCourseListItemDto[]),
      error: () => {
        // 静默失败，不影响主流程
      },
    });
  }

  /** 专业筛选 chips：全量专业字典（不受当前课程列表影响） */
  loadMajors(): void {
    this.majorService.getLookupList().subscribe({
      next: list => {
        const chips: MajorChip[] = [
          { id: null, name: '全部' },
          { id: this.majorPublicOnlyValue, name: '公共课' },
        ];
        (list || []).forEach(m => {
          if (!m?.id) return;
          chips.push({ id: m.id, name: m.name || '未命名专业' });
        });
        this.chips.set(chips);
        // 当前选中的专业若已不在字典中（如被删除），回落到全部
        const sel = this.selectedMajor();
        if (sel && sel !== this.majorPublicOnlyValue && !chips.some(c => c.id === sel)) {
          this.selectedMajor.set(null);
        }
      },
      error: () => {
        // 字典加载失败时保留默认两项，不影响主流程
      },
    });
  }

  /** 课程卡片的专业文本：多专业用“、”连接，无归属显示“公共课” */
  courseMajorText(c: CourseDto | null | undefined): string {
    const names = (c?.majorNames || []).filter(n => !!n);
    if (names.length > 0) return names.join('、');
    return (c?.majorName || '').trim() || '公共课';
  }

  /** 卡片 meta 左侧：24 课时 / 12 学分 / 6 章节（对齐原型 `${c[3]}` 位置） */
  courseMeta(c: CourseDto): string {
    if (c.semesterHours != null && c.semesterHours > 0) return `${c.semesterHours} 课时`;
    if (c.credits != null && c.credits > 0) return `${c.credits} 学分`;
    return `${c.chapterCount || 0} 章节`;
  }

  selectMajor(id: string | null) {
    if (this.selectedMajor() === id) return;
    this.selectedMajor.set(id);
    this.pageIndex.set(1);
    this.loadCourses();
  }

  setSort(value: SortKey) {
    this.sortKey.set(value);
    this.pageIndex.set(1);
  }

  onSearch() {
    this.pageIndex.set(1);
    this.loadCourses();
  }

  onReset() {
    this.filter.set('');
    this.selectedMajor.set(null);
    this.sortKey.set('default');
    this.pageIndex.set(1);
    this.loadCourses();
  }

  onPageChange(index: number) {
    this.pageIndex.set(index);
  }

  hasCover(course: CourseDto): boolean {
    return !!course.coverImageUrl && course.coverImageUrl.trim().length > 0;
  }

  /** 封面色块 c0-c3：按标题 hash 稳定取色（对齐原型 cover 色块） */
  coverClass(course: CourseDto, index: number): number {
    const key = course.title || course.id || `${index}`;
    let sum = 0;
    for (let i = 0; i < key.length; i++) sum = (sum + key.charCodeAt(i)) % 4;
    return sum;
  }

  /** 已选课：以「我的课程」为准，后端 isEnrolled 兜底 */
  isEnrolled(courseId: string): boolean {
    return this.myCourses().some(c => c.courseId === courseId) || !!this.courses().find(c => c.id === courseId)?.isEnrolled;
  }

  /** 学习进度 %（对齐原型 s-my-courses 卡片进度条，用真实数据） */
  getMyProgress(courseId: string): number {
    const mine = this.myCourses().find(c => c.courseId === courseId);
    if (mine) return mine.progress || 0;
    return this.courses().find(c => c.id === courseId)?.progress || 0;
  }
}
