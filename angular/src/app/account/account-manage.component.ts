import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfigStateService } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { UserService } from '../proxy/users/user.service';
import type { MyProfileDto, UpdateMyProfileDto } from '../proxy/users/models';
import { SchoolService } from '../proxy/schools/school.service';

/**
 * 我的账户（替换 ABP 自带的「个人设置」页）。
 *
 * <para>
 * ABP 默认页只有「个人设置 / 修改密码」两个标签，样式是主题自带的通用表单，
 * 与本项目管理端（卡片 + 绿色主色）不一致；这里按管理端风格重做一版，
 * 并把「个人信息 / 联系方式 / 实名认证 / 账号安全」集中到一个页面里。
 * </para>
 *
 * <para>
 * 接口全部走项目自有接口（生成代理，不手写 URL）：
 * · 资料 GET/PUT /api/app/user/my-profile
 * · 实名 POST    /api/app/user/verify-real-name
 * · 改密 POST    /api/account/my-profile/change-password（ABP Account 模块）
 * </para>
 */
@Component({
  selector: 'app-account-manage',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzTagModule,
  ],
  templateUrl: './account-manage.component.html',
  styleUrls: ['./account-manage.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountManageComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly schoolService = inject(SchoolService);
  private readonly message = inject(NzMessageService);
  private readonly configState = inject(ConfigStateService);

  /** 当前账号资料 */
  readonly profile = signal<MyProfileDto | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);

  /** 所属机构中文名（会话里的租户名是 slug，不能直接展示） */
  readonly schoolName = signal('');
  /** 当前用户角色 */
  readonly roles = computed(() => {
    const user = this.configState.getOne('currentUser') as { roles?: string[] } | undefined;
    return user?.roles ?? [];
  });

  // ── 个人信息 / 联系方式 表单（受控输入，事件本身会触发变更检测） ──
  formName = '';
  formEmail = '';
  formPhone = '';

  // ── 修改密码 ──
  pwdCurrent = '';
  pwdNext = '';
  pwdConfirm = '';
  readonly pwdSaving = signal(false);

  // ── 实名认证弹窗 ──
  readonly realNameOpen = signal(false);
  readonly realNameSaving = signal(false);
  readonly realNameError = signal('');
  realName = '';
  idCard = '';

  ngOnInit(): void {
    this.load();
    this.loadSchoolName();
  }

  private load(): void {
    this.loading.set(true);
    this.userService.getMyProfile().subscribe({
      next: p => {
        this.profile.set(p);
        this.formName = p?.name ?? '';
        this.formEmail = p?.email ?? '';
        this.formPhone = p?.phoneNumber ?? '';
        this.loading.set(false);
      },
      error: () => {
        this.profile.set(null);
        this.loading.set(false);
        this.message.error('加载个人资料失败');
      },
    });
  }

  private loadSchoolName(): void {
    const tenant = this.configState.getDeep('currentTenant') as Record<string, unknown> | undefined;
    const tenantId = (tenant?.['id'] as string) || '';
    if (!tenantId) return;
    this.schoolService.getProfile(tenantId).subscribe({
      next: profile => {
        const name = profile?.school?.name;
        if (name) this.schoolName.set(name);
      },
      error: () => {
        /* 取不到就留空，模板显示占位文案 */
      },
    });
  }

  // =================== 保存个人资料 ===================

  saveProfile(): void {
    const name = this.formName.trim();
    const email = this.formEmail.trim();
    const phone = this.formPhone.trim();

    if (!name) {
      this.message.error('请填写姓名');
      return;
    }
    if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.message.error('电子邮箱格式不正确');
      return;
    }
    if (phone && !/^[0-9+\-\s]{6,20}$/.test(phone)) {
      this.message.error('手机号格式不正确');
      return;
    }

    this.saving.set(true);
    // 邮箱：空 = 不修改（ABP 不支持邮箱解绑，邮箱常用作登录标识）；
    // 手机号：空 = 解绑手机号。
    const payload: UpdateMyProfileDto = {
      name,
      phoneNumber: phone,
    };
    if (email) {
      payload.email = email;
    }

    this.userService.updateMyProfile(payload).subscribe({
      next: () => {
        this.saving.set(false);
        this.message.success('个人资料已保存');
        this.load();
      },
      error: (err: any) => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '保存失败，请稍后重试');
      },
    });
  }

  /** 撤销未保存的修改 */
  resetForm(): void {
    const p = this.profile();
    this.formName = p?.name ?? '';
    this.formEmail = p?.email ?? '';
    this.formPhone = p?.phoneNumber ?? '';
  }

  // =================== 实名认证 ===================

  openRealName(): void {
    this.realNameError.set('');
    this.realName = this.profile()?.realName ?? '';
    this.idCard = '';
    this.realNameOpen.set(true);
  }

  closeRealName(): void {
    this.realNameOpen.set(false);
  }

  submitRealName(): void {
    if (!this.realName.trim()) {
      this.realNameError.set('请填写真实姓名');
      return;
    }
    if (!this.idCard.trim()) {
      this.realNameError.set('请填写身份证号');
      return;
    }

    this.realNameSaving.set(true);
    this.realNameError.set('');
    this.userService
      .verifyRealName({ realName: this.realName.trim(), idCardNumber: this.idCard.trim() })
      .subscribe({
        next: p => {
          this.realNameSaving.set(false);
          this.realNameOpen.set(false);
          if (p) {
            this.profile.set(p);
          }
          this.message.success('实名认证成功，现在可以学习共享 / 共建资源库的课程');
        },
        error: (err: any) => {
          this.realNameSaving.set(false);
          this.realNameError.set(err?.error?.error?.message || '认证失败，请核对后重试');
        },
      });
  }

  // =================== 修改密码 ===================

  pwdValid(): boolean {
    return !!this.pwdCurrent && !!this.pwdNext && !!this.pwdConfirm && this.pwdNext.length >= 6 && this.pwdNext === this.pwdConfirm;
  }

  submitPassword(): void {
    if (this.pwdNext !== this.pwdConfirm) {
      this.message.error('两次输入的新密码不一致');
      return;
    }
    if ((this.pwdNext || '').length < 6) {
      this.message.error('新密码至少 6 位');
      return;
    }
    if (!this.pwdCurrent) {
      this.message.error('请输入当前密码');
      return;
    }

    this.pwdSaving.set(true);
    this.userService.changePassword({ currentPassword: this.pwdCurrent, newPassword: this.pwdNext }).subscribe({
      next: () => {
        this.pwdSaving.set(false);
        this.pwdCurrent = '';
        this.pwdNext = '';
        this.pwdConfirm = '';
        this.message.success('密码修改成功');
      },
      error: (err: any) => {
        this.pwdSaving.set(false);
        const msg = err?.error?.error?.message || err?.error?.message || '密码修改失败，请检查当前密码是否正确';
        this.message.error(msg);
      },
    });
  }
}
