import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
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
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { LearningService } from '../../proxy/learning/learning.service';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import type { CourseDetailDto, ChapterDto } from '../../proxy/courses/dtos/models';
import type { ExerciseDto } from '../../proxy/exams/dtos/models';
import type { LearningProgressDto, KnowledgeMasteryDto } from '../../proxy/learning/dtos/models';
import type { StudentExerciseRecordDto } from '../../proxy/learning/dtos/models';
import { ChapterTreeGraphComponent } from '../../learning/knowledge-graph/chapter-tree-graph.component';
import { MasteryRadarComponent, type RadarAxis } from '../../shared/charts/mastery-radar.component';

type TabKey = 'chapters' | 'graph' | 'progress' | 'related';

interface RelatedCourse {
  id: string;
  title: string;
  major?: string;
  studentCount: number;
  difficulty: number;
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
    ChapterTreeGraphComponent,
    MasteryRadarComponent,
  ],
  templateUrl: './student-course-detail.component.html',
  styleUrls: ['./student-course-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentCourseDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);
  private readonly learningService = inject(LearningService);
  private readonly exerciseService = inject(ExerciseService);
  private readonly recordService = inject(StudentExerciseRecordService);
  private readonly authService = inject(AuthService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(true);
  readonly enrolling = signal(false);
  readonly activeTab = signal<TabKey>('chapters');

  readonly course = signal<CourseDetailDto | null>(null);
  readonly chapters = signal<ChapterDto[]>([]);
  readonly chaptersLoading = signal(true);
  readonly expandedNodes = signal<Set<string>>(new Set());

  readonly progress = signal<LearningProgressDto | null>(null);
  readonly mastery = signal<KnowledgeMasteryDto[]>([]);

  /** 按章节统计习题进度（与学习页面一致） */
  readonly chapterProgressMap = signal<Map<string, { total: number; completed: number }>>(new Map());
  readonly masteredChapterCount = computed(() => {
    let count = 0;
    for (const v of this.chapterProgressMap().values()) {
      if (v.total > 0 && v.completed >= v.total) count++;
    }
    return count;
  });

  readonly related = signal<RelatedCourse[]>([]);

  /** 当前章节的所有资源（聚合自课程） */
  readonly resources = signal<ResourceItem[]>([]);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.router.navigate(['/student/courses']);
      return;
    }

    this.loadCourse(id);
    this.loadChapters(id);
    this.loadProgress(id);
    this.loadMastery(id);
    this.loadExerciseProgress(id);
  }

  loadCourse(id: string) {
    this.loading.set(true);
    this.courseService.getDetail(id).subscribe({
      next: result => {
        this.course.set(result);
        this.loading.set(false);
        // 课程加载完成后再加载相关推荐（需要 majorId）
        this.loadRelated(result?.majorId);
        // 默认展开一级章节
        const initial = new Set<string>();
        (result.chapters || []).forEach(c => c.id && initial.add(c.id));
        this.expandedNodes.set(initial);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('课程加载失败');
        this.router.navigate(['/student/courses']);
      },
    });
  }

  loadChapters(courseId: string) {
    this.chaptersLoading.set(true);
    this.chapterService.getChapterTree(courseId).subscribe({
      next: data => {
        this.chapters.set(data || []);
        this.chaptersLoading.set(false);
        this.collectResources(data || []);
      },
      error: () => {
        this.chaptersLoading.set(false);
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
    if (!this.authService.isAuthenticated) return;
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
          },
        });
      },
    });
  }

  loadRelated(majorId?: string | null) {
    if (!majorId) {
      this.related.set([]);
      return;
    }
    this.courseService.getPublished({
      majorId: majorId,
      skipCount: 0,
      maxResultCount: 20,
    } as any).subscribe({
      next: result => {
        const currentId = this.course()?.id;
        const items = (result.items || [])
          .filter(x => x.id !== currentId)
          // 按选课人数降序排列
          .sort((a, b) => (b.studentCount || 0) - (a.studentCount || 0))
          .slice(0, 4)
          .map(x => ({
            id: x.id!,
            title: x.title || '未命名课程',
            major: x.majorName,
            studentCount: x.studentCount || 0,
            difficulty: x.difficulty || 1,
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
    this.router.navigate(['/student/courses']);
  }

  setTab(tab: TabKey) {
    this.activeTab.set(tab);
  }

  enrollCourse() {
    if (!this.authService.isAuthenticated) return;
    const c = this.course();
    if (!c?.id || this.enrolling()) return;
    this.enrolling.set(true);
    this.courseService.enroll(c.id).subscribe({
      next: () => {
        this.enrolling.set(false);
        this.message.success('选课成功');
        this.course.set({ ...c, isEnrolled: true });
      },
      error: () => {
        this.enrolling.set(false);
        this.message.error('选课失败');
      },
    });
  }

  startLearning() {
    const c = this.course();
    if (!c?.id) return;
    this.router.navigate(['/student/courses', c.id, 'learn']);
  }

  startExercise() {
    const c = this.course();
    if (!c?.id) return;
    this.router.navigate(['/student/courses', c.id, 'learn']);
  }

  viewKnowledgeGraph() {
    this.setTab('graph');
  }

  openRelated(id: string) {
    this.router.navigate(['/student/courses', id]);
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

  /** 计算资源数 */
  countResources(nodes: ChapterDto[]): number {
    let total = 0;
    const walk = (arr: ChapterDto[]) => {
      arr.forEach(n => {
        total += (n.knowledgeResources || []).length;
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
      '#0c4cb8',
      '#1e6ce8',
      '#1e6ce8',
      '#0891b2',
      '#0284c7',
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

  hasCover(c: CourseDetailDto): boolean {
    return !!c.coverImageUrl && c.coverImageUrl.trim().length > 0;
  }

  difficultyLabel(d?: number): string {
    const labels = ['入门', '初级', '中级', '高级', '专家'];
    return labels[(d || 1) - 1] || '未设置';
  }

  difficultyColor(d?: number): string {
    const colors = ['#34d399', '#22c55e', '#3b82f6', '#1e6ce8', '#0c4cb8'];
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
}
