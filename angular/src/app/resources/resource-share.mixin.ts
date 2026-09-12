import { Component, inject, signal } from '@angular/core';
import type { PagedResultDto } from '@abp/ng.core';
import {
  ResourceShareService,
  type ResourceShareDto,
  type CreateResourceShareDto,
} from '../proxy/resources';
import { PortalService, type TenantResourceSummaryDto } from '../proxy/portal';
import { LocalizationService } from '@abp/ng.core';
import { NzMessageService } from 'ng-zorro-antd/message';

/**
 * 资源共享 UI 逻辑混入（通过 ts-mixins 模式挂载到 ResourceComponent）。
 *
 * 简化版（v2）：
 * - 资源库主列表已合并「共享给我的」资源（在共享资源上展示来源租户 + 共享信息），
 *   不再单独维护「共享给我的」Tab。
 * - 「我共享的」资源也已直接展示在主列表里（通过 isShared 标记和「共享者」列反查），
 *   共享管理在资源详情 drawer 通过「共享」按钮操作。
 * - 本 mixin 只保留共享弹窗的最小逻辑：选择目标租户、确认共享、撤销共享、查看已共享列表。
 */
export class ResourceShareMixin {
  // 由宿主组件注入或外部注入
  shareService = inject(ResourceShareService);
  portalService = inject(PortalService);
  shareLocalization = inject(LocalizationService);
  shareMessage = inject(NzMessageService);

  isShareModalOpen = false;
  sharing = signal(false);
  shareNote = signal<string>('');
  selectedShareTargetIds = signal<string[]>([]);
  currentShares = signal<ResourceShareDto[]>([]);
  availableTenants = signal<TenantResourceSummaryDto[]>([]);

  /**
   * 当前选中的本租户资源已共享给多少个目标租户（用于在 drawer 上展示「已共享给 N 个租户」徽标，
   * 以及控制「取消共享」按钮的显隐）。被其它租户共享过来的资源（isShared=true）走另一套展示，
   * 这里的 count 仅对当前租户拥有的资源有意义。
   */
  outgoingShareCount = signal<number>(0);

  /** 当前选中的资源是否属于当前租户（用于控制共享按钮是否可点）。 */
  isOwnResource = signal<boolean>(true);

  /**
   * 共享资源弹窗宽度：响应式配置，避免窄屏占满全屏。
   * ng-zorro nz-modal 的 [nzWidth] 接受字符串或 NzBreakpointKey 响应式对象。
   */
  shareModalWidth: string | { xs?: string; sm?: string; md?: string; lg?: string; xl?: string; xxl?: string } = {
    xs: '92%',
    sm: '560px',
    md: '640px',
    lg: '720px',
  };

  loadAvailableTenants(): void {
    this.portalService.getPublicTenantList().subscribe({
      next: (list) => this.availableTenants.set(list || []),
      error: () => this.availableTenants.set([]),
    });
  }

  loadCurrentShares(resourceId: string): void {
    this.shareService.getShares(resourceId).subscribe({
      next: (list) => {
        this.currentShares.set(list || []);
        this.outgoingShareCount.set((list || []).length);
        // 已共享的目标租户默认不再出现在可选列表（避免误重复共享）
        const sharedIds = new Set((list || []).map(s => s.targetTenantId));
        this.availableTenants.set(
          this.availableTenants().filter(t => !sharedIds.has(t.id!))
        );
      },
      error: () => this.currentShares.set([]),
    });
  }

  /**
   * 加载当前选中资源的 outgoing shares（不打开弹窗），
   * 用于在 drawer 上展示「已共享给 N 个租户」徽标和控制「取消共享」按钮显隐。
   * 仅对当前租户拥有的资源调用；被其它租户共享过来的资源走 incoming 路径，不会调本方法。
   */
  loadOutgoingShares(resourceId: string): void {
    this.shareService.getShares(resourceId).subscribe({
      next: (list) => this.outgoingShareCount.set((list || []).length),
      error: () => this.outgoingShareCount.set(0),
    });
  }

  /** 切换资源时清空 outgoing share 状态。 */
  resetOutgoingShareState(): void {
    this.outgoingShareCount.set(0);
    this.currentShares.set([]);
    this.availableTenants.set([]);
    this.selectedShareTargetIds.set([]);
    this.shareNote.set('');
  }

  /**
   * 当前资源可被本租户管理共享吗？
   * 被其它租户共享过来的资源（isShared=true）不能在本租户取消共享 —— 仅创建者账户能取消。
   */
  canManageShare(): boolean {
    return this.isOwnResource();
  }

  openShareDialog(): void {
    this.isShareModalOpen = true;
    this.selectedShareTargetIds.set([]);
    this.shareNote.set('');
    const resource = (this as any).selectedResource?.();
    if (!resource?.id) return;
    this.loadAvailableTenants();
    this.loadCurrentShares(resource.id);
  }

  closeShareDialog(): void {
    this.isShareModalOpen = false;
  }

  confirmShare(): void {
    const resource = (this as any).selectedResource?.();
    if (!resource?.id) return;
    const targetIds = this.selectedShareTargetIds();
    if (targetIds.length === 0) return;

    this.sharing.set(true);
    const input: CreateResourceShareDto = {
      resourceId: resource.id,
      targetTenantIds: targetIds,
      note: this.shareNote() || undefined,
    };
    this.shareService.share(input).subscribe({
      next: () => {
        this.sharing.set(false);
        this.isShareModalOpen = false;
        this.shareMessage.success(this.shareLocalization.instant('::SharedResources'));
        this.loadCurrentShares(resource.id);
        // 共享成功后刷新主列表，让「共享者」列显示给本租户
        (this as any).loadResources?.();
      },
      error: (err) => {
        this.sharing.set(false);
        const msg = err?.error?.error?.message || this.shareLocalization.instant('::ShareResource') + ' ❌';
        this.shareMessage.error(msg);
      },
    });
  }

  unshareTenant(targetTenantId: string): void {
    const resource = (this as any).selectedResource?.();
    if (!resource?.id) return;
    this.shareService.unshare(resource.id, targetTenantId).subscribe({
      next: () => {
        this.shareMessage.success(this.shareLocalization.instant('::Unshare'));
        this.loadCurrentShares(resource.id);
        // 撤销后刷新主列表（当前资源 isShared 可能变 false）
        (this as any).loadResources?.();
      },
      error: () => this.shareMessage.error(this.shareLocalization.instant('::Unshare') + ' ❌'),
    });
  }
}
