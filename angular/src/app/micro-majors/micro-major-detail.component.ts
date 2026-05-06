import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { MicroMajorDetailDto, MicroMajorService } from './micro-major.service';

@Component({
  selector: 'app-micro-major-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzProgressModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './micro-major-detail.component.html',
  styleUrls: ['./micro-major-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MicroMajorDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly microMajorService = inject(MicroMajorService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly detail = signal<MicroMajorDetailDto | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }

    this.loadDetail(id);
  }

  loadDetail(id: string): void {
    this.loading.set(true);
    this.microMajorService.getDetail(id).subscribe({
      next: detail => {
        this.detail.set(detail);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  enroll(): void {
    const detail = this.detail();
    if (!detail) {
      return;
    }

    if (detail.isCurrentUserEnrolled) {
      this.message.info('您已报名该微专业');
      return;
    }

    this.microMajorService.enroll(detail.id).subscribe({
      next: () => {
        this.message.success('报名成功');
        this.loadDetail(detail.id);
      },
      error: () => {
        this.message.error('报名失败');
      },
    });
  }
}
