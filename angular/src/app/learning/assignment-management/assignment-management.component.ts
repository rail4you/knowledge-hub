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
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { MyCoursesService } from '../shared/my-courses.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { CourseContextService } from '../course-context.service';
import {
  AssignmentApiService,
  AssignmentCriterionItem,
  AssignmentDetail,
  AssignmentFileItem,
  AssignmentListItem,
  AssignmentStatus,
  AssignmentStatistics,
  AssignmentType,
  ASSIGNMENT_STATUS_LABELS,
  ASSIGNMENT_TYPE_LABELS,
  formatFileSize,
} from './assignment.service';

type TabKey = 'all' | 'ongoing' | 'pending' | 'closed' | 'draft';

interface CriterionRow {
  name: string;
  maxScore: number | null;
}

@Component({
  selector: 'app-assignment-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzDatePickerModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzPopconfirmModule,
    NzProgressModule,
    NzSelectModule,
    NzSpinModule,
    NzSwitchModule,
    NzTableModule,
    NzTagModule,
    NzTooltipModule,
  ],
  templateUrl: './assignment-management.component.html',
  styleUrls: ['./assignment-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssignmentManagementComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly assignmentApi = inject(AssignmentApiService);
  private readonly message = inject(NzMessageService);

  readonly assignmentTypes = [AssignmentType.Individual, AssignmentType.Group];
  readonly typeLabels = ASSIGNMENT_TYPE_LABELS;
  readonly statusLabels = ASSIGNMENT_STATUS_LABELS;

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly assignments = signal<AssignmentListItem[]>([]);
  readonly statistics = signal<AssignmentStatistics | null>(null);
  readonly loading = signal(false);
  readonly keyword = signal('');
  readonly activeTab = signal<TabKey>('all');

  readonly hasCourse = computed(() => !!this.selectedCourseId());
  readonly selectedCourse = computed(
    () => this.courses().find((c) => c.id === this.selectedCourseId()) ?? null,
  );

  // ── 新建 / 编辑弹窗 ──
  readonly formVisible = signal(false);
  readonly submitting = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly formTitle = signal('');
  readonly formDescription = signal('');
  readonly formType = signal<AssignmentType>(AssignmentType.Individual);
  readonly formMaxScore = signal<number>(100);
  readonly formDeadline = signal<Date | null>(null);
  readonly formAllowLate = signal(true);
  readonly formLatePenalty = signal<number>(0);
  readonly formAllowResubmit = signal(true);
  readonly formMaxFileSize = signal<number>(20);
  readonly formMaxFileCount = signal<number>(5);
  readonly formFileTypes = signal('pdf,doc,docx,xls,xlsx,ppt,pptx,zip,rar,7z,png,jpg,jpeg,txt,md');
  readonly formCriteria = signal<CriterionRow[]>([
    { name: '解题过程', maxScore: 40 },
    { name: '结果正确', maxScore: 40 },
    { name: '规范表达', maxScore: 20 },
  ]);
  readonly formAttachments = signal<AssignmentFileItem[]>([]);
  readonly uploading = signal(false);

  readonly criteriaTotal = computed(() =>
    this.formCriteria().reduce((sum, row) => sum + (row.maxScore || 0), 0),
  );

  ngOnInit(): void {
    this.loadCourses();
    this.route.queryParamMap.pipe(take(1)).subscribe((params) => {
      const courseId = params.get('courseId') || this.courseContext.currentCourseId();
      if (courseId) {
        this.onCourseChange(courseId);
      }
    });
  }

  // ── 数据加载 ──
  loadCourses(): void {
    // 教师只列自己创建的课程（管理员看本校全部）—— 口径由 MyCoursesService 统一维护。
    this.myCourses.getCourses(200).subscribe({
      next: (items) => this.courses.set(items || []),
      error: () => this.message.error('加载课程失败'),
    });
  }

  onCourseChange(courseId: string | null): void {
    this.courseContext.setCourse(courseId);
    this.selectedCourseId.set(courseId);
    this.assignments.set([]);
    this.statistics.set(null);
    if (!courseId) return;
    this.loadAssignments();
    this.loadStatistics();
  }

  loadAssignments(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    const status = this.statusFilter();
    this.assignmentApi
      .getList({
        courseId,
        status,
        filter: this.keyword().trim() || undefined,
        skipCount: 0,
        maxResultCount: 200,
      })
      .subscribe({
        next: (result) => {
          this.assignments.set(this.applyTabFilter(result.items || []));
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载作业列表失败');
        },
      });
  }

  /** 「待批阅」是派生状态（存在待批阅提交），需要在前端二次过滤。 */
  private applyTabFilter(items: AssignmentListItem[]): AssignmentListItem[] {
    if (this.activeTab() !== 'pending') {
      return items;
    }
    return items.filter((x) => x.pendingGradeCount > 0);
  }

  loadStatistics(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    this.assignmentApi.getStatistics(courseId).subscribe({
      next: (stats) => this.statistics.set(stats),
      error: () => this.statistics.set(null),
    });
  }

  /** 页签 → 后端状态过滤（部分状态是前端派生，需要本地过滤） */
  private statusFilter(): AssignmentStatus | undefined {
    switch (this.activeTab()) {
      case 'draft':
        return AssignmentStatus.Draft;
      case 'closed':
        return AssignmentStatus.Closed;
      case 'ongoing':
        return AssignmentStatus.Published;
      default:
        return undefined;
    }
  }

  onTabChange(tab: TabKey): void {
    this.activeTab.set(tab);
    this.loadAssignments();
  }

  onSearch(): void {
    this.loadAssignments();
  }

  reload(): void {
    this.loadAssignments();
    this.loadStatistics();
  }

  // ── 行状态展示 ──
  displayStatus(item: AssignmentListItem): string {
    if (item.status === AssignmentStatus.Draft) return '草稿 · 未发布';
    if (item.status === AssignmentStatus.Closed) return '已结束';
    if (item.pendingGradeCount > 0) return '批阅中';
    return '进行中';
  }

  displayStatusColor(item: AssignmentListItem): string {
    if (item.status === AssignmentStatus.Draft) return 'default';
    if (item.status === AssignmentStatus.Closed) return 'default';
    if (item.pendingGradeCount > 0) return 'blue';
    return 'green';
  }

  submitRate(item: AssignmentListItem): number {
    if (!item.totalStudents) return 0;
    return Math.round((item.submittedCount / item.totalStudents) * 100);
  }

  gradedRate(item: AssignmentListItem): number {
    if (!item.submittedCount) return 0;
    return Math.round((item.gradedCount / item.submittedCount) * 100);
  }

  formatDeadline(value?: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
  }

  formatDeadlineFull(value?: string | null): string {
    if (!value) return '—';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(
      date.getMinutes(),
    )}`;
  }

  typeLabel(type: AssignmentType): string {
    return this.typeLabels[type] || '个人';
  }

  // ── 发布 / 编辑 ──
  openCreate(): void {
    if (!this.hasCourse()) {
      this.message.warning('请先选择课程');
      return;
    }
    this.editingId.set(null);
    this.formTitle.set('');
    this.formDescription.set('');
    this.formType.set(AssignmentType.Individual);
    this.formMaxScore.set(100);
    this.formDeadline.set(null);
    this.formAllowLate.set(true);
    this.formLatePenalty.set(0);
    this.formAllowResubmit.set(true);
    this.formMaxFileSize.set(20);
    this.formMaxFileCount.set(5);
    this.formFileTypes.set('pdf,doc,docx,xls,xlsx,ppt,pptx,zip,rar,7z,png,jpg,jpeg,txt,md');
    this.formCriteria.set([
      { name: '解题过程', maxScore: 40 },
      { name: '结果正确', maxScore: 40 },
      { name: '规范表达', maxScore: 20 },
    ]);
    this.formAttachments.set([]);
    this.formVisible.set(true);
  }

  openEdit(item: AssignmentListItem): void {
    this.assignmentApi.get(item.id).subscribe({
      next: (detail: AssignmentDetail) => {
        this.editingId.set(detail.id);
        this.formTitle.set(detail.title);
        this.formDescription.set(detail.description || '');
        this.formType.set(detail.type);
        this.formMaxScore.set(detail.maxScore);
        this.formDeadline.set(detail.deadline ? new Date(detail.deadline) : null);
        this.formAllowLate.set(detail.allowLate);
        this.formLatePenalty.set(detail.latePenaltyPercent);
        this.formAllowResubmit.set(detail.allowResubmit);
        this.formMaxFileSize.set(detail.maxFileSizeMB);
        this.formMaxFileCount.set(detail.maxFileCount);
        this.formFileTypes.set(detail.allowedFileTypes);
        this.formCriteria.set(
          (detail.criteria || []).map((c) => ({ name: c.name, maxScore: c.maxScore })),
        );
        this.formAttachments.set(detail.attachments || []);
        this.formVisible.set(true);
      },
      error: () => this.message.error('加载作业详情失败'),
    });
  }

  closeForm(): void {
    if (this.submitting()) return;
    this.formVisible.set(false);
  }

  addCriterion(): void {
    this.formCriteria.set([...this.formCriteria(), { name: '', maxScore: 0 }]);
  }

  removeCriterion(index: number): void {
    this.formCriteria.set(this.formCriteria().filter((_, i) => i !== index));
  }

  updateCriterion(index: number, patch: Partial<CriterionRow>): void {
    this.formCriteria.set(
      this.formCriteria().map((row, i) => (i === index ? { ...row, ...patch } : row)),
    );
  }

  /** 上传「作业要求附件」（先建草稿作业，再传附件，最后更新） */
  onAttachmentSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }

    if (!this.editingId() && !this.formTitle().trim()) {
      // 新建时上传附件需要先落一份草稿作业拿到 Id，所以标题必须先填
      this.message.warning('请先填写作业标题，再上传附件');
      return;
    }

    this.uploading.set(true);
    const upload = (assignmentId: string) =>
      this.assignmentApi.uploadFile(assignmentId, file, 'requirement').subscribe({
        next: (result) => {
          this.uploading.set(false);
          this.formAttachments.set([...this.formAttachments(), result]);
          this.message.success('附件已上传');
        },
        error: (err) => {
          this.uploading.set(false);
          this.message.error(this.errorText(err, '附件上传失败'));
        },
      });

    const editingId = this.editingId();
    if (editingId) {
      upload(editingId);
      return;
    }

    // 新建：先落一个草稿作业拿到 Id，再挂附件
    this.assignmentApi
      .create(this.buildInput())
      .subscribe({
        next: (detail) => {
          this.editingId.set(detail.id);
          upload(detail.id);
        },
        error: (err) => {
          this.uploading.set(false);
          this.message.error(this.errorText(err, '创建作业失败'));
        },
      });
  }

  removeAttachment(index: number): void {
    this.formAttachments.set(this.formAttachments().filter((_, i) => i !== index));
  }

  private buildInput() {
    const deadline = this.formDeadline();
    const criteria: AssignmentCriterionItem[] = this.formCriteria()
      .filter((row) => !!row.name.trim())
      .map((row, index) => ({
        id: null,
        name: row.name.trim(),
        maxScore: row.maxScore || 0,
        sortOrder: index,
        description: null,
      }));

    return {
      courseId: this.selectedCourseId()!,
      chapterId: null,
      title: this.formTitle().trim(),
      description: this.formDescription().trim() || null,
      type: this.formType(),
      maxScore: criteria.length ? this.criteriaTotal() : this.formMaxScore(),
      deadline: deadline ? deadline.toISOString() : new Date().toISOString(),
      allowLate: this.formAllowLate(),
      latePenaltyPercent: this.formLatePenalty() || 0,
      allowedFileTypes: this.formFileTypes(),
      maxFileSizeMB: this.formMaxFileSize(),
      maxFileCount: this.formMaxFileCount(),
      allowResubmit: this.formAllowResubmit(),
      criteria,
      attachmentFileIds: this.formAttachments().map((f) => f.id),
    };
  }

  submitForm(): void {
    if (!this.hasCourse()) {
      this.message.warning('请先选择课程');
      return;
    }
    const title = this.formTitle().trim();
    if (!title) {
      this.message.warning('请填写作业标题');
      return;
    }
    if (!this.formDeadline()) {
      this.message.warning('请设置截止时间');
      return;
    }
    if (this.formCriteria().some((row) => row.name.trim() && (row.maxScore || 0) <= 0)) {
      this.message.warning('评分细则的满分必须大于 0');
      return;
    }

    this.submitting.set(true);
    const input = this.buildInput();
    const editingId = this.editingId();

    const done = () => {
      this.submitting.set(false);
      this.formVisible.set(false);
      this.reload();
      this.message.success(editingId ? '作业已保存' : '作业已创建（草稿）');
    };

    if (editingId) {
      this.assignmentApi.update(editingId, input).subscribe({
        next: () => done(),
        error: (err) => {
          this.submitting.set(false);
          this.message.error(this.errorText(err, '保存失败'));
        },
      });
    } else {
      this.assignmentApi.create(input).subscribe({
        next: () => done(),
        error: (err) => {
          this.submitting.set(false);
          this.message.error(this.errorText(err, '创建失败'));
        },
      });
    }
  }

  // ── 行操作 ──
  publish(item: AssignmentListItem): void {
    this.assignmentApi.publish(item.id).subscribe({
      next: () => {
        this.message.success('作业已发布，学生端可见');
        this.reload();
      },
      error: (err) => this.message.error(this.errorText(err, '发布失败')),
    });
  }

  closeAssignment(item: AssignmentListItem): void {
    this.assignmentApi.close(item.id).subscribe({
      next: () => {
        this.message.success('作业已结束');
        this.reload();
      },
      error: (err) => this.message.error(this.errorText(err, '结束失败')),
    });
  }

  remove(item: AssignmentListItem): void {
    this.assignmentApi.delete(item.id).subscribe({
      next: () => {
        this.message.success('作业已删除');
        this.reload();
      },
      error: (err) => this.message.error(this.errorText(err, '删除失败')),
    });
  }

  goGrading(item: AssignmentListItem): void {
    this.router.navigate(['/learning/assignment-management', item.id, 'grading']);
  }

  exportScores(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    const target = this.assignments().find((x) => x.status !== AssignmentStatus.Draft);
    if (!target) {
      this.message.warning('该课程还没有已发布的作业');
      return;
    }
    this.assignmentApi.getExportScores(target.id).subscribe({
      next: (csv) => {
        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${target.title}-成绩.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => this.message.error(this.errorText(err, '导出失败')),
    });
  }

  formatFileSize(bytes: number): string {
    return formatFileSize(bytes);
  }

  private errorText(err: any, fallback: string): string {
    return err?.error?.error?.message || err?.error?.message || err?.message || fallback;
  }
}
