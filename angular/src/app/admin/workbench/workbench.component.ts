import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { ConfigStateService } from '@abp/ng.core';
import { TenantListService } from '../../proxy/controllers/tenant-list.service';
import { WorkbenchService, WorkbenchStats } from './workbench.service';

interface TenantOption {
  id: string;
  name: string;
}

/**
 * 系统工作台（管理端首页）。
 *
 * 聚合展示资源、课程、AI、就业、实训、资讯、检索、用户等模块的实时统计。
 * - 租户管理员：仅展示本租户数据；
 * - host 全局管理员：头部提供租户列表切换，可查看单个租户或汇总全部租户。
 */
@Component({
  selector: 'app-workbench',
  standalone: true,
  imports: [CommonModule, DecimalPipe, DatePipe, NzIconModule, NzSpinModule, NzEmptyModule],
  templateUrl: './workbench.component.html',
  styleUrls: ['./workbench.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorkbenchComponent implements OnInit {
  private readonly config = inject(ConfigStateService);
  private readonly tenantListService = inject(TenantListService);
  private readonly workbench = inject(WorkbenchService);

  readonly stats = signal<WorkbenchStats | null>(null);
  readonly loading = signal(false);
  readonly loadError = signal(false);

  /** 是否 host 全局管理员（无租户上下文）。 */
  readonly isHost = signal(false);
  readonly tenants = signal<TenantOption[]>([]);
  /** null = 全部租户（仅 host 可选）。 */
  readonly selectedTenantId = signal<string | null>(null);

  readonly scopeName = computed(() => {
    const id = this.selectedTenantId();
    if (id) {
      return this.tenants().find(t => t.id === id)?.name || '';
    }
    if (this.isHost()) {
      return '全部租户';
    }
    // 租户管理员：显示后端返回的当前租户名。
    return this.stats()?.tenantName || '';
  });

  ngOnInit(): void {
    const tenantId = this.config.getDeep('currentUser.tenantId') as string | null | undefined;
    const host = !tenantId;
    this.isHost.set(host);

    if (host) {
      this.tenantListService.getTenants().subscribe({
        next: list => {
          const real = (list || [])
            .filter(t => !!t?.id && !!t?.name)
            .map(t => ({ id: t.id as string, name: t.name as string }));
          this.tenants.set(real);
        },
      });
    }

    this.loadStats();
  }

  selectTenant(id: string | null): void {
    if (this.selectedTenantId() === id) {
      return;
    }
    this.selectedTenantId.set(id);
    this.loadStats();
  }

  refresh(): void {
    this.loadStats();
  }

  private loadStats(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.workbench.getStats(this.selectedTenantId()).subscribe({
      next: stats => {
        this.stats.set(stats);
        this.loading.set(false);
      },
      error: () => {
        this.stats.set(null);
        this.loading.set(false);
        this.loadError.set(true);
      },
    });
  }

  /** 百分比展示（0 分母返回 0）。 */
  rate(part: number | undefined, total: number | undefined): number {
    if (!total) {
      return 0;
    }
    return Math.round(((part || 0) / total) * 100);
  }
}
