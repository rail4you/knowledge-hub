import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { EmploymentService, UpdateEmployerProfileDto } from './employment.service';

@Component({
  selector: 'app-employer-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzCardModule, NzInputModule],
  templateUrl: './employer-profile.component.html',
  styleUrls: ['./employer-profile.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployerProfileComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  form: UpdateEmployerProfileDto = {
    contactName: '',
    phoneNumber: '',
    email: '',
    companyName: '',
    unifiedSocialCreditCode: '',
    position: '',
    industry: '',
    partnerSchool: '',
    remark: '',
  };

  ngOnInit(): void {
    this.employmentService.getMyEmployerProfile().subscribe(profile => {
      this.form = {
        contactName: profile.contactName || '',
        phoneNumber: profile.phoneNumber || '',
        email: profile.email || '',
        companyName: profile.companyName || '',
        unifiedSocialCreditCode: profile.unifiedSocialCreditCode || '',
        position: profile.position || '',
        industry: profile.industry || '',
        partnerSchool: profile.partnerSchool || '',
        remark: profile.remark || '',
      };
    });
  }

  save(): void {
    this.employmentService.updateMyEmployerProfile(this.form).subscribe({
      next: () => {
        this.message.success('企业档案已更新');
        this.router.navigate(['/employment/jobs']);
      },
      error: () => this.message.error('保存失败'),
    });
  }
}
