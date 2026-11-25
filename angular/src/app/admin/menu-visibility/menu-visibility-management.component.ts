import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfigStateService, LocalizationService, RestService } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import {
  MenuVisibilityListItem,
  MenuVisibilityService,
} from '../../shared/menu-visibility/menu-visibility.service';
import { UserMenuVisibilityComponent } from '../identity-users/user-menu-visibility.component';

interface TenantOption {
  id?: string | null;
  name?: string;
}

/**
 * 菜单可见性管理（租户管理员 / 宿主管理员）。
 *
 * <p>按**老师**单独控制侧边栏第一级菜单的显示 / 隐藏。列表只列「老师」角色用户，
 * 每行可以打开配置弹窗；也可一键恢复全部可见。</p>
 *
 * <p>配置本身存在后端（`AppUserMenuVisibilities`），老师刷新页面后生效。
 * 只影响导航外观，不回收功能权限。</p>
 */
@Component({
  selector: 'app-menu-visibility-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzTableModule,
    NzButtonModule,
    NzCardModule,
    NzInputModule,
    NzSelectModule,
    NzTagModule,
    NzIconModule,
    NzTooltipModule,
    NzSpaceModule,
    NzEmptyModule,
    UserMenuVisibilityComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mv-page">
      <nz-card class="page-header-card" [nzBordered]="false">
        <div class="page-title-block">
          <h1>菜单可见性管理</h1>
          <p>
            按老师单独控制侧边栏 <strong>第一级菜单</strong> 的显示与隐藏。隐藏只影响导航外观，
            不会回收该老师的任何功能权限（直接输入网址仍可访问），老师刷新页面后生效。
          </p>
        </div>
      </nz-card>

      <nz-card [nzBordered]="false">
        <div class="mv-toolbar">
          <div class="mv-toolbar-left">
            <nz-input-group nzPrefixIcon="search" class="mv-search">
              <input
                nz-input
                placeholder="搜索登录名 / 姓名"
                [ngModel]="keyword()"
                (ngModelChange)="onKeywordChange($event)"
              />
            </nz-input-group>
            @if (isHostAdmin()) {
              <nz-select
                class="mv-tenant-select"
                [ngModel]="tenantId()"
                (ngModelChange)="onTenantChange($event)"
                nzPlaceHolder="选择租户"
              >
                <nz-option [nzValue]="''" nzLabel="（请选择租户）"></nz-option>
                @for (tenant of tenants(); track tenant.id) {
                  <nz-option [nzValue]="tenant.id" [nzLabel]="tenant.name || '未命名'"></nz-option>
                }
              </nz-select>
            }
            <button nz-button (click)="reload()">
              <span nz-icon nzType="reload"></span>
              刷新
            </button>
          </div>
          <div class="mv-toolbar-right">
            共 <b>{{ total() }}</b> 位老师，其中 <b>{{ restrictedCount() }}</b> 位已限制菜单
          </div>
        </div>

        <nz-table
          #table
          [nzData]="rows()"
          [nzTotal]="total()"
          [nzPageIndex]="pageIndex()"
          [nzPageSize]="pageSize()"
          [nzFrontPagination]="false"
          [nzShowSizeChanger]="true"
          [nzLoading]="loading()"
          (nzPageIndexChange)="onPageIndexChange($event)"
          (nzPageSizeChange)="onPageSizeChange($event)"
          [nzNoResult]="emptyTpl"
          nzTableLayout="fixed"
        >
          <thead>
            <tr>
              <th nzWidth="22%">登录名</th>
              <th nzWidth="18%">姓名</th>
              @if (isHostAdmin()) {
                <th nzWidth="16%">所属租户</th>
              }
              <th nzWidth="12%" nzAlign="center">已隐藏一级菜单</th>
              <th>隐藏详情</th>
              <th nzWidth="180px" nzAlign="center">操作</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.userId) {
              <tr>
                <td><span class="cell-strong">{{ row.userName }}</span></td>
                <td>{{ row.displayName || '-' }}</td>
                @if (isHostAdmin()) {
                  <td>{{ row.tenantName || row.tenantId || '-' }}</td>
                }
                <td nzAlign="center">
                  @if (row.hiddenMenus.length) {
                    <nz-tag nzColor="orange">{{ row.hiddenMenus.length }} 个</nz-tag>
                  } @else {
                    <nz-tag>全部可见</nz-tag>
                  }
                </td>
                <td>
                  @for (name of row.hiddenMenus; track name) {
                    <nz-tag class="mv-menu-tag">{{ menuLabel(name) }}</nz-tag>
                  }
                  @if (!row.hiddenMenus.length) {
                    <span class="cell-muted">—</span>
                  }
                </td>
                <td nzAlign="center">
                  <nz-space>
                    <button nz-button nzType="link" *nzSpaceItem (click)="configure(row)">配置菜单</button>
                    @if (row.hiddenMenus.length) {
                      <button
                        nz-button
                        nzType="link"
                        nzDanger
                        *nzSpaceItem
                        (click)="restore(row)"
                        nz-tooltip="清空该老师的全部菜单限制"
                      >
                        恢复全部
                      </button>
                    }
                  </nz-space>
                </td>
              </tr>
            }
          </tbody>
        </nz-table>

        <ng-template #emptyTpl>
          @if (needTenant()) {
            <nz-empty nzNotFoundImage="simple" nzNotFoundContent="请先在上方选择租户"></nz-empty>
          } @else {
            <nz-empty nzNotFoundImage="simple" nzNotFoundContent="没有匹配的老师"></nz-empty>
          }
        </ng-template>
      </nz-card>
    </div>

    @if (editing()) {
      <app-user-menu-visibility
        [userId]="editing()!.userId"
        [userName]="editing()!.userName"
        (closed)="onModalClosed($event)"
      ></app-user-menu-visibility>
    }
  `,
  styles: [
    `
      .mv-page {
        padding: 4px 0 16px;
      }
      .page-header-card {
        margin-bottom: 14px;
      }
      .page-title-block h1 {
        margin: 0 0 6px;
        font-size: 20px;
        font-weight: 600;
        color: #183444;
      }
      .page-title-block p {
        margin: 0;
        color: #6b7c79;
        font-size: 13px;
        line-height: 1.7;
      }
      .mv-toolbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 12px;
        margin-bottom: 14px;
        flex-wrap: wrap;
      }
      .mv-toolbar-left {
        display: flex;
        align-items: center;
        gap: 10px;
        flex-wrap: wrap;
      }
      .mv-search {
        width: 240px;
      }
      .mv-tenant-select {
        width: 180px;
      }
      .mv-toolbar-right {
        font-size: 13px;
        color: #4c5b58;
      }
      .mv-toolbar-right b {
        color: #087f7a;
      }
      .cell-strong {
        font-weight: 500;
        color: #183444;
      }
      .cell-muted {
        color: #9aa5a2;
      }
      .mv-menu-tag {
        margin-bottom: 4px;
      }
    `,
  ],
})
export class MenuVisibilityManagementComponent implements OnInit {
  private readonly service = inject(MenuVisibilityService);
  private readonly rest = inject(RestService);
  private readonly message = inject(NzMessageService);
  private readonly configState = inject(ConfigStateService);
  private readonly localization = inject(LocalizationService);

  readonly rows = signal<MenuVisibilityListItem[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  readonly keyword = signal('');
  readonly tenantId = signal<string>('');
  readonly tenants = signal<TenantOption[]>([]);
  readonly editing = signal<MenuVisibilityListItem | null>(null);

  readonly isHostAdmin = computed(() => !this.configState.getDeep('currentUser.tenantId'));
  readonly restrictedCount = computed(() => this.rows().filter(r => r.hiddenMenus.length > 0).length);

  private keywordTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnInit(): void {
    if (this.isHostAdmin()) {
      this.loadTenants();
    }
    this.reload();
  }

  private loadTenants(): void {
    this.rest.request<any, TenantOption[]>({ method: 'GET', url: '/api/public/tenants' }).subscribe({
      next: list => this.tenants.set((list || []).filter(t => !!t.id)),
      error: () => this.tenants.set([]),
    });
  }

  /** 宿主管理员必须选一个租户：Teacher 是同名角色按租户各建一份，跨租户按角色名查不到人。 */
  readonly needTenant = computed(() => this.isHostAdmin() && !this.tenantId());

  reload(): void {
    if (this.needTenant()) {
      this.rows.set([]);
      this.total.set(0);
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    this.service
      .getTeachers({
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
        filter: this.keyword(),
        tenantId: this.tenantId() || null,
      })
      .subscribe({
        next: result => {
          this.rows.set(result.items || []);
          this.total.set(result.totalCount || 0);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('读取老师列表失败');
        },
      });
  }

  onKeywordChange(value: string): void {
    this.keyword.set(value || '');
    if (this.keywordTimer) {
      clearTimeout(this.keywordTimer);
    }
    this.keywordTimer = setTimeout(() => {
      this.pageIndex.set(1);
      this.reload();
    }, 350);
  }

  onTenantChange(value: string): void {
    this.tenantId.set(value || '');
    this.pageIndex.set(1);
    this.reload();
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
    this.reload();
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
    this.reload();
  }

  configure(row: MenuVisibilityListItem): void {
    this.editing.set(row);
  }

  onModalClosed(event: { saved: boolean }): void {
    this.editing.set(null);
    if (event?.saved) {
      this.reload();
    }
  }

  restore(row: MenuVisibilityListItem): void {
    this.service.resetUserMenus(row.userId).subscribe({
      next: () => {
        this.message.success(`已恢复「${row.userName}」的全部菜单可见`);
        this.reload();
      },
      error: () => this.message.error('操作失败'),
    });
  }

  /** 菜单名 → 显示名（`::Menu:TrainingManagement` → 就业管理）。 */
  menuLabel(name: string): string {
    const label = this.localization.instant(name);
    return label && label !== name ? label : name.replace('::Menu:', '');
  }
}
