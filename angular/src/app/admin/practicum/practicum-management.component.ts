import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import {
  CreatePracticumAssessmentDto,
  CreatePracticumGuidanceRecordDto,
  CreateUpdatePracticumProjectDto,
  PracticumEnrollmentDto,
  PracticumProjectDto,
  PracticumProjectStatus,
  PracticumService,
  PracticumSubmissionDto,
  PracticumSubmissionStatus,
} from '../../practicum/practicum.service';

@Component({
  selector: 'app-practicum-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzSwitchModule,
    NzTableModule,
    NzTagModule,
    NzTooltipModule,
    NzIconModule,
  ],
  templateUrl: './practicum-management.component.html',
  styleUrls: ['./practicum-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumManagementComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly courseService = inject(CourseService);
  private readonly message = inject(NzMessageService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly projects = signal<PracticumProjectDto[]>([]);
  readonly enrollments = signal<PracticumEnrollmentDto[]>([]);
  readonly submissions = signal<PracticumSubmissionDto[]>([]);
  readonly courses = signal<CourseDto[]>([]);
  readonly statuses = PracticumProjectStatus;
  readonly submissionStatuses = PracticumSubmissionStatus;

  activeTab = 0;
  selectedProjectId: string | null = null;
  modalVisible = false;
  editingId: string | null = null;

  /** Form for basic info, tasks, and materials (all in one DTO) */
  form: CreateUpdatePracticumProjectDto = this.freshForm();

  guidanceVisible = false;
  scoreVisible = false;
  guidanceTarget: PracticumSubmissionDto | null = null;
  scoreTarget: PracticumSubmissionDto | null = null;
  guidanceForm: CreatePracticumGuidanceRecordDto = this.emptyGuidance();
  scoreForm: CreatePracticumAssessmentDto = this.emptyScore();

  ngOnInit(): void {
    this.loadCourses();
    this.reload();
  }

  private freshForm(): CreateUpdatePracticumProjectDto {
    return { title: '', summary: '', description: '', coverImageUrl: '', courseId: undefined,
      major: '', className: '', status: PracticumProjectStatus.Draft,
      startTime: undefined, endTime: undefined, maxScore: 100, allowResubmission: true,
      tasks: [], materials: [] };
  }

  private emptyGuidance(): CreatePracticumGuidanceRecordDto {
    return { enrollmentId: '', taskId: undefined, content: '', isVisibleToStudent: true };
  }

  private emptyScore(): CreatePracticumAssessmentDto {
    return { submissionId: undefined, score: 0, gradeLevel: '', comment: '', rubricJson: '' };
  }

  private loadCourses(): void {
    this.courseService.getList({ skipCount: 0, maxResultCount: 200 } as any)
      .subscribe(r => { this.courses.set(r.items || []); this.cdr.markForCheck(); });
  }

  reload(): void {
    this.practicumService.getList({ skipCount: 0, maxResultCount: 100 })
      .subscribe(r => { this.projects.set(r.items || []); this.cdr.markForCheck(); });
  }

  // --- Modal create / edit -------------------------------------

  openCreate(): void {
    this.editingId = null;
    this.form = this.freshForm();
    this.modalVisible = true;
    this.cdr.markForCheck();
  }

  openEdit(p: PracticumProjectDto): void {
    this.editingId = p.id;
    this.form = this.freshForm();
    this.modalVisible = true;
    this.cdr.markForCheck();

    this.practicumService.getDetail(p.id).subscribe(detail => {
      this.form = {
        title: detail.title,
        summary: detail.summary || '',
        description: detail.description || '',
        coverImageUrl: detail.coverImageUrl || '',
        courseId: detail.courseId,
        major: detail.major || '',
        className: detail.className || '',
        status: detail.status,
        startTime: detail.startTime,
        endTime: detail.endTime,
        maxScore: detail.maxScore,
        allowResubmission: detail.allowResubmission,
        tasks: detail.tasks.map(t => ({
          title: t.title, description: t.description || '', requirement: t.requirement || '',
          dueTime: t.dueTime, scoreWeight: t.scoreWeight, sortOrder: t.sortOrder,
        })),
        materials: detail.materials.map(m => ({
          taskId: m.taskId, title: m.title, description: m.description || '',
          materialType: m.materialType, resourceUrl: m.resourceUrl, sortOrder: m.sortOrder,
        })),
      };
      this.selectedProjectId = p.id;
      this.activeTab = 1;
      this.modalVisible = false;
      this.loadEnrollmentsAndSubmissions(p.id);
      this.cdr.markForCheck();
    });
  }

  saveModal(): void {
    // P1-15：保存时同时提交任务（原先在 saveModal 里把 tasks/materials 强制清空，迫使用户"先保存基本信息，再到 Tab1 保存任务"——分两步走容易漏）。
    // 现在 modal 内的"基本信息 + 任务配置"是同一个表单，一次性提交。
    const body: CreateUpdatePracticumProjectDto = { ...this.form };
    const obs = this.editingId
      ? this.practicumService.update(this.editingId, body)
      : this.practicumService.create(body);

    obs.subscribe({
      next: r => {
        this.message.success('实训项目已保存');
        this.modalVisible = false;
        this.selectedProjectId = r.id;
        this.activeTab = 1;
        this.cdr.markForCheck();
        // Reload detail so tabs have tasks/materials data
        this.practicumService.getDetail(r.id).subscribe(detail => {
          this.form = {
            title: detail.title,
            summary: detail.summary || '',
            description: detail.description || '',
            coverImageUrl: detail.coverImageUrl || '',
            courseId: detail.courseId,
            major: detail.major || '',
            className: detail.className || '',
            status: detail.status,
            startTime: detail.startTime,
            endTime: detail.endTime,
            maxScore: detail.maxScore,
            allowResubmission: detail.allowResubmission,
            tasks: detail.tasks.map(t => ({
              title: t.title, description: t.description || '', requirement: t.requirement || '',
              dueTime: t.dueTime, scoreWeight: t.scoreWeight, sortOrder: t.sortOrder,
            })),
            materials: detail.materials.map(m => ({
              taskId: m.taskId, title: m.title, description: m.description || '',
              materialType: m.materialType, resourceUrl: m.resourceUrl, sortOrder: m.sortOrder,
            })),
          };
          this.reload();
          this.loadEnrollmentsAndSubmissions(r.id);
          this.cdr.markForCheck();
        });
      },
      error: () => this.message.error('保存失败'),
    });
  }

  /**
   * P1-15：保存按钮的启用条件——至少要有一条任务。
   * 没任务时点保存没意义，禁用 + tooltip 提示用户先加任务。
   */
  get canSaveModal(): boolean {
    return Array.isArray(this.form?.tasks) && this.form.tasks.length > 0;
  }

  get saveModalTooltip(): string {
    return this.canSaveModal ? '' : '请先添加至少一个任务（点击上方"添加任务"按钮）';
  }

  /** Re-open the edit modal from the detail tab */
  openEditFromTab0(): void {
    if (this.selectedProjectId) {
      this.openEdit({ id: this.selectedProjectId } as PracticumProjectDto);
    }
  }

  deleteProject(id: string): void {
    this.practicumService.delete(id).subscribe({
      next: () => {
        this.message.success('实训项目已删除');
        this.selectedProjectId = null;
        this.form = this.freshForm();
        this.activeTab = 0;
        this.enrollments.set([]);
        this.submissions.set([]);
        this.reload();
        this.cdr.markForCheck();
      },
      error: () => this.message.error('删除失败'),
    });
  }

  // --- Tab 2 ------------------------------------------------

  addTask(): void {
    this.form.tasks.push({ title: '', description: '', requirement: '', dueTime: undefined, scoreWeight: 0, sortOrder: this.form.tasks.length + 1 });
  }

  removeTask(i: number): void {
    this.form.tasks.splice(i, 1);
    this.form.tasks.forEach((t, idx) => t.sortOrder = idx + 1);
  }

  saveTasks(): void {
    if (!this.selectedProjectId) { return; }
    this.practicumService.update(this.selectedProjectId, { ...this.form }).subscribe({
      next: () => { this.message.success('任务已保存'); this.refreshDetail(); },
      error: () => this.message.error('保存任务失败'),
    });
  }

  // --- Tab 3 ------------------------------------------------

  addMaterial(): void {
    this.form.materials.push({ title: '', description: '', materialType: 0, resourceUrl: '', sortOrder: this.form.materials.length + 1 });
  }

  removeMaterial(i: number): void {
    this.form.materials.splice(i, 1);
    this.form.materials.forEach((m, idx) => m.sortOrder = idx + 1);
  }

  saveMaterials(): void {
    if (!this.selectedProjectId) { return; }
    this.practicumService.update(this.selectedProjectId, { ...this.form }).subscribe({
      next: () => { this.message.success('资料已保存'); this.refreshDetail(); },
      error: () => this.message.error('保存资料失败'),
    });
  }

  // --- Tab 4 ------------------------------------------------

  private loadEnrollmentsAndSubmissions(pid: string): void {
    this.practicumService.getEnrollmentList({ projectId: pid, skipCount: 0, maxResultCount: 200 })
      .subscribe(r => { this.enrollments.set(r.items || []); this.cdr.markForCheck(); });
    this.practicumService.getSubmissionList({ projectId: pid, skipCount: 0, maxResultCount: 200 })
      .subscribe(r => { this.submissions.set(r.items || []); this.cdr.markForCheck(); });
  }

  private refreshDetail(): void {
    if (this.selectedProjectId) {
      this.openEdit({ id: this.selectedProjectId } as PracticumProjectDto);
    }
  }

  openGuidance(item: PracticumSubmissionDto): void {
    this.guidanceTarget = item;
    this.guidanceForm = { enrollmentId: item.enrollmentId, taskId: item.taskId, content: '', isVisibleToStudent: true };
    this.guidanceVisible = true;
  }

  saveGuidance(): void {
    this.practicumService.addGuidance(this.guidanceForm).subscribe({
      next: () => { this.guidanceVisible = false; this.message.success('指导记录已保存'); },
      error: () => this.message.error('指导记录保存失败'),
    });
  }

  openScore(item: PracticumSubmissionDto): void {
    this.scoreTarget = item;
    this.scoreForm = { submissionId: item.id, score: item.score || 0, gradeLevel: '', comment: item.teacherFeedback || '', rubricJson: '' };
    this.scoreVisible = true;
  }

  saveScore(): void {
    if (!this.scoreTarget) { return; }
    this.practicumService.scoreEnrollment(this.scoreTarget.enrollmentId, this.scoreForm).subscribe({
      next: () => {
        this.scoreVisible = false;
        this.message.success('评分已保存');
        if (this.selectedProjectId) { this.loadEnrollmentsAndSubmissions(this.selectedProjectId); }
      },
      error: () => this.message.error('评分失败'),
    });
  }

  exportScores(): void {
    this.practicumService.exportAssessments(this.selectedProjectId || undefined).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a'); a.href = url;
        a.download = `实训成绩_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click(); window.URL.revokeObjectURL(url);
      },
      error: () => this.message.error('导出失败'),
    });
  }

  statusLabel(s: PracticumProjectStatus): string {
    return ({ [PracticumProjectStatus.Draft]: '草稿', [PracticumProjectStatus.Published]: '已发布', [PracticumProjectStatus.Archived]: '已归档' } as Record<number, string>)[s] || '未知';
  }

  submissionLabel(s: PracticumSubmissionStatus): string {
    return ({ [PracticumSubmissionStatus.Submitted]: '已提交', [PracticumSubmissionStatus.Returned]: '已退回', [PracticumSubmissionStatus.Reviewed]: '已评阅' } as Record<number, string>)[s] || '未知';
  }
}
