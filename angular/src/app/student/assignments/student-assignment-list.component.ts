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
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LearningService } from '../../proxy/learning/learning.service';
import type { StudentCourseListItemDto } from '../../proxy/learning/dtos/models';
import {
  AssignmentApiService,
  StudentAssignmentItem,
  SubmissionStatus,
  SUBMISSION_STATUS_LABELS,
} from '../../learning/assignment-management/assignment.service';

type ListTab = 'all' | 'pending' | 'submitted' | 'graded';

@Component({
  selector: 'app-student-assignment-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzIconModule,
    NzSelectModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './student-assignment-list.component.html',
  styleUrls: ['./student-assignment-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAssignmentListComponent implements OnInit {
  private readonly learningService = inject(LearningService);
  private readonly assignmentApi = inject(AssignmentApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly courses = signal<StudentCourseListItemDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly assignments = signal<StudentAssignmentItem[]>([]);
  readonly loading = signal(false);
  readonly activeTab = signal<ListTab>('all');

  readonly statusLabels = SUBMISSION_STATUS_LABELS;

  readonly selectedCourseTitle = computed(
    () => this.courses().find((c) => c.courseId === this.selectedCourseId())?.courseTitle ?? '',
  );

  readonly pendingCount = computed(
    () => this.assignments().filter((x) => !x.submitted).length,
  );
  readonly submittedCount = computed(
    () => this.assignments().filter((x) => x.submitted && x.status === SubmissionStatus.Submitted).length,
  );
  readonly gradedCount = computed(
    () => this.assignments().filter((x) => x.status === SubmissionStatus.Graded).length,
  );
  readonly averageScore = computed(() => {
    const graded = this.assignments().filter((x) => x.status === SubmissionStatus.Graded && x.score != null);
    if (!graded.length) return 0;
    return Math.round((graded.reduce((s, x) => s + (x.score ?? 0), 0) / graded.length) * 10) / 10;
  });

  readonly filtered = computed(() => {
    switch (this.activeTab()) {
      case 'pending':
        return this.assignments().filter((x) => !x.submitted);
      case 'submitted':
        return this.assignments().filter((x) => x.submitted && x.status === SubmissionStatus.Submitted);
      case 'graded':
        return this.assignments().filter((x) => x.status === SubmissionStatus.Graded);
      default:
        return this.assignments();
    }
  });

  ngOnInit(): void {
    this.learningService.getMyCourses().subscribe({
      next: (list) => {
        this.courses.set(list || []);
        const fromUrl = this.route.snapshot.queryParamMap.get('courseId');
        const first = fromUrl || list?.[0]?.courseId || null;
        if (first) {
          this.onCourseChange(first);
        }
      },
      error: () => this.message.error('加载我的课程失败'),
    });
  }

  onCourseChange(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    this.assignments.set([]);
    if (!courseId) return;
    this.loadAssignments();
  }

  loadAssignments(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.assignmentApi.getMyAssignments(courseId).subscribe({
      next: (list) => {
        this.assignments.set(list || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载作业失败');
      },
    });
  }

  onTabChange(tab: ListTab): void {
    this.activeTab.set(tab);
  }

  goSubmit(item: StudentAssignmentItem): void {
    this.router.navigate(['/student/assignments', item.assignmentId, 'submit']);
  }

  goFeedback(item: StudentAssignmentItem): void {
    this.router.navigate(['/student/assignments', item.assignmentId, 'feedback']);
  }

  typeLabel(type: number): string {
    return type === 1 ? '小组' : '个人';
  }

  statusTag(item: StudentAssignmentItem): string {
    if (item.status === SubmissionStatus.Graded) return '已批阅';
    if (item.submitted) return '待批阅';
    return '待提交';
  }

  statusTagColor(item: StudentAssignmentItem): string {
    if (item.status === SubmissionStatus.Graded) return 'green';
    if (item.submitted) return 'blue';
    return 'orange';
  }

  formatDeadline(value?: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  formatSubmittedAt(value?: string | null): string {
    if (!value) return '';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} 提交`;
  }
}
