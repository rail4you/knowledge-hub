import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { JobCompetencyGraphService } from '../../../proxy/employment/job-competency-graph.service';
import { EmploymentService } from '../../../proxy/employment/employment.service';
import type {
  JobCompetencyGraphDto,
  JobCompetencyGraphDetailDto,
  JobOptionDto,
  CreateUpdateJobPostingDto,
} from '../../../proxy/employment/dtos/models';
import { JobGraphCanvasComponent } from './shared/job-graph-canvas.component';

/**
 * 岗位图谱**入口页**（双栏）：左侧岗位列表，右侧直接看该岗位的图谱。
 *
 * 为什么双栏：老师说"岗位→图谱"是同一件事的两个视角，翻页看很割裂。
 * 左栏点岗位 → 右栏立刻出图谱（已建图谱直接渲染 SVG，未建的一键生成）。
 *
 * 数据源：`job-competency-graph/job-options`（只要 Employment 读权限；`employment/manage-job-list`
 * 需要 PublishJob，老师会 403 导致列表空）。
 */
@Component({
  selector: 'app-job-graph-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzTagModule,
    NzProgressModule,
    NzModalModule,
    NzFormModule,
    NzInputNumberModule,
    JobGraphCanvasComponent,
  ],
  templateUrl: './job-graph-list.component.html',
  styleUrls: ['./job-graph-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JobGraphListComponent implements OnInit {
  private readonly service = inject(JobCompetencyGraphService);
  private readonly employmentService = inject(EmploymentService);
  private readonly configState = inject(ConfigStateService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly jobOptions = signal<JobOptionDto[]>([]);
  readonly graphs = signal<JobCompetencyGraphDto[]>([]);
  readonly loading = signal(false);
  readonly keyword = signal('');

  /** 左栏选中岗位 */
  readonly selectedJobId = signal<string | null>(null);
  /** 右栏图谱数据 */
  readonly preview = signal<JobCompetencyGraphDetailDto | null>(null);
  readonly previewLoading = signal(false);
  readonly creating = signal(false);

  readonly canCreateJob = computed(() => {
    const granted = this.configState.getDeep('auth.grantedPolicies') as Record<string, boolean> | undefined;
    return !!granted?.['KnowledgeHub.Employment.PublishJob'];
  });

  readonly selectedJob = computed(() => this.jobOptions().find(j => j.id === this.selectedJobId()) ?? null);

  readonly isJobModalOpen = signal(false);
  readonly savingJob = signal(false);
  readonly jobForm = signal<CreateUpdateJobPostingDto>(this.emptyJobForm());

  readonly stats = computed(() => {
    const jobs = this.jobOptions().length;
    const withGraph = this.jobOptions().filter(j => j.hasGraph).length;
    return { jobs, withGraph, withoutGraph: jobs - withGraph };
  });

  readonly coveragePct = computed(() => {
    const detail = this.preview();
    const total = detail?.competencyCount ?? 0;
    if (!total) return 0;
    return Math.round(((detail?.measurableCompetencyCount ?? 0) / total) * 100);
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const keyword = this.keyword()?.trim() || undefined;
    this.loading.set(true);

    this.service.getJobOptions(keyword).subscribe({
      next: rows => {
        this.jobOptions.set(rows ?? []);
        this.loading.set(false);

        // 保持选中；否则默认选第一个"已有图谱"的岗位（用户进页面就能看到图谱）
        const keep = this.selectedJobId();
        const exists = (rows ?? []).some(j => j.id === keep);
        if (!exists) {
          const withGraph = (rows ?? []).find(j => j.hasGraph) ?? (rows ?? [])[0];
          if (withGraph?.id) this.selectJob(withGraph);
          else {
            this.selectedJobId.set(null);
            this.preview.set(null);
          }
        }
      },
      error: () => {
        this.jobOptions.set([]);
        this.loading.set(false);
      },
    });

    this.service.getList(undefined, keyword).subscribe({
      next: rows => this.graphs.set(rows ?? []),
      error: () => this.graphs.set([]),
    });
  }

  selectJob(job: JobOptionDto): void {
    this.selectedJobId.set(job.id ?? null);
    this.preview.set(null);

    if (!job.graphId) {
      return; // 未建图谱：右栏显示"一键创建"空态
    }

    this.previewLoading.set(true);
    this.service.getDetail(job.graphId).subscribe({
      next: detail => {
        this.preview.set(detail);
        this.previewLoading.set(false);
      },
      error: () => {
        this.preview.set(null);
        this.previewLoading.set(false);
      },
    });
  }

  /** 为选中的岗位创建图谱（幂等），成功后直接在右栏显示 */
  createForSelected(): void {
    const job = this.selectedJob();
    if (!job?.id) return;

    this.creating.set(true);
    this.service.createFromJob(job.id).subscribe({
      next: detail => {
        this.creating.set(false);
        this.message.success(`已为「${job.title}」创建图谱`);
        this.preview.set(detail);
        this.load();
        if (detail?.id) this.selectedJobId.set(job.id ?? null);
      },
      error: err => {
        this.creating.set(false);
        this.message.error(err?.error?.error?.message || '创建失败');
      },
    });
  }

  openDetail(): void {
    const job = this.selectedJob();
    const graphId = job?.graphId ?? this.preview()?.id;
    if (graphId) this.router.navigate(['/admin/employment/job-graphs', graphId]);
  }

  // ── 就地新建岗位 ─────────────────────────────────────────────

  openCreateJob(): void {
    if (!this.canCreateJob()) {
      this.modal.info({
        nzTitle: '没有新建岗位的权限',
        nzContent:
          '新建岗位需要「发布岗位」权限（企业用户 / 院校管理员）。请管理员在「就业管理 → 岗位管理」里创建岗位，创建后回到本页即可一键生成图谱。',
      });
      return;
    }

    this.jobForm.set(this.emptyJobForm());
    this.isJobModalOpen.set(true);
  }

  patchJob(patch: Partial<CreateUpdateJobPostingDto>): void {
    this.jobForm.set({ ...this.jobForm(), ...patch });
  }

  saveJobAndGraph(): void {
    const model = this.jobForm();
    if (!model.title?.trim()) {
      this.message.warning('请填写岗位名称');
      return;
    }
    if (!model.description?.trim()) {
      this.message.warning('请填写岗位职责（description）');
      return;
    }

    this.savingJob.set(true);
    this.employmentService.createJob(model).subscribe({
      next: job => {
        this.service.createFromJob(job.id!).subscribe({
          next: detail => {
            this.savingJob.set(false);
            this.isJobModalOpen.set(false);
            this.message.success('岗位与图谱已创建');
            this.load();
            this.preview.set(detail);
            this.selectedJobId.set(job.id ?? null);
          },
          error: () => {
            this.savingJob.set(false);
            this.isJobModalOpen.set(false);
            this.message.warning('岗位已创建，但图谱创建失败，请在左栏重新点该岗位');
            this.load();
          },
        });
      },
      error: err => {
        this.savingJob.set(false);
        this.message.error(err?.error?.error?.message || '岗位创建失败');
      },
    });
  }

  goJobManagement(): void {
    this.router.navigate(['/admin/employment/jobs']);
  }

  jobStatusColor(label?: string): string {
    if (label === '招聘中') return 'success';
    if (label === '待审核') return 'processing';
    if (label === '已驳回') return 'error';
    return 'default';
  }

  private emptyJobForm(): CreateUpdateJobPostingDto {
    return {
      companyName: '',
      industry: '',
      title: '',
      summary: '',
      description: '',
      location: '',
      salaryRange: '',
      skillTags: '',
      recruitmentCount: 1,
    } as CreateUpdateJobPostingDto;
  }
}
