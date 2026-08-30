import { Component, inject, signal } from '@angular/core';
import type { PagedResultDto } from '@abp/ng.core';
import {
  ResourceShareService,
  type ResourceShareDto,
  type SharedResourceDto,
  type CreateResourceShareDto,
} from '../proxy/resources';
import { PortalService, type TenantResourceSummaryDto } from '../proxy/portal';
import { LocalizationService } from '@abp/ng.core';
import { NzMessageService } from 'ng-zorro-antd/message';

/**
 * 资源共享 UI 逻辑混入（通过 ts-mixins 模式挂载到 ResourceComponent）。
 *
 * 这里集中处理：
 * - "共享给我的"/"我共享的"两个 Tab 的列表加载与筛选
 * - 共享/取消共享操作（弹窗）
 * - "管理共享" 弹窗（查看已经共享给哪些租户并可撤销）
 */
export class ResourceShareMixin {
  // 由宿主组件注入或外部注入
  shareService = inject(ResourceShareService);
  portalService = inject(PortalService);
  shareLocalization = inject(LocalizationService);
  shareMessage = inject(NzMessageService);

  sharedToMeList = signal<PagedResultDto<SharedResourceDto>>({ items: [], totalCount: 0 } as any);
  sharedByMeList = signal<PagedResultDto<SharedResourceDto>>({ items: [], totalCount: 0 } as any);
  sharedFilter = signal<string>('');
  sharedPageIndex = 1;
  sharedPageSize = 10;

  isShareModalOpen = false;
  isManageSharesModalOpen = false;
  sharing = signal(false);
  shareNote = signal<string>('');
  selectedShareTargetIds = signal<string[]>([]);
  currentShares = signal<ResourceShareDto[]>([]);
  availableTenants = signal<TenantResourceSummaryDto[]>([]);

  loadSharedToMe(): void {
    this.shareService.getSharedToMe({
      filter: this.sharedFilter() || undefined,
      sorting: 'sharedAt DESC',
      skipCount: (this.sharedPageIndex - 1) * this.sharedPageSize,
      maxResultCount: this.sharedPageSize,
    } as any).subscribe({
      next: (res) => this.sharedToMeList.set(res),
      error: () => this.sharedToMeList.set({ items: [], totalCount: 0 } as any),
    });
  }

  loadSharedByMe(): void {
    this.shareService.getSharedByMe({
      filter: this.sharedFilter() || undefined,
      sorting: 'creationTime DESC',
      skipCount: (this.sharedPageIndex - 1) * this.sharedPageSize,
      maxResultCount: this.sharedPageSize,
    } as any).subscribe({
      next: (res) => {
        // 扩充 sharedTargetCount（占位：后端目前未返回，由前端从 currentShares 推断）
        this.sharedByMeList.set(res);
      },
      error: () => this.sharedByMeList.set({ items: [], totalCount: 0 } as any),
    });
  }

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
        // 已共享的默认不再出现在可选列表（避免误重复共享）
        const sharedIds = new Set((list || []).map(s => s.targetTenantId));
        this.availableTenants.set(
          this.availableTenants().filter(t => !sharedIds.has(t.id!))
        );
      },
      error: () => this.currentShares.set([]),
    });
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
        if ((this as any).selectedTabIndex === 5) this.loadSharedByMe();
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
        if ((this as any).selectedTabIndex === 4) this.loadSharedToMe();
        if ((this as any).selectedTabIndex === 5) this.loadSharedByMe();
      },
      error: () => this.shareMessage.error(this.shareLocalization.instant('::Unshare') + ' ❌'),
    });
  }

  manageShares(resource: SharedResourceDto): void {
    // 用 ResourceDto 的方式打开抽屉
    (this as any).selectedResource?.set?.({ ...resource, id: resource.id });
    (this as any).drawerVisible?.set?.(true);
    this.loadCurrentShares(resource.id!);
  }

  showSharedTargets(_resource: SharedResourceDto): void {
    // 切换到管理弹窗（重用 currentShares）
    this.isManageSharesModalOpen = true;
    this.loadCurrentShares(_resource.id!);
  }

  closeManageSharesDialog(): void {
    this.isManageSharesModalOpen = false;
  }

  onSharedFilterChange(value: string): void {
    this.sharedFilter.set(value);
    this.sharedPageIndex = 1;
    if ((this as any).selectedTabIndex === 4) this.loadSharedToMe();
    if ((this as any).selectedTabIndex === 5) this.loadSharedByMe();
  }

  onSharedPageChange(pageIndex: number): void {
    this.sharedPageIndex = pageIndex;
    if ((this as any).selectedTabIndex === 4) this.loadSharedToMe();
    if ((this as any).selectedTabIndex === 5) this.loadSharedByMe();
  }

  viewSharedResource(item: SharedResourceDto): void {
    // 共享资源：先调用 GetAsync 加载完整 ResourceDto（包含 filePath/originalFileName 等下载所需字段），
    // 然后打开抽屉。后端 GetAsync 已对共享资源禁用多租户过滤器。
    const resourceSvc = (this as any).resourceService;
    if (resourceSvc?.get) {
      resourceSvc.get(item.id!).subscribe({
        next: (full: any) => {
          (this as any).selectedResource?.set?.(full);
          (this as any).drawerVisible?.set?.(true);
          (this as any).loadVersions?.(item.id!);
        },
        error: () => {
          // fallback：仅用 SharedResourceDto 展示基本信息
          (this as any).selectedResource?.set?.({ ...item } as any);
          (this as any).drawerVisible?.set?.(true);
        },
      });
    } else {
      (this as any).selectedResource?.set?.({ ...item } as any);
      (this as any).drawerVisible?.set?.(true);
    }
  }
}
