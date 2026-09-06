import { Component, ChangeDetectionStrategy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-special-edu-admin',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, NzCardModule, NzButtonModule, NzTableModule, NzSwitchModule],
  template: `
  <nz-card nzTitle="特教模块开通管理（全局管理员）">
    <p style="color:#888">按租户整体开/关特教权限包（教学设计 / IEP / 多模态资源 / 审核）。开通后触发该租户角色权限自愈；关闭后前端分组隐藏、后端接口拦截。“写入Mock”向该租户写入特教示范课程 + zmq 选课 + 2教案/2IEP/4资源。</p>
    <nz-table [nzData]="tenants()" nzSize="small">
      <thead><tr><th>租户</th><th>开通状态</th><th>操作</th></tr></thead>
      <tbody>
        @for (t of tenants(); track t.tenantId) {
          <tr>
            <td>{{ t.tenantName }}</td>
            <td>{{ t.enabled ? '已开通' : '未开通' }}</td>
            <td>
              <button nz-button nzSize="small" (click)="toggle(t)">{{ t.enabled ? '关闭' : '开通' }}</button>
              <button nz-button nzSize="small" (click)="seedMock(t)" style="margin-left:8px">写入Mock</button>
            </td>
          </tr>
        }
      </tbody>
    </nz-table>
  </nz-card>
  `,
})
export class SpecialEduAdminComponent {
  private http = inject(HttpClient);
  private msg = inject(NzMessageService);
  tenants = signal<any[]>([]);

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
