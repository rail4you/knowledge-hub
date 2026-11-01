import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { EnterpriseService } from '../../proxy/enterprises/enterprise.service';
import { CooperationService } from '../../proxy/cooperation/cooperation.service';
import type {
  CooperationDemandDto,
  CooperationProjectDto,
  CooperationResponseDto,
  CreateUpdateCooperationDemandDto,
} from '../../proxy/cooperation/dtos/models';
import type { EnterpriseProfileDto } from '../../proxy/enterprises/dtos/models';
import { CreateUpdateEnterpriseProfileDto } from '../../proxy/enterprises/dtos/models';
import { CooperationDemandScope } from '../../proxy/cooperation/cooperation-demand-scope.enum';
import { CooperationResponseStatus } from '../../proxy/cooperation/cooperation-response-status.enum';
import { CooperationDemandStatus } from '../../proxy/cooperation/cooperation-demand-status.enum';
import { CooperationDemandType } from '../../proxy/cooperation/cooperation-demand-type.enum';
import { CooperationProjectStage } from '../../proxy/cooperation/cooperation-project-stage.enum';

type Tab = 'demands' | 'projects';

/**
 * 企业工作台（V2.0 P5）。
 *
 * <p>企业侧只看本企业数据：企业资料与认证、合作需求、项目台账。
 * 认证通过前可以建草稿需求，但提交审核会被拒；项目台账由学校建后在此查看进度。</p>
 */
@Component({
  selector: 'app-enterprise-workbench',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzEmptyModule,
    NzInputModule,
    NzModalModule,
    NzProgressModule,
    NzSelectModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './enterprise-workbench.component.html',
  styleUrls: ['./enterprise-workbench.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EnterpriseWorkbenchComponent implements OnInit {
  private readonly enterpriseService = inject(EnterpriseService);
  private readonly cooperationService = inject(CooperationService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly tab = signal<Tab>('demands');
  readonly enterprise = signal<EnterpriseProfileDto | null>(null);
  readonly loading = signal(false);
  readonly demands = signal<CooperationDemandDto[]>([]);
  readonly projects = signal<CooperationProjectDto[]>([]);

  readonly createOpen = signal(false);
  readonly saving = signal(false);
  readonly editOpen = signal(false);
  readonly form = signal<CreateUpdateCooperationDemandDto>({
    type: CooperationDemandType.TalentTraining,
    title: '',
    scope: CooperationDemandScope.HostSchoolOnly,
  } as CreateUpdateCooperationDemandDto);

  /** 企业资料编辑（P1-4：没有入口就永远无法补全资料 → 无法提交认证）。 */
  readonly profileOpen = signal(false);
  readonly profileSaving = signal(false);
  readonly profileForm = signal<CreateUpdateEnterpriseProfileDto>({} as CreateUpdateEnterpriseProfileDto);

  /** 学校响应处理（P1-5：企业侧必须能接受/拒绝响应并建项目）。 */
  readonly responseOpen = signal(false);
  readonly responseDemand = signal<CooperationDemandDto | null>(null);
  readonly responses = signal<CooperationResponseDto[]>([]);
  readonly responseLoading = signal(false);
  readonly responseAcceptedStatus = CooperationResponseStatus.Accepted;

  readonly certified = computed(() => this.enterprise()?.certificationStatus === 2);
  readonly certStatusName = computed(() => this.enterprise()?.certificationStatusName ?? '未建企业');
  readonly canSubmit = computed(() => (this.enterprise()?.isUsable ?? false));

  readonly typeOptions = [
    { value: CooperationDemandType.TalentTraining, label: '人才培养与订单班' },
    { value: CooperationDemandType.Internship, label: '实习实训与就业' },
    { value: CooperationDemandType.Curriculum, label: '课程教材及资源共建' },
    { value: CooperationDemandType.BaseSharing, label: '基地设备共享' },
    { value: CooperationDemandType.TeacherPractice, label: '教师企业实践与培训' },
    { value: CooperationDemandType.TechTransfer, label: '技术研发与成果转化' },
    { value: CooperationDemandType.IndustrialCollege, label: '产业学院与联合体' },
  ];

  readonly scopeOptions = [
    { value: CooperationDemandScope.HostSchoolOnly, label: '仅入驻校可见' },
    { value: CooperationDemandScope.Alliance, label: '联盟内学校可见' },
    { value: CooperationDemandScope.Public, label: '平台公开' },
  ];

  ngOnInit(): void {
    this.loadEnterprise();
    this.reload();
  }

  loadEnterprise(): void {
    this.enterpriseService.getMine().subscribe({
      next: p => this.enterprise.set(p),
      error: () => this.enterprise.set(null),
    });
  }

  setTab(tab: Tab): void {
    this.tab.set(tab);
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    if (this.tab() === 'demands') {
      this.cooperationService.getMyDemands({ onlyMine: true, maxResultCount: 100 } as any).subscribe({
        next: res => {
          this.demands.set(res.items ?? []);
          this.loading.set(false);
        },
        error: () => {
          this.demands.set([]);
          this.loading.set(false);
        },
      });
    } else {
      this.cooperationService.getProjects({ skipCount: 0, maxResultCount: 100 } as any).subscribe({
        next: res => {
          this.projects.set(res.items ?? []);
          this.loading.set(false);
        },
        error: () => {
          this.projects.set([]);
          this.loading.set(false);
        },
      });
    }
  }

  submitCertification(): void {
    const e = this.enterprise();
    if (!e?.id) {
      this.message.warning('请先完善企业资料');
      return;
    }

    this.modal.confirm({
      nzTitle: '提交企业认证',
      nzContent: '提交后由入驻学校初审、联盟终审；通过后即可发布合作需求、投递岗位。',
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.enterpriseService.submitCertification(e.id!).subscribe({
            next: () => {
              this.message.success('已提交认证，等待审核');
              this.loadEnterprise();
              resolve();
            },
            error: err => {
              this.message.error(err?.error?.error?.message ?? '提交失败');
              reject(err);
            },
          });
        }),
    });
  }

  openCreate(): void {
    this.form.set({
      type: CooperationDemandType.TalentTraining,
      title: '',
      scope: CooperationDemandScope.HostSchoolOnly,
    } as CreateUpdateCooperationDemandDto);
    this.createOpen.set(true);
  }

  // ── 企业资料编辑 ─────────────────────────────────────────────
  openProfileEdit(): void {
    const e = this.enterprise();
    if (!e?.id) {
      return;
    }
    this.profileForm.set({
      name: e.name ?? '',
      unifiedSocialCreditCode: e.unifiedSocialCreditCode ?? '',
      industry: e.industry ?? '',
      contactName: e.contactName ?? '',
      contactPhone: e.contactPhone ?? '',
      contactEmail: e.contactEmail ?? '',
      summary: e.summary ?? '',
    } as CreateUpdateEnterpriseProfileDto);
    this.profileOpen.set(true);
  }

  patchProfileForm(patch: Record<string, unknown>): void {
    this.profileForm.set({ ...this.profileForm(), ...patch } as CreateUpdateEnterpriseProfileDto);
  }

  saveProfile(): void {
    const e = this.enterprise();
    if (!e?.id) {
      return;
    }
    const body = this.profileForm();
    if (!body.name?.trim()) {
      this.message.warning('请填写企业名称');
      return;
    }

    this.profileSaving.set(true);
    this.enterpriseService.update(e.id, body).subscribe({
      next: () => {
        this.profileSaving.set(false);
        this.profileOpen.set(false);
        this.message.success('企业资料已保存');
        this.loadEnterprise();
      },
      error: err => {
        this.profileSaving.set(false);
        this.message.error(err?.error?.error?.message ?? '保存失败');
      },
    });
  }

  // ── 学校响应处理 ─────────────────────────────────────────────
  openResponses(demand: CooperationDemandDto): void {
    if (!demand?.id) {
      return;
    }
    this.responseDemand.set(demand);
    this.responseOpen.set(true);
    this.responseLoading.set(true);
    this.cooperationService.getResponses(demand.id).subscribe({
      next: list => {
        this.responses.set(list ?? []);
        this.responseLoading.set(false);
      },
      error: () => {
        this.responses.set([]);
        this.responseLoading.set(false);
      },
    });
  }

  decideResponse(response: CooperationResponseDto, accept: boolean): void {
    if (!response?.id) {
      return;
    }
    this.cooperationService.decideResponse(response.id, { accept } as any).subscribe({
      next: () => {
        this.message.success(accept ? '已接受学校响应' : '已拒绝学校响应');
        this.openResponses(this.responseDemand()!);
        this.reload();
      },
      error: err => this.message.error(err?.error?.error?.message ?? '操作失败'),
    });
  }

  createProject(response: CooperationResponseDto): void {
    if (!response?.id) {
      return;
    }
    const demand = this.responseDemand();
    const name = `校企合作项目 · ${demand?.title ?? ''}`.trim();
    this.cooperationService.createProject({ responseId: response.id, name } as any).subscribe({
      next: () => {
        this.message.success('项目台账已建立');
        this.responseOpen.set(false);
        this.setTab('projects');
      },
      error: err => this.message.error(err?.error?.error?.message ?? '建项目失败'),
    });
  }

  patchForm(patch: Record<string, unknown>): void {
    this.form.set({ ...this.form(), ...patch } as CreateUpdateCooperationDemandDto);
  }

  saveDemand(): void {
    const body = this.form();
    if (!body.title?.trim()) {
      this.message.warning('请填写需求标题');
      return;
    }

    this.saving.set(true);
    this.cooperationService.createDemand(body).subscribe({
      next: dto => {
        this.saving.set(false);
        this.createOpen.set(false);
        this.message.success('需求已保存为草稿。提交审核通过后才会进入需求大厅。');
        this.reload();
        if (dto?.id) {
          this.cooperationService.submitDemand(dto.id).subscribe({
            next: () => {
              this.message.success('已提交审核');
              this.reload();
            },
            error: err => this.message.warning(err?.error?.error?.message ?? '提交审核失败，可稍后重试'),
          });
        }
      },
      error: err => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message ?? '保存失败');
      },
    });
  }

  closeDemand(item: CooperationDemandDto): void {
    this.modal.confirm({
      nzTitle: '关闭需求',
      nzContent: `关闭后「${item.title}」不再接受学校响应。`,
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.cooperationService.closeDemand(item.id!).subscribe({
            next: () => {
              this.message.success('已关闭');
              this.reload();
              resolve();
            },
            error: err => {
              this.message.error(err?.error?.error?.message ?? '操作失败');
              reject(err);
            },
          });
        }),
    });
  }

  statusColor(status?: CooperationDemandStatus): string {
    switch (status) {
      case CooperationDemandStatus.Published:
        return 'green';
      case CooperationDemandStatus.Rejected:
        return 'red';
      case CooperationDemandStatus.PendingReview:
        return 'processing';
      default:
        return 'default';
    }
  }

  stageName(stage?: CooperationProjectStage): string {
    switch (stage) {
      case CooperationProjectStage.Confirming:
        return '洽谈确认';
      case CooperationProjectStage.Executing:
        return '执行中';
      case CooperationProjectStage.AcceptancePending:
        return '待验收';
      case CooperationProjectStage.Archived:
        return '已归档';
      case CooperationProjectStage.Terminated:
        return '已终止';
      default:
        return '—';
    }
  }

  trackById = (_: number, item: { id?: string }) => item.id;
}
