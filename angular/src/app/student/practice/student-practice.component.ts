import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';

import {
  PracticeExercise,
  PracticeMasteryItem,
  PracticeService,
  PracticeSet,
} from '../../learning-analytics/services/practice.service';

interface OptionItem {
  key: string;
  content: string;
}

interface AnswerRecord {
  exerciseId: string;
  title: string;
  answer: string | null;
  isCorrect: boolean | null;
  correctAnswer?: string | null;
  analysis?: string | null;
}

/**
 * 学生端「专项练习」：
 * 按推荐知识点逐题作答 → 每题即时判定 → 全部答完后展示本次正确率与知识点掌握率变化。
 * 掌握度由后端在提交时重算（在线作答 + 纸质作答统一口径）。
 */
@Component({
  selector: 'app-student-practice',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NzIconModule, NzSpinModule, NzEmptyModule],
  templateUrl: './student-practice.component.html',
  styleUrls: ['./student-practice.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentPracticeComponent implements OnInit {
  private readonly service = inject(PracticeService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly set = signal<PracticeSet | null>(null);
  readonly currentIndex = signal(0);
  readonly answer = signal<string>('');
  readonly multiSelected = signal<Record<string, boolean>>({});
  /** 当前题的判定结果（undefined = 未提交/可作答，null = 主观题待批改） */
  readonly feedback = signal<Record<string, boolean | null>>({});
  /** 每题作答明细（结果页逐题回顾） */
  readonly records = signal<AnswerRecord[]>([]);
  /** 最新一次提交回写的知识点掌握率（最后一次即最终值） */
  readonly mastery = signal<PracticeMasteryItem[]>([]);
  readonly showSummary = signal(false);

  private courseId = '';
  private knowledgePointId = '';
  private questionStartedAt = Date.now();

  readonly current = computed<PracticeExercise | null>(() => {
    const items = this.set()?.items ?? [];
    return items[this.currentIndex()] ?? null;
  });

  readonly progress = computed(() => {
    const total = this.set()?.items.length ?? 0;
    return { current: Math.min(this.currentIndex() + 1, total), total };
  });

  readonly currentFeedback = computed(() => {
    const q = this.current();
    return q ? this.feedback()[q.id] : undefined;
  });

  readonly canSubmit = computed(() => {
    const q = this.current();
    if (!q || this.submitting()) return false;
    if (this.isMulti(q)) return Object.values(this.multiSelected()).some(Boolean);
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

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    this.courseId = params.get('courseId') || '';
    this.knowledgePointId = params.get('knowledgePointId') || '';

    if (!this.courseId) {
      this.loading.set(false);
      return;
    }

    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.service.getSet(this.courseId, this.knowledgePointId || undefined, 6).subscribe({
      next: set => {
        this.set.set(set);
        this.loading.set(false);
        this.questionStartedAt = Date.now();
      },
      error: () => {
        this.set.set(null);
        this.loading.set(false);
      },
    });
  }

  // ══════════════ 题目渲染 ══════════════

  /** 解析选项：兼容 [{"key","content"}] 与 ["A. 内容"] 两种格式 */
  parseOptions(question: PracticeExercise): OptionItem[] {
    if (!question.options) return [];
    try {
      const parsed = JSON.parse(question.options);
      if (!Array.isArray(parsed)) return [];
      return parsed.map((item: any, idx: number) => {
        if (item !== null && typeof item === 'object') {
          return {
            key: String(item.key ?? item.Key ?? String.fromCharCode(65 + idx)),
            content: String(item.content ?? item.Content ?? ''),
          };
        }
        const text = String(item);
        const match = text.match(/^([A-Z])[.、)\s]+(.*)$/);
        return match
          ? { key: match[1], content: match[2] }
          : { key: String.fromCharCode(65 + idx), content: text };
      });
    } catch {
      return [];
    }
  }

  isMulti(q: PracticeExercise): boolean {
    return q.type === 1;
  }

  isTrueFalse(q: PracticeExercise): boolean {
    return q.type === 2;
  }

  isChoice(q: PracticeExercise): boolean {
    return q.type === 0;
  }

  isTextInput(q: PracticeExercise): boolean {
    return q.type !== 0 && q.type !== 1 && q.type !== 2;
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
    if (!question || !this.canSubmit()) return;

    const value = this.isMulti(question)
      ? Object.entries(this.multiSelected())
          .filter(([, v]) => v)
          .map(([k]) => k)
          .sort()
          .join('')
      : this.answer();

    this.submitting.set(true);
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
        next: res => {
          this.submitting.set(false);
          const item = res.items.find(i => i.exerciseId === question.id);
          const isCorrect = item?.isCorrect ?? null;

          this.feedback.update(map => ({ ...map, [question.id]: isCorrect }));
          this.mastery.set(res.knowledgePoints ?? []);
          this.records.update(list => [
            ...list.filter(r => r.exerciseId !== question.id),
            {
              exerciseId: question.id,
              title: question.title,
              answer: value,
              isCorrect,
              correctAnswer: item?.correctAnswer ?? null,
              analysis: item?.analysis ?? null,
            },
          ]);
        },
        error: () => this.submitting.set(false),
      });
  }

  next(): void {
    const total = this.set()?.items.length ?? 0;
    if (this.currentIndex() < total - 1) {
      this.currentIndex.update(i => i + 1);
      this.answer.set('');
      this.multiSelected.set({});
      this.questionStartedAt = Date.now();
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
    this.questionStartedAt = Date.now();
    this.load();
  }

  backToAnalysis(): void {
    this.router.navigate(['/student/analysis']);
  }

  goCourses(): void {
    this.router.navigate(['/student/courses']);
  }

  percent(value: number): number {
    return Math.round((value ?? 0) * 100);
  }

  levelClass(level: number): string {
    return level === 0 ? 'is-weak' : level === 1 ? 'is-fair' : 'is-ok';
  }
}
