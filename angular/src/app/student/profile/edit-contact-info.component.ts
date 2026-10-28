import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzModalModule, NZ_MODAL_DATA, NzModalRef } from 'ng-zorro-antd/modal';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { UserService } from '../../proxy/users/user.service';
import type { MyProfileDto, UpdateMyProfileDto } from '../../proxy/users/models';

export interface EditContactInfoData {
  profile: MyProfileDto;
}

/**
 * 学生端「修改个人信息」弹窗：姓名 + 手机号 + 电子邮箱。
 *
 * <para>
 * 走 `/api/app/user/my-profile`（UpdateMyProfileAsync）：
 * · 姓名必填；
 * · 手机号留空 = 解绑，变更后需重新做手机认证；
 * · 邮箱留空 = 不修改（ABP 不支持置空邮箱，且邮箱可能作为登录标识）。
 * </para>
 */
@Component({
  selector: 'app-edit-contact-info',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzModalModule,
    NzInputModule,
    NzButtonModule,
    NzSpinModule,
  ],
  template: `
    <ng-container>
      <div class="edit-contact">
        <div class="field">
          <label class="field__label">姓名<span class="field__required">*</span></label>
          <input nz-input [(ngModel)]="name" placeholder="请输入姓名" maxlength="64" />
          <small class="field__hint">用于课程、班级、证书等场景下的显示名。</small>
        </div>
        <div class="field">
          <label class="field__label">手机号</label>
          <input
            nz-input
            type="tel"
            [(ngModel)]="phoneNumber"
            placeholder="请输入手机号"
            maxlength="20"
            autocomplete="tel"
          />
          <small class="field__hint">留空表示解绑手机号；更换后需重新做手机认证。</small>
        </div>
        <div class="field">
          <label class="field__label">电子邮箱</label>
          <input
            nz-input
            type="email"
            [(ngModel)]="email"
            placeholder="请输入电子邮箱"
            maxlength="256"
            autocomplete="email"
          />
          <small class="field__hint">留空表示不修改（邮箱不支持解绑）。</small>
        </div>
      </div>
      <ng-template [nzModalFooter]>
        <button nz-button (click)="cancel()">取消</button>
        <button nz-button nzType="primary" (click)="save()" [nzLoading]="saving()">保存</button>
      </ng-template>
    </ng-container>
  `,
  styles: [
    `
      .edit-contact {
        display: flex;
        flex-direction: column;
        gap: 16px;
        padding: 4px 0;
      }
      .field {
        display: flex;
        flex-direction: column;
        gap: 6px;
      }
      .field__label {
        font-size: 13px;
        font-weight: 600;
        color: var(--kh-ink);
      }
      .field__required {
        color: #cf1322;
        margin-left: 2px;
      }
      .field__hint {
        font-size: 12px;
        color: #98a49d;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditContactInfoComponent {
  private readonly modalRef = inject(NzModalRef);
  private readonly modalData: EditContactInfoData = inject(NZ_MODAL_DATA);
  private readonly userService = inject(UserService);
  private readonly message = inject(NzMessageService);

  readonly saving = signal(false);

  name: string;
  phoneNumber: string;
  email: string;

  constructor() {
    const p = this.modalData?.profile;
    this.name = p?.name || '';
    this.phoneNumber = p?.phoneNumber || '';
    this.email = p?.email || '';
  }

  save(): void {
    const name = (this.name || '').trim();
    const email = (this.email || '').trim();

    if (!name) {
      this.message.error('请填写姓名');
      return;
    }
    if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.message.error('电子邮箱格式不正确');
      return;
    }

    const payload: UpdateMyProfileDto = {
      name,
      // 手机号留空 = 解绑
      phoneNumber: (this.phoneNumber || '').trim(),
    };
    // 邮箱留空 = 不修改
    if (email) {
      payload.email = email;
    }

    this.saving.set(true);
    this.userService.updateMyProfile(payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.message.success('个人信息已更新');
        this.modalRef.close(true);
      },
      error: (err: any) => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '更新失败，请稍后重试');
      },
    });
  }

  cancel(): void {
    this.modalRef.close(false);
  }
}
