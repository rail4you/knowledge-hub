import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NZ_MODAL_DATA, NzModalRef } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { UserService } from '../../proxy/users/user.service';
import type { MyProfileDto } from '../../proxy/users/models';

export interface VerifyRealNameModalData {
  /** 预填的真实姓名（可空） */
  realName?: string | null;
}

/**
 * 实名认证弹窗（学生端通用）。
 *
 * <para>
 * 由 <see cref="StudentVerifyService"/> 通过 NzModalService 打开，
 * 提交成功后把新的 MyProfileDto 作为 afterClose 结果返回（取消返回 null）。
 * </para>
 *
 * <para>
 * 认证口径：平台未接权威核验/短信，只要姓名非空 + 身份证号格式合法（15/18 位）即通过。
 * </para>
 */
@Component({
  selector: 'app-verify-real-name-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzIconModule, NzInputModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="vrm">
      <p class="vrm__intro">
        对外开放的共享 / 共建资源库课程需要先完成<b>实名认证</b>才能学习，结业证书也以此为准。
      </p>

      <label class="vrm__field">
        <span>真实姓名<span class="vrm__required">*</span></span>
        <input nz-input [(ngModel)]="realName" placeholder="请输入真实姓名" />
      </label>

      <label class="vrm__field">
        <span>身份证号<span class="vrm__required">*</span></span>
        <input nz-input [(ngModel)]="idCard" placeholder="15 位或 18 位身份证号" />
      </label>

      @if (error()) {
        <p class="vrm__error"><span nz-icon nzType="exclamation-circle"></span> {{ error() }}</p>
      }

      <p class="vrm__hint">
        身份证号提交后仅做脱敏展示（如 1101************11），用于校内实名学员登记与证书核验。
      </p>

      <div class="vrm__actions">
        <button nz-button (click)="cancel()" [disabled]="saving()">取消</button>
        <button nz-button nzType="primary" [nzLoading]="saving()" (click)="submit()">提交认证</button>
      </div>
    </div>
  `,
  styles: [
    `
      .vrm__intro {
        margin: 0 0 14px;
        font-size: 13px;
        line-height: 1.7;
        color: #63726a;
      }
      .vrm__field {
        display: block;
        margin-bottom: 14px;
      }
      .vrm__field > span {
        display: block;
        margin-bottom: 6px;
        font-size: 13px;
        font-weight: 600;
        color: #2f3a35;
      }
      .vrm__required {
        color: #cf1322;
        margin-left: 2px;
      }
      .vrm__error {
        display: flex;
        align-items: center;
        gap: 4px;
        margin: 0 0 10px;
        font-size: 12px;
        color: #cf1322;
      }
      .vrm__hint {
        margin: 0 0 16px;
        font-size: 12px;
        line-height: 1.7;
        color: #98a49d;
      }
      .vrm__actions {
        display: flex;
        justify-content: flex-end;
        gap: 10px;
      }
    `,
  ],
})
export class VerifyRealNameModalComponent {
  private readonly userService = inject(UserService);
  private readonly message = inject(NzMessageService);
  private readonly modalRef = inject(NzModalRef);
  private readonly data = inject(NZ_MODAL_DATA, { optional: true }) as VerifyRealNameModalData | null;

  realName = this.data?.realName ?? '';
  idCard = '';

  readonly saving = signal(false);
  readonly error = signal('');

  cancel(): void {
    this.modalRef.close(null);
  }

  submit(): void {
    const realName = (this.realName || '').trim();
    const idCard = (this.idCard || '').trim().toUpperCase();

    if (!realName) {
      this.error.set('请填写真实姓名');
      return;
    }
    if (idCard.length !== 15 && idCard.length !== 18) {
      this.error.set('身份证号应为 15 位或 18 位');
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.userService.verifyRealName({ realName, idCardNumber: idCard }).subscribe({
      next: (profile: MyProfileDto) => {
        this.saving.set(false);
        this.message.success('实名认证成功，现在可以学习对外开放的课程了');
        this.modalRef.close(profile ?? true);
      },
      error: (err: any) => {
        this.saving.set(false);
        this.error.set(err?.error?.error?.message || '认证失败，请稍后重试');
      },
    });
  }
}
