import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzInputModule } from 'ng-zorro-antd/input';
import {
  EmploymentApplicationStatus,
  EmploymentService,
  JobApplicationDto,
  UpdateJobApplicationStatusDto,
} from '../../employment/employment.service';

interface StatItem {
  label: string;
  value: number;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-employment-application-management',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    FormsModule,
    NzIconModule,
    NzSpinModule,
    NzModalModule,
    NzInputModule,
  ],
  templateUrl: './employment-application-management.component.html',
  styleUrls: ['./employment-application-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmploymentApplicationManagementComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly items = signal<JobApplicationDto[]>([]);
  readonly loading = signal(false);
  readonly updating = signal(false);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  readonly keyword = signal('');
  readonly statusFilter = signal<EmploymentApplicationStatus | null>(null);
  readonly statuses = EmploymentApplicationStatus;

  /** 选中的条目（用于详情/审核弹窗） */
  selectedItem: JobApplicationDto | null = null;
  reviewVisible = false;
  reviewStatus: EmploymentApplicationStatus = EmploymentApplicationStatus.Rejected;
  reviewRemark = '';

  readonly stats = computed<StatItem[]>(() => {
    const items = this.items();
    const total = this.totalCount();
    const submitted = items.filter(x => x.status === EmploymentApplicationStatus.Submitted).length;
    const interview = items.filter(x => x.status === EmploymentApplicationStatus.InterviewScheduled).length;
    const offered = items.filter(x => x.status === EmploymentApplicationStatus.Offered).length;
    const rejected = items.filter(x => x.status === EmploymentApplicationStatus.Rejected).length;
    return [
      { label: '累计投递', value: total, icon: 'send', color: 'linear-gradient(135deg, #1e6ce8 0%, #00b7ff 100%)' },
      { label: '待审核', value: submitted, icon: 'clock-circle', color: 'linear-gradient(135deg, #f59e0b 0%, #fb923c 100%)' },
      { label: '面试邀请', value: interview, icon: 'calendar', color: 'linear-gradient(135deg, #0c4cb8 0%, #1e6ce8 100%)' },
      { label: '已录用', value: offered, icon: 'trophy', color: 'linear-gradient(135deg, #10b981 0%, #06b6d4 100%)' },
    ];
  });

  ngOnInit(): void {
    this.loadItems();
  }

  loadItems(): void {
    this.loading.set(true);
    this.employmentService.getJobApplicationList({
      status: this.statusFilter() ?? undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    }).subscribe({
      next: result => {
        this.items.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载投递记录失败');
      },
    });
  }

  /**
   * 乐观更新：本地修改 item 的状态字段，并按"是否仍在当前 status 过滤范围内"增删 totalCount。
   * 替代原来的 loadItems() 全量刷新，避免出现：
   *   1. 整页跳到 loading spinner（min-height: 360px 与表格实际高度不一致 → 抖动）
   *   2. 状态切换后该条目被服务端从当前页过滤掉 → 表格瞬间空白
   *   3. 顶部 4 个统计数字前后矛盾（累计/待审/面试/录用）
   *
   * 当某条被改成与 statusFilter 不符的状态时：
   *   - totalCount -1
   *   - 从 items 数组里移除该条
   * 否则（没过滤 或 改后仍符合过滤）：
   *   - 仅替换 items 中对应条目的状态字段
   *
   * 注意：服务端排序/分页可能不再准确。极端情况下若 totalCount 因此变 0，
   * 可在外部 catch 时再走一次 loadItems() 重置。
   */
  private applyLocalStatusChange(itemId: string, newStatus: EmploymentApplicationStatus): void {
    const statusFilter = this.statusFilter();
    const list = this.items();
    const idx = list.findIndex(x => x.id === itemId);
    if (idx < 0) return;

    const oldItem = list[idx];
    const oldStatus = oldItem.status;
    const newItem = { ...oldItem, status: newStatus };

    // 当前 status 过滤会"过滤掉"该条 → 移除
    if (statusFilter != null && statusFilter !== newStatus) {
      const next = [...list.slice(0, idx), ...list.slice(idx + 1)];
      this.items.set(next);
      this.totalCount.update(n => Math.max(0, n - 1));
      return;
    }

    // 当前 status 过滤下仍然显示该条 → 仅更新状态
    // 这种情况：old 不在过滤内（被替换上来）？不可能。
    // 或 statusFilter 为 null → 任意状态都显示。
    if (statusFilter == null) {
      this.items.set([...list.slice(0, idx), newItem, ...list.slice(idx + 1)]);
      return;
    }

    // statusFilter === newStatus，但 old !== newStatus：说明该条被切换进了当前过滤范围。
    // 这种情况理论上不会发生（后端初始列表就是按 statusFilter 过滤的），
    // 但为保险起见，仍用替换 + totalCount +1。
    this.items.set([...list.slice(0, idx), newItem, ...list.slice(idx + 1)]);
    if (oldStatus !== newStatus) {
      this.totalCount.update(n => n + 1);
    }
  }

  onFilterChange(): void {
    this.pageIndex.set(1);
    this.loadItems();
  }

  onSearch(): void {
    this.pageIndex.set(1);
    this.loadItems();
  }

  onPageChange(page: number): void {
    this.pageIndex.set(page);
    this.loadItems();
  }

  /** 快速通过 */
  quickApprove(item: JobApplicationDto): void {
    if (item.status === EmploymentApplicationStatus.Offered) {
      this.message.warning('该投递已是录用状态');
      return;
    }
    this.modal.confirm({
      nzTitle: '确认通过该投递？',
      nzContent: `学生【${item.studentName || '-'}】投递【${item.jobTitle || '-'}】将标记为"已录用"。`,
      nzOkText: '确认通过',
      nzOkType: 'primary',
      nzCancelText: '取消',
      nzOnOk: () => {
        return new Promise<void>(resolve => {
          this.employmentService
            .updateApplicationStatus(item.id, {
              status: EmploymentApplicationStatus.Offered,
              employerRemark: '教师端已审核通过',
            })
            .subscribe({
              next: () => {
                this.message.success('已通过');
                // 乐观更新本地数据，避免 loadItems() 造成的整页闪烁 / 表格瞬间空白 / 统计数字矛盾
                this.applyLocalStatusChange(item.id, EmploymentApplicationStatus.Offered);
                resolve();
              },
              error: () => {
                this.message.error('操作失败');
                resolve();
              },
            });
        });
      },
    });
  }

  /** 快速拒绝 */
  quickReject(item: JobApplicationDto): void {
    if (item.status === EmploymentApplicationStatus.Rejected) {
      this.message.warning('该投递已是未通过状态');
      return;
    }
    this.selectedItem = item;
    this.reviewStatus = EmploymentApplicationStatus.Rejected;
    this.reviewRemark = '';
    this.reviewVisible = true;
  }

  openReview(item: JobApplicationDto): void {
    this.selectedItem = item;
    this.reviewStatus =
      item.status === EmploymentApplicationStatus.Rejected
        ? EmploymentApplicationStatus.Rejected
        : EmploymentApplicationStatus.InterviewScheduled;
    this.reviewRemark = '';
    this.reviewVisible = true;
  }

  closeReview(): void {
    this.reviewVisible = false;
    this.selectedItem = null;
    this.reviewRemark = '';
  }

  submitReview(): void {
    if (!this.selectedItem) {
      return;
    }
    if (this.reviewStatus === EmploymentApplicationStatus.Rejected && !this.reviewRemark.trim()) {
      this.message.warning('请填写拒绝原因');
      return;
    }

    // 关键：先在闭包里捕获 id 和 targetStatus，再 closeReview()，否则 selectedItem
    // 被置 null 后，下面乐观更新用的 this.selectedItem!.id 是 undefined，状态不会变。
    const targetItemId = this.selectedItem.id;
    const targetStatus = this.reviewStatus;

    this.updating.set(true);
    const payload: UpdateJobApplicationStatusDto = {
      status: this.reviewStatus,
      employerRemark: this.reviewRemark.trim() || undefined,
    };
    this.employmentService.updateApplicationStatus(targetItemId, payload).subscribe({
      next: () => {
        this.updating.set(false);
        this.message.success('审核完成');
        this.closeReview();
        // 乐观更新本地数据，避免 loadItems() 造成的整页闪烁 / 表格瞬间空白 / 统计数字矛盾
        this.applyLocalStatusChange(targetItemId, targetStatus);
      },
      error: () => {
        this.updating.set(false);
        this.message.error('审核失败');
      },
    });
  }

  openJobDetail(item: JobApplicationDto): void {
    this.router.navigate(['/employment/jobs', item.jobPostingId]);
  }

  getStatusInfo(status: EmploymentApplicationStatus): { label: string; color: string; icon: string } {
    const map: Record<number, { label: string; color: string; icon: string }> = {
      [EmploymentApplicationStatus.Submitted]: { label: '已投递', color: '#1e6ce8', icon: 'send' },
      [EmploymentApplicationStatus.Viewed]: { label: '已查看', color: '#6366f1', icon: 'eye' },
      [EmploymentApplicationStatus.InterviewScheduled]: { label: '面试邀请', color: '#00b7ff', icon: 'calendar' },
      [EmploymentApplicationStatus.Offered]: { label: '已录用', color: '#10b981', icon: 'trophy' },
      [EmploymentApplicationStatus.Rejected]: { label: '未通过', color: '#ef4444', icon: 'close-circle' },
      [EmploymentApplicationStatus.Withdrawn]: { label: '已撤回', color: '#94a3b8', icon: 'rollback' },
    };
    return map[status] || { label: '未知', color: '#94a3b8', icon: 'question' };
  }

  /** 学生头像背景色（基于学生名稳定生成）。与 student-layout.avatarGradient 同款算法。 */
  getAvatarGradient(name: string): string {
    const palettes = [
      '#1e6ce8',
      '#0891b2',
      '#059669',
      '#10b981',
      '#0284c7',
      '#0c4cb8',
    ];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = (hash * 31 + name.charCodeAt(i)) | 0;
    }
    return palettes[Math.abs(hash) % palettes.length];
  }

  /** 用于过滤后端已加载的列表（前端关键字过滤） */
  filteredItems(): JobApplicationDto[] {
    const k = this.keyword().trim().toLowerCase();
    if (!k) return this.items();
    return this.items().filter(item => {
      const fields = [
        item.jobTitle,
        item.companyName,
        item.studentName,
        item.resumeTitle,
      ];
      return fields.some(f => (f || '').toLowerCase().includes(k));
    });
  }
}
