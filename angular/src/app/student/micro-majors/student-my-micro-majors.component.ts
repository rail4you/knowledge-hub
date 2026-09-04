import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import {
  MicroMajorService,
  MicroMajorEnrollmentStatus,
} from '../../micro-majors/micro-major.service';
import type {
  MyMicroMajorDto,
} from '../../micro-majors/micro-major.service';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';

@Component({
  selector: 'app-student-my-micro-majors',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, RouterModule,
    NzIconModule, NzSpinModule,
    StudentHeroComponent,
  ],
  templateUrl: './student-my-micro-majors.component.html',
  styleUrls: ['./student-my-micro-majors.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyMicroMajorsComponent implements OnInit {
  private readonly microMajorService = inject(MicroMajorService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly items = signal<MyMicroMajorDto[]>([]);
  readonly loading = signal(true);
  readonly EnrollmentStatus = MicroMajorEnrollmentStatus;

  /** 头部数据总览 */
  readonly heroStats = computed(() => {
    const items = this.items();
    const countOf = (s: MicroMajorEnrollmentStatus) =>
      items.filter(i => i.enrollmentStatus === s).length;
    return [
      { label: '已报名', value: items.length, suffix: '个', icon: 'appstore', color: '#1e6ce8' },
      { label: '学习中', value: countOf(MicroMajorEnrollmentStatus.InProgress), suffix: '个', icon: 'play-circle', color: '#06b6d4' },
      { label: '已完成', value: countOf(MicroMajorEnrollmentStatus.Completed), suffix: '个', icon: 'check-circle', color: '#10b981' },
      { label: '已获证书', value: countOf(MicroMajorEnrollmentStatus.Certified), suffix: '个', icon: 'safety-certificate', color: '#f59e0b' },
    ];
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.microMajorService.getMyMicroMajors().subscribe({
      next: result => {
        this.items.set(result || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载我的微专业失败');
      },
    });
  }

  getStatusLabel(status: number): string {
    const labels: Record<number, string> = {
      [MicroMajorEnrollmentStatus.Pending]: '待审批',
      [MicroMajorEnrollmentStatus.Enrolled]: '已通过',
      [MicroMajorEnrollmentStatus.InProgress]: '学习中',
      [MicroMajorEnrollmentStatus.Completed]: '已完成',
      [MicroMajorEnrollmentStatus.Certified]: '已发证',
      [MicroMajorEnrollmentStatus.Cancelled]: '已取消',
    };
    return labels[status] || '未知';
  }

  getStatusIcon(status: number): string {
    const icons: Record<number, string> = {
      [MicroMajorEnrollmentStatus.Pending]: 'clock-circle',
      [MicroMajorEnrollmentStatus.Enrolled]: 'check-circle',
      [MicroMajorEnrollmentStatus.InProgress]: 'read',
      [MicroMajorEnrollmentStatus.Completed]: 'check-circle',
      [MicroMajorEnrollmentStatus.Certified]: 'safety-certificate',
      [MicroMajorEnrollmentStatus.Cancelled]: 'close-circle',
    };
    return icons[status] || 'question';
  }

  openDetail(item: MyMicroMajorDto): void {
    if (!item.id) return;
    // 携带 from=my-micro-majors，详情页的“返回”会回到我的微专业（原路返回）
    this.router.navigate(['/student/micro-majors', item.id], { queryParams: { from: 'my-micro-majors' } });
  }

  coverGradient(item: MyMicroMajorDto): string {
    const palettes = ['#1e6ce8', '#0c4cb8', '#2563eb', '#0284c7', '#0891b2'];
    const key = item.title || item.id || '';
    let hash = 0;
    for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
    return palettes[Math.abs(hash) % palettes.length];
  }

  hasCover(item: MyMicroMajorDto): boolean {
    return !!item.coverImageUrl && item.coverImageUrl.trim().length > 0;
  }

  /** 学习进度保留 2 位小数 */
  roundProgress(value: number | null | undefined): number {
    if (value == null || isNaN(value)) return 0;
    return Math.round(value * 100) / 100;
  }
}
