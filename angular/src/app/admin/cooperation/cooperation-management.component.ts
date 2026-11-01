import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
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
import { CooperationService } from '../../proxy/cooperation/cooperation.service';
import { EnterpriseService } from '../../proxy/enterprises/enterprise.service';
import { TenantUserService } from '../../proxy/application/identity/tenant-user.service';
import type { EnterpriseProfileDto } from '../../proxy/enterprises/dtos/models';
import type {
  CooperationDemandDto,
  CooperationProjectDto,
  CooperationResponseDto,
} from '../../proxy/cooperation/dtos/models';
import { CooperationDemandType } from '../../proxy/cooperation/cooperation-demand-type.enum';
import { CooperationProjectStage } from '../../proxy/cooperation/cooperation-project-stage.enum';

type Tab = 'hall' | 'projects' | 'enterprises';

/**
 * 产教合作管理端（学校侧）：需求大厅（响应企业需求）+ 项目台账（里程碑与阶段）。
 *
 * <p>与「企业工作台」互补：企业发需求/看项目，学校响应/推进项目。</p>
 */
@Component({
  selector: 'app-cooperation-management',
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
  templateUrl: './cooperation-management.component.html',
  styleUrls: ['./cooperation-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CooperationManagementComponent implements OnInit {
  private readonly cooperationService = inject(CooperationService);
  private readonly enterpriseService = inject(EnterpriseService);
  private readonly tenantUserService = inject(TenantUserService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly tab = signal<Tab>('hall');
  readonly loading = signal(false);
  readonly hall = signal<CooperationDemandDto[]>([]);
  readonly projects = signal<CooperationProjectDto[]>([]);
  readonly typeFilter = signal<CooperationDemandType | null>(null);

  // 企业管理（学校管理员开通企业账号）
  readonly enterprises = signal<EnterpriseProfileDto[]>([]);
  readonly entOpen = signal(false);
  readonly entSaving = signal(false);
  readonly entForm = signal({
    userName: '',
    name: '',
    password: '123456',
    enterpriseProfileId: null as string | null,
    companyName: '',
  });
  readonly keyword = signal('');

  readonly responsesOpen = signal(false);
  readonly responses = signal<CooperationResponseDto[]>([]);
  readonly currentDemand = signal<CooperationDemandDto | null>(null);
  readonly respondNote = signal('');
  readonly projectOpen = signal(false);
  readonly currentProject = signal<CooperationProjectDto | null>(null);
  readonly milestoneName = signal('');
  readonly stageNote = signal('');

  readonly typeOptions = [
    { value: CooperationDemandType.TalentTraining, label: '人才培养与订单班' },
    { value: CooperationDemandType.Internship, label: '实习实训与就业' },
    { value: CooperationDemandType.Curriculum, label: '课程教材及资源共建' },
    { value: CooperationDemandType.BaseSharing, label: '基地设备共享' },
    { value: CooperationDemandType.TeacherPractice, label: '教师企业实践与培训' },
    { value: CooperationDemandType.TechTransfer, label: '技术研发与成果转化' },
    { value: CooperationDemandType.IndustrialCollege, label: '产业学院与联合体' },
  ];

  ngOnInit(): void {
    this.reload();
  }

  setTab(tab: Tab): void {
    this.tab.set(tab);
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    if (this.tab() === 'hall') {
      this.cooperationService
        .getHall({
          skipCount: 0,
          maxResultCount: 50,
          type: this.typeFilter() ?? undefined,
          filter: this.keyword() || undefined,
        } as any)
        .subscribe({
          next: res => {
            this.hall.set(res.items ?? []);
            this.loading.set(false);
          },
          error: () => {
            this.hall.set([]);
            this.loading.set(false);
          },
        });
    } else if (this.tab() === 'enterprises') {
      this.enterpriseService.getList({ skipCount: 0, maxResultCount: 100 } as any).subscribe({
        next: res => {
          this.enterprises.set(res.items ?? []);
          this.loading.set(false);
        },
        error: () => {
          this.enterprises.set([]);
          this.loading.set(false);
        },
      });
    } else {
      this.cooperationService.getProjects({ skipCount: 0, maxResultCount: 50 } as any).subscribe({
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

  openEntAccount(): void {
    this.entForm.set({ userName: '', name: '', password: '123456', enterpriseProfileId: null, companyName: '' });
    this.entOpen.set(true);
  }

  patchEntForm(patch: Record<string, unknown>): void {
    this.entForm.set({ ...this.entForm(), ...patch } as any);
  }

  /** 开通企业账号：绑定已有企业，或按公司名新建一家草稿企业（后端自动处理）。 */
  saveEntAccount(): void {
    const f = this.entForm();
    if (!f.userName.trim() || !f.name.trim()) {
      this.message.warning('请填写用户名与姓名');
      return;
    }
    if (!f.enterpriseProfileId && !f.companyName.trim()) {
      this.message.warning('请选择已有企业，或填写新企业名称');
      return;
    }

    this.entSaving.set(true);
    this.tenantUserService
      .createUserForTenant({
        tenantId: null,
        userName: f.userName.trim(),
        emailAddress: `${f.userName.trim()}@demo.local`,
        password: f.password || '123456',
        name: f.name.trim(),
        surname: '企业',
        isActive: true,
        roleNames: ['EnterpriseUser'],
        enterpriseProfileId: f.enterpriseProfileId ?? undefined,
        companyName: f.enterpriseProfileId ? undefined : f.companyName.trim(),
      } as any)
      .subscribe({
        next: () => {
          this.entSaving.set(false);
          this.entOpen.set(false);
          this.message.success('企业账号已开通并绑定企业（企业初始为「草稿」，需提交认证）');
          this.reload();
        },
        error: err => {
          this.entSaving.set(false);
          this.message.error(err?.error?.error?.message ?? '开通失败');
        },
      });
  }

  onTypeChange(value: CooperationDemandType | null): void {
    this.typeFilter.set(value);
    this.reload();
  }

  onKeyword(value: string): void {
    this.keyword.set(value ?? '');
    this.reload();
  }

  respond(demand: CooperationDemandDto): void {
    this.currentDemand.set(demand);
    this.respondNote.set('');
    this.cooperationService.getResponses(demand.id!).subscribe({
      next: list => {
        this.responses.set(list ?? []);
        this.responsesOpen.set(true);
      },
      error: () => {
        this.responses.set([]);
        this.responsesOpen.set(true);
      },
    });
  }

  submitRespond(): void {
    const demand = this.currentDemand();
    if (!demand?.id) return;

    this.cooperationService.respond({ demandId: demand.id, note: this.respondNote() || undefined } as any).subscribe({
      next: () => {
        this.message.success('已提交合作意向，等待企业确认');
        this.responsesOpen.set(false);
        this.reload();
      },
      error: err => this.message.error(err?.error?.error?.message ?? '提交失败'),
    });
  }

  openProject(project: CooperationProjectDto): void {
    this.cooperationService.getProjects({ skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: res => {
        const latest = (res.items ?? []).find(x => x.id === project.id) ?? project;
        this.currentProject.set(latest);
        this.milestoneName.set('');
        this.stageNote.set('');
        this.projectOpen.set(true);
      },
    });
  }

  addMilestone(): void {
    const project = this.currentProject();
    if (!project?.id || !this.milestoneName().trim()) {
      this.message.warning('请填写里程碑名称');
      return;
    }

    this.cooperationService
      .addMilestone({
        projectId: project.id,
        name: this.milestoneName().trim(),
        sortOrder: (project.milestones?.length ?? 0) + 1,
      } as any)
      .subscribe({
        next: () => {
          this.message.success('里程碑已添加');
          this.openProject(project);
          this.reload();
        },
        error: err => this.message.error(err?.error?.error?.message ?? '添加失败'),
      });
  }

  completeMilestone(milestoneId?: string): void {
    const project = this.currentProject();
    if (!milestoneId) return;

    this.cooperationService
      .completeMilestone({ milestoneId, evidenceRef: 'platform', note: '平台标记完成' } as any)
      .subscribe({
        next: () => {
          this.message.success('里程碑已完成');
          if (project) this.openProject(project);
          this.reload();
        },
        error: err => this.message.error(err?.error?.error?.message ?? '操作失败'),
      });
  }

  advance(stage: CooperationProjectStage): void {
    const project = this.currentProject();
    if (!project?.id) return;

    this.cooperationService
      .advanceStage({ projectId: project.id, stage, acceptanceNote: this.stageNote() || undefined } as any)
      .subscribe({
        next: () => {
          this.message.success('阶段已更新');
          this.openProject(project);
          this.reload();
        },
        error: err => this.message.error(err?.error?.error?.message ?? '操作失败'),
      });
  }

  acceptResponse(item: CooperationResponseDto, accept: boolean): void {
    this.modal.confirm({
      nzTitle: accept ? '接受响应' : '婉拒响应',
      nzContent: accept
        ? `接受后可与「${item.schoolTenantName}」建立项目台账。`
        : `确认婉拒「${item.schoolTenantName}」的合作意向？`,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.cooperationService.decideResponse(item.id!, { accept } as any).subscribe({
            next: () => {
              this.message.success(accept ? '已接受' : '已婉拒');
              const demand = this.currentDemand();
              if (demand?.id) this.respond(demand);
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

  createProjectFrom(item: CooperationResponseDto): void {
    this.cooperationService
      .createProject({ responseId: item.id, name: `与${item.schoolTenantName}的合作项目` } as any)
      .subscribe({
        next: () => {
          this.message.success('项目台账已建立');
          this.responsesOpen.set(false);
          this.setTab('projects');
        },
        error: err => this.message.error(err?.error?.error?.message ?? '建立失败'),
      });
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
