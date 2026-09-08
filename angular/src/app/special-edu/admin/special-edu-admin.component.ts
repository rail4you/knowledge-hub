import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-special-edu-admin',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, NzCardModule, NzButtonModule, NzTableModule, NzSwitchModule, NzEmptyModule],
  styles: [`
    .spedu-container { padding: 24px; max-width: 1400px; margin: 0 auto; }
    .spedu-header { background: var(--kh-panel); border: 1px solid var(--kh-line); border-radius: var(--kh-r-card); box-shadow: var(--kh-shadow-card); padding: 16px 20px; margin-bottom: 16px; }
    .spedu-header h1 { margin: 0; font-size: 20px; font-weight: 700; }
    .spedu-header p { margin: 4px 0 0; color: #888; font-size: 13px; }
    .spedu-card { min-height: calc(100vh - 320px); display: flex; flex-direction: column; }
    .spedu-card ::ng-deep .ant-card { flex: 1; display: flex; flex-direction: column; }
    .spedu-card ::ng-deep .ant-card-body { flex: 1; display: flex; flex-direction: column; }
    .row-actions { white-space: nowrap; }
  `],
  template: `
  <div class="spedu-container">
  <div class="spedu-header">
    <h1>特教模块开通管理（全局管理员）</h1>
    <p>按租户整体开/关特教权限包（教学设计 / IEP / 多模态资源 / 审核）。开通后触发该租户角色权限自愈；关闭后前端分组隐藏、后端接口拦截。“写入Mock”向该租户写入特教示范课程 + zmq 选课 + 2教案/2IEP/4资源。</p>
  </div>
  <nz-card class="spedu-card">
    <nz-table [nzData]="pagedTenants()" [nzFrontPagination]="false" [nzTotal]="tenants().length"
      [nzPageIndex]="pageIndex()" [nzPageSize]="pageSize()"
      (nzPageIndexChange)="onPageIndexChange($event)" (nzPageSizeChange)="onPageSizeChange($event)"
      [nzShowSizeChanger]="true" [nzShowQuickJumper]="true" [nzShowTotal]="totalTpl" [nzNoResult]="emptyTpl" nzSize="small">
      <thead><tr><th>租户</th><th>开通状态</th><th>操作</th></tr></thead>
      <tbody>
        @for (t of pagedTenants(); track t.tenantId) {
          <tr>
            <td>{{ t.tenantName }}</td>
            <td>{{ t.enabled ? '已开通' : '未开通' }}</td>
            <td class="row-actions">
              <button nz-button nzSize="small" [nzType]="t.enabled ? 'default' : 'primary'" (click)="toggle(t)">{{ t.enabled ? '关闭' : '开通' }}</button>
              <button nz-button nzSize="small" (click)="seedMock(t)" style="margin-left:8px">写入Mock</button>
            </td>
          </tr>
        }
      </tbody>
    </nz-table>
    <ng-template #totalTpl let-total>共 {{ total }} 条</ng-template>
    <ng-template #emptyTpl><nz-empty nzNotFoundContent="暂无租户数据"></nz-empty></ng-template>
  </nz-card>
  </div>
  `,
})
export class SpecialEduAdminComponent {
  private http = inject(HttpClient);
  private msg = inject(NzMessageService);
  tenants = signal<any[]>([]);
  // 列表分页（前端分页：数据已全量加载，按页切片展示）
  pageIndex = signal(1);
  pageSize = signal(10);
  pagedTenants = computed(() => {
    const all = this.tenants();
    const start = (this.pageIndex() - 1) * this.pageSize();
    return all.slice(start, start + this.pageSize());
  });
  onPageIndexChange(i: number): void { this.pageIndex.set(i); }
  onPageSizeChange(s: number): void { this.pageSize.set(s); this.pageIndex.set(1); }

  constructor() {
    this.load();
  }

  load(): void {
    this.http.get<any[]>('/api/learning/special-edu/admin/tenants')
      .subscribe({ next: r => this.tenants.set(r ?? []), error: () => this.msg.error('加载租户状态失败') });
  }

  toggle(t: any): void {
    this.http.post('/api/learning/special-edu/admin/tenants/enabled', { tenantId: t.tenantId, enabled: !t.enabled })
      .subscribe({ next: () => { this.msg.success(t.enabled ? '已关闭' : '已开通'); this.load(); }, error: () => this.msg.error('操作失败') });
  }

  seedMock(t: any): void {
    this.http.post<any>('/api/learning/special-edu/admin/seed-mock-data', { tenantId: t.tenantId })
      .subscribe({ next: r => this.msg.success(r?.message ?? 'Mock 数据已写入'), error: () => this.msg.error('写入失败') });
  }
}
