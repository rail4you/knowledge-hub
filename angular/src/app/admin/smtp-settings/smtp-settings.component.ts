import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzPageHeaderModule } from 'ng-zorro-antd/page-header';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import {
  SmtpConfigService,
  SmtpConfigDto,
} from '../../proxy/emailing';

/** 163 / QQ 常用 SMTP 预设，一键填充（密码仍需手动填授权码）。 */
const PRESETS: Array<{ key: string; label: string; host: string; port: number }> = [
  { key: '163', label: '网易 163', host: 'smtp.163.com', port: 465 },
  { key: 'qq', label: 'QQ 邮箱', host: 'smtp.qq.com', port: 465 },
  { key: 'gmail', label: 'Gmail', host: 'smtp.gmail.com', port: 587 },
];

@Component({
  selector: 'app-smtp-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzFormModule,
    NzInputModule,
    NzButtonModule,
    NzAlertModule,
    NzSpinModule,
    NzIconModule,
    NzPageHeaderModule,
    NzInputNumberModule,
  ],
  template: `
    <nz-page-header>
      <nz-page-header-title>SMTP 邮件设置</nz-page-header-title>
      <nz-page-header-subtitle>
        资源审核提醒 · 审核结果通知 · 招聘投递（当前租户生效，host 上下文为全局）
      </nz-page-header-subtitle>
      <nz-page-header-extra>
        <button nz-button (click)="load()" [nzLoading]="loading()">
          <span nz-icon nzType="reload"></span>刷新
        </button>
        <button
          nz-button
          nzType="primary"
          (click)="save()"
          [nzLoading]="saving()"
          [disabled]="!hasPassword() && !form.password"
        >
          <span nz-icon nzType="save"></span>保存
        </button>
      </nz-page-header-extra>
    </nz-page-header>

    <nz-alert
      nzType="info"
      nzShowIcon
      nzMessage="密码/授权码加密存储，已保存的值不会回显（眼睛图标仅显示本次输入内容）；留空保存表示保持原密码不变。"
      style="margin-bottom: 16px;"
    ></nz-alert>

    <nz-spin [nzSpinning]="loading()">
      <div class="smtp-grid">
        <nz-card nzTitle="服务器配置" [nzBordered]="false">
          <form nz-form nzLayout="vertical">
            <nz-form-item>
              <nz-form-label>预设</nz-form-label>
              <nz-form-control>
                <div class="preset-row">
                  @for (p of presets; track p.key) {
                    <button nz-button nzType="default" type="button" (click)="applyPreset(p)">
                      {{ p.label }}
                    </button>
                  }
                </div>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>SMTP 服务器</nz-form-label>
              <nz-form-control>
                <input nz-input [(ngModel)]="form.host" name="host" placeholder="如：smtp.163.com" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>端口</nz-form-label>
              <nz-form-control>
                <nz-input-number
                  [(ngModel)]="form.port"
                  name="port"
                  [nzMin]="1"
                  [nzMax]="65535"
                  style="width: 140px;"
                ></nz-input-number>
                <div class="hint">SSL（465）或 STARTTLS（587）</div>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>账号</nz-form-label>
              <nz-form-control>
                <input
                  nz-input
                  [(ngModel)]="form.userName"
                  name="userName"
                  placeholder="如：your_name@163.com"
                  autocomplete="username"
                />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>密码 / 授权码</nz-form-label>
              <nz-form-control>
                <div class="password-wrap">
                  <input
                    nz-input
                    [type]="showPassword() ? 'text' : 'password'"
                    [(ngModel)]="form.password"
                    name="password"
                    placeholder="{{
                      hasPassword() ? '已设置（留空则保持不变）' : '未设置，请填写授权码'
                    }}"
                    autocomplete="new-password"
                  />
                  <button
                    type="button"
                    class="toggle-visibility"
                    (click)="togglePassword()"
                    [attr.aria-label]="showPassword() ? '隐藏密码' : '显示密码'"
                  >
                    <span nz-icon [nzType]="showPassword() ? 'eye-invisible' : 'eye'" nzTheme="outline"></span>
                  </button>
                </div>
                @if (hasPassword()) {
                  <div class="hint-row">
                    <span class="hint">已保存密码（不可回显）</span>
                    <button nz-button nzType="link" nzSize="small" type="button" (click)="clearPassword()">
                      清除密码
                    </button>
                  </div>
                }
              </nz-form-control>
            </nz-form-item>
          </form>
        </nz-card>

        <nz-card nzTitle="发信与测试" [nzBordered]="false">
          <form nz-form nzLayout="vertical">
            <nz-form-item>
              <nz-form-label>发件人地址</nz-form-label>
              <nz-form-control>
                <input
                  nz-input
                  [(ngModel)]="form.fromAddress"
                  name="fromAddress"
                  placeholder="留空默认用账号地址"
                />
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label>发件人名称</nz-form-label>
              <nz-form-control>
                <input nz-input [(ngModel)]="form.fromName" name="fromName" placeholder="如：知识资源库" />
              </nz-form-control>
            </nz-form-item>
          </form>

          <nz-alert
            [nzType]="configured() ? 'success' : 'warning'"
            nzShowIcon
            [nzMessage]="configured() ? '当前配置可用' : '尚未配置完整（缺服务器或账号）'"
            style="margin-bottom: 16px;"
          ></nz-alert>

          <form nz-form nzLayout="vertical">
            <nz-form-item>
              <nz-form-label>测试收件邮箱</nz-form-label>
              <nz-form-control>
                <div class="test-row">
                  <input
                    nz-input
                    [(ngModel)]="testTo"
                    name="testTo"
                    placeholder="you@example.com"
                    style="flex: 1;"
                  />
                  <button
                    nz-button
                    nzType="primary"
                    (click)="sendTest()"
                    [nzLoading]="testing()"
                    [disabled]="!testTo.includes('@')"
                  >
                    发送测试邮件
                  </button>
                </div>
              </nz-form-control>
            </nz-form-item>
          </form>
        </nz-card>
      </div>
    </nz-spin>
  `,
  styles: [
    `
      .smtp-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 16px;
      }
      @media (max-width: 900px) {
        .smtp-grid {
          grid-template-columns: 1fr;
        }
      }
      .preset-row {
        display: flex;
        gap: 8px;
        flex-wrap: wrap;
      }
      .hint {
        color: #8c8c8c;
        font-size: 12px;
      }
      .hint-row {
        display: flex;
        align-items: center;
        gap: 4px;
        margin-top: 4px;
      }
      /* 密码/授权码：眼睛图标切换明文（与登录页密码框交互一致） */
      .password-wrap {
        position: relative;
      }
      .password-wrap input {
        padding-right: 2.6rem;
      }
      .toggle-visibility {
        position: absolute;
        right: 0.6rem;
        top: 50%;
        transform: translateY(-50%);
        background: none;
        border: none;
        padding: 4px;
        cursor: pointer;
        color: #5f6f81;
        display: flex;
        border-radius: 6px;
        line-height: 1;
      }
      .toggle-visibility:hover {
        color: #13233a;
      }
      .test-row {
        display: flex;
        gap: 8px;
        align-items: center;
      }
    `,
  ],
})
export class SmtpSettingsComponent implements OnInit {
  private readonly smtp = inject(SmtpConfigService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly testing = signal(false);
  readonly hasPassword = signal(false);
  readonly configured = signal(false);
  /** 显示/隐藏本次输入的密码（授权码），已保存的密码后端不回显 */
  readonly showPassword = signal(false);

  readonly presets = PRESETS;

  form = {
    host: '',
    port: 465,
    userName: '',
    password: '',
    fromAddress: '',
    fromName: '',
  };

  clearPwd = false;
  testTo = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.smtp.get().subscribe({
      next: (res: SmtpConfigDto) => {
        this.form.host = res.host ?? '';
        this.form.port = res.port ?? 465;
        this.form.userName = res.userName ?? '';
        this.form.password = '';
        this.form.fromAddress = res.fromAddress ?? '';
        this.form.fromName = res.fromName ?? '';
        this.hasPassword.set(res.hasPassword ?? false);
        this.configured.set(res.isConfigured ?? false);
        this.clearPwd = false;
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('读取 SMTP 配置失败');
      },
    });
  }

  applyPreset(p: { host: string; port: number }): void {
    this.form.host = p.host;
    this.form.port = p.port;
    this.message.info(`已填充 ${p.host}:${p.port}，请确认账号与授权码`);
  }

  /** 明文显示/隐藏本次输入的密码（授权码），与登录页密码框一致 */
  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  clearPassword(): void {
    this.clearPwd = true;
    this.form.password = '';
    this.hasPassword.set(false);
    this.message.info('已标记清除密码，保存后生效');
  }

  save(): void {
    if (!this.form.host.trim() || !this.form.userName.trim()) {
      this.message.warning('SMTP 服务器与账号不能为空');
      return;
    }
    this.saving.set(true);
    this.smtp
      .update({
        host: this.form.host.trim(),
        port: this.form.port,
        userName: this.form.userName.trim(),
        password: this.form.password?.trim() || undefined,
        clearPassword: this.clearPwd,
        fromAddress: this.form.fromAddress?.trim() || '',
        fromName: this.form.fromName?.trim() || '',
      })
      .subscribe({
        next: res => {
          this.saving.set(false);
          this.hasPassword.set(res.hasPassword ?? false);
          this.configured.set(res.isConfigured ?? false);
          this.form.password = '';
          this.clearPwd = false;
          this.message.success('SMTP 配置已保存');
        },
        error: (err: unknown) => {
          this.saving.set(false);
          const e = err as { error?: { error?: { message?: string } }; message?: string };
          this.message.error(e?.error?.error?.message || e?.message || '保存失败');
        },
      });
  }

  sendTest(): void {
    if (!this.testTo.includes('@')) {
      this.message.warning('请输入有效的测试收件邮箱');
      return;
    }
    this.testing.set(true);
    this.smtp.sendTestEmail({ to: this.testTo.trim() }).subscribe({
      next: res => {
        this.testing.set(false);
        if (res.success) {
          this.message.success(`测试邮件已发送至 ${this.testTo}`);
        } else {
          this.message.error(res.error || '发送失败');
        }
      },
      error: () => {
        this.testing.set(false);
        this.message.error('发送失败');
      },
    });
  }
}
