import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzModalModule, NZ_MODAL_DATA, NzModalRef } from 'ng-zorro-antd/modal';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { UserService } from '../../proxy/users/user.service';
import type { MyProfileDto } from '../../proxy/users/models';

export interface EditContactInfoData {
  profile: MyProfileDto;
}

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
    <div class="edit-contact">
      <div class="field">
        <label class="field__label">手机号</label>
        <input
          nz-input
          type="tel"
          [(ngModel)]="phoneNumber"
          placeholder="请输入手机号"
          maxlength="20"
        />
      </div>
      <div class="field">
        <label class="field__label">电子邮箱</label>
        <input
          nz-input
          type="email"
          [(ngModel)]="email"
          placeholder="请输入电子邮箱"
          maxlength="256"
        />
      </div>
    </div>
    <div class="edit-actions">
      <button nz-button (click)="cancel()">取消</button>
      <button nz-button nzType="primary" (click)="save()" [nzLoading]="saving()">保存</button>
    </div>
  `,
  styles: [`
    .edit-contact { display: flex; flex-direction: column; gap: 18px; padding: 4px 0; }
    .field { display: flex; flex-direction: column; gap: 6px; }
    .field__label { font-size: 13px; font-weight: 600; color: #1e293b; }
    .edit-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 4px; }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EditContactInfoComponent {
  private readonly modalRef = inject(NzModalRef);
  private readonly modalData: EditContactInfoData = inject(NZ_MODAL_DATA);
  private readonly userService = inject(UserService);
  private readonly message = inject(NzMessageService);

  readonly saving = signal(false);

  phoneNumber: string;
  email: string;

  constructor() {
    this.phoneNumber = this.modalData.profile.phoneNumber || '';
    this.email = this.modalData.profile.email || '';
  }

  save(): void {
    this.saving.set(true);
    this.userService.updateMyProfile({
      phoneNumber: this.phoneNumber?.trim() || undefined,
      email: this.email?.trim() || undefined,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.message.success('联系方式已更新');
        this.modalRef.close(true);
      },
      error: () => {
        this.saving.set(false);
        this.message.error('更新失败，请稍后重试');
      },
    });
  }

  cancel(): void {
    this.modalRef.close(false);
  }
}
