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
import { ActivatedRoute, Router } from '@angular/router';
import { take } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import {
  AssignmentApiService,
  AssignmentFileItem,
  AssignmentSubmissionDetail,
  AssignmentSubmissionItem,
  SubmissionStatus,
  SUBMISSION_STATUS_LABELS,
  formatFileSize,
} from './assignment.service';

interface GradeRow {
  criterionId: string | null;
  criterionName: string;
  maxScore: number;
  score: number | null;
  comment: string;
}

@Component({
  selector: 'app-assignment-grading',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzCheckboxModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzSpinModule,
    NzTagModule,
    NzTooltipModule,
  ],
  templateUrl: './assignment-grading.component.html',
  styleUrls: ['./assignment-grading.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssignmentGradingComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly assignmentApi = inject(AssignmentApiService);
  private readonly message = inject(NzMessageService);

  readonly assignmentId = signal<string>('');
  readonly assignmentTitle = signal('');
  readonly assignmentDeadline = signal<string | null>(null);
  readonly assignmentMaxScore = signal(100);
  readonly latePenaltyPercent = signal(0);

  readonly submissions = signal<AssignmentSubmissionItem[]>([]);
  readonly submissionsLoading = signal(false);
  readonly keyword = signal('');
  readonly filter = signal<'all' | 'pending' | 'notSubmitted'>('all');

  readonly selectedSubmissionId = signal<string | null>(null);
  readonly detail = signal<AssignmentSubmissionDetail | null>(null);
  readonly detailLoading = signal(false);

  readonly gradeRows = signal<GradeRow[]>([]);
  readonly totalScore = signal<number | null>(null);
  readonly comment = signal('');
  readonly allowResubmit = signal(false);
  readonly applyLatePenalty = signal(false);
  readonly saving = signal(false);

  readonly statusLabels = SUBMISSION_STATUS_LABELS;

  readonly filteredSubmissions = computed(() => {
    const kw = this.keyword().trim().toLowerCase();
    const filter = this.filter();
    return this.submissions().filter((item) => {
      if (filter === 'pending' && !(item.submitted && item.status === SubmissionStatus.Submitted)) {
        return false;
      }
      if (filter === 'notSubmitted' && item.submitted) {
        return false;
      }
      if (!kw) return true;
      return (
        (item.studentName || '').toLowerCase().includes(kw) ||
        (item.studentNumber || '').toLowerCase().includes(kw)
      );
    });
  });

  readonly gradedCount = computed(
    () => this.submissions().filter((x) => x.status === SubmissionStatus.Graded).length,
  );
  readonly submittedCount = computed(() => this.submissions().filter((x) => x.submitted).length);
  readonly notSubmittedCount = computed(() => this.submissions().filter((x) => !x.submitted).length);
  readonly pendingCount = computed(
    () => this.submissions().filter((x) => x.submitted && x.status === SubmissionStatus.Submitted).length,
  );

  ngOnInit(): void {
    this.route.paramMap.pipe(take(1)).subscribe((params) => {
      const id = params.get('id');
      if (!id) {
        this.message.error('缺少作业参数');
        return;
      }
      this.assignmentId.set(id);
      this.loadAssignment();
      this.loadSubmissions();
    });
  }

  loadAssignment(): void {
    this.assignmentApi.get(this.assignmentId()).subscribe({
      next: (detail) => {
        this.assignmentTitle.set(detail.title);
        this.assignmentDeadline.set(detail.deadline);
        this.assignmentMaxScore.set(detail.maxScore);
        this.latePenaltyPercent.set(detail.latePenaltyPercent);
      },
      error: () => this.message.error('加载作业失败'),
    });
  }

  loadSubmissions(): void {
    this.submissionsLoading.set(true);
    this.assignmentApi
      .getSubmissions({
        assignmentId: this.assignmentId(),
        skipCount: 0,
        maxResultCount: 500,
      })
      .subscribe({
        next: (result) => {
          this.submissions.set(result.items || []);
          this.submissionsLoading.set(false);
        },
        error: () => {
          this.submissionsLoading.set(false);
          this.message.error('加载提交列表失败');
        },
      });
  }

  selectSubmission(item: AssignmentSubmissionItem): void {
    if (!item.submitted) {
      this.message.warning('该学生尚未提交，无需批阅');
      return;
    }
    this.selectedSubmissionId.set(item.submissionId);
    this.detailLoading.set(true);
    this.assignmentApi.getSubmission(item.submissionId).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        // 已批阅过的提交：回填上次的得分明细，避免教师重开页面看到空白
        const savedScores = new Map(
          (detail.scores || []).map((s) => [s.criterionId ?? s.criterionName, s.score]),
        );
        this.gradeRows.set(
          (detail.criteria || []).map((c) => ({
            criterionId: c.id,
            criterionName: c.name,
            maxScore: c.maxScore,
            score: savedScores.has(c.id)
              ? (savedScores.get(c.id) as number)
              : savedScores.has(c.name)
                ? (savedScores.get(c.name) as number)
                : null,
            comment: (detail.scores || []).find((s) => s.criterionId === c.id)?.comment || '',
          })),
        );
        this.totalScore.set(detail.score ?? null);
        this.comment.set(detail.comment || '');
        this.allowResubmit.set(detail.allowResubmit);
        this.applyLatePenalty.set(false);
        this.detailLoading.set(false);
      },
      error: () => {
        this.detailLoading.set(false);
        this.message.error('加载提交详情失败');
      },
    });
  }

  updateRow(index: number, patch: Partial<GradeRow>): void {
    this.gradeRows.set(
      this.gradeRows().map((row, i) => (i === index ? { ...row, ...patch } : row)),
    );
  }

  rowsTotal(): number {
    return this.gradeRows().reduce((sum, row) => sum + (row.score || 0), 0);
  }

  saveGrade(moveNext = false): void {
    const submissionId = this.selectedSubmissionId();
    if (!submissionId) {
      this.message.warning('请先选择一份提交');
      return;
    }

    const scores = this.gradeRows().map((row) => ({
      criterionId: row.criterionId,
      criterionName: row.criterionName,
      score: row.score ?? 0,
      comment: row.comment || null,
    }));

    const total =
      this.totalScore() !== null && this.totalScore() !== undefined
        ? this.totalScore()
        : this.rowsTotal();

    if (total > this.assignmentMaxScore()) {
      this.message.warning(`总分不能超过满分 ${this.assignmentMaxScore()}`);
      return;
    }

    this.saving.set(true);
    this.assignmentApi
      .grade(submissionId, {
        score: total,
        comment: this.comment() || null,
        allowResubmit: this.allowResubmit(),
        applyLatePenalty: this.applyLatePenalty(),
        scores,
      })
      .subscribe({
        next: (detail) => {
          this.saving.set(false);
          this.message.success(`批阅完成：${detail.score ?? 0}/${detail.maxScore} 分`);
          this.loadSubmissions();
          if (moveNext) {
            this.selectNextPending();
          } else {
            this.detail.set(detail);
          }
        },
        error: (err) => {
          this.saving.set(false);
          this.message.error(this.errorText(err, '保存批阅失败'));
        },
      });
  }

  /** 提交成绩后自动跳到下一份待批阅 */
  selectNextPending(): void {
    const pending = this.filteredSubmissions().find(
      (x) => x.submitted && x.status === SubmissionStatus.Submitted && x.submissionId !== this.selectedSubmissionId(),
    );
    if (pending) {
      this.selectSubmission(pending);
    } else {
      this.message.success('本课程已没有待批阅的提交');
    }
  }

  back(): void {
    this.router.navigate(['/learning/assignment-management']);
  }

  // ── 文件预览 / 下载 ──
  openFile(file: AssignmentFileItem): void {
    this.assignmentApi.getFile(file.id).subscribe({
      next: (base64) => {
        const blob = this.base64ToBlob(base64, file.contentType);
        const url = URL.createObjectURL(blob);
        window.open(url, '_blank');
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: (err) => this.message.error(this.errorText(err, '文件读取失败')),
    });
  }

  downloadFile(file: AssignmentFileItem): void {
    this.assignmentApi.getFile(file.id).subscribe({
      next: (base64) => {
        const blob = this.base64ToBlob(base64, file.contentType);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = file.fileName;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => this.message.error(this.errorText(err, '文件下载失败')),
    });
  }

  private base64ToBlob(base64: string, contentType?: string | null): Blob {
    const clean = (base64 || '').replace(/^data:[^;]+;base64,/, '');
    const binary = atob(clean);
    const buffer = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
      buffer[i] = binary.charCodeAt(i);
    }
    return new Blob([buffer], { type: contentType || 'application/octet-stream' });
  }

  formatFileSize(bytes: number): string {
    return formatFileSize(bytes);
  }

  formatTime(value?: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  scoreTag(item: AssignmentSubmissionItem): string {
    if (!item.submitted) return '未提交';
    if (item.status === SubmissionStatus.Graded) return `${item.score ?? 0} 分`;
    if (item.status === SubmissionStatus.Submitted) return '待批阅';
    return '草稿';
  }

  scoreTagColor(item: AssignmentSubmissionItem): string {
    if (!item.submitted) return 'red';
    if (item.status === SubmissionStatus.Graded) return 'green';
    if (item.status === SubmissionStatus.Submitted) return 'orange';
    return 'default';
  }

  private errorText(err: any, fallback: string): string {
    return err?.error?.error?.message || err?.error?.message || err?.message || fallback;
  }
}
