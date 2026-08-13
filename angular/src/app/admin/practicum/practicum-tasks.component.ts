import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom, forkJoin, Observable, map, catchError, of } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import {
  CreatePracticumAssessmentDto,
  CreatePracticumGuidanceRecordDto,
  CreateUpdatePracticumProjectDto,
  PracticumEnrollmentDto,
  PracticumEnrollmentStatus,
  PracticumGuidanceRecordDto,
  PracticumProjectDto,
  PracticumProjectStatus,
  PracticumService,
  PracticumSubmissionDto,
  UpdatePracticumGuidanceRecordDto,
} from '../../practicum/practicum.service';
import { SafeResourceUrlPipe } from '../../shared/safe-resource-url.pipe';

@Component({
  selector: 'app-practicum-tasks',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    NzButtonModule, NzCardModule, NzEmptyModule, NzInputModule, NzInputNumberModule,
    NzModalModule, NzSelectModule, NzSpinModule, NzSwitchModule, NzTableModule, NzTagModule, NzIconModule,
    SafeResourceUrlPipe,
  ],
  templateUrl: './practicum-tasks.component.html',
  styleUrls: ['./practicum-tasks.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumTasksComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly message = inject(NzMessageService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly statuses = PracticumProjectStatus;

  // ─── 项目选择 ───────────────────────────
  readonly projects = signal<PracticumProjectDto[]>([]);
  selectedProjectId = '';
  selectedProjectTitle = '';
  /** 当前项目任务列表（来自 detail，保存后刷新）。 */
  form: CreateUpdatePracticumProjectDto = { title: '', summary: '', description: '', coverImageUrl: '', courseId: undefined, major: '', className: '', status: PracticumProjectStatus.Draft, startTime: undefined, endTime: undefined, maxScore: 100, allowResubmission: true, tasks: [], materials: [] };

  activeTab = 0;

  // ─── 任务抽屉 ───────────────────────────
  readonly drawerVisible = signal(false);
  readonly drawerMode = signal<'add' | 'edit'>('add');
  readonly drawerIndex = signal(-1);
  readonly drawerSaving = signal(false);
  readonly taskDraft = signal<{
    title: string;
    description: string;
    requirement: string;
    scoreWeight: number;
    sortOrder: number;
    dueTimeInput: string;
  } | null>(null);

  // ─── 评分 ───────────────────────────────
  readonly enrollments = signal<PracticumEnrollmentDto[]>([]);
  readonly submissions = signal<PracticumSubmissionDto[]>([]);
  readonly scoreRows = signal<{ enrollment: PracticumEnrollmentDto; submissions: PracticumSubmissionDto[] }[]>([]);
  scoreVisible = false;
  scoreTarget: PracticumSubmissionDto | null = null;
  scoreTitle = '实训评分';
  scoreEnrollmentId = '';
  scoreMax = 100;
  scoreForm: CreatePracticumAssessmentDto = this.emptyScore();
  viewVisible = false;
  viewTarget: PracticumSubmissionDto | null = null;
  viewAttachments: string[] = [];
  previewVisible = false;
  previewUrl = '';
  previewType: 'image' | 'pdf' | 'other' = 'other';

  // ─── 指导 ───────────────────────────────
  readonly studentGuidance = signal<{ enrollment: PracticumEnrollmentDto; records: PracticumGuidanceRecordDto[] }[]>([]);
  readonly guidanceTabLoading = signal(false);
  readonly guidanceRows = signal<{ studentName: string; status: PracticumEnrollmentStatus; record: PracticumGuidanceRecordDto }[]>([]);
  guidanceModalVisible = false;
  guidanceSaving = false;
  guidanceForm: CreatePracticumGuidanceRecordDto = this.emptyGuidance();
  /** 指导记录编辑 */
  guidanceEditVisible = false;
  guidanceEditSaving = false;
  guidanceEditRecord: PracticumGuidanceRecordDto | null = null;
  guidanceEditForm: UpdatePracticumGuidanceRecordDto = { content: '', isVisibleToStudent: true };

  ngOnInit(): void {
    this.loadProjects();
  }

  // ─── 项目加载 ───────────────────────────

  private loadProjects(): void {
    this.practicumService.getList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: r => {
        this.projects.set(r.items || []);
        if (r.items?.length && !this.selectedProjectId) {
          this.onProjectChange(r.items[0].id);
        }
      },
      error: () => this.message.error('加载实训列表失败'),
    });
  }

  onProjectChange(id: string): void {
    this.selectedProjectId = id;
    const p = this.projects().find(x => x.id === id);
    this.selectedProjectTitle = p?.title || '';
    this.activeTab = 0;
    if (id) {
      this.loadDetailAndWork(id);
    }
  }

  private loadDetailAndWork(pid: string): void {
    this.practicumService.getDetail(pid).subscribe({
      next: detail => {
        this.form = {
          title: detail.title, summary: detail.summary || '', description: detail.description || '',
          coverImageUrl: detail.coverImageUrl || '', courseId: detail.courseId, major: detail.major || '',
          className: detail.className || '', status: detail.status, startTime: detail.startTime, endTime: detail.endTime,
          maxScore: detail.maxScore, allowResubmission: detail.allowResubmission, tasks: detail.tasks || [], materials: detail.materials || [],
        };
        this.cdr.markForCheck();
      },
      error: () => this.message.error('加载实训详情失败'),
    });
    this.loadEnrollmentsAndSubmissions(pid);
  }

  private loadEnrollmentsAndSubmissions(pid: string): void {
    this.practicumService.getEnrollmentList({ projectId: pid, skipCount: 0, maxResultCount: 200 })
      .subscribe(r => { this.enrollments.set(r.items || []); this.buildScoreRows(); this.cdr.markForCheck(); });
    this.practicumService.getSubmissionList({ projectId: pid, skipCount: 0, maxResultCount: 200 })
      .subscribe(r => { this.submissions.set(r.items || []); this.buildScoreRows(); this.cdr.markForCheck(); });
    this.loadStudentGuidance(pid);
  }

  // ─── 任务 CRUD（抽屉） ───────────────────

  openAddTaskDrawer(): void {
    this.drawerMode.set('add');
    this.drawerIndex.set(-1);
    this.taskDraft.set({ title: '', description: '', requirement: '', scoreWeight: 0, sortOrder: this.form.tasks.length + 1, dueTimeInput: '' });
    this.drawerVisible.set(true);
  }

  openEditTaskDrawer(i: number): void {
    const src = this.form.tasks[i];
    if (!src) return;
    this.drawerMode.set('edit');
    this.drawerIndex.set(i);
    this.taskDraft.set({
      title: src.title ?? '',
      description: src.description ?? '',
      requirement: src.requirement ?? '',
      scoreWeight: src.scoreWeight ?? 0,
      sortOrder: src.sortOrder ?? (i + 1),
      dueTimeInput: this.toDateTimeLocal(src.dueTime),
    });
    this.drawerVisible.set(true);
  }

  closeDrawer(): void {
    this.drawerVisible.set(false);
    setTimeout(() => {
      this.drawerIndex.set(-1);
      this.taskDraft.set(null);
    }, 200);
  }

  saveDrawer(): void {
    const draft = this.taskDraft();
    if (!draft || !this.selectedProjectId) return;
    if (!(draft.title || '').trim()) {
      this.message.warning('请填写任务名称');
      return;
    }
    const next = {
      title: (draft.title || '').trim(),
      description: (draft.description || '').trim(),
      requirement: (draft.requirement || '').trim(),
      scoreWeight: Number(draft.scoreWeight) || 0,
      sortOrder: draft.sortOrder ?? 1,
      dueTime: draft.dueTimeInput ? this.fromDateTimeLocal(draft.dueTimeInput) : undefined,
    };
    if (this.drawerMode() === 'add') {
      this.form.tasks.push(next);
    } else {
      const i = this.drawerIndex();
      if (this.form.tasks[i]) this.form.tasks[i] = next;
    }
    this.form.tasks.forEach((t, idx) => t.sortOrder = idx + 1);
    this.drawerSaving.set(true);
    this.practicumService.update(this.selectedProjectId, this.preparePayload()).subscribe({
      next: () => {
        this.drawerSaving.set(false);
        this.message.success(this.drawerMode() === 'add' ? '任务已新增' : '任务已更新');
        this.closeDrawer();
        this.refreshDetail();
      },
      error: () => { this.drawerSaving.set(false); this.message.error('保存失败'); },
    });
  }

  removeTask(i: number): void {
    if (!this.selectedProjectId) return;
    this.form.tasks.splice(i, 1);
    this.form.tasks.forEach((t, idx) => t.sortOrder = idx + 1);
    this.practicumService.update(this.selectedProjectId, this.preparePayload()).subscribe({
      next: () => { this.message.success('任务已删除'); this.refreshDetail(); },
      error: () => this.message.error('删除失败'),
    });
  }

  private refreshDetail(): void {
    if (!this.selectedProjectId) return;
    this.practicumService.getDetail(this.selectedProjectId).subscribe(detail => {
      this.form.tasks = detail.tasks || [];
      this.cdr.markForCheck();
    });
  }

  private preparePayload(): CreateUpdatePracticumProjectDto {
    return { ...this.form };
  }

  private toDateTimeLocal(value: string | Date | undefined | null): string {
    if (!value) return '';
    const d = typeof value === 'string' ? new Date(value) : value;
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  private fromDateTimeLocal(input: string | undefined | null): string | undefined {
    if (!input) return undefined;
    const d = new Date(input);
    if (isNaN(d.getTime())) return undefined;
    return d.toISOString();
  }

  // ─── 评分 ───────────────────────────────

  private buildScoreRows(): void {
    const ens = this.enrollments();
    const subs = this.submissions();
    if (!ens.length) { this.scoreRows.set([]); return; }
    const rows = ens.map(enrollment => ({
      enrollment,
      submissions: subs
        .filter(s => s.enrollmentId === enrollment.id)
        .sort((a, b) => new Date(b.submittedAt).getTime() - new Date(a.submittedAt).getTime()),
    }));
    this.scoreRows.set(rows);
  }

  private latestSubmissionOf(enrollmentId: string): PracticumSubmissionDto | undefined {
    return this.scoreRows().find(r => r.enrollment.id === enrollmentId)?.submissions?.[0];
  }

  private emptyScore(): CreatePracticumAssessmentDto {
    return { submissionId: undefined, score: 0, comment: '' };
  }

  openScore(item: PracticumSubmissionDto): void {
    this.scoreTarget = item;
    this.scoreEnrollmentId = item.enrollmentId;
    const taskMax = this.form.tasks?.find(t => t.title === item.taskTitle)?.scoreWeight;
    this.scoreMax = taskMax && taskMax > 0 ? taskMax : (this.form.maxScore || 100);
    this.scoreForm = { submissionId: item.id, score: Math.min(item.score || 0, this.scoreMax), comment: item.teacherFeedback || '' };
    this.scoreTitle = `实训评分 - ${item.studentName} / ${item.taskTitle}`;
    this.scoreVisible = true;
  }

  openScoreForEnrollment(enrollment: PracticumEnrollmentDto): void {
    const sub = this.latestSubmissionOf(enrollment.id);
    if (sub) { this.openScore(sub); return; }
    this.scoreTarget = null;
    this.scoreEnrollmentId = enrollment.id;
    this.scoreMax = this.form.maxScore || 100;
    this.scoreForm = { submissionId: undefined, score: 0, comment: '' };
    this.scoreTitle = `实训评分 - ${enrollment.studentName || '学生'}`;
    this.scoreVisible = true;
  }

  saveScore(): void {
    if (!this.scoreEnrollmentId) return;
    this.practicumService.scoreEnrollment(this.scoreEnrollmentId, this.scoreForm).subscribe({
      next: () => {
        this.scoreVisible = false;
        this.message.success('评分已保存');
        if (this.selectedProjectId) this.loadEnrollmentsAndSubmissions(this.selectedProjectId);
      },
      error: () => this.message.error('评分失败'),
    });
  }

  openView(item: PracticumSubmissionDto): void {
    this.viewTarget = item;
    this.viewAttachments = this.submissionAttachments(item);
    this.viewVisible = true;
  }

  openViewForEnrollment(enrollment: PracticumEnrollmentDto): void {
    const sub = this.latestSubmissionOf(enrollment.id);
    if (sub) this.openView(sub);
  }

  submissionAttachments(s: PracticumSubmissionDto | null): string[] {
    if (!s?.attachmentUrls) return [];
    return s.attachmentUrls.split('\n').map(u => u.trim()).filter(u => !!u);
  }

  openPreview(url: string): void {
    this.previewUrl = url;
    this.previewType = this.attachmentKind(url);
    this.previewVisible = true;
  }

  private attachmentKind(url: string): 'image' | 'pdf' | 'other' {
    try {
      const ext = (new URL(url).pathname.split('.').pop() || '').toLowerCase();
      if (['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp', 'svg'].includes(ext)) return 'image';
      if (ext === 'pdf') return 'pdf';
    } catch { /* ignore */ }
    return 'other';
  }

  exportScores(): void {
    if (!this.selectedProjectId) return;
    this.practicumService.exportAssessments(this.selectedProjectId).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a'); a.href = url;
        a.download = `实训成绩_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click(); window.URL.revokeObjectURL(url);
      },
      error: () => this.message.error('导出失败'),
    });
  }

  // ─── 指导 ───────────────────────────────

  private emptyGuidance(): CreatePracticumGuidanceRecordDto {
    return { enrollmentId: '', taskId: undefined, content: '', isVisibleToStudent: true };
  }

  openAddGuidance(): void {
    this.guidanceForm = this.emptyGuidance();
    this.guidanceModalVisible = true;
  }

  openGuidanceForStudent(enrollment: PracticumEnrollmentDto): void {
    this.guidanceForm = { enrollmentId: enrollment.id, taskId: undefined, content: '', isVisibleToStudent: true };
    this.guidanceModalVisible = true;
  }

  saveGuidance(): void {
    if (!this.guidanceForm.enrollmentId) { this.message.warning('请选择学生'); return; }
    if (!(this.guidanceForm.content || '').trim()) { this.message.warning('请填写指导意见'); return; }
    const enrollmentId = this.guidanceForm.enrollmentId;
    this.guidanceSaving = true;
    this.practicumService.addGuidance(this.guidanceForm).subscribe({
      next: () => {
        this.guidanceSaving = false;
        this.guidanceModalVisible = false;
        this.message.success('指导记录已保存');
        if (enrollmentId) {
          const entry = this.studentGuidance().find(x => x.enrollment.id === enrollmentId);
          if (entry) this.loadGuidanceForEnrollment(entry.enrollment).subscribe();
        }
      },
      error: () => { this.guidanceSaving = false; this.message.error('指导记录保存失败'); },
    });
  }

  openGuidanceEdit(row: { studentName: string; status: PracticumEnrollmentStatus; record: PracticumGuidanceRecordDto }): void {
    this.guidanceEditRecord = row.record;
    this.guidanceEditForm = { content: row.record.content || '', isVisibleToStudent: row.record.isVisibleToStudent };
    this.guidanceEditVisible = true;
  }

  saveGuidanceEdit(): void {
    if (!this.guidanceEditRecord || !(this.guidanceEditForm.content || '').trim()) {
      this.message.warning('请填写指导意见');
      return;
    }
    const recordId = this.guidanceEditRecord.id;
    const enrollmentId = this.guidanceEditRecord.enrollmentId;
    this.guidanceEditSaving = true;
    this.practicumService.updateGuidance(recordId, this.guidanceEditForm).subscribe({
      next: () => {
        this.guidanceEditSaving = false;
        this.guidanceEditVisible = false;
        this.message.success('指导记录已更新');
        if (enrollmentId) {
          const entry = this.studentGuidance().find(x => x.enrollment.id === enrollmentId);
          if (entry) this.loadGuidanceForEnrollment(entry.enrollment).subscribe();
        }
      },
      error: () => { this.guidanceEditSaving = false; this.message.error('指导记录更新失败'); },
    });
  }

  private loadStudentGuidance(pid: string): void {
    this.guidanceTabLoading.set(true);
    this.practicumService.getEnrollmentList({ projectId: pid, skipCount: 0, maxResultCount: 200 }).subscribe({
      next: r => {
        const enrollments = r.items || [];
        if (!enrollments.length) {
          this.studentGuidance.set([]);
          this.rebuildGuidanceRows();
          this.guidanceTabLoading.set(false);
          return;
        }
        this.studentGuidance.set(enrollments.map(e => ({ enrollment: e, records: [] })));
        forkJoin(enrollments.map(e => this.loadGuidanceForEnrollment(e))).subscribe({
          next: () => this.guidanceTabLoading.set(false),
          error: () => this.guidanceTabLoading.set(false),
        });
      },
      error: () => this.guidanceTabLoading.set(false),
    });
  }

  private loadGuidanceForEnrollment(enrollment: PracticumEnrollmentDto): Observable<void> {
    return this.practicumService.getGuidanceList(enrollment.id).pipe(
      map(records => {
        const current = this.studentGuidance();
        const idx = current.findIndex(x => x.enrollment.id === enrollment.id);
        const next = [...current];
        if (idx >= 0) next[idx] = { enrollment, records: records || [] };
        else next.push({ enrollment, records: records || [] });
        this.studentGuidance.set(next);
        this.rebuildGuidanceRows();
        this.cdr.markForCheck();
      }),
      catchError(() => {
        const current = this.studentGuidance();
        const idx = current.findIndex(x => x.enrollment.id === enrollment.id);
        const next = [...current];
        if (idx >= 0) next[idx] = { enrollment, records: [] };
        this.studentGuidance.set(next);
        this.rebuildGuidanceRows();
        this.cdr.markForCheck();
        return of(undefined);
      }),
    );
  }

  private rebuildGuidanceRows(): void {
    const rows: { studentName: string; status: PracticumEnrollmentStatus; record: PracticumGuidanceRecordDto }[] = [];
    for (const entry of this.studentGuidance()) {
      for (const record of entry.records) {
        rows.push({ studentName: entry.enrollment.studentName || '学生', status: entry.enrollment.status, record });
      }
    }
    this.guidanceRows.set(rows);
  }

  // ─── 标签 ───────────────────────────────

  statusLabel(s: PracticumProjectStatus): string {
    return ({ [PracticumProjectStatus.Draft]: '草稿', [PracticumProjectStatus.Published]: '已发布', [PracticumProjectStatus.Archived]: '已归档' } as Record<number, string>)[s] || '未知';
  }

  enrollmentLabel(s: PracticumEnrollmentStatus | undefined): string {
    return ({ [PracticumEnrollmentStatus.Enrolled]: '已参与', [PracticumEnrollmentStatus.InProgress]: '进行中', [PracticumEnrollmentStatus.Submitted]: '待评阅', [PracticumEnrollmentStatus.Reviewed]: '已评阅', [PracticumEnrollmentStatus.Completed]: '已完成', [PracticumEnrollmentStatus.Cancelled]: '已取消' } as Record<number, string>)[s ?? -1] || '未知';
  }
}
