import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PermissionService } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabLinkDirective, NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTimelineModule } from 'ng-zorro-antd/timeline';
import { ReviewTicketService } from '../../../proxy/review/review-ticket.service';
import type { ReviewActionDto, ReviewTicketDto } from '../../../proxy/review/dtos/models';
import { ReviewDecision } from '../../../proxy/review/review-decision.enum';
import { ReviewStage } from '../../../proxy/review/review-stage.enum';
import { ReviewTicketStatus } from '../../../proxy/review/review-ticket-status.enum';

type TabKey = 'todo' | 'mine' | 'done';

/**
 * 审核中心（V2.0 教学资源库审核流）。
 *
 * <para>
 * 一个页面承载两类审核人（与需求保持一致，不新增角色）：
 * 院校管理员审本校对象（初审），联盟管理员审跨校对象（终审）。
 * 两个 Tab 的可见性与数据范围由后端按权限过滤，前端只负责展示与操作。
 * </para>
 *
 * <para>
 * 页面刻意做成「目标类型无关」的通用容器：P3 的共建任务交付、P5 的企业认证/合作需求
 * 都会作为新的 `targetType` 出现在同一张表里，不需要再改这个页面。
 * </para>
 */
@Component({
  selector: 'app-review-center',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTabsModule,
    NzTagModule,
    NzTimelineModule,
    NzTabLinkDirective,
  ],
  templateUrl: './review-center.component.html',
  styleUrls: ['./review-center.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReviewCenterComponent implements OnInit {
  private readonly ticketService = inject(ReviewTicketService);
  private readonly message = inject(NzMessageService);
  private readonly permission = inject(PermissionService);

  readonly hasSchoolAudit = this.permission.getGrantedPolicy('KnowledgeHub.ResourceLibraries.Audit');
  readonly hasLeagueAudit = this.permission.getGrantedPolicy('KnowledgeHub.ResourceLibraries.LeagueAudit');
  readonly canAuditAny = this.hasSchoolAudit || this.hasLeagueAudit;

  readonly tab = signal<TabKey>('todo');
  readonly loading = signal(false);
  readonly items = signal<ReviewTicketDto[]>([]);
  readonly total = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(20);
  readonly filter = signal('');

  readonly detail = signal<ReviewTicketDto | null>(null);
  readonly detailVisible = signal(false);
  readonly deciding = signal(false);
  readonly rejectReason = signal('');
  readonly showRejectBox = signal(false);

  /** 变更快照解析结果（用于 diff 展示）。 */
  readonly payloadFields = computed(() => this.buildPayloadRows(this.detail()));

  readonly stageSteps = computed(() => {
    const dto = this.detail();
    if (!dto) return [];
    const total = dto.totalStages ?? 1;
    const current = (dto.stage ?? 0) + 1;
    return Array.from({ length: total }, (_, i) => ({
      index: i + 1,
      label: i === 0 ? '院校初审' : '联盟终审',
      active: (dto.status as ReviewTicketStatus) !== ReviewTicketStatus.Rejected && i + 1 <= current,
      done: (dto.status as ReviewTicketStatus) === ReviewTicketStatus.Approved || i + 1 < current,
    }));
  });

  ngOnInit(): void {
    this.reload();
  }

  setTab(tab: TabKey): void {
    this.tab.set(tab);
    this.pageIndex.set(1);
    this.reload();
  }

  onFilterChange(value: string): void {
    this.filter.set(value ?? '');
    this.pageIndex.set(1);
    this.reload();
  }

  reload(): void {
    if (!this.canAuditAny) {
      this.items.set([]);
      this.total.set(0);
      return;
    }

    this.loading.set(true);
    const tab = this.tab();
    this.ticketService
      .getList({
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
        onlyMine: tab === 'mine',
        status: tab === 'todo' ? ReviewTicketStatus.Pending : undefined,
        filter: this.filter() || undefined,
        sorting: 'creationTime desc',
      } as any)
      .subscribe({
        next: result => {
          const rows = (result.items ?? []).filter(x => (tab === 'done' ? x.status !== ReviewTicketStatus.Pending : true));
          this.items.set(rows);
          this.total.set(tab === 'done' ? result.totalCount : result.totalCount);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载审核列表失败');
        },
      });
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
    this.reload();
  }

  openDetail(row: ReviewTicketDto): void {
    if (!row.id) return;
    this.ticketService.get(row.id).subscribe({
      next: dto => {
        this.detail.set(dto);
        this.detailVisible.set(true);
        this.showRejectBox.set(false);
        this.rejectReason.set('');
      },
      error: () => this.message.error('加载审核详情失败'),
    });
  }

  closeDetail(): void {
    this.detailVisible.set(false);
    this.detail.set(null);
  }

  approve(): void {
    const dto = this.detail();
    if (!dto?.id) return;
    this.deciding.set(true);
    this.ticketService
      .decide({ ticketId: dto.id, decision: ReviewDecision.Approved, expectedRoundNo: dto.roundNo } as any)
      .subscribe({
        next: () => {
          this.deciding.set(false);
          this.message.success('已通过');
          this.closeDetail();
          this.reload();
        },
        error: err => {
          this.deciding.set(false);
          this.message.error(err?.error?.error?.message ?? '操作失败');
        },
      });
  }

  reject(): void {
    const dto = this.detail();
    if (!dto?.id) return;
    if (!this.rejectReason().trim()) {
      this.message.warning('驳回必须填写原因');
      return;
    }
    this.deciding.set(true);
    this.ticketService
      .decide({
        ticketId: dto.id,
        decision: ReviewDecision.Rejected,
        reason: this.rejectReason().trim(),
        expectedRoundNo: dto.roundNo,
      } as any)
      .subscribe({
        next: () => {
          this.deciding.set(false);
          this.message.success('已驳回');
          this.closeDetail();
          this.reload();
        },
        error: err => {
          this.deciding.set(false);
          this.message.error(err?.error?.error?.message ?? '操作失败');
        },
      });
  }

  withdraw(): void {
    const dto = this.detail();
    if (!dto?.id) return;
    this.deciding.set(true);
    this.ticketService.withdraw(dto.id, '提交人撤回').subscribe({
      next: () => {
        this.deciding.set(false);
        this.message.success('已撤回');
        this.closeDetail();
        this.reload();
      },
      error: err => {
        this.deciding.set(false);
        this.message.error(err?.error?.error?.message ?? '撤回失败');
      },
    });
  }

  resubmit(): void {
    const dto = this.detail();
    if (!dto?.id) return;
    this.deciding.set(true);
    this.ticketService.resubmit(dto.id).subscribe({
      next: () => {
        this.deciding.set(false);
        this.message.success('已重新提交，等待审核');
        this.closeDetail();
        this.reload();
      },
      error: err => {
        this.deciding.set(false);
        this.message.error(err?.error?.error?.message ?? '重新提交失败');
      },
    });
  }

  statusColor(status?: ReviewTicketStatus): string {
    switch (status) {
      case ReviewTicketStatus.Approved:
        return 'success';
      case ReviewTicketStatus.Rejected:
        return 'error';
      case ReviewTicketStatus.Cancelled:
        return 'default';
      default:
        return 'processing';
    }
  }

  stageColor(stage?: ReviewStage): string {
    return stage === ReviewStage.League ? 'purple' : 'blue';
  }

  trackById = (_: number, item: ReviewTicketDto) => item.id;

  /** 把 payloadJson 拍平成「字段 / 值」行，供人工核对变更内容。 */
  private buildPayloadRows(dto: ReviewTicketDto | null): Array<{ key: string; value: string }> {
    if (!dto?.payloadJson) return [];
    try {
      const parsed = JSON.parse(dto.payloadJson);
      const source = parsed && typeof parsed === 'object' && parsed.input ? parsed.input : parsed;
      return Object.entries(source)
        .filter(([key]) => !['previousStatus', 'id'].includes(key))
        .map(([key, value]) => ({ key: this.fieldLabel(key), value: this.formatValue(value) }));
    } catch {
      return [];
    }
  }

  /** 变更快照字段的中文标签（payload 用 C# 属性名，直接展示英文对业务方不友好）。 */
  private fieldLabel(key: string): string {
    const labels: Record<string, string> = {
      name: '名称',
      description: '简介',
      coverUrl: '封面',
      primaryMajorId: '主建专业',
      principal: '建设负责人',
      isPublic: '门户公开展示',
      openToExternalLearners: '对外开放自学',
      sortOrder: '排序',
      visibility: '可见性',
      partnerTenantIds: '关联学校',
      libraryId: '资源库',
      contentType: '内容类型',
      contentId: '内容 Id',
      contentVersionId: '内容版本',
      contentTenantId: '内容归属学校',
      policyJson: '共享策略',
      submitterType: '贡献方类型',
      status: '状态',
      previousStatus: '变更前状态',
    };
    return labels[key] ?? key;
  }

  private formatValue(value: unknown): string {
    if (value === null || value === undefined || value === '') return '—';
    if (Array.isArray(value)) return value.length ? value.join('、') : '—';
    if (typeof value === 'object') return JSON.stringify(value);
    if (typeof value === 'boolean') return value ? '是' : '否';
    return String(value);
  }

  actionText(action: ReviewActionDto): string {
    const op = action.operation;
    if (op === undefined) return action.operationName ?? '';
    return action.operationName ?? String(op);
  }
}
