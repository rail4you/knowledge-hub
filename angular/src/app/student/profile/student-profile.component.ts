import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzInputModule } from 'ng-zorro-antd/input';
import { ConfigStateService } from '@abp/ng.core';
import { UserService } from '../../proxy/users/user.service';
import type { MyProfileDto } from '../../proxy/users/models';
import { EditContactInfoComponent } from './edit-contact-info.component';

/**
 * 学生端「个人中心」
 *
 * 设计：参考原型 #/s-profile 的 .detail-layout（1fr 信息卡 + 侧栏）。
 * - 用户信息只读展示（用户名 / 姓名 / 所属机构 / 邮箱 / 手机号）
 * - 邮箱与手机号可编辑（复用既有 EditContactInfoComponent，走 nz-modal）
 * - 修改密码用内联弹窗 + 强度提示，调 ABP Account ProfileController
 */
@Component({
  selector: 'app-student-profile',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NzIconModule,
    NzSpinModule,
    NzModalModule,
    NzInputModule,
    EditContactInfoComponent,
  ],
  templateUrl: './student-profile.component.html',
  styleUrls: ['./student-profile.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentProfileComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly configState = inject(ConfigStateService);
  readonly route = inject(ActivatedRoute);

  readonly profile = signal<MyProfileDto | null>(null);
  readonly loading = signal(false);
  /** 当前机构名称（从 ABP ConfigState.currentTenant 读取，只读展示） */
  readonly tenantName = signal<string>('');

  // =================== 修改密码 ===================
  readonly pwdModalOpen = signal(false);
  readonly pwdCurrent = signal('');
  readonly pwdNew = signal('');
  readonly pwdConfirm = signal('');
  readonly pwdSaving = signal(false);
  readonly showCurrent = signal(false);
  readonly showNew = signal(false);
  readonly showConfirm = signal(false);

  ngOnInit(): void {
    this.load();
    this.loadExtras();
  }

  private load(): void {
    this.loading.set(true);
    this.userService.getMyProfile().subscribe({
      next: (p) => { this.profile.set(p); this.loading.set(false); },
      error: () => { this.loading.set(false); this.message.error('加载个人信息失败'); },
    });
  }

  /** 从 ABP ConfigState 读取当前机构名（只读展示，不另起接口） */
  private loadExtras(): void {
    const tenant = this.configState.getDeep('currentTenant') as Record<string, unknown> | undefined;
    const tenantDisplay = (tenant?.['name'] as string) || '';
    if (tenantDisplay) this.tenantName.set(tenantDisplay);
  }

  /** 头像文字：优先 name → userName 首字符 */
  initial(): string {
    const p = this.profile();
    const n = (p?.name || p?.userName || '同').trim();
    return n ? n.charAt(0).toUpperCase() : '同';
  }

  displayName(): string {
    return this.profile()?.name || this.profile()?.userName || '未命名用户';
  }

  /** 打开联系方式编辑弹窗（复用既有组件，走 NzModalService.create） */
  editContact(): void {
    const p = this.profile();
    if (!p) return;
    this.modal
      .create({
        nzTitle: '修改联系方式',
        nzContent: EditContactInfoComponent,
        nzData: { profile: p },
        nzWidth: 520,
        nzMaskClosable: false,
        nzFooter: null,
      })
      .afterClose.subscribe((ok) => { if (ok) this.load(); });
  }

  // =================== 修改密码 ===================
  openPwd(): void {
    this.pwdCurrent.set('');
    this.pwdNew.set('');
    this.pwdConfirm.set('');
    this.pwdModalOpen.set(true);
  }

  closePwd(): void { this.pwdModalOpen.set(false); }

  private pwdStrength(): 'weak' | 'medium' | 'strong' {
    const v = this.pwdNew();
    if (v.length < 6) return 'weak';
    let s = 0;
    if (/[a-z]/.test(v)) s++;
    if (/[A-Z]/.test(v)) s++;
    if (/\d/.test(v)) s++;
    if (/[^A-Za-z0-9]/.test(v)) s++;
    return s >= 3 ? 'strong' : s === 2 ? 'medium' : 'weak';
  }

  pwdStrengthText(): string {
    return { weak: '弱', medium: '中', strong: '强' }[this.pwdStrength()];
  }

  pwdValid(): boolean {
    const cur = this.pwdCurrent();
    const np = this.pwdNew();
    const cf = this.pwdConfirm();
    if (!cur || !np || !cf) return false;
    if (np.length < 6) return false;
    return np === cf;
  }

  submitPwd(): void {
    if (!this.pwdValid()) {
      if (this.pwdNew() !== this.pwdConfirm()) { this.message.error('两次输入的新密码不一致'); return; }
      if ((this.pwdNew() || '').length < 6) { this.message.error('新密码至少 6 位'); return; }
      this.message.error('请完整填写密码信息');
      return;
    }
    this.pwdSaving.set(true);
    this.userService.changePassword({
      currentPassword: this.pwdCurrent(),
      newPassword: this.pwdNew(),
    }).subscribe({
      next: () => {
        this.pwdSaving.set(false);
        this.message.success('密码修改成功');
        this.closePwd();
      },
      error: (err) => {
        this.pwdSaving.set(false);
        const msg = err?.error?.error?.message || err?.error?.message || '密码修改失败，请检查当前密码是否正确';
        this.message.error(msg);
      },
    });
  }
}
