import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import type { ChapterDto, CourseDto } from '../../proxy/courses/dtos/models';
import {
  EXAM_STATUS_LABELS,
  EXAM_TYPE_LABELS,
  EXERCISE_TYPE_LABELS,
  ExamApiService,
  ExamDetail,
  ExamListItem,
  ExamStatus,
  ExamSubmissionItem,
  ExamType,
  ExerciseType,
  StudentExamResult,
  isSubjective,
  parseExerciseOptions,
} from './exam.service';

interface RuleRow {
  type: ExerciseType;
  count: number;
  scorePerItem: number;
}

interface GradeRow {
  exerciseId: string;
  title: string;
  type: ExerciseType;
  questionContent: string;
  options?: string;
  content?: string;
  standardAnswer?: string;
  maxScore: number;
  score: number | null;
  feedback: string;
  isCorrect?: boolean;
  isAiGraded: boolean;
}

const OBJECTIVE_TYPES = [
  ExerciseType.SingleChoice,
  ExerciseType.MultiChoice,
  ExerciseType.TrueFalse,
  ExerciseType.FillBlank,
];

@Component({
  selector: 'app-exam-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzPopconfirmModule,
    NzPaginationModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTabsModule,
    NzTagModule,
    NzTooltipModule,
    NzRadioModule,
    NzDatePickerModule,
  ],
  templateUrl: './exam-management.component.html',
  styleUrls: ['./exam-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExamManagementComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);
  private readonly examApi = inject(ExamApiService);
  private readonly message = inject(NzMessageService);

  readonly exerciseTypes = OBJECTIVE_TYPES.concat([
    ExerciseType.ShortAnswer,
    ExerciseType.Essay,
    ExerciseType.CaseAnalysis,
    ExerciseType.TermExplanation,
    ExerciseType.Calculation,
    ExerciseType.Programming,
  ]);
  readonly examTypes = [ExamType.Quiz, ExamType.ChapterTest, ExamType.Midterm, ExamType.Final];

  // ── 列表 ──
  readonly courses = signal<CourseDto[]>([]);
  readonly chapters = signal<ChapterDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly exams = signal<ExamListItem[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly keyword = signal('');
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  readonly hasCourse = computed(() => !!this.selectedCourseId());
  readonly selectedCourse = computed(
    () => this.courses().find((c) => c.id === this.selectedCourseId()) ?? null,
  );

  // ── 组卷弹窗 ──
  readonly formVisible = signal(false);
  readonly submitting = signal(false);
  readonly mode = signal<'rules' | 'total'>('rules');
  readonly formTitle = signal('');
  readonly formDescription = signal('');
  readonly formType = signal<ExamType>(ExamType.Quiz);
  readonly formDuration = signal(60);
  readonly formPassing = signal(60);
  readonly formChapterId = signal<string | null>(null);
  readonly formStartTime = signal<Date | null>(null);
  readonly formEndTime = signal<Date | null>(null);
  readonly formRules = signal<RuleRow[]>([
    { type: ExerciseType.SingleChoice, count: 5, scorePerItem: 5 },
  ]);
  readonly formTargetScore = signal(100);
  readonly formScopeTypes = signal<ExerciseType[]>([
    ExerciseType.SingleChoice,
    ExerciseType.MultiChoice,
    ExerciseType.TrueFalse,
  ]);

  readonly ruleTotalCount = computed(() =>
    this.formRules().reduce((sum, r) => sum + (r.count || 0), 0),
  );
  readonly ruleTotalScore = computed(() =>
    this.formRules().reduce((sum, r) => sum + (r.count || 0) * (r.scorePerItem || 0), 0),
  );

  // ── 试卷详情抽屉 ──
  readonly detailVisible = signal(false);
  readonly detail = signal<ExamDetail | null>(null);
  readonly detailLoading = signal(false);

  // ── 判卷抽屉 ──
  readonly gradeVisible = signal(false);
  readonly gradingExam = signal<ExamListItem | null>(null);
  readonly submissions = signal<ExamSubmissionItem[]>([]);
  readonly submissionsLoading = signal(false);
  readonly submissionTotal = signal(0);
  readonly submissionPage = signal(1);
  readonly gradeRows = signal<GradeRow[]>([]);
  readonly gradingStudentExamId = signal<string | null>(null);
  readonly gradingStudentName = signal('');
  readonly gradingSaving = signal(false);
  readonly gradingAi = signal(false);

  readonly gradeTotal = computed(() =>
    this.gradeRows().reduce((sum, r) => sum + (r.score ?? 0), 0),
  );
  readonly gradeMaxScore = computed(() =>
    this.gradeRows().reduce((sum, r) => sum + r.maxScore, 0),
  );

  ngOnInit(): void {
    this.loadCourses();
  }

  // ── 数据加载 ──
  loadCourses(): void {
    this.courseService.getList({ maxResultCount: 200, skipCount: 0 } as any).subscribe({
      next: (result) => this.courses.set(result.items || []),
      error: () => this.message.error('加载课程失败'),
    });
  }

  onCourseChange(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    this.pageIndex.set(1);
    this.exams.set([]);
    this.totalCount.set(0);
    this.chapters.set([]);
    if (!courseId) return;

    this.chapterService.getChapterTree(courseId).subscribe({
      next: (tree) => this.chapters.set(this.flattenChapters(tree || [])),
      error: () => {},
    });
    this.loadExams();
  }

  private flattenChapters(tree: ChapterDto[]): ChapterDto[] {
    const out: ChapterDto[] = [];
    const walk = (nodes: ChapterDto[], prefix: string) => {
      for (const node of nodes || []) {
        const title = `${prefix}${node.title || '未命名章节'}`;
        out.push({ ...node, title });
        if (node.children?.length) walk(node.children, `${title} / `);
      }
    };
    walk(tree, '');
    return out;
  }

  loadExams(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.examApi
      .getList({
        courseId,
        filter: this.keyword().trim() || undefined,
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      })
      .subscribe({
        next: (result) => {
          this.exams.set(result.items || []);
          this.totalCount.set(result.totalCount || 0);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载试卷失败');
        },
      });
  }

  onSearch(): void {
    this.pageIndex.set(1);
    this.loadExams();
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
    this.loadExams();
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
    this.loadExams();
  }

  // ── 组卷 ──
  openForm(): void {
    if (!this.hasCourse()) {
      this.message.warning('请先选择课程');
      return;
    }
    this.mode.set('rules');
    this.formTitle.set('');
    this.formDescription.set('');
    this.formType.set(ExamType.Quiz);
    this.formDuration.set(60);
    this.formPassing.set(60);
    this.formChapterId.set(null);
    this.formStartTime.set(null);
    this.formEndTime.set(null);
    this.formRules.set([{ type: ExerciseType.SingleChoice, count: 5, scorePerItem: 5 }]);
    this.formTargetScore.set(100);
    this.formScopeTypes.set([
      ExerciseType.SingleChoice,
      ExerciseType.MultiChoice,
      ExerciseType.TrueFalse,
    ]);
    this.formVisible.set(true);
  }

  closeForm(): void {
    if (this.submitting()) return;
    this.formVisible.set(false);
  }

  addRule(): void {
    this.formRules.set([
      ...this.formRules(),
      { type: ExerciseType.TrueFalse, count: 3, scorePerItem: 5 },
    ]);
  }

  removeRule(index: number): void {
    this.formRules.set(this.formRules().filter((_, i) => i !== index));
  }

  updateRule(index: number, patch: Partial<RuleRow>): void {
    this.formRules.set(
      this.formRules().map((row, i) => (i === index ? { ...row, ...patch } : row)),
    );
  }

  submitForm(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    const title = this.formTitle().trim();
    if (!title) {
      this.message.warning('请填写试卷标题');
      return;
    }

    if (this.mode() === 'rules') {
      const rules = this.formRules().filter((r) => r.count > 0 && r.scorePerItem > 0);
      if (rules.length === 0) {
        this.message.warning('请至少配置一条组卷规则（题型 + 数量 + 每题分）');
        return;
      }
    } else if (this.formTargetScore() <= 0) {
      this.message.warning('请填写目标总分');
      return;
    }

    this.submitting.set(true);
    this.examApi
      .generate({
        courseId,
        chapterId: this.formChapterId(),
        title,
        description: this.formDescription().trim() || null,
        type: this.formType(),
        durationMinutes: this.formDuration() || 60,
        passingScore: this.formPassing() || 60,
        startTime: this.formStartTime() ? this.formStartTime()!.toISOString() : null,
        endTime: this.formEndTime() ? this.formEndTime()!.toISOString() : null,
        rules: this.mode() === 'rules' ? this.formRules() : this.formScopeTypes().map((t) => ({ type: t, count: 0, scorePerItem: 0 })),
        targetTotalScore: this.mode() === 'total' ? this.formTargetScore() : null,
        shuffle: true,
      })
      .subscribe({
        next: (exam) => {
          this.submitting.set(false);
          this.formVisible.set(false);
          this.message.success(`组卷成功：${exam.exerciseCount} 题，总分 ${exam.totalScore}`);
          this.pageIndex.set(1);
          this.loadExams();
        },
        error: (err) => {
          this.submitting.set(false);
          this.message.error(this.errorText(err, '组卷失败'));
        },
      });
  }

  // ── 详情 ──
  openDetail(exam: ExamListItem): void {
    this.detailVisible.set(true);
    this.detailLoading.set(true);
    this.examApi.get(exam.id).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.detailLoading.set(false);
      },
      error: () => {
        this.detailLoading.set(false);
        this.message.error('加载试卷详情失败');
      },
    });
  }

  closeDetail(): void {
    this.detailVisible.set(false);
    this.detail.set(null);
  }

  deleteExam(exam: ExamListItem): void {
    this.examApi.delete(exam.id).subscribe({
      next: () => {
        this.message.success('试卷已删除');
        this.loadExams();
      },
      error: (err) => this.message.error(this.errorText(err, '删除失败')),
    });
  }

  // ── 判卷 ──
  openGrade(exam: ExamListItem): void {
    this.gradingExam.set(exam);
    this.gradeVisible.set(true);
    this.gradeRows.set([]);
    this.gradingStudentExamId.set(null);
    this.gradingStudentName.set('');
    this.submissionPage.set(1);
    this.loadSubmissions();
  }

  closeGrade(): void {
    this.gradeVisible.set(false);
    this.gradingExam.set(null);
    this.gradeRows.set([]);
    this.gradingStudentExamId.set(null);
    this.loadExams();
  }

  loadSubmissions(): void {
    const exam = this.gradingExam();
    if (!exam) return;

    this.submissionsLoading.set(true);
    this.examApi
      .getSubmissions({
        examId: exam.id,
        skipCount: (this.submissionPage() - 1) * 10,
        maxResultCount: 10,
      })
      .subscribe({
        next: (result) => {
          this.submissions.set(result.items || []);
          this.submissionTotal.set(result.totalCount || 0);
          this.submissionsLoading.set(false);
        },
        error: () => {
          this.submissionsLoading.set(false);
          this.message.error('加载提交记录失败');
        },
      });
  }

  onSubmissionPageChange(index: number): void {
    this.submissionPage.set(index);
    this.loadSubmissions();
  }

  selectSubmission(item: ExamSubmissionItem): void {
    this.gradingStudentExamId.set(item.studentExamId);
    this.gradingStudentName.set(item.studentName || '');
    this.examApi.getSubmission(item.studentExamId).subscribe({
      next: (result) => this.gradeRows.set(this.toGradeRows(result)),
      error: () => this.message.error('加载答卷失败'),
    });
  }

  private toGradeRows(result: StudentExamResult): GradeRow[] {
    return (result.answers || [])
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((a) => ({
        exerciseId: a.exerciseId,
        title: a.title,
        type: a.type,
        questionContent: a.questionContent,
        options: a.options,
        content: a.content,
        standardAnswer: a.standardAnswer,
        maxScore: a.maxScore,
        score: a.score ?? null,
        feedback: a.feedback || '',
        isCorrect: a.isCorrect,
        isAiGraded: a.isAiGraded,
      }));
  }

  setGradeScore(exerciseId: string, score: number | null): void {
    this.gradeRows.set(
      this.gradeRows().map((row) =>
        row.exerciseId === exerciseId ? { ...row, score } : row,
      ),
    );
  }

  setGradeFeedback(exerciseId: string, feedback: string): void {
    this.gradeRows.set(
      this.gradeRows().map((row) =>
        row.exerciseId === exerciseId ? { ...row, feedback } : row,
      ),
    );
  }

  /** AI 辅助判分：后端自动判客观题 + 主观题 AI 打分，回填到表单后教师可再改 */
  runAutoGrade(): void {
    const id = this.gradingStudentExamId();
    if (!id) return;

    this.gradingAi.set(true);
    this.examApi.autoGrade(id).subscribe({
      next: (result) => {
        this.gradingAi.set(false);
        this.gradeRows.set(this.toGradeRows(result));
        this.message.success('已自动判分（客观题按标准答案，主观题 AI 辅助），可继续人工调整');
      },
      error: (err) => {
        this.gradingAi.set(false);
        this.message.error(this.errorText(err, '自动判分失败'));
      },
    });
  }

  saveGrade(): void {
    const id = this.gradingStudentExamId();
    if (!id) return;

    this.gradingSaving.set(true);
    this.examApi
      .grade(id, {
        answers: this.gradeRows().map((row) => ({
          exerciseId: row.exerciseId,
          score: row.score,
          feedback: row.feedback || null,
        })),
      })
      .subscribe({
        next: (result) => {
          this.gradingSaving.set(false);
          this.message.success(
            `判卷完成：${result.totalScore ?? 0}/${result.maxScore} 分${result.isPassed ? '（及格）' : '（不及格）'}`,
          );
          this.loadSubmissions();
        },
        error: (err) => {
          this.gradingSaving.set(false);
          this.message.error(this.errorText(err, '保存判卷失败'));
        },
      });
  }

  // ── 展示辅助 ──
  exerciseTypeLabel(type: ExerciseType): string {
    return EXERCISE_TYPE_LABELS[type] || '题目';
  }

  examTypeLabel(type: ExamType): string {
    return EXAM_TYPE_LABELS[type] || '测验';
  }

  statusLabel(status: ExamStatus): string {
    return EXAM_STATUS_LABELS[status] || '';
  }

  statusColor(status: ExamStatus): string {
    switch (status) {
      case ExamStatus.Graded:
        return 'green';
      case ExamStatus.Submitted:
        return 'orange';
      case ExamStatus.InProgress:
        return 'blue';
      default:
        return 'default';
    }
  }

  isSubjectiveType(type: ExerciseType): boolean {
    return isSubjective(type);
  }

  parseOptions(options?: string): { key: string; content: string }[] {
    return parseExerciseOptions(options);
  }

  letters(index: number): string {
    return String.fromCharCode(65 + index);
  }

  private errorText(err: any, fallback: string): string {
    return err?.error?.error?.message || err?.error?.message || err?.message || fallback;
  }
}
