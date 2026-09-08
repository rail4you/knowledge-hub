import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SessionStateService } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzPageHeaderModule } from 'ng-zorro-antd/page-header';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzMessageService } from 'ng-zorro-antd/message';
import { BrandingService } from '../../shared/branding/branding.service';
import { OssUploadService } from '../../shared/oss-upload.service';
import { SiteBrandComponent } from '../../shared/branding/site-brand.component';
import { SiteFooterComponent } from '../../shared/branding/site-footer.component';

/**
 * 品牌设置（仅 host 全局管理员）：
 * 应用标题 / 副标题 / 三端统一页脚文本 / Logo（OSS 图片上传，为空用文字 Logo）。
 */
@Component({
  selector: 'app-branding-management',
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
    NzDividerModule,
    SiteBrandComponent,
    SiteFooterComponent,
  ],
  template: `
    <nz-page-header>
      <nz-page-header-title>品牌设置</nz-page-header-title>
      <nz-page-header-subtitle>应用标题 · 副标题 · 页脚 · Logo（全局生效）</nz-page-header-subtitle>
      <nz-page-header-extra>
        <button nz-button (click)="resetDefaults()" [disabled]="saving()">
          <span nz-icon nzType="redo"></span>恢复默认
        </button>
        <button nz-button nzType="primary" (click)="save()" [nzLoading]="saving()" [disabled]="!isHost">
          <span nz-icon nzType="save"></span>保存
        </button>
      </nz-page-header-extra>
    </nz-page-header>

    <nz-alert
      *ngIf="!isHost"
      nzType="warning"
      nzShowIcon
      nzMessage="仅全局管理员（host）可维护品牌设置，当前为租户上下文。"
      style="margin-bottom: 16px;"
    ></nz-alert>

    <nz-spin [nzSpinning]="loading()">
      <div class="branding-grid">
        <nz-card nzTitle="品牌内容" [nzBordered]="false">
          <form nz-form nzLayout="vertical">
            <nz-form-item>
              <nz-form-label nzRequired>应用标题</nz-form-label>
              <nz-form-control>
                <input nz-input [(ngModel)]="form.appTitle" name="appTitle" maxlength="32" placeholder="如：易课通" />
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label>应用副标题</nz-form-label>
              <nz-form-control>
                <input nz-input [(ngModel)]="form.appSubtitle" name="appSubtitle" maxlength="64" placeholder="如：知识资源库" />
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label>页脚文本（三端统一）</nz-form-label>
              <nz-form-control>
                <input nz-input [(ngModel)]="form.footerText" name="footerText" maxlength="256" placeholder="如：© 2026 易课通 · 知识资源库" />
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label>Logo 图片</nz-form-label>
              <nz-form-control>
                <div class="logo-row">
                  @if (form.logoUrl) {
                    <img class="logo-preview" [src]="form.logoUrl" alt="Logo 预览" />
                  }
                  <div class="logo-actions">
                    <label class="upload-btn">
                      <button nz-button (click)="fileInput.click()" [nzLoading]="uploading()" type="button">
                        <span nz-icon nzType="upload"></span>{{ form.logoUrl ? '重新上传' : '上传 Logo' }}
                      </button>
                      <input #fileInput type="file" accept="image/*" hidden (change)="onFileSelected($event)" />
                    </label>
                    @if (form.logoUrl) {
                      <button nz-button nzDanger nzGhost (click)="clearLogo()" type="button">清除（用文字 Logo）</button>
                    }
                  </div>
                </div>
                <div class="hint">建议正方形 PNG，≤ 5MB；清空后显示标题首字文字 Logo。</div>
                <input nz-input [(ngModel)]="form.logoUrl" name="logoUrl" maxlength="512" placeholder="或直接粘贴图片 URL" style="margin-top: 8px;" />
              </nz-form-control>
            </nz-form-item>
          </form>
        </nz-card>

        <nz-card nzTitle="实时预览" [nzBordered]="false">
          <div class="preview-block">
            <div class="preview-label">品牌 lockup（首页 / 学生端 / 管理端侧栏共用）</div>
            <div class="preview-brand">
              <app-site-brand [sub]="form.appSubtitle"></app-site-brand>
            </div>
            <nz-divider></nz-divider>
            <div class="preview-label">页脚（三端统一）</div>
            <div class="preview-footer">{{ form.footerText || '© 2026 易课通 · 知识资源库' }}</div>
          </div>
        </nz-card>
      </div>
    </nz-spin>
  `,
  styles: [
    `
      .branding-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 16px;
      }
      @media (max-width: 900px) {
        .branding-grid {
          grid-template-columns: 1fr;
        }
      }
      .logo-row {
        display: flex;
        align-items: center;
        gap: 12px;
      }
      .logo-preview {
        width: 56px;
        height: 56px;
        border-radius: 12px;
        object-fit: contain;
        border: 1px solid #f0f0f0;
      }
      .logo-actions {
        display: flex;
        gap: 8px;
        align-items: center;
      }
      .hint {
        color: #8c8c8c;
        font-size: 12px;
        margin-top: 6px;
      }
      .preview-label {
        color: #8c8c8c;
        font-size: 12px;
        margin-bottom: 12px;
      }
      .preview-brand {
        padding: 16px;
        background: #fafafa;
        border-radius: 8px;
      }
      .preview-footer {
        text-align: center;
        padding: 16px;
        color: #8c8c8c;
        font-size: 13px;
        background: #fafafa;
        border-radius: 8px;
      }
    `,
  ],
})
export class BrandingManagementComponent implements OnInit {
  private readonly branding = inject(BrandingService);
  private readonly ossUpload = inject(OssUploadService);
  private readonly session = inject(SessionStateService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly uploading = signal(false);

  isHost = true;

  form = {
    appTitle: '易课通',
    appSubtitle: '知识资源库',
    footerText: '© 2026 易课通 · 知识资源库',
    logoUrl: '',
  };

  ngOnInit(): void {
    const tenant = this.session.getTenant();
    this.isHost = !tenant?.id;
    this.branding.load().subscribe({
      next: res => {
        if (res) {
          this.form = {
            appTitle: res.appTitle,
            appSubtitle: res.appSubtitle,
            footerText: res.footerText,
            logoUrl: res.logoUrl || '',
          };
        }
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      this.message.error('只能上传图片文件');
      return;
    }
    if (file.size / 1024 / 1024 > 5) {
      this.message.error('图片大小不能超过 5MB');
      return;
    }
    this.uploading.set(true);
    this.ossUpload.uploadImage(file).subscribe({
      next: res => {
        this.form.logoUrl = res.url;
        this.uploading.set(false);
        this.message.success('Logo 上传成功，保存后生效');
      },
      error: () => {
        this.uploading.set(false);
        this.message.error('上传失败，请重试');
      },
    });
  }

  clearLogo(): void {
    this.form.logoUrl = '';
  }

  resetDefaults(): void {
    this.form = {
      appTitle: '易课通',
      appSubtitle: '知识资源库',
      footerText: '© 2026 易课通 · 知识资源库',
      logoUrl: '',
    };
    this.message.info('已恢复默认值，点保存后生效');
  }

  save(): void {
    if (!this.form.appTitle?.trim()) {
      this.message.warning('应用标题不能为空');
      return;
    }
    this.saving.set(true);
    this.branding
      .update({
        appTitle: this.form.appTitle.trim(),
        appSubtitle: this.form.appSubtitle?.trim() || '',
        footerText: this.form.footerText?.trim() || '',
        logoUrl: this.form.logoUrl?.trim() || '',
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.message.success('品牌设置已保存，三端即时生效');
        },
        error: (err: unknown) => {
          this.saving.set(false);
          const e = err as { error?: { error?: { message?: string } }; message?: string };
          this.message.error(e?.error?.error?.message || e?.message || '保存失败');
        },
      });
  }
}
