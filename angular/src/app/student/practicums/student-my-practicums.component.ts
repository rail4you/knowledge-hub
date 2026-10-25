import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import { PracticumSimulationService } from '../../proxy/practicums/practicum-simulation.service';
import type {
  PracticumEnrollmentDto,
  PracticumGuidanceRecordDto,
  PracticumProjectDetailDto,
  PracticumSubmissionDto,
  PracticumTaskDto,
  PracticumTimelineItemDto,
} from '../../proxy/practicums/dtos/models';
import type { PracticumSimulationDto } from '../../proxy/practicums/simulations/models';
import { PracticumSimulationStatus } from '../../proxy/practicums/enums/practicum-simulation-status.enum';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import {
  AI_ASSIST_DISCLOSURE,
  PROJECT_WORK_STATE_LABELS,
  PROJECT_WORK_STATE_TONES,
  SUBMISSION_STATUS_LABELS,
  WORK_STATE_LABELS,
  WORK_STATE_TONES,
  projectWorkStateOf,
  scoreView,
  workStateOf,
  type PracticumProjectWorkState,
  type PracticumWorkState,
} from './practicum-labels';

/** 业务链五步（原型 #practicum-workspace 的说明条） */
interface ChainStep {
  title: string;
  desc: string;
  /** 尚未建设的环节，如实标注 */
  pending?: boolean;
}

/**
 * 学生端 · 我的实训（原型 #practicum-workspace）。
 *
 * 结构：业务链说明 → 实训任务卡（要求 / 截止 / 所属课程 / 成果要求）
 *      → 成果说明 + 「本次成果使用过 AI 辅助」→ 提交
 *      → 教师反馈块（分数 / 评语 / 提交状态）→ 过程记录 / 教师指导。
 *
 * 状态机（严格区分，绝不合并）：
 *   未提交 —— 该任务没有任何提交记录
 *   待批阅 —— 已提交、教师尚未批阅
 *   待修改（退回）—— 教师退回，submission.status = Returned
 *   已反馈 —— 已批阅；成绩可能是 0 分（零分），与「未评分」不同
 */
@Component({
  selector: 'app-student-my-practicums',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
  ],
  templateUrl: './student-my-practicums.component.html',
  styleUrls: ['./student-my-practicums.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyPracticumsComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly simulationService = inject(PracticumSimulationService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly enrollments = signal<PracticumEnrollmentDto[]>([]);

  /** 项目详情（按 projectId） */
  readonly details = signal<Record<string, PracticumProjectDetailDto | null>>({});
  /** 提交记录（按 enrollmentId） */
  readonly submissions = signal<Record<string, PracticumSubmissionDto[]>>({});
  /** 仿真镜像（按 projectId，仅 Ready） */
  readonly simulations = signal<Record<string, PracticumSimulationDto[]>>({});
  /** 过程记录（按 enrollmentId，按需加载） */
  readonly timelines = signal<Record<string, PracticumTimelineItemDto[]>>({});
  /** 教师指导（按 enrollmentId，按需加载） */
  readonly guidances = signal<Record<string, PracticumGuidanceRecordDto[]>>({});
  /** 折叠区展开状态（按 enrollmentId） */
  readonly expanded = signal<Record<string, 'timeline' | 'guidance' | null>>({});
  /** 各工作区加载状态 */
  readonly workspaceLoading = signal<Record<string, boolean>>({});
  readonly workspaceFailed = signal<Record<string, boolean>>({});

  /** 成果说明草稿（key = enrollmentId + '|' + taskId） */
  readonly draftContent = signal<Record<string, string>>({});
  /** 「本次成果使用过 AI 辅助」勾选（key 同上） */
  readonly draftAi = signal<Record<string, boolean>>({});
  /** 正在提交的 key */
  readonly submittingKey = signal<string | null>(null);

  /** 业务链五步 */
  readonly chain: ChainStep[] = [
    { title: '关联课程', desc: '实训项目与课程绑定，任务要求来自课程目标。' },
    { title: '明确接收班级', desc: '项目指定接收班级，成果与考核归属该班级。' },
    { title: '提交过程与成果', desc: '按任务要求提交成果说明，可声明是否使用 AI 辅助。' },
    { title: '教师反馈', desc: '教师批阅后给出分数与评语，退回修改会单独标注。' },
    { title: '个人成果归档', desc: '个人成果档案能力建设后开放。', pending: true },
  ];

  /** 我的实训整体状态（用于顶部说明） */
  readonly summary = computed(() => {
    const list = this.enrollments();
    return {
      total: list.length,
      pending: list.filter(e => this.stateOf(e) === 'pending-review').length,
      todo: list.filter(e => {
        const state = this.stateOf(e);
        return state === 'not-submitted' || state === 'in-progress';
      }).length,
      done: list.filter(e => this.stateOf(e) === 'scored').length,
    };
  });

  ngOnInit(): void {
    this.reload();
  }

  /** 重新加载我的实训 */
  reload(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.practicumService.getMyEnrollments().subscribe({
      next: list => {
        const enrollments = list || [];
        this.enrollments.set(enrollments);
        this.loading.set(false);
        enrollments.forEach(e => this.loadWorkspace(e));
      },
      error: () => {
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  /** 加载单个实训工作区（项目详情 + 提交记录 + 仿真镜像） */
  private loadWorkspace(enrollment: PracticumEnrollmentDto): void {
    if (!enrollment.projectId) return;
    const projectId = enrollment.projectId;
    const enrollmentId = enrollment.id;
    this.workspaceLoading.update(m => ({ ...m, [enrollmentId]: true }));
    this.workspaceFailed.update(m => ({ ...m, [enrollmentId]: false }));

    this.practicumService.getDetail(projectId).subscribe({
      next: detail => {
        this.details.update(m => ({ ...m, [projectId]: detail }));
        this.workspaceLoading.update(m => ({ ...m, [enrollmentId]: false }));
      },
      error: () => {
        this.details.update(m => ({ ...m, [projectId]: null }));
        this.workspaceFailed.update(m => ({ ...m, [enrollmentId]: true }));
        this.workspaceLoading.update(m => ({ ...m, [enrollmentId]: false }));
      },
    });

    if (enrollmentId) {
      this.practicumService
        .getSubmissionList({ enrollmentId, skipCount: 0, maxResultCount: 100 })
        .subscribe({
          next: result => this.submissions.update(m => ({ ...m, [enrollmentId]: result?.items || [] })),
          error: () => this.submissions.update(m => ({ ...m, [enrollmentId]: [] })),
        });
    }

    this.simulationService.getListByProject(projectId).subscribe({
      next: list =>
        this.simulations.update(m => ({
          ...m,
          [projectId]: (list || []).filter(s => s.status === PracticumSimulationStatus.Ready),
        })),
      error: () => this.simulations.update(m => ({ ...m, [projectId]: [] })),
    });
  }

  /** 展开/收起过程记录（首次展开时按需加载） */
  toggleTimeline(enrollment: PracticumEnrollmentDto): void {
    const key = enrollment.id;
    if (!key) return;
    const current = this.expanded()[key] ?? null;
    if (current === 'timeline') {
      this.expanded.update(m => ({ ...m, [key]: null }));
      return;
    }
    this.expanded.update(m => ({ ...m, [key]: 'timeline' }));
    if (!this.timelines()[key]) {
      this.practicumService.getTimeline(key).subscribe({
        next: list => this.timelines.update(m => ({ ...m, [key]: list || [] })),
        error: () => this.timelines.update(m => ({ ...m, [key]: [] })),
      });
    }
  }

  /** 展开/收起教师指导（首次展开时按需加载） */
  toggleGuidance(enrollment: PracticumEnrollmentDto): void {
    const key = enrollment.id;
    if (!key) return;
    const current = this.expanded()[key] ?? null;
    if (current === 'guidance') {
      this.expanded.update(m => ({ ...m, [key]: null }));
      return;
    }
    this.expanded.update(m => ({ ...m, [key]: 'guidance' }));
    if (!this.guidances()[key]) {
      this.practicumService.getGuidanceList(key).subscribe({
        next: list => this.guidances.update(m => ({ ...m, [key]: list || [] })),
        error: () => this.guidances.update(m => ({ ...m, [key]: [] })),
      });
    }
  }

  // ── 状态派生 ────────────────────────────────────────────────

  /** 某条记录的提交列表 */
  submissionsOf(enrollment: PracticumEnrollmentDto): PracticumSubmissionDto[] {
    return this.submissions()[enrollment.id || ''] || [];
  }

  /** 项目整体状态 */
  stateOf(enrollment: PracticumEnrollmentDto): PracticumProjectWorkState {
    const detail = this.details()[enrollment.projectId || ''];
    const tasks = detail?.tasks || [];
    return projectWorkStateOf(enrollment, this.submissionsOf(enrollment), tasks.length);
  }

  /** 项目整体状态文案 */
  stateLabel(enrollment: PracticumEnrollmentDto): string {
    return PROJECT_WORK_STATE_LABELS[this.stateOf(enrollment)];
  }

  /** 项目整体状态色调 */
  stateTone(enrollment: PracticumEnrollmentDto): string {
    return PROJECT_WORK_STATE_TONES[this.stateOf(enrollment)];
  }

  /** 任务的提交记录（取版本号最大的那条） */
  submissionOfTask(enrollment: PracticumEnrollmentDto, task: PracticumTaskDto): PracticumSubmissionDto | null {
    const list = this.submissionsOf(enrollment).filter(s => s.taskId === task.id);
    if (!list.length) return null;
    return list.reduce((a, b) => ((b.versionNo ?? 0) > (a.versionNo ?? 0) ? b : a));
  }

  /** 任务状态文案 */
  taskStateLabel(enrollment: PracticumEnrollmentDto, task: PracticumTaskDto): string {
    return WORK_STATE_LABELS[workStateOf(this.submissionOfTask(enrollment, task))];
  }

  /** 任务状态色调 */
  taskStateTone(enrollment: PracticumEnrollmentDto, task: PracticumTaskDto): string {
    return WORK_STATE_TONES[workStateOf(this.submissionOfTask(enrollment, task))];
  }

  /** 任务状态（类型化，便于模板调用） */
  taskState(enrollment: PracticumEnrollmentDto, task: PracticumTaskDto): PracticumWorkState {
    return workStateOf(this.submissionOfTask(enrollment, task));
  }

  /** 提交记录状态文案 */
  submissionStatusLabel(submission: PracticumSubmissionDto): string {
    if (submission.status === undefined || submission.status === null) return '待批阅';
    return SUBMISSION_STATUS_LABELS[submission.status] ?? '待批阅';
  }

  /** 成绩（未评分 / 0 分 / N 分 三者区分） */
  scoreText(enrollment: PracticumEnrollmentDto): string {
    return scoreView(enrollment.finalScore).text;
  }

  /** 成绩色调 */
  scoreTone(enrollment: PracticumEnrollmentDto): string {
    return scoreView(enrollment.finalScore).tone;
  }

  /** 任务截止时间（取项目下最晚的任务截止；无任务时用项目结束时间） */
  deadlineText(enrollment: PracticumEnrollmentDto): string {
    const detail = this.details()[enrollment.projectId || ''];
    const dues = (detail?.tasks || [])
      .map(t => t.dueTime)
      .filter((d): d is string => !!d)
      .sort();
    if (dues.length) return dues[dues.length - 1].slice(0, 10);
    const end = detail?.endTime;
    return end ? end.slice(0, 10) : '以任务内截止时间为准';
  }

  /** 成果要求（优先任务要求，其次任务描述） */
  requirementText(enrollment: PracticumEnrollmentDto, task: PracticumTaskDto): string {
    return task.requirement || task.description || '按任务说明提交过程记录与成果。';
  }

  /** 所属课程 */
  courseTitle(enrollment: PracticumEnrollmentDto): string {
    return this.details()[enrollment.projectId || '']?.courseTitle || '未关联课程';
  }

  /** 接收班级 */
  className(enrollment: PracticumEnrollmentDto): string {
    return this.details()[enrollment.projectId || '']?.className || '未指定班级';
  }

  /** 任务清单 */
  tasksOf(enrollment: PracticumEnrollmentDto): PracticumTaskDto[] {
    return this.details()[enrollment.projectId || '']?.tasks || [];
  }

  /** 该项目的仿真镜像（仅 Ready） */
  simulationsOf(enrollment: PracticumEnrollmentDto): PracticumSimulationDto[] {
    return this.simulations()[enrollment.projectId || ''] || [];
  }

  // ── 提交 ────────────────────────────────────────────────────

  /** 草稿 key（模板需要，用于禁用态判断） */
  draftKeyOf(enrollment: PracticumEnrollmentDto, task: PracticumTaskDto): string {
    return `${enrollment.id || ''}|${task.id || ''}`;
  }

  /** 读取草稿内容 */
  draftOf(enrollment: PracticumEnrollmentDto, task: PracticumTaskDto): string {
    return this.draftContent()[this.draftKeyOf(enrollment, task)] ?? '';
  }

  /** 写入草稿内容 */
  setDraft(enrollment: PracticumEnrollmentDto, task: PracticumTaskDto, value: string): void {
    const key = this.draftKeyOf(enrollment, task);
    this.draftContent.update(m => ({ ...m, [key]: value }));
  }

  /** 是否勾选 AI 辅助声明 */
  aiOf(enrollment: PracticumEnrollmentDto, task: PracticumTaskDto): boolean {
    return this.draftAi()[this.draftKeyOf(enrollment, task)] ?? false;
  }

  /** 勾选 AI 辅助声明 */
  setAi(enrollment: PracticumEnrollmentDto, task: PracticumTaskDto, value: boolean): void {
    const key = this.draftKeyOf(enrollment, task);
    this.draftAi.update(m => ({ ...m, [key]: value }));
  }

  /** 提交成果 */
  submitTask(enrollment: PracticumEnrollmentDto, task: PracticumTaskDto): void {
    const key = this.draftKeyOf(enrollment, task);
    const content = (this.draftContent()[key] ?? '').trim();
    if (!content) {
      this.message.warning('请填写成果说明后再提交。');
      return;
    }
    if (!enrollment.projectId || !task.id) {
      this.message.warning('任务信息不完整，无法提交。');
      return;
    }
    const withDisclosure = this.draftAi()[key] ? `${content}\n${AI_ASSIST_DISCLOSURE}` : content;

    this.submittingKey.set(key);
    this.practicumService
      .createSubmission({
        projectId: enrollment.projectId,
        taskId: task.id,
        content: withDisclosure,
      })
      .subscribe({
        next: () => {
          this.submittingKey.set(null);
          this.message.success('提交成功，等待教师批阅。');
          this.refreshWorkspace(enrollment);
        },
        error: err => {
          this.submittingKey.set(null);
          this.message.error(err?.error?.error?.message || err?.error?.message || '提交失败');
        },
      });
  }

  /** 前往实训中心 */
  navigateToCenter(): void {
    this.router.navigate(['/student/practicums']);
  }

  /** 提交后刷新单个工作区 */
  private refreshWorkspace(enrollment: PracticumEnrollmentDto): void {
    const enrollmentId = enrollment.id;
    if (!enrollmentId) return;
    this.practicumService
      .getSubmissionList({ enrollmentId, skipCount: 0, maxResultCount: 100 })
      .subscribe({
        next: result => {
          this.submissions.update(m => ({ ...m, [enrollmentId]: result?.items || [] }));
          this.practicumService.getMyEnrollments().subscribe({
            next: list => this.enrollments.set(list || []),
            error: () => undefined,
          });
        },
        error: () => undefined,
      });
  }
}
