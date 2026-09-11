import { ChangeDetectionStrategy, Component, DestroyRef, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { AuthService } from '@abp/ng.core';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzTableModule } from 'ng-zorro-antd/table';
import { CourseService } from '../../proxy/courses/course.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import type { CourseDetailDto, ChapterDto } from '../../proxy/courses/dtos/models';
import type { ExerciseDto } from '../../proxy/exams/dtos/models';
import type { LearningProgressDto, KnowledgeMasteryDto } from '../../proxy/learning/dtos/models';
import type { StudentExerciseRecordDto } from '../../proxy/learning/dtos/models';
import { ChapterTreeGraphComponent } from '../../learning/knowledge-graph/chapter-tree-graph.component';
import { MasteryRadarComponent, type RadarAxis } from '../../shared/charts/mastery-radar.component';
import { ClientCacheService } from '../../shared/cache/client-cache.service';
import { VoiceContextService } from '../voice/voice-context.service';

type TabKey = 'chapters' | 'graph' | 'progress' | 'related';

interface RelatedCourse {
  id: string;
  title: string;
  majorName?: string;
  majorNames?: string[];
  majorIds?: string[];
  studentCount: number;
  difficulty: number;
  coverImageUrl?: string;
}

interface ResourceItem {
  id: string;
  chapterId: string;
  name: string;
  difficulty: number;
  importance: string;
}

@Component({
  selector: 'app-student-course-detail',
  standalone: true,
  imports: [
    CommonModule,
    DecimalPipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzButtonModule,
    NzSpinModule,
    NzProgressModule,
    NzEmptyModule,
    NzTabsModule,
    NzTooltipModule,
    NzDividerModule,
    NzTableModule,
    ChapterTreeGraphComponent,
    MasteryRadarComponent,
  ],
  templateUrl: './student-course-detail.component.html',
  styleUrls: ['./student-course-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentCourseDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly courseService = inject(CourseService);
  private readonly learningService = inject(LearningService);
  private readonly exerciseService = inject(ExerciseService);
  private readonly recordService = inject(StudentExerciseRecordService);
  private readonly authService = inject(AuthService);
  private readonly message = inject(NzMessageService);
  private readonly cache = inject(ClientCacheService);
  private readonly voiceContext = inject(VoiceContextService);

  readonly loading = signal(true);
  readonly activeTab = signal<TabKey>('chapters');

  readonly course = signal<CourseDetailDto | null>(null);
  readonly chapters = signal<ChapterDto[]>([]);
  readonly chaptersLoading = signal(true);
  readonly expandedNodes = signal<Set<string>>(new Set());

  readonly progress = signal<LearningProgressDto | null>(null);
  readonly mastery = signal<KnowledgeMasteryDto[]>([]);

  /** 本课程的全部习题完成记录（用于汇总统计） */
  readonly allExerciseRecords = signal<StudentExerciseRecordDto[]>([]);
  /** 最近习题记录加载状态（与章节进度共用 loading） */
  readonly recentExerciseLoading = signal(true);

  /** 习题完成汇总（基于全部已提交记录，保证正确率准确） */
  readonly exerciseStats = computed(() => {
    const records = this.allExerciseRecords();
    const total = records.length;
    const correct = records.filter(r => r.isCorrect === true).length;
    const wrong = records.filter(r => r.isCorrect === false).length;
    const rate = total > 0 ? Math.round((correct / total) * 100) : 0;
    return { total, correct, wrong, rate };
  });

  /** 累计练习用时（毫秒） */
  readonly totalTimeSpentMs = computed<number>(() => {
    return this.allExerciseRecords().reduce((sum, r) => {
      const ms = this.parseTimeSpentMs(r.timeSpent);
      return sum + (Number.isFinite(ms) ? ms : 0);
    }, 0);
  });

  /** 最近习题记录（去重，每个习题取最近一次提交，最多 10 条） */
  readonly recentExerciseDedup = computed<StudentExerciseRecordDto[]>(() => {
    const seen = new Set<string>();
    const list: StudentExerciseRecordDto[] = [];
    for (const r of this.allExerciseRecords()) {
      const key = r.exerciseId;
      if (!key || seen.has(key)) continue;
      seen.add(key);
      list.push(r);
      if (list.length >= 10) break;
    }
    return list;
  });

  /** 按章节统计习题进度（与学习页面一致） */
  readonly chapterProgressMap = signal<Map<string, { total: number; completed: number }>>(new Map());
  readonly masteredChapterCount = computed(() => {
    let count = 0;
    for (const v of this.chapterProgressMap().values()) {
      if (v.total > 0 && v.completed >= v.total) count++;
    }
    return count;
  });

  /** 习题完成进度（与学习页面 courseProgress 一致） */
  readonly exerciseProgress = computed(() => {
    const map = this.chapterProgressMap();
    let total = 0, completed = 0;
    for (const v of map.values()) {
      total += v.total;
      completed += v.completed;
    }
    if (total === 0) return 0;
    return Math.min(Math.round((completed / total) * 100), 100);
  });

  readonly related = signal<RelatedCourse[]>([]);

  /** 从“相关课程”进入时的来源课程 id；存在时返回按钮显示“返回相关课程” */
  readonly backToCourse = signal<string | null>(null);

  /** 从租户页（资源库）进入时的租户 id；存在时返回按钮显示“返回资源库”并回到该租户页 */
  readonly backToTenant = signal<string | null>(null);

  /** 从微专业课程列表进入时的微专业 id；存在时返回按钮显示“返回微专业”并回到该页 */
  readonly backToMicroMajor = signal<string | null>(null);
  /** 微专业页透传的原始 from 参数（home / my-micro-majors / my-micro-majors-list），用于还原本页 URL */
  readonly microMajorFrom = signal<string | null>(null);

  /** 从门户首页（PortalHome）的精品课程卡片进入时为 true；返回按钮显示“返回首页”并跳 `/` */
  readonly backToHome = signal<boolean>(false);

  /** 返回按钮文案：门户首页 > 租户资源库 > 微专业上下文 > 相关课程上下文 > 默认课程中心 */
  readonly backLabel = computed(() => {
    if (this.backToHome()) return '返回首页';
    if (this.backToTenant()) return '返回资源库';
    if (this.backToMicroMajor()) {
      return this.microMajorFrom() === 'my-micro-majors-list' ? '返回我的微专业' : '返回微专业';
    }
    return this.backToCourse() ? '返回相关课程' : '返回课程中心';
  });

  /** 当前章节的所有资源（聚合自课程） */
  readonly resources = signal<ResourceItem[]>([]);

  ngOnInit() {
    // 语音助手：注册本页上下文（只读摘要，供总结/问答/跳转学习页用）。
    this.voiceContext.register('course-detail', () => {
      const c = this.course();
      if (!c) return null;
      const chapters = this.chapters() || [];
      const flat: string[] = [];
      const walk = (nodes: ChapterDto[], depth: number) => {
        for (const n of nodes.slice(0, depth === 0 ? 8 : 4)) {
          flat.push(`${'—'.repeat(Math.min(depth, 2))}${n.title || '未命名章节'}`);
          if (flat.length >= 20) return;
          if (n.children?.length && depth < 2) walk(n.children, depth + 1);
        }
      };
      walk(chapters, 0);
        const count = this.countChapters(chapters);
      const majorText = this.courseMajorText(c);
      return {
        key: 'course-detail',
        route: this.router.url,
        title: `《${c.title || '未命名'}》课程详情`,
        summary:
          `课程《${c.title || '未命名'}》${c.teacherName ? `，主讲${c.teacherName}` : ''}` +
          `${majorText ? `，所属${majorText}` : ''}，共${count}个章节` +
          `${c.description ? `。简介：${c.description.slice(0, 300)}` : ''}` +
          `${flat.length ? `。章节有：${flat.join('；')}` : ''}` +
          `${c.isEnrolled ? '。你已选本课程，可说“开始学习”进入学习页。' : '。你尚未选课。'}`,
        items: [],
        courseId: c.id,
      };
    });
    // 订阅路由参数：从“相关课程”点击跳转到其他课程时，URL 参数变化但组件会被复用，
    // 只靠 snapshot 的 ngOnInit 不会再次执行，必须监听 paramMap 才能重新加载目标课程。
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const id = params.get('id');
      if (!id) {
        this.router.navigate(['/student/courses']);
        return;
      }

      // 从 URL（相关课程跳转 / 回退 / 刷新）恢复来源课程与 Tab 状态
      const qp = this.route.snapshot.queryParamMap;
      const fromCourse = qp.get('fromCourse');
      const urlTab = qp.get('tab') as TabKey | null;
      const validTabs: TabKey[] = ['chapters', 'graph', 'progress', 'related'];

      // 切换课程时重置状态，避免旧课程内容残留
      this.course.set(null);
      this.chapters.set([]);
      this.related.set([]);
      this.resources.set([]);
      this.progress.set(null);
      this.mastery.set([]);
      this.chapterProgressMap.set(new Map());
      this.allExerciseRecords.set([]);
      this.recentExerciseLoading.set(true);
      this.expandedNodes.set(new Set());
      this.backToCourse.set(fromCourse || null);
      // 门户首页上下文：从 `/` 的精品课程卡片进入时，返回按钮回到门户首页
      this.backToHome.set(qp.get('from') === 'home');
      // 租户资源库上下文：从租户页进入时，返回按钮回到租户页（/tenant/:id）
      this.backToTenant.set(qp.get('tenantId') || null);
      // 微专业上下文：从微专业课程进入时，返回按钮回到微专业页（保留 from 参数还原原 URL）
      this.backToMicroMajor.set(qp.get('fromMicroMajor') || null);
      this.microMajorFrom.set(qp.get('from') || null);
      this.activeTab.set(validTabs.includes(urlTab!) ? urlTab! : 'chapters');

      this.loadCourse(id);
      this.loadProgress(id);
      this.loadMastery(id);
      this.loadExerciseProgress(id);
    });
  }

  loadCourse(id: string) {
    this.loading.set(true);
    this.cache.load<CourseDetailDto>('student.course-detail', `course:${id}`, () => this.courseService.getDetail(id)).subscribe({
      next: result => {
        this.course.set(result);
        this.loading.set(false);
        // 详情接口已返回完整章节树（含知识点资源），直接复用，避免重复请求章节接口
        const tree = result?.chapters || [];
        this.chapters.set(tree);
        this.chaptersLoading.set(false);
        this.collectResources(tree);
        // 课程加载完成后再加载相关推荐（需要专业归属，多专业时按全部专业找相关）
        this.loadRelated(result?.majorIds, result?.majorId);
        // 默认展开一级章节
        const initial = new Set<string>();
        tree.forEach(c => c.id && initial.add(c.id));
        this.expandedNodes.set(initial);
      },
      error: () => {
        this.loading.set(false);
        this.chaptersLoading.set(false);
        this.message.error('课程加载失败');
        this.router.navigate(['/student/courses']);
      },
    });
  }

  loadProgress(courseId: string) {
    if (!this.authService.isAuthenticated) return;
    this.learningService.getProgress(courseId).subscribe({
      next: data => this.progress.set(data),
      error: () => this.progress.set(null),
    });
  }

  loadMastery(courseId: string) {
    if (!this.authService.isAuthenticated) return;
    this.learningService.getKnowledgeMastery(courseId).subscribe({
      next: data => this.mastery.set(data || []),
      error: () => this.mastery.set([]),
    });
  }

  private loadExerciseProgress(courseId: string) {
    if (!this.authService.isAuthenticated) {
      this.recentExerciseLoading.set(false);
      return;
    }
    this.recentExerciseLoading.set(true);
    this.exerciseService.getByCourse(courseId).subscribe({
      next: (data: any) => {
        const list = (data?.items || data || []) as ExerciseDto[];
        // 按章节统计习题总数
        const chapterTotalMap = new Map<string, number>();
        for (const ex of list) {
          const chId = ex.chapterId;
          if (chId) chapterTotalMap.set(chId, (chapterTotalMap.get(chId) || 0) + 1);
        }

        this.recordService.getRecordsByCourse({
          courseId,
          skipCount: 0,
          maxResultCount: 10000,
        } as any).subscribe({
          next: (recordResult: any) => {
            const records = (recordResult?.items || []) as StudentExerciseRecordDto[];
            const completedIds = new Set<string>(
              records.filter(r => r.exerciseId).map(r => r.exerciseId!)
            );

            // 按章节统计已提交數
            const chapterCompletedMap = new Map<string, number>();
            for (const ex of list) {
              const chId = ex.chapterId;
              if (chId && completedIds.has(ex.id!)) {
                chapterCompletedMap.set(chId, (chapterCompletedMap.get(chId) || 0) + 1);
              }
            }

            const progressMap = new Map<string, { total: number; completed: number }>();
            for (const [chId, total] of chapterTotalMap) {
              progressMap.set(chId, { total, completed: chapterCompletedMap.get(chId) || 0 });
            }
            this.chapterProgressMap.set(progressMap);

            // 保存全部记录（按完成时间倒序，统计和最近列表都基于此计算）
            const sorted = [...records].sort((a, b) => {
              const at = a.completedAt ? new Date(a.completedAt).getTime() : 0;
              const bt = b.completedAt ? new Date(b.completedAt).getTime() : 0;
              return bt - at;
            });
            this.allExerciseRecords.set(sorted);

            this.recentExerciseLoading.set(false);
          },
          error: () => this.recentExerciseLoading.set(false),
        });
      },
      error: () => this.recentExerciseLoading.set(false),
    });
  }

  /** 解析 TimeSpan 字符串（如 "00:05:30"、"00:00:45"、"1.02:30:00"）为毫秒数 */
  private parseTimeSpentMs(value?: string | null): number {
    if (!value) return 0;
    const trimmed = value.trim();
    if (!trimmed) return 0;

    // 含天数的格式："d.hh:mm:ss" 或 "d.hh:mm"
    const dayMatch = /^(\d+)\.(\d{1,2}):(\d{2})(?::(\d{2}))?$/.exec(trimmed);
    if (dayMatch) {
      const d = Number(dayMatch[1]);
      const h = Number(dayMatch[2]);
      const m = Number(dayMatch[3]);
      const s = Number(dayMatch[4] || 0);
      return ((d * 24 + h) * 3600 + m * 60 + s) * 1000;
    }

    // 标准格式："hh:mm:ss" 或 "hh:mm"
    const parts = trimmed.split(':');
    if (parts.length === 3) {
      const h = Number(parts[0]);
      const m = Number(parts[1]);
      const s = Number(parts[2]);
      return (h * 3600 + m * 60 + s) * 1000;
    }
    if (parts.length === 2) {
      const h = Number(parts[0]);
      const m = Number(parts[1]);
      return (h * 3600 + m * 60) * 1000;
    }
    return 0;
  }

  /** 把毫秒数格式化为 "Xh Ym" / "Ym Ys" / "Ys" */
  formatDuration(ms: number): string {
    if (!ms || ms <= 0) return '0分钟';
    const totalSeconds = Math.round(ms / 1000);
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    if (hours > 0 && minutes > 0) return `${hours}小时${minutes}分钟`;
    if (hours > 0) return `${hours}小时`;
    if (minutes > 0) return `${minutes}分钟`;
    return `${seconds}秒`;
  }

  /** 单条记录的用时（"3分钟" / "45秒"） */
  formatRecordTimeSpent(record: StudentExerciseRecordDto): string {
    return this.formatDuration(this.parseTimeSpentMs(record.timeSpent));
  }

  /** 掌握等级文本 */
  masteryLevelLabel(level?: number | null): string {
    if (level === 0) return '未学习';
    if (level === 1) return '入门';
    if (level === 2) return '熟练';
    return '精通';
  }

  /** 记录结果展示文本 */
  recordResultLabel(record: StudentExerciseRecordDto): string {
    if (record.isCorrect === true) return '正确';
    if (record.isCorrect === false) return '错误';
    return '已作答';
  }

  /** 记录结果 CSS 类（用于标签颜色） */
  recordResultClass(record: StudentExerciseRecordDto): string {
    if (record.isCorrect === true) return 'is-correct';
    if (record.isCorrect === false) return 'is-wrong';
    return 'is-neutral';
  }

  /** 主课程的专业文本：多专业用“、”连接，无归属时返回空（模板隐藏该行） */
  courseMajorText(c: CourseDetailDto | null | undefined): string {
    if (!c) return '';
    const names = (c.majorNames || []).filter(n => !!n && n.trim().length > 0);
    if (names.length > 0) return names.join('、');
    return (c.majorName || '').trim();
  }

  /** 相关课程的专业文本：多专业用“、”连接 */
  relatedMajorText(r: RelatedCourse | null | undefined): string {
    if (!r) return '';
    const names = (r.majorNames || []).filter(n => !!n && n.trim().length > 0);
    if (names.length > 0) return names.join('、');
    return (r.majorName || '').trim();
  }

  loadRelated(majorIds?: string[] | null, majorId?: string | null) {
    const ids = (majorIds || []).filter(x => !!x);
    const singleId = majorId || ids[0] || null;
    // 当前课程无专业归属 → 按公共课找相关（无归属的课程）；有专业 → 严格同专业（含兼属）
    const currentIsPublic = ids.length === 0 && !singleId;
    // 多拉取候选再在前端按“严格同专业”过滤 + 按学习人数排序（后端 GetPublished 按创建时间排序且含公共课）
    const input: Record<string, unknown> = {
      skipCount: 0,
      maxResultCount: 50,
    };
    if (ids.length > 1) input['majorIds'] = ids;
    else if (singleId) input['majorId'] = singleId;
    this.cache.load<any>('student.course-detail', `related:${JSON.stringify(input)}`, () => this.courseService.getPublished(input as any)).subscribe({
      next: result => {
        const currentId = this.course()?.id;
        const wanted = new Set(ids);
        if (singleId) wanted.add(singleId);
        const items = (result.items || [])
          .filter(x => x.id !== currentId)
          .filter(x => {
            const xIds = ((x.majorIds || []) as string[]).filter(v => !!v);
            if (x.majorId && !xIds.includes(x.majorId)) xIds.push(x.majorId);
            if (currentIsPublic) return xIds.length === 0; // 公共课只配公共课
            if (wanted.size === 0) return true;
            return xIds.some(v => wanted.has(v)); // 严格同专业：至少命中一个相同专业
          })
          // 按学习人数降序排列，从左往右按顺序显示
          .sort((a, b) => (b.studentCount || 0) - (a.studentCount || 0))
          .slice(0, 5)
          .map(x => ({
            id: x.id!,
            title: x.title || '未命名课程',
            majorName: x.majorName || undefined,
            majorNames: (x.majorNames || []).filter(n => !!n),
            majorIds: (() => {
              const list = ((x.majorIds || []) as string[]).filter(v => !!v);
              if (x.majorId && !list.includes(x.majorId)) list.push(x.majorId);
              return list;
            })(),
            studentCount: x.studentCount || 0,
            difficulty: x.difficulty || 1,
            coverImageUrl: (x as { coverImageUrl?: string }).coverImageUrl,
          }));
        this.related.set(items);
      },
      error: () => this.related.set([]),
    });
  }

  private collectResources(chapters: ChapterDto[]) {
    const list: ResourceItem[] = [];
    const walk = (nodes: ChapterDto[]) => {
      nodes.forEach(n => {
        (n.knowledgeResources || []).forEach(r => {
          list.push({
            id: r.id!,
            chapterId: n.id!,
            name: r.name || '未命名资源',
            difficulty: r.difficulty || 1,
            importance: r.importanceLevel || 'normal',
          });
        });
        if (n.children) walk(n.children);
      });
    };
    walk(chapters);
    this.resources.set(list);
  }

  isExpanded(nodeId: string): boolean {
    return this.expandedNodes().has(nodeId);
  }

  toggleNode(nodeId: string) {
    this.expandedNodes.update(set => {
      const newSet = new Set(set);
      if (newSet.has(nodeId)) newSet.delete(nodeId);
      else newSet.add(nodeId);
      return newSet;
    });
  }

  goBack() {
    // 从门户首页（精品课程卡片）进入：直接返回门户首页 `/`
    if (this.backToHome()) {
      this.router.navigate(['/']);
      return;
    }
    // 从租户资源库进入：直接返回对应的租户页
    const tenantId = this.backToTenant();
    if (tenantId) {
      this.router.navigate(['/tenant', tenantId]);
      return;
    }
    // 从微专业课程进入：直接返回之前的微专业页面（原 URL 含 from 参数时一并还原）
    const mmId = this.backToMicroMajor();
    if (mmId) {
      const from = this.microMajorFrom();
      if (from === 'my-micro-majors-list') {
        // 从“我的微专业”列表的课程直达进入，返回列表页
        this.router.navigate(['/student/my-micro-majors']);
        return;
      }
      const queryParams: Record<string, string> = {};
      if (from) queryParams['from'] = from;
      this.router.navigate(['/student/micro-majors', mmId], { queryParams });
      return;
    }
    // 从“相关课程”进入：回退浏览器历史到上一课程，保留其 Tab 与返回链；否则返回课程中心列表
    if (this.backToCourse() && window.history.length > 1) {
      window.history.back();
      return;
    }
    this.router.navigate(['/student/courses']);
  }

  setTab(tab: TabKey) {
    this.activeTab.set(tab);
    // 把 Tab 状态写入 URL（replaceUrl 不增加历史记录），返回上一课程时可恢复原 Tab
    this.router.navigate([], { queryParams: { tab }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  notifyNotEnrolled() {
    this.message.warning('未选课，请联系老师分配课程');
  }

  startLearning() {
    const c = this.course();
    if (!c?.id) return;
    // 未选课（或跨租户无选课可能）禁止进入学习页，给出明确提示而非静默跳转
    if (!c.isEnrolled) {
      this.message.error('未选课，不能访问该课程学习页');
      return;
    }
    // 保留 fromMicroMajor 等来源参数，学习页返回课程详情时仍能回到来源微专业
    this.router.navigate(['/student/courses', c.id, 'learn'], { queryParamsHandling: 'preserve' });
  }

  openRelated(id: string) {
    const c = this.course();
    // 携带来源课程 id，详情页可据此显示“返回相关课程”链接
    const queryParams: Record<string, string> = {};
    if (c?.id) queryParams['fromCourse'] = c.id;
    this.router.navigate(['/student/courses', id], { queryParams });
  }

  /** 计算章节数（含子章节） */
  countChapters(nodes: ChapterDto[]): number {
    let total = 0;
    const walk = (arr: ChapterDto[]) => {
      arr.forEach(n => {
        total++;
        if (n.children) walk(n.children);
      });
    };
    walk(nodes);
    return total;
  }

  /** 课程封面渐变（与列表一致） */
  courseGradient(course: CourseDetailDto | { title?: string; id?: string; majorName?: string }): string {
    return this.gradientByKey(
      course?.title || course?.id || 'x',
      course?.majorName || ''
    );
  }

  private gradientByKey(primary: string, secondary: string): string {
    const palettes = [
      '#1f56ad',
      '#2b6cd4',
      '#2b6cd4',
      '#0891b2',
      '#2b6cd4',
      '#059669',
      '#10b981',
      '#0e7490',
    ];
    const key = (primary || 'x') + (secondary || '');
    let hash = 0;
    for (let i = 0; i < key.length; i++) {
      hash = (hash * 31 + key.charCodeAt(i)) | 0;
    }
    return palettes[Math.abs(hash) % palettes.length];
  }

  hasCover(c: CourseDetailDto | RelatedCourse): boolean {
    return !!c.coverImageUrl && c.coverImageUrl.trim().length > 0;
  }

  difficultyLabel(d?: number): string {
    const labels = ['入门', '初级', '中级', '高级', '专家'];
    return labels[(d || 1) - 1] || '未设置';
  }

  difficultyColor(d?: number): string {
    const colors = ['#34d399', '#22c55e', '#5b93db', '#2b6cd4', '#1f56ad'];
    return colors[(d || 1) - 1] || '#94a3b8';
  }

  statusLabel(status?: number): string {
    const map: Record<number, string> = {
      0: '草稿',
      1: '已发布',
      2: '已结课',
    };
    return map[status || 0] || '已发布';
  }

  /** 跟踪模板：递归渲染章节树 */
  trackById = (_: number, n: ChapterDto) => n.id;
  trackByResource = (_: number, r: ResourceItem) => r.id;

  /** 掌握度雷达图数据（按知识点归类） */
  masteryRadarData = computed<RadarAxis[]>(() => {
    const data = this.mastery();
    if (data.length === 0) return [];
    return data.map(m => ({
      name: m.knowledgeResourceName || '未命名',
      value: m.accuracy || 0,
      max: 100,
    }));
  });

  /** 掌握度平均值 */
  masteryAverage = computed<number>(() => {
    const data = this.mastery();
    if (data.length === 0) return 0;
    return Math.round(data.reduce((s, m) => s + (m.accuracy || 0), 0) / data.length);
  });

  ngOnDestroy(): void {
    this.voiceContext.unregister('course-detail');
  }
}
