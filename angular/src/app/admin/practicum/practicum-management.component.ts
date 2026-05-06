import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import {
  CreatePracticumAssessmentDto,
  CreatePracticumGuidanceRecordDto,
  CreateUpdatePracticumMaterialDto,
  CreateUpdatePracticumProjectDto,
  CreateUpdatePracticumTaskDto,
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
    NzDatePickerModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzSwitchModule,
    NzTableModule,
  ],
  templateUrl: './practicum-management.component.html',
  styleUrls: ['./practicum-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumManagementComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly courseService = inject(CourseService);
  private readonly message = inject(NzMessageService);

  readonly projects = signal<PracticumProjectDto[]>([]);
  readonly enrollments = signal<PracticumEnrollmentDto[]>([]);
  readonly submissions = signal<PracticumSubmissionDto[]>([]);
  readonly courses = signal<CourseDto[]>([]);
  readonly statuses = PracticumProjectStatus;
  readonly submissionStatuses = PracticumSubmissionStatus;

  modalVisible = false;
  guidanceVisible = false;
  scoreVisible = false;
  editingId: string | null = null;
  selectedProjectId: string | null = null;
  guidanceTarget: PracticumSubmissionDto | null = null;
  scoreTarget: PracticumSubmissionDto | null = null;

  form: CreateUpdatePracticumProjectDto = this.createEmptyForm();
  guidanceForm: CreatePracticumGuidanceRecordDto = this.createEmptyGuidanceForm();
  scoreForm: CreatePracticumAssessmentDto = this.createEmptyScoreForm();

  ngOnInit(): void {
    this.loadCourses();
    this.reload();
  }

  createEmptyForm(): CreateUpdatePracticumProjectDto {
    return {
      title: '',
      summary: '',
      description: '',
      coverImageUrl: '',
      courseId: undefined,
      major: '',
      className: '',
      status: PracticumProjectStatus.Draft,
      startTime: undefined,
      endTime: undefined,
      maxScore: 100,
      allowResubmission: true,
      tasks: [{ title: '', description: '', requirement: '', dueTime: undefined, scoreWeight: 100, sortOrder: 1 }],
      materials: [{ title: '', description: '', materialType: 0, resourceUrl: '', sortOrder: 1 }],
    };
  }

  createEmptyGuidanceForm(): CreatePracticumGuidanceRecordDto {
    return {
      enrollmentId: '',
      taskId: undefined,
      content: '',
      isVisibleToStudent: true,
    };
  }

  createEmptyScoreForm(): CreatePracticumAssessmentDto {
    return {
      submissionId: undefined,
      score: 0,
      gradeLevel: '',
      comment: '',
      rubricJson: '',
    };
  }

  loadCourses(): void {
    this.courseService.getList({
      skipCount: 0,
      maxResultCount: 200,
    } as any).subscribe(result => this.courses.set(result.items || []));
  }

  reload(): void {
    this.practicumService.getList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => {
      this.projects.set(result.items || []);
      const firstProjectId = this.selectedProjectId || result.items?.[0]?.id || null;
      if (firstProjectId) {
        this.selectProject(firstProjectId);
      }
    });
  }

  selectProject(projectId: string): void {
    this.selectedProjectId = projectId;
    this.practicumService.getEnrollmentList({
      projectId,
      skipCount: 0,
      maxResultCount: 200,
    }).subscribe(result => this.enrollments.set(result.items || []));

    this.practicumService.getSubmissionList({
      projectId,
      skipCount: 0,
      maxResultCount: 200,
    }).subscribe(result => this.submissions.set(result.items || []));
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.createEmptyForm();
    this.modalVisible = true;
  }

  openEdit(item: PracticumProjectDto): void {
    this.editingId = item.id;
    this.practicumService.getDetail(item.id).subscribe(detail => {
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
        tasks: detail.tasks.map(x => ({
          title: x.title,
          description: x.description || '',
          requirement: x.requirement || '',
          dueTime: x.dueTime,
          scoreWeight: x.scoreWeight,
          sortOrder: x.sortOrder,
        })),
        materials: detail.materials.map(x => ({
          taskId: x.taskId,
          title: x.title,
          description: x.description || '',
          materialType: x.materialType,
          resourceUrl: x.resourceUrl,
          sortOrder: x.sortOrder,
        })),
      };
      this.modalVisible = true;
    });
  }

  addTask(): void {
    this.form.tasks.push({
      title: '',
      description: '',
      requirement: '',
      dueTime: undefined,
      scoreWeight: 0,
      sortOrder: this.form.tasks.length + 1,
    });
  }

  removeTask(index: number): void {
    this.form.tasks.splice(index, 1);
    this.reorderTasks();
  }

  reorderTasks(): void {
    this.form.tasks.forEach((item, index) => item.sortOrder = index + 1);
  }

  addMaterial(): void {
    this.form.materials.push({
      title: '',
      description: '',
      materialType: 0,
      resourceUrl: '',
      sortOrder: this.form.materials.length + 1,
    });
  }

  removeMaterial(index: number): void {
    this.form.materials.splice(index, 1);
    this.form.materials.forEach((item, idx) => item.sortOrder = idx + 1);
  }

  save(): void {
    const request = this.editingId
      ? this.practicumService.update(this.editingId, this.form)
      : this.practicumService.create(this.form);

    request.subscribe({
      next: () => {
        this.modalVisible = false;
        this.message.success('实训项目已保存');
        this.reload();
      },
      error: () => this.message.error('保存失败'),
    });
  }

  delete(id: string): void {
    this.practicumService.delete(id).subscribe({
      next: () => {
        this.message.success('实训项目已删除');
        this.reload();
      },
      error: () => this.message.error('删除失败'),
    });
  }

  openGuidance(item: PracticumSubmissionDto): void {
    this.guidanceTarget = item;
    this.guidanceForm = {
      enrollmentId: item.enrollmentId,
      taskId: item.taskId,
      content: '',
      isVisibleToStudent: true,
    };
    this.guidanceVisible = true;
  }

  saveGuidance(): void {
    this.practicumService.addGuidance(this.guidanceForm).subscribe({
      next: () => {
        this.guidanceVisible = false;
        this.message.success('指导记录已保存');
      },
      error: () => this.message.error('指导记录保存失败'),
    });
  }

  openScore(item: PracticumSubmissionDto): void {
    this.scoreTarget = item;
    this.scoreForm = {
      submissionId: item.id,
      score: item.score || 0,
      gradeLevel: '',
      comment: item.teacherFeedback || '',
      rubricJson: '',
    };
    this.scoreVisible = true;
  }

  saveScore(): void {
    if (!this.scoreTarget) {
      return;
    }

    this.practicumService.scoreEnrollment(this.scoreTarget.enrollmentId, this.scoreForm).subscribe({
      next: () => {
        this.scoreVisible = false;
        this.message.success('评分已保存');
        if (this.selectedProjectId) {
          this.selectProject(this.selectedProjectId);
        }
      },
      error: () => this.message.error('评分失败'),
    });
  }

  export(): void {
    this.practicumService.exportAssessments(this.selectedProjectId || undefined).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `实训成绩_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: () => this.message.error('导出失败'),
    });
  }

  getStatusLabel(status: PracticumProjectStatus): string {
    const labels: Record<number, string> = {
      [PracticumProjectStatus.Draft]: '草稿',
      [PracticumProjectStatus.Published]: '已发布',
      [PracticumProjectStatus.Archived]: '已归档',
    };
    return labels[status] || '未知';
  }

  getSubmissionStatusLabel(status: PracticumSubmissionStatus): string {
    const labels: Record<number, string> = {
      [PracticumSubmissionStatus.Submitted]: '已提交',
      [PracticumSubmissionStatus.Returned]: '已退回',
      [PracticumSubmissionStatus.Reviewed]: '已评阅',
    };
    return labels[status] || '未知';
  }
}
