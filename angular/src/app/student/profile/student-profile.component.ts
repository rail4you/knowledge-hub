import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzInputModule } from 'ng-zorro-antd/input';
import { ConfigStateService } from '@abp/ng.core';
import { UserService } from '../../proxy/users/user.service';
import type { MyProfileDto } from '../../proxy/users/models';
import { SchoolService } from '../../proxy/schools/school.service';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { EditContactInfoComponent } from './edit-contact-info.component';

/**
 * 学生端 · 个人中心（对齐原型 #personal-center 的「个人资料」部分）
 *
 * 数据来源（全部真实接口）：
 *   · 我的资料 GET  /api/app/user/my-profile
 *   · 机构档案 GET  /api/app/school/profile/{tenantId}（取中文校名，不显示租户 slug）
 *   · 修改联系方式 PUT /api/app/user/my-profile（复用 EditContactInfoComponent）
 *   · 修改密码     POST /api/account/my-profile/change-password
 *
 * 账号有效期：后端 /api/app/account-validity 仅对全局管理员开放，
 * 且只覆盖租户管理员 / 教师账号，学生端读不到 —— 因此这里如实标注为
 * 「暂不可查」，不伪造有效期数字。
 */
@Component({
  selector: 'app-student-profile',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
  ],
  templateUrl: './student-profile.component.html',
  styleUrls: ['./student-profile.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentProfileComponent implements OnInit {
  private readonly userService = inject(UserService);
  private readonly schoolService = inject(SchoolService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly configState = inject(ConfigStateService);

  readonly profile = signal<MyProfileDto | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  /** 当前机构中文名称（来自学校档案，不使用租户 slug） */
  readonly schoolName = signal('');

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
    this.loadSchoolName();
  }

  /** 载入个人资料 */
  private load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.userService.getMyProfile().subscribe({
      next: p => {
        this.profile.set(p);
        this.loading.set(false);
      },
      error: () => {
        this.profile.set(null);
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  /** 重新加载 */
  reload(): void {
    this.load();
  }

  /**
   * 机构中文名：会话里的租户名是 slug（如 qidi / social），不能直接展示。
   * 与学生端布局一致，用学校档案里的中文校名。
   */
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

  /** 头像文字：优先 name → userName 首字符 */
  initial(): string {
    const p = this.profile();
    const n = (p?.name || p?.userName || '同').trim();
    return n ? n.charAt(0).toUpperCase() : '同';
  }

  displayName(): string {
    return this.profile()?.name || this.profile()?.userName || '未命名用户';
  }

  /** 资料卡上的只读字段（值都来自 my-profile 接口） */
  readonly rows = computed(() => {
    const p = this.profile();
    if (!p) return [];
    return [
      { label: '用户名', value: p.userName || '-' },
      { label: '姓名', value: p.name || '未填写', editable: false },
      { label: '电子邮箱', value: p.email || '未绑定', editable: true, confirmed: !!p.emailConfirmed },
      { label: '手机号', value: p.phoneNumber || '未绑定', editable: true, confirmed: !!p.phoneNumberConfirmed },
      { label: '所属机构', value: this.schoolName() || '当前机构', editable: false },
    ];
  });

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
        // 不设 nzFooter：让 component 内部的 <ng-template [nzModalFooter]> 生效
      })
      .afterClose.subscribe(ok => {
        if (ok) this.load();
      });
  }

  // =================== 修改密码 ===================
  openPwd(): void {
    this.pwdCurrent.set('');
    this.pwdNew.set('');
    this.pwdConfirm.set('');
    this.pwdModalOpen.set(true);
  }

  closePwd(): void {
    this.pwdModalOpen.set(false);
  }

  protected pwdStrength(): 'weak' | 'medium' | 'strong' {
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
      if (this.pwdNew() !== this.pwdConfirm()) {
        this.message.error('两次输入的新密码不一致');
        return;
      }
      if ((this.pwdNew() || '').length < 6) {
        this.message.error('新密码至少 6 位');
        return;
      }
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
      error: err => {
        this.pwdSaving.set(false);
        const msg =
          err?.error?.error?.message || err?.error?.message || '密码修改失败，请检查当前密码是否正确';
        this.message.error(msg);
      },
    });
  }
}
