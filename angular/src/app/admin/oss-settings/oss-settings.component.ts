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
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { OssSettingsService, OssConfigDto } from './oss-settings.service';

/** 常用区域 Endpoint 预设：一键填充（AccessKey 仍需自行填写）。 */
const REGION_PRESETS: Array<{ key: string; label: string; endpoint: string }> = [
  { key: 'beijing', label: '华北2（北京）', endpoint: 'oss-cn-beijing.aliyuncs.com' },
  { key: 'shanghai', label: '华东1（上海）', endpoint: 'oss-cn-shanghai.aliyuncs.com' },
  { key: 'shenzhen', label: '华南1（深圳）', endpoint: 'oss-cn-shenzhen.aliyuncs.com' },
  { key: 'hangzhou', label: '华东1（杭州）', endpoint: 'oss-cn-hangzhou.aliyuncs.com' },
  { key: 'chengdu', label: '西南1（成都）', endpoint: 'oss-cn-chengdu.aliyuncs.com' },
];

@Component({
  selector: 'app-oss-settings',
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
    NzTagModule,
  ],
  template: `
    <nz-page-header>
      <nz-page-header-title>对象存储设置（阿里云 OSS）</nz-page-header-title>
      <nz-page-header-subtitle>
        图片 / 简历 / 通用文件上传的存储位置（当前租户生效，host 上下文为全局回退）
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
          [disabled]="!hasSecret() && !form.accessKeySecret"
        >
          <span nz-icon nzType="save"></span>保存
        </button>
      </nz-page-header-extra>
    </nz-page-header>

    <nz-alert
      nzType="info"
      nzShowIcon
      nzMessage="AccessKey 加密存储在数据库中，已保存的值不会回显；留空保存表示保持原密钥不变。配置保存在数据库，保存即生效，无需重启 API。"
      style="margin-bottom: 16px;"
    ></nz-alert>

    <nz-spin [nzSpinning]="loading()">
      <div class="oss-grid">
        <nz-card nzTitle="Bucket 与区域" [nzBordered]="false">
          <form nz-form nzLayout="vertical">
            <nz-form-item>
              <nz-form-label>常用区域</nz-form-label>
              <nz-form-control>
                <div class="preset-row">
                  @for (p of presets; track p.key) {
                    <button nz-button nzType="default" type="button" (click)="applyPreset(p)">
                      {{ p.label }}
                    </button>
                  }
                </div>
                <div class="hint">Endpoint 与 AccessKey 必须属于同一账号同一区域，否则会报签名不匹配。</div>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label required>Endpoint</nz-form-label>
              <nz-form-control>
                <input nz-input placeholder="oss-cn-beijing.aliyuncs.com" [(ngModel)]="form.endpoint" name="endpoint" />
                <div class="hint">不带 bucket 前缀、不带 https://。例：oss-cn-beijing.aliyuncs.com</div>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label required>Bucket 名称</nz-form-label>
              <nz-form-control>
                <input nz-input placeholder="kg-edu" [(ngModel)]="form.bucketName" name="bucketName" />
                <div class="hint">桶名全站唯一且不可修改，需与 Endpoint 同区域。</div>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>上传路径前缀</nz-form-label>
              <nz-form-control>
                <input nz-input placeholder="knowledgehub" [(ngModel)]="form.uploadPath" name="uploadPath" />
                <div class="hint">所有对象键的统一前缀，用于区分业务；修改会导致新旧文件分散在两个目录。</div>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>自定义访问域名 / CDN</nz-form-label>
              <nz-form-control>
                <input nz-input placeholder="留空则使用 https://{bucket}.oss-{region}.aliyuncs.com" [(ngModel)]="form.urlPrefix" name="urlPrefix" />
                <div class="hint">填 https://cdn.example.com 后，返回给前端的 URL 会改用该域名。</div>
              </nz-form-control>
            </nz-form-item>
          </form>
        </nz-card>

        <nz-card nzTitle="AccessKey 凭证" [nzBordered]="false">
          <form nz-form nzLayout="vertical">
            <nz-form-item>
              <nz-form-label>AccessKeyId</nz-form-label>
              <nz-form-control>
                <input nz-input placeholder="LTAI5t..." [(ngModel)]="form.accessKeyId" name="accessKeyId" />
                <div class="hint">阿里云 RAM 用户 AccessKey ID，建议为应用单独建一个 RAM 子账号。</div>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>AccessKeySecret</nz-form-label>
              <nz-form-control>
                <div class="secret-wrap">
                  <input
                    nz-input
                    [type]="showSecret() ? 'text' : 'password'"
                    placeholder="已保存则留空即可"
                    [(ngModel)]="form.accessKeySecret"
                    name="accessKeySecret"
                  />
                  <button type="button" class="toggle-visibility" (click)="toggleSecret()">
                    <span nz-icon [nzType]="showSecret() ? 'eye-invisible' : 'eye'"></span>
                  </button>
                </div>
                <div class="hint-row">
                  <span class="hint">已配置密钥：</span>
                  <nz-tag [nzColor]="hasSecret() ? 'green' : 'default'">
                    {{ hasSecret() ? '已配置' : '未配置' }}
                  </nz-tag>
                  @if (hasSecret()) {
                    <a (click)="clearSecret()">清除密钥</a>
                  }
                </div>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>当前状态</nz-form-label>
              <nz-form-control>
                <div class="status-row">
                  <nz-tag [nzColor]="configured() ? 'green' : 'red'">
                    {{ configured() ? '可用' : '不可用' }}
                  </nz-tag>
                  <nz-tag [nzColor]="sourceColor()">{{ sourceText() }}</nz-tag>
                </div>
                @if (publicHost()) {
                  <div class="hint">公开访问域名：{{ publicHost() }}</div>
                }
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>连通性测试</nz-form-label>
              <nz-form-control>
                <button nz-button nzType="default" (click)="test()" [nzLoading]="testing()">
                  <span nz-icon nzType="thunderbolt"></span>写入并删除测试对象
                </button>
                <div class="hint">会上传 1 个 1 字节探测对象后立即删除，验证"能写能删"（几乎不产生存储费用）。</div>
              </nz-form-control>
            </nz-form-item>
          </form>
        </nz-card>
      </div>
    </nz-spin>
  `,
  styles: [
    `
      .oss-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(380px, 1fr));
        gap: 16px;
      }
      .preset-row {
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
      }
      .hint {
        color: #8a99a1;
        font-size: 12px;
      }
      .hint-row {
        display: flex;
        align-items: center;
        gap: 4px;
        margin-top: 4px;
      }
      .status-row {
        display: flex;
        gap: 8px;
        align-items: center;
      }
      .secret-wrap {
        position: relative;
      }
      .secret-wrap input {
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
        color: #12434b;
      }
    `,
  ],
})
export class OssSettingsComponent implements OnInit {
  private readonly oss = inject(OssSettingsService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly testing = signal(false);
  readonly hasSecret = signal(false);
  readonly configured = signal(false);
  readonly source = signal('none');
  readonly publicHost = signal('');
  /** 显示/隐藏本次输入的密钥，已保存的密钥后端不回显 */
  readonly showSecret = signal(false);

  readonly presets = REGION_PRESETS;

  form = {
    endpoint: '',
    accessKeyId: '',
    accessKeySecret: '',
    bucketName: '',
    uploadPath: 'knowledgehub',
    urlPrefix: '',
  };

  clearPwd = false;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.oss.get().subscribe({
      next: (res: OssConfigDto) => {
        this.form.endpoint = res.endpoint ?? '';
        this.form.accessKeyId = res.accessKeyId ?? '';
        this.form.accessKeySecret = '';
        this.form.bucketName = res.bucketName ?? '';
        this.form.uploadPath = res.uploadPath || 'knowledgehub';
        this.form.urlPrefix = res.urlPrefix ?? '';
        this.hasSecret.set(res.hasAccessKeySecret ?? false);
        this.configured.set(res.isConfigured ?? false);
        this.source.set(res.source ?? 'none');
        this.publicHost.set(res.publicHost ?? '');
        this.clearPwd = false;
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('读取对象存储配置失败');
      },
    });
  }

  applyPreset(p: { endpoint: string }): void {
    this.form.endpoint = p.endpoint;
    this.message.info(`已填充 Endpoint：${p.endpoint}`);
  }

  toggleSecret(): void {
    this.showSecret.update(v => !v);
  }

  clearSecret(): void {
    this.clearPwd = true;
    this.form.accessKeySecret = '';
    this.hasSecret.set(false);
    this.message.info('已标记清除密钥，保存后生效');
  }

  save(): void {
    if (!this.form.endpoint.trim() || !this.form.bucketName.trim()) {
      this.message.warning('Endpoint 与 Bucket 名称不能为空');
      return;
    }
    this.saving.set(true);
    this.oss
      .update({
        endpoint: this.form.endpoint.trim(),
        accessKeyId: this.form.accessKeyId.trim(),
        accessKeySecret: this.form.accessKeySecret?.trim() || undefined,
        clearAccessKeySecret: this.clearPwd,
        bucketName: this.form.bucketName.trim(),
        uploadPath: this.form.uploadPath.trim(),
        urlPrefix: this.form.urlPrefix.trim(),
      })
      .subscribe({
        next: res => {
          this.saving.set(false);
          this.hasSecret.set(res.hasAccessKeySecret ?? false);
          this.configured.set(res.isConfigured ?? false);
          this.source.set(res.source ?? 'none');
          this.publicHost.set(res.publicHost ?? '');
          this.form.accessKeySecret = '';
          this.clearPwd = false;
          this.message.success('对象存储配置已保存并生效');
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.message.error(extractError(err, '保存失败'));
        },
      });
  }

  test(): void {
    this.testing.set(true);
    this.oss.test().subscribe({
      next: res => {
        this.testing.set(false);
        if (res.success) {
          this.message.success('连通性正常：已成功写入并删除探测对象');
        } else {
          this.message.error(res.error || '连通性测试失败');
        }
      },
      error: (err: unknown) => {
        this.testing.set(false);
        this.message.error(extractError(err, '连通性测试失败'));
      },
    });
  }

  sourceText(): string {
    switch (this.source()) {
      case 'database':
        return '来源：数据库设置';
      case 'config':
        return '来源：配置文件回退';
      default:
        return '未配置';
    }
  }

  sourceColor(): string {
    switch (this.source()) {
      case 'database':
        return 'blue';
      case 'config':
        return 'gold';
      default:
        return 'default';
    }
  }
}

function extractError(err: unknown, fallback: string): string {
  const e = err as { error?: { error?: { message?: string } }; message?: string };
  return e?.error?.error?.message || e?.message || fallback;
}
