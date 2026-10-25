import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';

import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { MasteryBarComponent } from '../../learning-analytics/components/mastery-bar/mastery-bar.component';
import { MasteryLevel } from '../../learning-analytics/models/learning-analytics.models';
import { PracticeService } from '../../proxy/learning-analytics/practice.service';
import {
  ANSWER_MODE,
  AnswerMode,
  EMPTY_TEXT,
  EXERCISE_TYPE_LABELS,
  FEEDBACK_TEXT,
  PRACTICE_TAKE_SIZE,
} from './practice-labels';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';
import type {
  KnowledgeMasteryItemDto,
  PracticeExerciseDto,
  PracticeSetDto,
} from '../../proxy/learning-analytics/dtos/models';

/** 一个选项 */
interface OptionItem {
  key: string;
  content: string;
}

/** 一道题的作答明细（结果页逐题回顾） */
interface AnswerRecord {
  exerciseId: string;
  title: string;
  answer: string | null;
  isCorrect: boolean | null;
  correctAnswer?: string | null;
  analysis?: string | null;
}

/**
 * 学生端「专项练习」。
 *
 * 数据全部来自生成的代理服务（`proxy/learning-analytics/practice.service`）：
 *   · GET  /api/app/practice/set/{courseId}   → 取题
 *   · POST /api/app/practice/submit           → 提交并判分（掌握率由后端重算）
 *
 * 流程：题干 → 选择/作答 → 提交 → 判分反馈与解析 → 结果汇总。
 */
@Component({
  selector: 'app-student-practice',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    MasteryBarComponent,
  ],
  templateUrl: './student-practice.component.html',
  styleUrls: ['./student-practice.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentPracticeComponent implements OnInit {
  private readonly service = inject(PracticeService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly submitting = signal(false);
  readonly submitFailed = signal(false);

  readonly set = signal<PracticeSetDto | null>(null);
  readonly currentIndex = signal(0);
  readonly answer = signal<string>('');
  readonly multiSelected = signal<Record<string, boolean>>({});
  /** 当前题的判定结果（undefined = 未提交/可作答，null = 主观题待批改） */
  readonly feedback = signal<Record<string, boolean | null>>({});
  readonly records = signal<AnswerRecord[]>([]);
  /** 最新一次提交回写的知识点掌握率 */
  readonly mastery = signal<KnowledgeMasteryItemDto[]>([]);
  readonly showSummary = signal(false);

  private courseId = '';
  private knowledgePointId = '';
  private questionStartedAt = Date.now();

  readonly emptyText = EMPTY_TEXT;
  readonly feedbackText = FEEDBACK_TEXT;

  /** URL 传入的知识点名（接口未返回时作为标题） */
  private knowledgePointName = '';

  // ══════════════ 计算属性 ══════════════

  readonly current = computed<PracticeExerciseDto | null>(() => {
    const items = this.set()?.items ?? [];
    return items[this.currentIndex()] ?? null;
  });

  readonly total = computed(() => this.set()?.items?.length ?? 0);

  readonly progress = computed(() => ({
    current: Math.min(this.currentIndex() + 1, this.total()),
    total: this.total(),
  }));

  readonly progressPercent = computed(() => {
    const { current, total } = this.progress();
    return total > 0 ? Math.round((current / total) * 100) : 0;
  });

  readonly currentFeedback = computed(() => {
    const question = this.current();
    return question?.id ? this.feedback()[question.id] : undefined;
  });

  readonly canSubmit = computed(() => {
    const question = this.current();
    if (!question || this.submitting()) return false;
    if (this.answerMode(question) === ANSWER_MODE.multi) {
      return Object.values(this.multiSelected()).some(Boolean);
    }
    return this.answer().trim().length > 0;
  });

  readonly stats = computed(() => {
    const list = this.records();
    return {
      total: list.length,
      correct: list.filter(r => r.isCorrect === true).length,
      wrong: list.filter(r => r.isCorrect === false).length,
      pending: list.filter(r => r.isCorrect === null).length,
    };
  });

  readonly correctRate = computed(() => {
    const { correct, total } = this.stats();
    return total > 0 ? Math.round((correct / total) * 100) : 0;
  });

  /** 练习标题：优先用 URL 传入的知识点名，其次用接口返回 */
  readonly title = computed(
    () => this.set()?.knowledgePointName || this.knowledgePointName || '薄弱点专项练习',
  );

  // ══════════════ 生命周期 ══════════════

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    this.courseId = params.get('courseId') || '';
    this.knowledgePointId = params.get('knowledgePointId') || '';
    this.knowledgePointName = params.get('kpName') || '';

    if (!this.courseId) {
      this.loading.set(false);
      return;
    }

    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.service.getSet(this.courseId, this.knowledgePointId || undefined, PRACTICE_TAKE_SIZE).subscribe({
      next: set => {
        this.set.set(set);
        this.loading.set(false);
        this.questionStartedAt = Date.now();
      },
      error: () => {
        this.set.set(null);
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  /** 重新取题 */
  reload(): void {
    this.load();
  }

  // ══════════════ 题目渲染 ══════════════

  /** 题型标签 */
  typeLabel(type?: ExerciseType): string {
    if (type === undefined || type === null) return '习题';
    return EXERCISE_TYPE_LABELS[type] ?? '习题';
  }

  /** 作答方式 */
  answerMode(question: PracticeExerciseDto): AnswerMode {
    switch (question.type) {
      case ExerciseType.SingleChoice:
        return ANSWER_MODE.choice;
      case ExerciseType.MultiChoice:
        return ANSWER_MODE.multi;
      case ExerciseType.TrueFalse:
        return ANSWER_MODE.judge;
      default:
        return ANSWER_MODE.text;
    }
  }

  /** 解析选项：兼容 [{"key","content"}] 与 ["A. 内容"] 两种格式 */
  parseOptions(question: PracticeExerciseDto): OptionItem[] {
    if (!question.options) return [];
    try {
      const parsed: unknown = JSON.parse(question.options);
      if (!Array.isArray(parsed)) return [];
      return parsed.map((item: unknown, index: number) => {
        if (item !== null && typeof item === 'object') {
          const option = item as Record<string, unknown>;
          const key = option['key'] ?? option['Key'] ?? String.fromCharCode(65 + index);
          const content = option['content'] ?? option['Content'] ?? '';
          return { key: String(key), content: String(content) };
        }
        const text = String(item);
        const match = text.match(/^([A-Z])[.、)\s]+(.*)$/);
        return match
          ? { key: match[1], content: match[2] }
          : { key: String.fromCharCode(65 + index), content: text };
      });
    } catch {
      return [];
    }
  }

  selectOption(key: string): void {
    if (this.currentFeedback() !== undefined) return;
    this.answer.set(key);
  }

  toggleMulti(key: string): void {
    if (this.currentFeedback() !== undefined) return;
    const next = { ...this.multiSelected() };
    next[key] = !next[key];
    this.multiSelected.set(next);
  }

  // ══════════════ 作答 ══════════════

  submitAnswer(): void {
    const question = this.current();
    if (!question || !question.id || !this.canSubmit()) return;

    const value =
      this.answerMode(question) === ANSWER_MODE.multi
        ? Object.entries(this.multiSelected())
            .filter(([, selected]) => selected)
            .map(([key]) => key)
            .sort()
            .join('')
        : this.answer();

    this.submitting.set(true);
    this.submitFailed.set(false);
    this.service
      .submit({
        courseId: this.courseId,
        knowledgePointId: this.knowledgePointId || null,
        answers: [
          {
            exerciseId: question.id,
            answer: value,
            timeSpentTicks: (Date.now() - this.questionStartedAt) * 10000,
          },
        ],
      })
      .subscribe({
        next: result => {
          this.submitting.set(false);
          const item = (result.items ?? []).find(i => i.exerciseId === question.id);
          const isCorrect = item?.isCorrect ?? null;

          this.feedback.update(map => ({ ...map, [question.id]: isCorrect }));
          this.mastery.set(result.knowledgePoints ?? []);
          this.records.update(list => [
            ...list.filter(r => r.exerciseId !== question.id),
            {
              exerciseId: question.id,
              title: question.title || '习题',
              answer: value,
              isCorrect,
              correctAnswer: item?.correctAnswer ?? null,
              analysis: item?.analysis ?? null,
            },
          ]);
        },
        error: () => {
          this.submitting.set(false);
          this.submitFailed.set(true);
        },
      });
  }

  next(): void {
    if (this.currentIndex() < this.total() - 1) {
      this.currentIndex.update(i => i + 1);
      this.answer.set('');
      this.multiSelected.set({});
      this.questionStartedAt = Date.now();
      this.submitFailed.set(false);
    } else {
      this.showSummary.set(true);
    }
  }

  // ══════════════ 结果 ══════════════

  retry(): void {
    this.showSummary.set(false);
    this.records.set([]);
    this.feedback.set({});
    this.mastery.set([]);
    this.currentIndex.set(0);
    this.answer.set('');
    this.multiSelected.set({});
    this.submitFailed.set(false);
    this.questionStartedAt = Date.now();
    this.load();
  }

  backToAnalysis(): void {
    this.router.navigate(['/student/analysis']);
  }

  goCourses(): void {
    this.router.navigate(['/student/courses']);
  }

  /** 百分比（0~1 → 0~100） */
  percent(value?: number | null): number {
    return Math.round((value ?? 0) * 100);
  }

  /** 后端等级值 → 掌握度枚举 */
  masteryLevel(level?: number): MasteryLevel | null {
    if (level === undefined || level === null) return null;
    if (level === MasteryLevel.Weak) return MasteryLevel.Weak;
    if (level === MasteryLevel.Fair) return MasteryLevel.Fair;
    if (level === MasteryLevel.Ok) return MasteryLevel.Ok;
    return null;
  }
}
