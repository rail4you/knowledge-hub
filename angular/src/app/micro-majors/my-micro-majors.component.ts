import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { MicroMajorCertificateDto, MicroMajorEnrollmentDto, MicroMajorService } from './micro-major.service';

@Component({
  selector: 'app-my-micro-majors',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzProgressModule,
    NzSpinModule,
    NzTableModule,
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
}
