import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzListModule } from 'ng-zorro-antd/list';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import { StudentExerciseRecordService } from '../../proxy/learning/student-exercise-record.service';
import { ChapterProgressDto, SaveExerciseRecordInput, StudentExerciseRecordDto } from '../../proxy/learning/dtos/models';
import { ExerciseDto } from '../../proxy/exams/dtos';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';
import { SelfAssessment } from '../../proxy/learning/enums/self-assessment.enum';
import { ChapterService } from '../../proxy/courses/chapter.service';

interface OptionItem {
  key: string;
  content: string;
}

interface ChapterWithExercises {
  id: string;
  title: string;
  exercises: ExerciseDto[];
}

@Component({
  selector: 'app-exercise-learning',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzRadioModule,
    NzCheckboxModule,
    NzInputModule,
    NzTagModule,
    NzProgressModule,
    NzAlertModule,
    NzIconModule,
    NzListModule,
    NzSpinModule,
    NzSpaceModule,
    NzDividerModule,
    NzTooltipModule
  ],
  templateUrl: './exercise-learning.component.html',
  styleUrls: ['./exercise-learning.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExerciseLearningComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly exerciseService = inject(ExerciseService);
  private readonly recordService = inject(StudentExerciseRecordService);
  private readonly chapterService = inject(ChapterService);
  private readonly message = inject(NzMessageService);

  courseId = signal('');
  loading = signal(true);
  chapters = signal<ChapterWithExercises[]>([]);
  selectedChapterIndex = signal(0);
  currentExerciseIndex = signal(0);
  currentAnswer = '';
  multiSelectedOptions: Record<string, boolean> = {};
  hasSubmitted = signal(false);
  hasViewedAnswer = signal(false);
  selfAssessment = signal<SelfAssessment>(SelfAssessment.None);
  exerciseRecords = signal<Map<string, StudentExerciseRecordDto>>(new Map());
  startTime = signal<Date>(new Date());

  currentChapter = computed(() => this.chapters()[this.selectedChapterIndex()]);
  currentExercises = computed(() => this.currentChapter()?.exercises ?? []);
  currentExercise = computed(() => this.currentExercises()[this.currentExerciseIndex()]);
  totalExercises = computed(() => this.chapters().reduce((sum, ch) => sum + ch.exercises.length, 0));
  completedCount = computed(() => {
    let count = 0;
    this.exerciseRecords().forEach((_, key) => count++);
    return count;
  });
  progressPercent = computed(() =>
    this.totalExercises() > 0 ? Math.round(this.completedCount() / this.totalExercises() * 100) : 0
  );

  protected readonly SelfAssessment = SelfAssessment;

  ngOnInit() {
    const courseId = this.route.snapshot.paramMap.get('courseId');
    if (courseId) {
      this.courseId.set(courseId);
      this.loadData(courseId);
    }
  }

  loadData(courseId: string) {
    this.loading.set(true);

    // Load chapters
    this.chapterService.getChaptersByCourse(courseId).subscribe({
      next: (chapters) => {
        // Load exercises for course
        this.exerciseService.getByCourse(courseId).subscribe({
          next: (exercises) => {
            const chapterMap = new Map<string, ExerciseDto[]>();
            const unassigned: ExerciseDto[] = [];

            for (const ex of exercises) {
              if (ex.chapterId) {
                if (!chapterMap.has(ex.chapterId)) chapterMap.set(ex.chapterId, []);
                chapterMap.get(ex.chapterId)!.push(ex);
              } else {
                unassigned.push(ex);
              }
            }

            const chaptersWithExercises: ChapterWithExercises[] = chapters.map(ch => ({
              id: ch.id!,
              title: ch.title ?? '未命名章节',
              exercises: chapterMap.get(ch.id!) ?? []
            }));

            if (unassigned.length > 0) {
              chaptersWithExercises.push({
                id: 'unassigned',
                title: '未分类习题',
                exercises: unassigned
              });
            }

            this.chapters.set(chaptersWithExercises.filter(ch => ch.exercises.length > 0));
            this.loadRecords(courseId);
          },
          error: () => {
            this.message.error('加载习题失败');
            this.loading.set(false);
          }
        });
      },
      error: () => {
        this.message.error('加载章节失败');
        this.loading.set(false);
      }
    });
  }

  loadRecords(courseId: string) {
    this.recordService.getRecordsByCourse({ courseId, skipCount: 0, maxResultCount: 1000 }).subscribe({
      next: (result) => {
        const map = new Map<string, StudentExerciseRecordDto>();
        for (const r of result.items ?? []) {
          if (r.exerciseId) map.set(r.exerciseId, r);
        }
        this.exerciseRecords.set(map);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  selectChapter(index: number) {
    this.selectedChapterIndex.set(index);
    this.currentExerciseIndex.set(0);
    this.resetCurrentState();
  }

  selectExercise(index: number) {
    this.currentExerciseIndex.set(index);
    this.resetCurrentState();
    this.startTime.set(new Date());
    this.loadCurrentRecordState();
  }

  resetCurrentState() {
    this.currentAnswer = '';
    this.multiSelectedOptions = {};
    this.hasSubmitted.set(false);
    this.hasViewedAnswer.set(false);
    this.selfAssessment.set(SelfAssessment.None);
  }

  loadCurrentRecordState() {
    const exercise = this.currentExercise();
    if (!exercise?.id) return;
    const record = this.exerciseRecords().get(exercise.id);
    if (record) {
      this.hasSubmitted.set(!!record.completedAt);
      this.hasViewedAnswer.set(!!record.hasViewedAnswer);
      this.selfAssessment.set(record.selfAssessment ?? SelfAssessment.None);
      if (record.studentAnswer) {
        this.currentAnswer = record.studentAnswer;
      }
    }
  }

  parseOptions(exercise: ExerciseDto): OptionItem[] {
    if (!exercise.options) return [];
    try {
      return JSON.parse(exercise.options);
    } catch {
      return [];
    }
  }

  submitAnswer() {
    const exercise = this.currentExercise();
    if (!exercise?.id) return;

    let answer = this.currentAnswer;
    if (exercise.type === ExerciseType.MultiChoice) {
      answer = Object.entries(this.multiSelectedOptions)
        .filter(([_, v]) => v)
        .map(([k]) => k)
        .sort()
        .join(',');
    }

    const elapsed = Date.now() - this.startTime().getTime();
    const input: SaveExerciseRecordInput = {
      courseId: this.courseId(),
      chapterId: exercise.chapterId,
      exerciseId: exercise.id,
      studentAnswer: answer,
      timeSpentTicks: elapsed * 10000 // ms to ticks
    };

    this.recordService.saveOrUpdateRecord(input).subscribe({
      next: (record) => {
        const map = new Map(this.exerciseRecords());
        if (record.exerciseId) map.set(record.exerciseId, record);
        this.exerciseRecords.set(map);
        this.hasSubmitted.set(true);
        this.message.success('答案已提交');
      },
      error: () => {
        this.message.error('提交失败');
      }
    });
  }

  viewAnswer() {
    const exercise = this.currentExercise();
    if (!exercise?.id) return;

    this.recordService.markAnswerViewed({
      exerciseId: exercise.id,
      courseId: this.courseId()
    }).subscribe({
      next: () => {
        this.hasViewedAnswer.set(true);
        const map = new Map(this.exerciseRecords());
        const record = map.get(exercise.id!);
        if (record) {
          record.hasViewedAnswer = true;
          map.set(exercise.id!, { ...record });
          this.exerciseRecords.set(map);
        }
      },
      error: () => {
        this.hasViewedAnswer.set(true); // Show anyway
      }
    });
  }

  submitSelfAssessment(assessment: SelfAssessment) {
    const exercise = this.currentExercise();
    if (!exercise?.id) return;

    this.recordService.submitSelfAssessment({
      exerciseId: exercise.id,
      courseId: this.courseId(),
      assessment
    }).subscribe({
      next: () => {
        this.selfAssessment.set(assessment);
        const map = new Map(this.exerciseRecords());
        const record = map.get(exercise.id!);
        if (record) {
          record.selfAssessment = assessment;
          map.set(exercise.id!, { ...record });
          this.exerciseRecords.set(map);
        }
        this.message.success('评估已提交');
      },
      error: () => {
        this.message.error('提交评估失败');
      }
    });
  }

  prevExercise() {
    if (this.currentExerciseIndex() > 0) {
      this.selectExercise(this.currentExerciseIndex() - 1);
    }
  }

  nextExercise() {
    if (this.currentExerciseIndex() < this.currentExercises().length - 1) {
      this.selectExercise(this.currentExerciseIndex() + 1);
    }
  }

  getExerciseStatus(exerciseId: string): 'completed' | 'current' | 'unanswered' {
    const record = this.exerciseRecords().get(exerciseId);
    if (record?.completedAt) return 'completed';
    return 'unanswered';
  }

  getChapterCompletion(chapter: ChapterWithExercises): number {
    if (chapter.exercises.length === 0) return 0;
    const completed = chapter.exercises.filter(e => this.exerciseRecords().get(e.id!)?.completedAt).length;
    return Math.round(completed / chapter.exercises.length * 100);
  }

  goBack() {
    this.router.navigate(['/learning/course-detail', this.courseId()]);
  }

  getTypeName(type: ExerciseType): string {
    const names = ['单选题', '多选题', '判断题', '填空题', '简答题', '论述题', '案例分析'];
    return names[type] || '未知';
  }

  getTypeColor(type: ExerciseType): string {
    const colors = ['blue', 'purple', 'cyan', 'orange', 'gold', 'red', 'green'];
    return colors[type] || 'default';
  }

  getDifficultyColor(difficulty: number): string {
    const colors = ['green', 'lime', 'gold', 'orange', 'red'];
    return colors[difficulty - 1] || 'default';
  }

  getDifficultyLabel(difficulty: number): string {
    const labels = ['入门', '初级', '中级', '高级', '专家'];
    return labels[difficulty - 1] || '未知';
  }

  getSelfAssessmentLabel(assessment: SelfAssessment): string {
    const labels = ['未评估', '正确', '部分正确', '错误'];
    return labels[assessment] ?? '未评估';
  }

  getSelfAssessmentColor(assessment: SelfAssessment): string {
    const colors = ['default', 'green', 'gold', 'red'];
    return colors[assessment] ?? 'default';
  }
}
