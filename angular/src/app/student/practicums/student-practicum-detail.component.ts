import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { NzMessageService } from 'ng-zorro-antd/message';
import { forkJoin } from 'rxjs';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import { PracticumSimulationService } from '../../proxy/practicums/practicum-simulation.service';
import type { PracticumProjectDetailDto, PracticumMaterialDto, PracticumGuidanceRecordDto, PracticumEnrollmentDto } from '../../proxy/practicums/dtos/models';
import type { PracticumSimulationDto } from '../../proxy/practicums/simulations/models';
import { PracticumEnrollmentStatus } from '../../proxy/practicums/enums/practicum-enrollment-status.enum';
import { OssUploadService } from '../../shared/oss-upload.service';
import { ClientCacheService } from '../../shared/cache/client-cache.service';

@Component({
  selector: 'app-student-practicum-detail',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, FormsModule, RouterModule,
    NzButtonModule, NzIconModule, NzSpinModule, NzInputModule, NzModalModule, NzUploadModule,
  ],
  templateUrl: './student-practicum-detail.component.html',
  styleUrls: ['./student-practicum-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentPracticumDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly practicumService = inject(PracticumService);
  private readonly simulationService = inject(PracticumSimulationService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);
  private readonly cache = inject(ClientCacheService);

  readonly detail = signal<PracticumProjectDetailDto | null>(null);
  readonly simulations = signal<PracticumSimulationDto[]>([]);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly submitModalVisible = signal(false);
  readonly selectedTaskId = signal<string | null>(null);
  readonly submissionContent = signal('');
  readonly submissionUrl = signal('');
  readonly attachmentUploading = signal(false);
  readonly attachmentFiles = signal<{ name: string; url: string }[]>([]);

  readonly enrollment = signal<PracticumEnrollmentDto | null>(null);
  /** 当前学生已提交过成果的任务 ID，用于“不允许重复提交”时隐藏入口 */
  readonly submittedTaskIds = signal<Set<string>>(new Set());
  /** 项目是否已完成（用于锁定提交入口） */
  readonly projectCompleted = computed(() => this.enrollment()?.status === PracticumEnrollmentStatus.Completed);
  /** 提交弹窗中当前任务标题 */
  readonly submitTaskTitle = computed(() => {
    const taskId = this.selectedTaskId();
    if (!taskId) return '';
    return this.detail()?.tasks?.find(t => t.id === taskId)?.title || '';
  });
  readonly guidanceItems = signal<PracticumGuidanceRecordDto[]>([]);
  readonly guidanceLoading = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.loadDetailAndSimulations(id);
  }

  loadDetailAndSimulations(id: string): void {
    this.loading.set(true);
    forkJoin({
      detail: this.cache.load<any>('student.practicums', `detail:${id}`, () => this.practicumService.getDetail(id)),
      simulations: this.cache.load<any>('student.practicums', `simulations:${id}`, () => this.simulationService.getListByProject(id)),
    }).subscribe({
      next: ({ detail, simulations }) => {
        this.detail.set({
          ...detail,
          materials: (detail.materials ?? []).filter(material => material.materialType !== 4),
        });
        this.simulations.set(simulations ?? []);
        this.loading.set(false);
        this.loadMyProgress(id);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载实训详情失败');
      },
    });
  }

  loadMyProgress(projectId: string): void {
    const enrollmentId = this.detail()?.currentUserEnrollmentId;
    this.cache.load<any[]>('student.practicums', 'my-enrollments', () => this.practicumService.getMyEnrollments()).subscribe({
      next: list => {
        const mine = (list || []).find(e => e.projectId === projectId && (enrollmentId ? e.id === enrollmentId : true));
        this.enrollment.set(mine ?? null);
        if (mine?.id) {
          this.loadGuidance(mine.id);
          this.loadSubmittedTasks(mine.id);
        }
      },
      error: () => {},
    });
  }

  /** 是否允许对某个任务提交成果：项目已完成则锁定；关闭重复提交时已提交过的不再显示入口（决策 4） */
  canSubmitTask(taskId?: string | null): boolean {
    const en = this.enrollment();
    if (!en || !taskId) return false;
    if (en.status === PracticumEnrollmentStatus.Completed) return false;
    if (this.detail()?.allowResubmission === false && this.submittedTaskIds().has(taskId)) return false;
    return true;
  }

  private loadSubmittedTasks(enrollmentId: string): void {
    this.practicumService.getSubmissionList({ enrollmentId, skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: r => {
        const ids = (r.items || []).map(s => s.taskId).filter((id): id is string => !!id);
        this.submittedTaskIds.set(new Set(ids));
      },
      error: () => {},
    });
  }

  loadGuidance(enrollmentId: string): void {
    this.guidanceLoading.set(true);
    this.cache.load<any[]>('student.practicums', `guidance:${enrollmentId}`, () => this.practicumService.getGuidanceList(enrollmentId)).subscribe({
      next: list => {
        this.guidanceItems.set(list || []);
        this.guidanceLoading.set(false);
      },
      error: () => {
        this.guidanceLoading.set(false);
      },
    });
  }

  hasScore(en: PracticumEnrollmentDto): boolean {
    return en.finalScore != null;
  }

  enroll(): void {
    const id = this.detail()?.id;
    if (!id) return;
    this.practicumService.enroll(id).subscribe({
      next: () => {
        this.message.success('报名成功，等待教师审核');
        this.cache.clear('student.practicums');
        this.loadDetailAndSimulations(id);
      },
      error: () => this.message.error('报名失败'),
    });
  }

  openSubmit(taskId?: string): void {
    // 未指定任务时自动选择第一个可提交的任务
    const targetId = taskId
      || this.detail()?.tasks?.find(t => this.canSubmitTask(t.id))?.id
      || null;
    if (!targetId) {
      this.message.warning(this.projectCompleted() ? '项目已完成，无需再提交' : '当前没有可提交的任务');
      return;
    }
    this.selectedTaskId.set(targetId);
    this.submissionContent.set('');
    this.submissionUrl.set('');
    this.attachmentFiles.set([]);
    this.attachmentUploading.set(false);
    this.submitModalVisible.set(true);
  }

  /** 附件上传前的校验 + 触发 OSS 上传，返回 false 阻止 nz-upload 默认行为。 */
  beforeAttachmentUpload = (file: NzUploadFile): boolean => {
    const rawFile = file as any as File;
    if (rawFile.size > 50 * 1024 * 1024) {
      this.message.error('附件不能超过 50MB');
      return false;
    }
    this.attachmentUploading.set(true);
    this.ossUploadService.uploadFile(rawFile).subscribe({
      next: res => {
        this.attachmentFiles.update(list => [...list, { name: res.originalFileName, url: res.url }]);
        this.attachmentUploading.set(false);
        this.message.success(`附件上传成功：${res.originalFileName}`);
      },
      error: () => {
        this.attachmentUploading.set(false);
        this.message.error('附件上传失败');
      },
    });
    return false;
  };

  removeAttachment(index: number): void {
    this.attachmentFiles.update(list => list.filter((_, i) => i !== index));
  }

  submitWork(): void {
    const projectId = this.detail()?.id;
    const taskId = this.selectedTaskId();
    const content = this.submissionContent().trim();
    if (!projectId || !taskId || !content) {
      this.message.warning('请填写提交内容');
      return;
    }
    this.submitting.set(true);
    const attachmentUrls = this.attachmentFiles().map(f => f.url).join('\n');
    this.practicumService.createSubmission({
      projectId, taskId,
      content,
      attachmentUrls: attachmentUrls || undefined,
      linkUrl: this.submissionUrl().trim() || undefined,
    }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.submitModalVisible.set(false);
        this.message.success('提交成功');
        if (this.detail()?.currentUserEnrollmentId) {
          this.cache.clear('student.practicums');
          this.loadMyProgress(this.detail()!.id);
        }
      },
      error: (err) => {
        this.submitting.set(false);
        // 透传后端业务提示（如“项目不允许重复提交”“项目已完成”），不再统一显示“提交失败”
        this.message.error(err?.error?.error?.message || err?.error?.message || '提交失败');
      },
    });
  }

  fileNameFromUrl(url: string): string {
    try {
      const u = new URL(url);
      const last = u.pathname.split('/').pop() || '附件';
      return decodeURIComponent(last);
    } catch {
      return '附件';
    }
  }

  downloadMaterial(material: PracticumMaterialDto): void {
    if (!material.resourceUrl) return;
    window.open(material.resourceUrl, '_blank');
  }

  openChat(): void {
    const id = this.detail()?.id;
    if (!id) return;
    this.router.navigate(['/student/practicums', id, 'chat']);
  }
}
