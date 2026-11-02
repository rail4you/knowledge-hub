import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDividerModule } from 'ng-zorro-antd/divider';
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
import type { EnterpriseMemberDto, EnterpriseProfileDto } from '../../proxy/enterprises/dtos/models';
import { EnterpriseMemberRole } from '../../proxy/enterprises/enterprise-member-role.enum';
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
    NzDatePickerModule,
    NzDividerModule,
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
  private readonly router: Router = inject(Router);
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
  /** 企业账号明细（学校管理员查看/绑定账号） */
  readonly membersVisible = signal(false);
  readonly membersLoading = signal(false);
  readonly members = signal<EnterpriseMemberDto[]>([]);
  readonly memberEnterprise = signal<EnterpriseProfileDto | null>(null);
  readonly memberCandidates = signal<Array<{ id: string; label: string }>>([]);
  readonly memberPickId = signal<string | null>(null);
  readonly memberRole = signal<EnterpriseMemberRole>(EnterpriseMemberRole.Member);
  readonly memberBinding = signal(false);

  readonly entForm = signal({
    userName: '',
    name: '',
    password: '123456',
    enterpriseProfileId: null as string | null,
    companyName: '',
  });
  /** 归属企业方式：绑定已有企业 / 新建企业（互斥，避免同时填两个字段） */
  readonly entMode = signal<'existing' | 'new'>('new');
  readonly keyword = signal('');

  readonly responsesOpen = signal(false);
  readonly responses = signal<CooperationResponseDto[]>([]);
  readonly currentDemand = signal<CooperationDemandDto | null>(null);
  readonly respondNote = signal('');
  readonly projectOpen = signal(false);
  readonly currentProject = signal<CooperationProjectDto | null>(null);
  /** 判定某个日期是否已过（用于「下个节点 / 项目截止」标红） */
  isPast(value?: string | null): boolean {
    if (!value) return false;
    const t = new Date(value).getTime();
    return !Number.isNaN(t) && t < Date.now();
  }

  readonly milestoneName = signal('');
  /** 里程碑截止时间（可留空）；不得早于上一个里程碑的截止 */
  readonly milestoneDueAt = signal<Date | null>(null);
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

  /** 企业认证在「审核中心」处理（类型：企业认证；院校初审 → 联盟终审） */
  openAuditCenter(): void {
    void this.router.navigate(['/admin/libraries/audits']);
  }

  /** 查看该企业下的账号（用户名 / 角色 / 主联系人），并可绑定已有企业用户 */
  openMembers(e: EnterpriseProfileDto): void {
    if (!e.id) return;
    this.memberEnterprise.set(e);
    this.memberPickId.set(null);
    this.memberRole.set(EnterpriseMemberRole.Member);
    this.membersVisible.set(true);
    this.loadMembers(e.id);
    this.loadMemberCandidates(e.id);
  }

  private loadMembers(enterpriseId: string): void {
    this.membersLoading.set(true);
    this.enterpriseService.getMembers(enterpriseId).subscribe({
      next: list => { this.members.set(list ?? []); this.membersLoading.set(false); },
      error: () => { this.members.set([]); this.membersLoading.set(false); },
    });
  }

  /** 候选：本校「企业用户」角色、且还没绑定到该企业的账号 */
  private loadMemberCandidates(enterpriseId: string): void {
    this.tenantUserService.getList({ maxResultCount: 500, skipCount: 0 } as any).subscribe({
      next: res => {
        const bound = new Set((this.members() ?? []).map(m => m.userId));
        // TenantUserDto 不含角色字段（IdentityUserDto 无 roleNames），
        // 这里列「本校启用中的用户」，由管理员自行判断绑定哪个（通常选企业用户）。
        const rows = (res.items ?? [])
          .filter(u => u.id && u.isActive !== false && !bound.has(u.id))
          .map(u => ({
            id: u.id as string,
            label: `${u.userName || ''}${u.name ? `（${u.name}）` : ''}`,
          }));
        this.memberCandidates.set(rows);
      },
      error: () => this.memberCandidates.set([]),
    });
  }

  bindMember(): void {
    const e = this.memberEnterprise();
    const userId = this.memberPickId();
    if (!e?.id || !userId) {
      this.message.warning('请选择要绑定的企业用户');
      return;
    }
    this.memberBinding.set(true);
    this.enterpriseService
      .bindMember({
        enterpriseProfileId: e.id,
        userId,
        role: this.memberRole(),
        isPrimary: false,
      } as any)
      .subscribe({
        next: () => {
          this.memberBinding.set(false);
          this.message.success('已绑定到该企业');
          this.memberPickId.set(null);
          this.loadMembers(e.id!);
          // 刷新列表上的「账号数」
          this.enterpriseService.getList({ skipCount: 0, maxResultCount: 100 } as any).subscribe({
            next: res => this.enterprises.set(res.items ?? []),
            error: () => {},
          });
        },
        error: err => {
          this.memberBinding.set(false);
          this.message.error(err?.error?.error?.message ?? '绑定失败');
        },
      });
  }

  /** 里程碑状态色：已完成 / 已逾期 / 进行中 */
  milestoneColor(m: { isCompleted?: boolean; isOverdue?: boolean }): string {
    if (m?.isCompleted) return 'green';
    return m?.isOverdue ? 'red' : 'processing';
  }

  roleName(role?: EnterpriseMemberRole): string {
    return role === EnterpriseMemberRole.Admin ? '企业管理员' : '企业成员';
  }

  openEntAccount(): void {
    this.entForm.set({ userName: '', name: '', password: '123456', enterpriseProfileId: null, companyName: '' });
    // 默认「新建企业」：多数场景是学校新开一家合作企业；已有企业走绑定
    this.entMode.set('new');
    this.entOpen.set(true);
  }

  patchEntForm(patch: Record<string, unknown>): void {
    this.entForm.set({ ...this.entForm(), ...patch } as any);
  }

  /** 开通企业账号：绑定已有企业，或按公司名新建一家草稿企业（后端自动处理）。 */
  setEntMode(mode: 'existing' | 'new'): void {
    this.entMode.set(mode);
    // 两种方式互斥：切换时清掉另一种的取值，避免把两个字段一起提交
    this.entForm.set({
      ...this.entForm(),
      enterpriseProfileId: mode === 'existing' ? this.entForm().enterpriseProfileId : null,
      companyName: mode === 'new' ? this.entForm().companyName : '',
    });
  }

  saveEntAccount(): void {
    const f = this.entForm();
    if (!f.userName.trim() || !f.name.trim()) {
      this.message.warning('请填写用户名与姓名');
      return;
    }
    if (this.entMode() === 'existing' && !f.enterpriseProfileId) {
      this.message.warning('请选择要绑定的已有企业');
      return;
    }
    if (this.entMode() === 'new' && !f.companyName.trim()) {
      this.message.warning('请填写新企业名称');
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
        this.milestoneDueAt.set(null);
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
        // 留空表示不设截止（null）；传值用 ISO 便于后端统一解析
        dueAt: this.milestoneDueAt() ? this.milestoneDueAt()!.toISOString() : null,
        sortOrder: (project.milestones?.length ?? 0) + 1,
      } as any)
      .subscribe({
        next: () => {
          this.message.success('里程碑已添加');
          this.milestoneDueAt.set(null);
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
