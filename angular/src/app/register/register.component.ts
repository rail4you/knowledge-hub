import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { PublicRegistrationService } from '../proxy/public-registrations/public-registration.service';
import type {
  PublicRegistrationDto,
  PublicRegistrationResultDto,
} from '../proxy/public-registrations/dtos/models';
import type { PublicSchoolDto } from '../proxy/schools/dtos/models';

/**
 * 公开注册页（社会学校）。
 *
 * 需求「租户有公开的『社会学校』，该租户的学生可以自由注册」：
 * 注册用户统一进入平台唯一的社会学校租户，并自动获得该租户的 Student 角色。
 *
 * 社会学校未配置时（`Configured=false`）不展示表单，直接提示平台尚未开放注册。
 */
@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzAlertModule,
    NzButtonModule,
    NzFormModule,
    NzInputModule,
    NzSpinModule,
  ],
  templateUrl: './register.component.html',
  styleUrls: ['./register.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent implements OnInit {
  private readonly registrationService = inject(PublicRegistrationService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  readonly socialSchool = signal<PublicSchoolDto | null>(null);
  readonly loading = signal<boolean>(true);
  readonly submitting = signal<boolean>(false);
  readonly result = signal<PublicRegistrationResultDto | null>(null);
  readonly errorMessage = signal<string | null>(null);

  form: PublicRegistrationDto = {
    userName: '',
    email: '',
    password: '',
    name: '',
    surname: '',
    phoneNumber: '',
  };

  ngOnInit(): void {
    this.registrationService.getSocialSchool().subscribe({
      next: (res) => {
        this.socialSchool.set(res);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  submit(): void {
    const f = this.form;
    if (!f.userName?.trim() || !f.email?.trim() || !f.password) {
      this.errorMessage.set('请填写用户名、邮箱和密码。');
      return;
    }

    this.errorMessage.set(null);
    this.submitting.set(true);

    this.registrationService.register(f).subscribe({
      next: (res) => {
        this.submitting.set(false);
        this.result.set(res);
        this.message.success(`注册成功，欢迎加入「${res.socialSchoolName || '社会学校'}」。`);
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.error?.error?.message || '注册失败，请稍后重试。');
      },
    });
  }

  goLogin(): void {
    this.router.navigate(['/account/login']);
  }
}
