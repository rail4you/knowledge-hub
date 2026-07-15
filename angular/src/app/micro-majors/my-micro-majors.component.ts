import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzImageModule } from 'ng-zorro-antd/image';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  MicroMajorCertificateDto,
  MicroMajorEnrollmentDto,
  MicroMajorEnrollmentStatus,
  MicroMajorService,
} from './micro-major.service';

@Component({
  selector: 'app-my-micro-majors',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzImageModule,
    NzProgressModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './my-micro-majors.component.html',
  styleUrls: ['./my-micro-majors.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyMicroMajorsComponent implements OnInit {
  private readonly microMajorService = inject(MicroMajorService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly enrollments = signal<MicroMajorEnrollmentDto[]>([]);
  readonly certificates = signal<MicroMajorCertificateDto[]>([]);

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.microMajorService.getMyEnrollments().subscribe({
      next: items => {
        this.enrollments.set(items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });

    this.microMajorService.getMyCertificates().subscribe({
      next: items => this.certificates.set(items || []),
    });
  }

  openDetail(id: string): void {
    this.router.navigate(['/micro-majors', id]);
  }

  getEnrollmentStatusLabel(status: MicroMajorEnrollmentStatus): string {
    const labels: Record<number, string> = {
      [MicroMajorEnrollmentStatus.Pending]: '待审批',
      [MicroMajorEnrollmentStatus.Enrolled]: '已通过',
      [MicroMajorEnrollmentStatus.InProgress]: '学习中',
      [MicroMajorEnrollmentStatus.Completed]: '已结业',
      [MicroMajorEnrollmentStatus.Certified]: '已发证',
      [MicroMajorEnrollmentStatus.Cancelled]: '已取消',
    };
    return labels[status] || '未知';
  }
}
