import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { SchoolValidityService } from '../../proxy/schools/school-validity.service';
import type { SchoolValidityDto } from '../../proxy/enterprises/dtos/models';

/**
 * 学校有效期管理（平台侧，Host-only）。
 *
 * <p>到期后：写操作被后端全局过滤器拒绝（读保持放行）。此处只做配置与状态展示。</p>
 */
@Component({
  selector: 'app-school-validity',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzEmptyModule, NzModalModule, NzTableModule, NzTagModule],
  templateUrl: './school-validity.component.html',
  styleUrls: ['./school-validity.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SchoolValidityComponent implements OnInit {
  private readonly validityService = inject(SchoolValidityService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly rows = signal<SchoolValidityDto[]>([]);
  readonly editOpen = signal(false);
  readonly saving = signal(false);
  readonly current = signal<SchoolValidityDto | null>(null);
  readonly validFrom = signal<string | null>(null);
  readonly validTo = signal<string | null>(null);
  readonly readOnlyAfterExpiry = signal(true);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.validityService.getList().subscribe({
      next: list => {
        this.rows.set(list ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.rows.set([]);
        this.loading.set(false);
        this.message.error('加载失败（需要平台管理员权限）');
      },
    });
  }

  openEdit(row: SchoolValidityDto): void {
    this.current.set(row);
    this.validFrom.set(row.validFrom ? row.validFrom.substring(0, 10) : null);
    this.validTo.set(row.validTo ? row.validTo.substring(0, 10) : null);
    this.readOnlyAfterExpiry.set(row.readOnlyAfterExpiry ?? true);
    this.editOpen.set(true);
  }

  save(): void {
    const row = this.current();
    if (!row?.tenantId) return;

    this.saving.set(true);
    this.validityService
      .setValidity({
        tenantId: row.tenantId,
        validFrom: this.validFrom() ? `${this.validFrom()}T00:00:00Z` : null,
        validTo: this.validTo() ? `${this.validTo()}T00:00:00Z` : null,
        readOnlyAfterExpiry: this.readOnlyAfterExpiry(),
      } as any)
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.editOpen.set(false);
          this.message.success('已保存');
          this.reload();
        },
        error: err => {
          this.saving.set(false);
          this.message.error(err?.error?.error?.message ?? '保存失败');
        },
      });
  }

  statusColor(row: SchoolValidityDto): string {
    if (!row.validTo) return 'default';
    return row.isValid ? 'green' : 'red';
  }

  trackByTenant = (_: number, item: SchoolValidityDto) => item.tenantId;
}
