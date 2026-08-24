import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationService, LocalizationPipe, SessionStateService, RestService } from '@abp/ng.core';
import { ConfirmationService, Confirmation } from '@abp/ng.theme.shared';
import { Observable } from 'rxjs';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzPageHeaderModule } from 'ng-zorro-antd/page-header';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { AccountValidityService, AccountValidityItem } from './account-validity.service';

interface TenantOption { id: string; name: string; }

@Component({
  selector: 'app-account-validity',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzTableModule,
    NzButtonModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    NzTagModule,
    NzDatePickerModule,
    NzInputNumberModule,
    NzRadioModule,
    NzCardModule,
    NzSpaceModule,
    NzAlertModule,
    NzIconModule,
    NzPageHeaderModule,
    NzGridModule,
    NzDividerModule,
    NzCheckboxModule,
  ],
  templateUrl: './account-validity.component.html',
  styleUrls: ['./account-validity.component.scss'],
})
export class AccountValidityComponent implements OnInit {
  loading = signal(false);
  isHost = true;

  items: AccountValidityItem[] = [];
  totalCount = 0;
  pageIndex = 1;
  pageSize = 10;

  tenants: TenantOption[] = [];
  tenantId?: string;
  roleName?: string;
  status?: string;
  expiringSoon = false;
  filter = '';

  selectedRows = new Set<string>();
  allChecked = false;
  someChecked = false;

  // modal
  isModalOpen = false;
  saving = signal(false);
  modalTitle = '';
  isBatch = false;
  mode: 'fixed' | 'days' | 'permanent' = 'fixed';
  validUntil?: Date;
  days?: number;
  modalUsers: AccountValidityItem[] = [];

  private readonly service = inject(AccountValidityService);
  private readonly rest = inject(RestService);
  private readonly localization = inject(LocalizationService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly session = inject(SessionStateService);

  l(key: string, fallback?: string): string {
    return this.localization.instant(key, fallback);
  }

  ngOnInit(): void {
    const tenant = this.session.getTenant();
    this.isHost = !tenant?.id;
    this.loadTenants();
    this.load();
  }

  getRoleDisplay(role: string | undefined): string {
    if (!role) return '';
    return this.l(`::RoleName:${role}`);
  }

  private loadTenants(): void {
    this.rest.request<any, TenantOption[]>({ method: 'GET', url: '/api/public/tenants' }).subscribe((tenants) => {
      this.tenants = tenants || [];
    });
  }

  load(): void {
    this.loading.set(true);
    this.service.getList({
      filter: this.filter || undefined,
      tenantId: this.tenantId,
      roleName: this.roleName,
      status: this.status ? Number(this.status) : undefined,
      expiringSoon: this.expiringSoon || undefined,
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize,
    }).subscribe({
      next: (res) => {
        this.items = res.items || [];
        this.totalCount = res.totalCount || 0;
        this.refreshCheckedState();
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onTenantFilterChange(): void { this.pageIndex = 1; this.load(); }
  onRoleFilterChange(): void { this.pageIndex = 1; this.load(); }
  onStatusFilterChange(): void { this.pageIndex = 1; this.load(); }
  onExpiringSoonChange(): void { this.pageIndex = 1; this.load(); }

  search(): void {
    this.pageIndex = 1;
    this.load();
  }

  resetFilters(): void {
    this.filter = '';
    this.tenantId = undefined;
    this.roleName = undefined;
    this.status = undefined;
    this.expiringSoon = false;
    this.pageIndex = 1;
    this.load();
  }

  onPageIndexChange(i: number): void { this.pageIndex = i; this.load(); }
  onPageSizeChange(s: number): void { this.pageSize = s; this.pageIndex = 1; this.load(); }

  getStatusMeta(item: AccountValidityItem): { text: string; color: string } {
    if (item.isExpired) return { text: this.l('::AccountValidity:Status:Expired'), color: 'red' };
    if (item.isExpiringSoon) return { text: this.l('::AccountValidity:Status:ExpiringSoon'), color: 'orange' };
    if (item.status == null || item.validUntil == null) return { text: this.l('::AccountValidity:Status:Never'), color: 'blue' };
    return { text: this.l('::AccountValidity:Status:Active'), color: 'green' };
  }

  formatValidUntil(item: AccountValidityItem): string {
    if (!item.validUntil) return this.l('::AccountValidity:Status:Never');
    const d = new Date(item.validUntil);
    const s = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    if (item.isExpired) return `${s}（${this.l('::AccountValidity:Status:Expired')}）`;
    if (item.remainingDays != null && item.remainingDays <= 7) {
      return `${s}（${this.l('::AccountValidity:DaysLeft').replace('{0}', String(item.remainingDays))}）`;
    }
    return s;
  }

  // ── 行选择 ──
  onCheckChange(id: string, checked: boolean): void {
    if (checked) this.selectedRows.add(id);
    else this.selectedRows.delete(id);
    this.refreshCheckedState();
  }

  onAllChecked(checked: boolean): void {
    this.items.forEach((i) => {
      if (checked) this.selectedRows.add(i.id);
      else this.selectedRows.delete(i.id);
    });
    this.refreshCheckedState();
  }

  private refreshCheckedState(): void {
    this.allChecked = this.items.length > 0 && this.items.every((i) => this.selectedRows.has(i.id));
    this.someChecked = this.items.some((i) => this.selectedRows.has(i.id)) && !this.allChecked;
  }

  isChecked(id: string): boolean {
    return this.selectedRows.has(id);
  }

  // ── 弹窗 ──
  openSetSingle(item: AccountValidityItem): void {
    this.isBatch = false;
    this.modalUsers = [item];
    this.modalTitle = this.l('::AccountValidity:SetValidity');
    this.resetModalForm(item);
    this.isModalOpen = true;
  }

  openSetBatch(): void {
    const selected = this.items.filter((i) => this.selectedRows.has(i.id));
    if (!selected.length) {
      this.confirmation.info(this.l('::AccountValidity:SelectUsers'), this.l('::Notice'));
      return;
    }
    this.isBatch = true;
    this.modalUsers = selected;
    this.modalTitle = `${this.l('::AccountValidity:BatchSet')}（${selected.length}）`;
    this.resetModalForm(undefined);
    this.isModalOpen = true;
  }

  private resetModalForm(item?: AccountValidityItem): void {
    this.mode = 'fixed';
    this.validUntil = item?.validUntil ? new Date(item.validUntil) : undefined;
    this.days = undefined;
  }

  closeModal(): void {
    this.isModalOpen = false;
  }

  onModeChange(): void { /* bound via ngModel */ }

  save(): void {
    if (this.mode === 'fixed' && !this.validUntil) {
      this.confirmation.info(this.l('::ThisFieldIsRequired'), this.l('::Notice'));
      return;
    }
    if (this.mode === 'days' && (!this.days || this.days <= 0)) {
      this.confirmation.info(this.l('::ThisFieldIsRequired'), this.l('::Notice'));
      return;
    }

    const permanent = this.mode === 'permanent';
    const body = {
      validUntil: this.mode === 'fixed' && this.validUntil ? this.validUntil.toISOString() : null,
      permanent,
      days: this.mode === 'days' ? this.days : null,
    };

    this.saving.set(true);
    const action: Observable<unknown> = this.isBatch
      ? this.service.setBatch({ userIds: this.modalUsers.map((u) => u.userId), ...body })
      : this.service.setValidity({ userId: this.modalUsers[0].userId, ...body });

    action.subscribe({
      next: () => {
        this.saving.set(false);
        this.isModalOpen = false;
        this.selectedRows.clear();
        this.refreshCheckedState();
        this.confirmation.success(this.l('::AccountValidity:SetSuccess'), '');
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.confirmation.error(this.l('::AccountValidity:SetFail'), '');
      },
    });
  }

  renew(item: AccountValidityItem): void {
    this.confirmation.warn(
      this.l('::AccountValidity:Renew') + '：' + (item.displayName || item.userName),
      this.l('::AreYouSure')
    ).subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.service.setValidity({ userId: item.userId, validUntil: null, permanent: true }).subscribe({
          next: () => {
            this.confirmation.success(this.l('::AccountValidity:SetSuccess'), '');
            this.load();
          },
        });
      }
    });
  }
}
