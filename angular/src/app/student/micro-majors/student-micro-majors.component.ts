import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MicroMajorService, MicroMajorEnrollmentStatus } from '../../micro-majors/micro-major.service';
import type { MicroMajorDto, MicroMajorEnrollmentDto } from '../../micro-majors/micro-major.service';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';

@Component({
  selector: 'app-student-micro-majors',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, RouterModule,
    NzButtonModule, NzIconModule, NzSpinModule, NzPaginationModule,
    StudentHeroComponent,
  ],
  templateUrl: './student-micro-majors.component.html',
  styleUrls: ['./student-micro-majors.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMicroMajorsComponent implements OnInit {
  private readonly microMajorService = inject(MicroMajorService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly items = signal<MicroMajorDto[]>([]);
  readonly enrollmentStatusMap = signal<Record<string, number>>({});
  readonly loading = signal(false);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(12);
  readonly EnrollmentStatus = MicroMajorEnrollmentStatus;

  /** 头部数据总览 */
  readonly heroStats = computed(() => {
    const map = this.enrollmentStatusMap();
    const values = Object.values(map);
    return [
      { label: '全部微专业', value: this.totalCount(), suffix: '个', icon: 'appstore', color: '#2b6cd4' },
      { label: '已报名', value: values.length, suffix: '个', icon: 'check-circle', color: '#10b981' },
      { label: '学习中', value: values.filter(s => s === MicroMajorEnrollmentStatus.InProgress).length, suffix: '个', icon: 'play-circle', color: '#06b6d4' },
      { label: '已完成', value: values.filter(s => s === MicroMajorEnrollmentStatus.Completed).length, suffix: '个', icon: 'safety-certificate', color: '#f59e0b' },
    ];
  });

  ngOnInit(): void {
    this.loadItems();
    this.loadEnrollmentStatus();
  }

  loadEnrollmentStatus(): void {
    this.microMajorService.getMyEnrollments().subscribe({
      next: enrollments => {
        const map: Record<string, number> = {};
        for (const e of enrollments) {
          if (e.microMajorId) map[e.microMajorId] = e.status;
        }
        this.enrollmentStatusMap.set(map);
      },
    });
  }

  getEnrollmentStatusLabel(status: number): string {
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

  loadItems(): void {
    this.loading.set(true);
    this.microMajorService.getPublished({
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    }).subscribe({
      next: result => {
        this.items.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载微专业列表失败');
      },
    });
  }

  onPageChange(pageIndex: number): void {
    this.pageIndex.set(pageIndex);
    this.loadItems();
  }

  openDetail(item: MicroMajorDto): void {
    this.router.navigate(['/student/micro-majors', item.id]);
  }

  enroll(event: Event, item: MicroMajorDto): void {
    event.stopPropagation();
    if (!item.id) return;
    this.microMajorService.enroll(item.id).subscribe({
      next: () => {
        this.message.success('报名成功，等待教师审核');
        this.loadItems();
        // 立即更新状态映射，不等异步返回
        this.enrollmentStatusMap.update(m => ({ ...m, [item.id!]: MicroMajorEnrollmentStatus.Pending }));
      },
      error: (err: any) => {
        const msg = err?.error?.error?.message || err?.error?.message;
        this.message.error(msg || '报名失败');
      },
    });
  }

  hasCover(item: MicroMajorDto): boolean {
    return !!item.coverImageUrl && item.coverImageUrl.trim().length > 0;
  }

  coverGradient(item: MicroMajorDto): string {
    const palettes = [
      '#2b6cd4',
      '#1f56ad',
      '#2b6cd4',
      '#2b6cd4',
      '#0891b2',
    ];
    const key = item.title || item.id || '';
    let hash = 0;
    for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
    return palettes[Math.abs(hash) % palettes.length];
  }
}
