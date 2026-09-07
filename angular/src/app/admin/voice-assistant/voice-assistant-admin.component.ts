import { Component, ChangeDetectionStrategy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { HttpClient } from '@angular/common/http';

interface VoiceTenantState {
  tenantId: string;
  tenantName: string;
  enabled: boolean;
}

/**
 * 语音助手开通管理（全局管理员）：
 * 按租户开/关学生端语音助手悬浮按钮。关闭后该租户学生端不渲染面板；
 * 开关对已登录学生下次加载页面生效（刷新即可），无需重启服务。
 */
@Component({
  selector: 'app-voice-assistant-admin',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, NzCardModule, NzButtonModule, NzTableModule],
  template: `
  <nz-card nzTitle="语音助手开通管理（全局管理员）" [nzBordered]="false">
    <p style="color:#888">按租户开/关学生端语音助手（课程中心/详情/学习页的悬浮麦克风：导航/总结/朗读/问答，只读）。关闭后该租户学生端不再显示入口；调整后学生刷新页面即生效。</p>
    <nz-table [nzData]="tenants()" nzSize="small">
      <thead><tr><th>租户（学校）</th><th>语音助手</th><th>操作</th></tr></thead>
      <tbody>
        @for (t of tenants(); track t.tenantId) {
          <tr>
            <td>{{ t.tenantName }}</td>
            <td>{{ t.enabled ? '已开启' : '已关闭' }}</td>
            <td>
              <button nz-button nzSize="small" (click)="toggle(t)" [disabled]="saving() === t.tenantId">
                {{ t.enabled ? '关闭' : '开启' }}
              </button>
            </td>
          </tr>
        }
      </tbody>
    </nz-table>
  </nz-card>
  `,
})
export class VoiceAssistantAdminComponent {
  private readonly http = inject(HttpClient);
  private readonly msg = inject(NzMessageService);

  readonly tenants = signal<VoiceTenantState[]>([]);
  readonly saving = signal<string | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.http.get<VoiceTenantState[]>('/api/app/voice-assistant-admin/tenant-states').subscribe({
      next: rows => this.tenants.set(rows ?? []),
      error: () => this.msg.error('加载租户语音助手状态失败'),
    });
  }

  toggle(t: VoiceTenantState): void {
    if (this.saving()) return;
    this.saving.set(t.tenantId);
    const enabled = !t.enabled;
    this.http
      .post('/api/app/voice-assistant-admin/set-tenant-enabled', { tenantId: t.tenantId, enabled })
      .subscribe({
        next: () => {
          this.saving.set(null);
          this.msg.success(enabled ? `已为 ${t.tenantName} 开启语音助手` : `已为 ${t.tenantName} 关闭语音助手`);
          this.load();
        },
        error: () => {
          this.saving.set(null);
          this.msg.error('操作失败');
        },
      });
  }
}
