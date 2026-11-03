import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import {
  EXAM_STATUS_LABELS,
  EXAM_TYPE_LABELS,
  EXERCISE_TYPE_LABELS,
  ExamApiService,
  ExamPaperQuestion,
  ExamStatus,
  ExerciseType,
  StudentExamPaper,
  StudentExamResult,
  isSubjective,
  parseExerciseOptions,
} from '../../learning/exam-management/exam.service';

interface AnswerState {
  exerciseId: string;
  content: string;
  selected: string[];
  trueFalse: '正确' | '错误' | null;
}

@Component({
  selector: 'app-student-exam-take',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzRadioModule,
    NzSpinModule,
    NzTagModule,
    NzCheckboxModule,
  ],
  templateUrl: './student-exam-take.component.html',
  styleUrls: ['./student-exam-take.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentExamTakeComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly examApi = inject(ExamApiService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly loading = signal(true);
  readonly errorText = signal('');
  readonly paper = signal<StudentExamPaper | null>(null);
  readonly result = signal<StudentExamResult | null>(null);
  readonly submitting = signal(false);
  readonly answers = signal<Record<string, AnswerState>>({});
  readonly remainingSeconds = signal<number | null>(null);
  private timerHandle: any = null;

  readonly isViewMode = computed(() => !!this.result());

  /** 已判部分得分（未评卷时用于展示客观题得分） */
  readonly currentScore = computed(() =>
    (this.result()?.answers ?? []).reduce((sum, a) => sum + (a.score ?? 0), 0),
  );

  /** 是否存在尚未判分的主观题 */
  readonly hasUngradedSubjective = computed(() =>
    (this.result()?.answers ?? []).some(
      (a) => isSubjective(a.type) && (a.score === null || a.score === undefined),
    ),
  );
  readonly answeredCount = computed(
    () => Object.values(this.answers()).filter((a) => this.hasAnswer(a)).length,
  );
  readonly questionCount = computed(() => this.paper()?.questions.length ?? 0);

  ngOnInit(): void {
    const examId = this.route.snapshot.paramMap.get('examId') || '';
    const studentExamId = this.route.snapshot.queryParamMap.get('studentExamId');
    const view = this.route.snapshot.queryParamMap.get('view') === '1';

    if (view && studentExamId) {
      this.loadResult(studentExamId);
      return;
    }
    this.startExam(examId);
  }

  ngOnDestroy(): void {
    this.stopTimer();
  }

  // ── 加载 ──
  private loadResult(studentExamId: string): void {
    this.examApi.getMyResult(studentExamId).subscribe({
      next: (result) => {
        this.result.set(result);
        this.loading.set(false);
      },
      error: (err) => {
        this.errorText.set(this.errorMessage(err, '加载答卷失败'));
        this.loading.set(false);
      },
    });
  }

  private startExam(examId: string): void {
    if (!examId) {
      this.errorText.set('缺少试卷参数');
      this.loading.set(false);
      return;
    }
    this.examApi.start(examId).subscribe({
      next: (paper) => {
        this.paper.set(paper);
        this.initAnswers(paper);
        this.loading.set(false);
        this.startTimer(paper);
      },
      error: (err) => {
        // 已提交过的试卷：引导查看结果
        const studentExamId = this.route.snapshot.queryParamMap.get('studentExamId');
        if (studentExamId) {
          this.loadResult(studentExamId);
          return;
        }
        this.errorText.set(this.errorMessage(err, '无法开始作答'));
        this.loading.set(false);
      },
    });
  }

  private initAnswers(paper: StudentExamPaper): void {
    const map: Record<string, AnswerState> = {};
    for (const q of paper.questions) {
      map[q.exerciseId] = {
        exerciseId: q.exerciseId,
        content: q.myAnswer || '',
        selected: this.parseSelected(q, q.myAnswer),
        trueFalse: this.parseTrueFalse(q.myAnswer),
      };
    }
    this.answers.set(map);
  }

  private parseSelected(q: ExamPaperQuestion, value?: string): string[] {
    if (!value) return [];
    const letters = (value.toUpperCase().match(/[A-Z]/g) || []).filter(
      (x, i, arr) => arr.indexOf(x) === i,
    );
    return letters;
  }

  private parseTrueFalse(value?: string): '正确' | '错误' | null {
    if (!value) return null;
    const v = value.trim().toLowerCase();
    if (['正确', '对', 'true', 't', '1', '√', '是'].includes(v)) return '正确';
    if (['错误', '错', 'false', 'f', '0', '×', '否'].includes(v)) return '错误';
    return null;
  }

  // ── 倒计时 ──
  private startTimer(paper: StudentExamPaper): void {
    if (!paper.startedAt || paper.durationMinutes <= 0) return;
    const end = new Date(paper.startedAt).getTime() + paper.durationMinutes * 60_000;

    const tick = () => {
      const left = Math.max(0, Math.floor((end - Date.now()) / 1000));
      this.remainingSeconds.set(left);
      if (left <= 0) {
        this.stopTimer();
        this.message.warning('考试时间已到，正在自动提交');
        this.submit(true);
      }
    };
    tick();
    this.timerHandle = setInterval(tick, 1000);
  }

  private stopTimer(): void {
    if (this.timerHandle) {
      clearInterval(this.timerHandle);
      this.timerHandle = null;
    }
  }

  timerText(): string {
    const seconds = this.remainingSeconds();
    if (seconds === null) return '';
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }

  // ── 作答 ──
  hasAnswer(state: AnswerState | undefined): boolean {
    if (!state) return false;
    return (
      !!state.content.trim() || state.selected.length > 0 || state.trueFalse !== null
    );
  }

  onTextChange(exerciseId: string, value: string): void {
    this.updateAnswer(exerciseId, (a) => {
      a.content = value;
    });
  }

  onSingleSelect(exerciseId: string, letter: string): void {
    this.updateAnswer(exerciseId, (a) => {
      a.selected = [letter];
      a.content = letter;
    });
  }

  onMultiToggle(exerciseId: string, letter: string, checked: boolean): void {
    this.updateAnswer(exerciseId, (a) => {
      const set = new Set(a.selected);
      if (checked) set.add(letter);
      else set.delete(letter);
      a.selected = [...set].sort();
      a.content = a.selected.join(',');
    });
  }

  onTrueFalse(exerciseId: string, value: '正确' | '错误'): void {
    this.updateAnswer(exerciseId, (a) => {
      a.trueFalse = value;
      a.content = value;
    });
  }

  private updateAnswer(exerciseId: string, mutate: (a: AnswerState) => void): void {
    const current = this.answers();
    const existing = current[exerciseId] ?? {
      exerciseId,
      content: '',
      selected: [],
      trueFalse: null,
    };
    const next = { ...existing };
    mutate(next);
    this.answers.set({ ...current, [exerciseId]: next });
  }

  // ── 提交 ──
  confirmSubmit(): void {
    const unanswered = this.questionCount() - this.answeredCount();
    this.modal.confirm({
      nzTitle: '确认提交试卷？',
      nzContent:
        unanswered > 0
          ? `还有 ${unanswered} 道题未作答，提交后不能修改。客观题会立即判分，主观题等待老师评卷。`
          : '提交后不能修改。客观题会立即判分，主观题等待老师评卷。',
      nzOkText: '提交',
      nzCancelText: '再检查一下',
      nzOnOk: () => this.submit(false),
    });
  }

  private submit(auto: boolean): void {
    const paper = this.paper();
    if (!paper || this.submitting()) return;

    this.stopTimer();
    this.submitting.set(true);

    const payload = paper.questions.map((q) => {
      const state = this.answers()[q.exerciseId];
      return {
        exerciseId: q.exerciseId,
        content: state && this.hasAnswer(state) ? state.content : null,
      };
    });

    this.examApi.submit(paper.studentExamId, payload).subscribe({
      next: (result) => {
        this.submitting.set(false);
        this.result.set(result);
        this.message.success(
          auto ? '已自动提交' : `提交成功：客观题得分 ${result.totalScore ?? 0} 分（主观题待评卷）`,
        );
      },
      error: (err) => {
        this.submitting.set(false);
        this.message.error(this.errorMessage(err, '提交失败，请重试'));
      },
    });
  }

  // ── 展示辅助 ──
  isSubjectiveType(type: ExerciseType): boolean {
    return isSubjective(type);
  }

  typeLabel(type: ExerciseType): string {
    return EXERCISE_TYPE_LABELS[type] || '题目';
  }

  examStatusLabel(status: ExamStatus): string {
    return EXAM_STATUS_LABELS[status] || '';
  }

  examTypeLabel(type: number): string {
    return EXAM_TYPE_LABELS[type] || '';
  }

  letters(index: number): string {
    return String.fromCharCode(65 + index);
  }

  options(question: { options?: string }): { key: string; content: string }[] {
    return parseExerciseOptions(question.options);
  }

  answerState(exerciseId: string): AnswerState {
    return (
      this.answers()[exerciseId] ?? {
        exerciseId,
        content: '',
        selected: [],
        trueFalse: null,
      }
    );
  }

  goBack(): void {
    this.router.navigate(['..'], { relativeTo: this.route }).catch(() => {
      this.router.navigateByUrl('/student/courses');
    });
  }

  backToCourse(): void {
    this.router.navigateByUrl('/student/courses');
  }

  private errorMessage(err: any, fallback: string): string {
    return err?.error?.error?.message || err?.error?.message || err?.message || fallback;
  }
}
