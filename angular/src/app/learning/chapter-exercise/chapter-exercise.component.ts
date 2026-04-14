import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import type { CourseDto, ChapterDto } from '../../proxy/courses/dtos/models';
import type { CreateUpdateExerciseDto, ExerciseDto, ExerciseImportResultDto } from '../../proxy/exams/dtos/models';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';

@Component({
  selector: 'app-chapter-exercise',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzTagModule,
    NzIconModule,
    NzSpinModule,
    NzEmptyModule,
    NzSelectModule,
    NzModalModule,
  ],
  templateUrl: './chapter-exercise.component.html',
  styleUrls: ['./chapter-exercise.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChapterExerciseComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);
  private readonly exerciseService = inject(ExerciseService);
  private readonly message = inject(NzMessageService);
  private readonly cdr = inject(ChangeDetectorRef);

  courses = signal<CourseDto[]>([]);
  selectedCourseId = signal<string | null>(null);
  chapters = signal<ChapterDto[]>([]);
  expandedNodes = signal<Set<string>>(new Set());
  selectedChapterId = signal<string | null>(null);
  selectedChapterTitle = signal('');

  chapterExercises = signal<ExerciseDto[]>([]);
  courseExercises = signal<ExerciseDto[]>([]);
  loading = signal(false);
  searchText = signal('');

  // Available exercises: course exercises not linked to this chapter
  availableExercises = computed(() => {
    const linked = new Set(this.chapterExercises().map(e => e.id));
    const search = this.searchText().toLowerCase();
    let available = this.courseExercises().filter(e => !linked.has(e.id));
    if (search) {
      available = available.filter(e =>
        e.title?.toLowerCase().includes(search) ||
        e.questionContent?.toLowerCase().includes(search)
      );
    }
    return available;
  });

  ngOnInit() {
    this.loadCourses();
  }

  loadCourses() {
    this.courseService.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe({
      next: (result) => {
        this.courses.set(result.items || []);
      },
    });
  }

  onCourseSelected(courseId: string) {
    this.selectedCourseId.set(courseId);
    this.selectedChapterId.set(null);
    this.selectedChapterTitle.set('');
    this.chapterExercises.set([]);
    this.searchText.set('');
    this.loadChapterTree();
    this.loadCourseExercises();
  }

  loadChapterTree() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.chapterService.getChapterTree(courseId).subscribe({
      next: (data) => {
        this.chapters.set(data || []);
        const expanded = new Set<string>();
        this.collectNodeIds(data || [], expanded);
        this.expandedNodes.set(expanded);
      },
    });
  }

  loadCourseExercises() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.exerciseService.getByCourse(courseId).subscribe({
      next: (data) => {
        this.courseExercises.set(data || []);
      },
    });
  }

  private collectNodeIds(nodes: ChapterDto[], set: Set<string>) {
    for (const node of nodes) {
      if (node.id) set.add(node.id);
      if (node.children?.length) this.collectNodeIds(node.children, set);
    }
  }

  selectChapter(chapter: ChapterDto) {
    this.selectedChapterId.set(chapter.id ?? null);
    this.selectedChapterTitle.set(chapter.title ?? '');
    this.loadChapterExercises();
  }

  loadChapterExercises() {
    const chapterId = this.selectedChapterId();
    if (!chapterId) return;

    this.loading.set(true);
    this.exerciseService.getByChapter(chapterId).subscribe({
      next: (data) => {
        this.chapterExercises.set(data || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载习题失败');
      },
    });
  }

  toggleNode(nodeId: string) {
    const current = new Set(this.expandedNodes());
    if (current.has(nodeId)) {
      current.delete(nodeId);
    } else {
      current.add(nodeId);
    }
    this.expandedNodes.set(current);
  }

  isExpanded(nodeId: string): boolean {
    return this.expandedNodes().has(nodeId);
  }

  hasChildren(node: ChapterDto): boolean {
    return !!node.children && node.children.length > 0;
  }

  linkExercise(exercise: ExerciseDto) {
    const chapterId = this.selectedChapterId();
    if (!chapterId || !exercise.id) return;

    const dto: CreateUpdateExerciseDto = {
      courseId: exercise.courseId,
      chapterId: chapterId,
      title: exercise.title,
      questionContent: exercise.questionContent,
      type: exercise.type,
      options: exercise.options,
      answer: exercise.answer,
      answerExplanation: exercise.answerExplanation,
      difficulty: exercise.difficulty,
      score: exercise.score,
    };

    this.exerciseService.update(exercise.id, dto).subscribe({
      next: () => {
        this.message.success('习题已关联到章节');
        this.loadChapterExercises();
      },
      error: () => {
        this.message.error('关联失败');
      },
    });
  }

  unlinkExercise(exercise: ExerciseDto) {
    if (!exercise.id) return;

    const dto: CreateUpdateExerciseDto = {
      courseId: exercise.courseId,
      chapterId: null,
      title: exercise.title,
      questionContent: exercise.questionContent,
      type: exercise.type,
      options: exercise.options,
      answer: exercise.answer,
      answerExplanation: exercise.answerExplanation,
      difficulty: exercise.difficulty,
      score: exercise.score,
    };

    this.exerciseService.update(exercise.id, dto).subscribe({
      next: () => {
        this.message.success('已取消关联');
        this.loadChapterExercises();
      },
      error: () => {
        this.message.error('取消关联失败');
      },
    });
  }

  getTypeName(type: ExerciseType | undefined): string {
    if (type === undefined) return '未知';
    const names: Record<number, string> = {
      [ExerciseType.SingleChoice]: '单选题',
      [ExerciseType.MultiChoice]: '多选题',
      [ExerciseType.TrueFalse]: '判断题',
      [ExerciseType.FillBlank]: '填空题',
      [ExerciseType.ShortAnswer]: '问答题',
      [ExerciseType.Essay]: '论述题',
      [ExerciseType.CaseAnalysis]: '案例分析',
    };
    return names[type] ?? '未知';
  }

  getTypeColor(type: ExerciseType | undefined): string {
    if (type === undefined) return 'default';
    const colors: Record<number, string> = {
      [ExerciseType.SingleChoice]: 'blue',
      [ExerciseType.MultiChoice]: 'purple',
      [ExerciseType.TrueFalse]: 'cyan',
      [ExerciseType.FillBlank]: 'orange',
      [ExerciseType.ShortAnswer]: 'gold',
      [ExerciseType.Essay]: 'red',
      [ExerciseType.CaseAnalysis]: 'green',
    };
    return colors[type] ?? 'default';
  }

  // Import modal state
  isImportModalVisible = false;
  importing = false;
  selectedImportFile: File | null = null;

  openImportModal() {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    this.isImportModalVisible = true;
    this.selectedImportFile = null;
  }

  closeImportModal() {
    this.isImportModalVisible = false;
    this.selectedImportFile = null;
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      const isExcel = file.name.endsWith('.xlsx') || file.name.endsWith('.xls');
      if (!isExcel) {
        this.message.error('只能上传 Excel 文件 (.xlsx, .xls)');
        return;
      }
      const isLt10M = file.size / 1024 / 1024 < 10;
      if (!isLt10M) {
        this.message.error('文件大小不能超过 10MB');
        return;
      }

      this.selectedImportFile = file;
      this.cdr.markForCheck();
    }
  }

  handleImport() {
    if (!this.selectedImportFile) {
      this.message.warning('请先选择文件');
      return;
    }

    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.importing = true;
    const file = this.selectedImportFile;

    this.exerciseService.importFromExcel(courseId, file).subscribe({
      next: (result: ExerciseImportResultDto) => {
        this.importing = false;
        if (result.failCount === 0) {
          this.message.success(`导入成功！共导入 ${result.successCount} 个习题`);
          this.closeImportModal();
          this.loadCourseExercises();
        } else {
          const errorMsg = result.errors?.slice(0, 5).join('\n') || '';
          this.message.warning(`导入完成：成功 ${result.successCount}，失败 ${result.failCount}\n${errorMsg}`);
        }
      },
      error: (err) => {
        this.importing = false;
        this.message.error('导入失败: ' + (err.message || '未知错误'));
      },
    });
  }
}
