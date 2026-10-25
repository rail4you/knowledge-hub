import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { NzMessageService } from 'ng-zorro-antd/message';
import { forkJoin } from 'rxjs';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import { PracticumSimulationService } from '../../proxy/practicums/practicum-simulation.service';
import type {
  PracticumEnrollmentDto,
  PracticumGuidanceRecordDto,
  PracticumMaterialDto,
  PracticumProjectDetailDto,
  PracticumSubmissionDto,
  PracticumTaskDto,
  PracticumTimelineItemDto,
} from '../../proxy/practicums/dtos/models';
import type { PracticumSimulationDto } from '../../proxy/practicums/simulations/models';
import { PracticumEnrollmentStatus } from '../../proxy/practicums/enums/practicum-enrollment-status.enum';
import { PracticumSimulationStatus } from '../../proxy/practicums/enums/practicum-simulation-status.enum';
import { PracticumSubmissionStatus } from '../../proxy/practicums/enums/practicum-submission-status.enum';
import { OssUploadService } from '../../shared/oss-upload.service';
import { ClientCacheService } from '../../shared/cache/client-cache.service';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import {
  AI_ASSIST_DISCLOSURE,
  ENROLLMENT_STATUS_LABELS,
  SUBMISSION_STATUS_LABELS,
  WORK_STATE_LABELS,
  WORK_STATE_TONES,
  scoreView,
  workStateOf,
  type PracticumWorkState,
} from './practicum-labels';

/** 阶段推进的一个阶段 */
interface Stage {
  title: string;
  desc: string;
}

/** 主区页签 */
type DetailTab = 'tasks' | 'timeline' | 'simulations' | 'materials';

/**
 * 学生端 · 实训详情（原型 #simulation）。
 *
 * 结构：项目 Hero → 阶段推进 → 主区页签（实训任务 / 过程记录 / 仿真镜像 / 实训资料）
 *      → 右侧「任务要求」kv 与「我的成绩 / 实训报告」。
 *
 * 成绩状态严格区分三种情况：
 *   未提交（没有提交记录）/ 待批阅（已提交未批阅）/ 零分（已批阅且分数为 0）。
 */
@Component({
  selector: 'app-student-practicum-detail',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzUploadModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
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
  readonly loadFailed = signal(false);

  readonly enrollment = signal<PracticumEnrollmentDto | null>(null);
  readonly submissions = signal<PracticumSubmissionDto[]>([]);
  readonly timeline = signal<PracticumTimelineItemDto[]>([]);
  readonly timelineLoading = signal(false);
  readonly guidanceItems = signal<PracticumGuidanceRecordDto[]>([]);
  readonly guidanceLoading = signal(false);

  /** 主区页签 */
  readonly tab = signal<DetailTab>('tasks');

  /** 提交弹窗 */
  readonly submitting = signal(false);
  readonly submitModalVisible = signal(false);
  readonly selectedTaskId = signal<string | null>(null);
  readonly submissionContent = signal('');
  readonly submissionUrl = signal('');
  readonly submissionAi = signal(false);
  readonly attachmentUploading = signal(false);
  readonly attachmentFiles = signal<{ name: string; url: string }[]>([]);

  /** 阶段推进定义（原型 #simulation 的四阶段） */
  readonly stages: Stage[] = [
    { title: '准备阶段', desc: '阅读任务书与成果要求' },
    { title: '阶段一 · 提交过程与成果', desc: '按任务提交成果说明' },
    { title: '阶段二 · 教师批阅', desc: '教师给出分数与评语' },
    { title: '阶段三 · 已反馈', desc: '查看成绩与教师指导记录' },
  ];

  /** 我的报名记录 */
  readonly myEnrollment = computed(() => this.enrollment());

  /** 已就绪的仿真镜像（仅 Ready） */
  readonly readySimulations = computed(() =>
    this.simulations().filter(s => s.status === PracticumSimulationStatus.Ready),
  );

  /** 我的提交记录 */
  readonly mySubmissions = computed(() => this.submissions());

  /** 实训报告条目（来自过程记录里的教师评分事件） */
  readonly assessmentReports = computed(() => {
    const report = this.timeline().filter(item => item.type === 'Assessment');
    return report.length ? report : [];
  });

  /** 阶段推进：每个阶段是否已完成 + 当前阶段 */
  readonly stageState = computed(() => {
    const en = this.enrollment();
    const subs = this.mySubmissions();
    const reviewed = subs.filter(s => s.status === PracticumSubmissionStatus.Reviewed);
    const returned = subs.some(s => s.status === PracticumSubmissionStatus.Returned);
    const submitted = subs.length > 0;
    const scored = en?.finalScore !== null && en?.finalScore !== undefined;

    const done = [
      !!en,
      submitted,
      reviewed.length > 0,
      scored,
    ];
    let current = 0;
    if (en) current = 1;
    if (submitted) current = 2;
    if (reviewed.length > 0) current = 3;
    if (scored) current = 4;
    return { done, current, returned, submitted, reviewed: reviewed.length, scored };
  });

  /** 项目是否已完成（锁定提交入口） */
  readonly projectCompleted = computed(() => this.enrollment()?.status === PracticumEnrollmentStatus.Completed);

  /** 提交弹窗中当前任务标题 */
  readonly submitTaskTitle = computed(() => {
    const taskId = this.selectedTaskId();
    if (!taskId) return '';
    return this.detail()?.tasks?.find(t => t.id === taskId)?.title || '';
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.loading.set(false);
      this.loadFailed.set(true);
      return;
    }
    this.loadDetailAndSimulations(id);
  }

  /** 加载项目详情与仿真镜像 */
  loadDetailAndSimulations(id: string): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    forkJoin({
      detail: this.cache.load<PracticumProjectDetailDto>('student.practicums', `detail:${id}`, () =>
        this.practicumService.getDetail(id),
      ),
      simulations: this.cache.load<PracticumSimulationDto[]>('student.practicums', `simulations:${id}`, () =>
        this.simulationService.getListByProject(id),
      ),
    }).subscribe({
      next: ({ detail, simulations }) => {
        this.detail.set({
          ...detail,
          materials: (detail?.materials ?? []).filter(material => material.materialType !== 4),
        });
        this.simulations.set(simulations ?? []);
        this.loading.set(false);
        this.loadMyProgress(id);
      },
      error: () => {
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  /** 加载当前学生的报名、提交、过程记录与指导 */
  loadMyProgress(projectId: string): void {
    // 学生只能看到自己的报名记录：get-enrollment-list 需要教师评阅权限，学生端用 my-enrollments。
    this.practicumService.getMyEnrollments().subscribe({
      next: list => {
        const enrollmentId = this.detail()?.currentUserEnrollmentId;
        const mine = (list || []).find(e => e.projectId === projectId && (enrollmentId ? e.id === enrollmentId : true));
        this.enrollment.set(mine ?? null);
        if (mine?.id) {
          this.loadSubmissions(mine.id);
          this.loadTimeline(mine.id);
          this.loadGuidance(mine.id);
        }
      },
      error: () => undefined,
    });
  }

  /** 我的提交记录 */
  private loadSubmissions(enrollmentId: string): void {
    this.practicumService.getSubmissionList({ enrollmentId, skipCount: 0, maxResultCount: 100 }).subscribe({
      next: result => this.submissions.set(result?.items || []),
      error: () => this.submissions.set([]),
    });
  }

  /** 过程记录 */
  loadTimeline(enrollmentId: string): void {
    this.timelineLoading.set(true);
    this.practicumService.getTimeline(enrollmentId).subscribe({
      next: list => {
        this.timeline.set(list || []);
        this.timelineLoading.set(false);
      },
      error: () => {
        this.timeline.set([]);
        this.timelineLoading.set(false);
      },
    });
  }

  /** 教师指导记录 */
  loadGuidance(enrollmentId: string): void {
    this.guidanceLoading.set(true);
    this.practicumService.getGuidanceList(enrollmentId).subscribe({
      next: list => {
        this.guidanceItems.set(list || []);
        this.guidanceLoading.set(false);
      },
      error: () => {
        this.guidanceItems.set([]);
        this.guidanceLoading.set(false);
      },
    });
  }

  /** 重新加载 */
  reload(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.loadDetailAndSimulations(id);
  }

  /** 切换主区页签 */
  setTab(tab: DetailTab): void {
    this.tab.set(tab);
  }

  // ── 状态派生 ────────────────────────────────────────────────

  /** 任务的提交记录（取版本号最大的那条） */
  submissionOfTask(task: PracticumTaskDto): PracticumSubmissionDto | null {
    const list = this.mySubmissions().filter(s => s.taskId === task.id);
    if (!list.length) return null;
    return list.reduce((a, b) => ((b.versionNo ?? 0) > (a.versionNo ?? 0) ? b : a));
  }

  /** 任务状态 */
  taskState(task: PracticumTaskDto): PracticumWorkState {
    return workStateOf(this.submissionOfTask(task));
  }

  /** 任务状态文案 */
  taskStateLabel(task: PracticumTaskDto): string {
    return WORK_STATE_LABELS[this.taskState(task)];
  }

  /** 任务状态色调 */
  taskStateTone(task: PracticumTaskDto): string {
    return WORK_STATE_TONES[this.taskState(task)];
  }

  /** 提交记录状态文案 */
  submissionStatusLabel(submission: PracticumSubmissionDto): string {
    if (submission.status === undefined || submission.status === null) return '待批阅';
    return SUBMISSION_STATUS_LABELS[submission.status] ?? '待批阅';
  }

  /** 报名状态文案 */
  enrollmentStatusLabel(): string {
    const status = this.enrollment()?.status;
    if (status === undefined || status === null) return '未报名';
    return ENROLLMENT_STATUS_LABELS[status] ?? '未报名';
  }

  /** 成绩文案（未评分 / 0 分 / N 分） */
  scoreText(): string {
    return scoreView(this.enrollment()?.finalScore).text;
  }

  /** 成绩色调 */
  scoreTone(): string {
    return scoreView(this.enrollment()?.finalScore).tone;
  }

  /** 成绩状态（未提交 / 待批阅 / 零分 / 已反馈） */
  scoreStateText(): string {
    const en = this.enrollment();
    if (!en) return '未报名';
    const subs = this.mySubmissions();
    if (!subs.length) return '未提交';
    if (subs.some(s => s.status === PracticumSubmissionStatus.Returned)) return '待修改（退回）';
    if (subs.some(s => s.status !== PracticumSubmissionStatus.Reviewed)) return '待批阅';
    return en.finalScore === 0 ? '已反馈（零分）' : '已反馈';
  }

  /** 是否允许对某个任务提交成果 */
  canSubmitTask(taskId?: string | null): boolean {
    const en = this.enrollment();
    if (!en || !taskId) return false;
    if (en.status === PracticumEnrollmentStatus.Completed) return false;
    if (this.detail()?.isExpired) return false;
    return true;
  }

  // ── 交互 ────────────────────────────────────────────────────

  /** 报名 */
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

  /** 打开提交弹窗 */
  openSubmit(taskId?: string): void {
    const targetId =
      taskId || this.detail()?.tasks?.find(t => this.canSubmitTask(t.id))?.id || null;
    if (!targetId) {
      this.message.warning(this.projectCompleted() ? '项目已完成，无需再提交' : '当前没有可提交的任务');
      return;
    }
    this.selectedTaskId.set(targetId);
    this.submissionContent.set('');
    this.submissionUrl.set('');
    this.submissionAi.set(false);
    this.attachmentFiles.set([]);
    this.attachmentUploading.set(false);
    this.submitModalVisible.set(true);
  }

  /** 附件上传前的校验 + 触发 OSS 上传 */
  beforeAttachmentUpload = (file: NzUploadFile): boolean => {
    const rawFile = file as unknown as File;
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

  /** 移除附件 */
  removeAttachment(index: number): void {
    this.attachmentFiles.update(list => list.filter((_, i) => i !== index));
  }

  /** 提交成果 */
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
    const payload = this.submissionAi() ? `${content}\n${AI_ASSIST_DISCLOSURE}` : content;
    this.practicumService
      .createSubmission({
        projectId,
        taskId,
        content: payload,
        attachmentUrls: attachmentUrls || undefined,
        linkUrl: this.submissionUrl().trim() || undefined,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.submitModalVisible.set(false);
          this.message.success('提交成功');
          this.cache.clear('student.practicums');
          const enrollmentId = this.enrollment()?.id;
          if (enrollmentId) {
            this.loadSubmissions(enrollmentId);
            this.loadTimeline(enrollmentId);
          }
        },
        error: err => {
          this.submitting.set(false);
          this.message.error(err?.error?.error?.message || err?.error?.message || '提交失败');
        },
      });
  }

  /** 下载 / 打开资料 */
  downloadMaterial(material: PracticumMaterialDto): void {
    if (!material.resourceUrl) return;
    window.open(material.resourceUrl, '_blank');
  }

  /** 打开实训沟通 */
  openChat(): void {
    const id = this.detail()?.id;
    if (!id) return;
    this.router.navigate(['/student/practicums', id, 'chat']);
  }
}
