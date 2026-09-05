import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzMessageService } from 'ng-zorro-antd/message';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import type { PracticumProjectDto } from '../../proxy/practicums/dtos/models';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';

@Component({
  selector: 'app-student-practicums',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, RouterModule,
    NzIconModule, NzSpinModule, NzPaginationModule,
    StudentHeroComponent,
  ],
  templateUrl: './student-practicums.component.html',
  styleUrls: ['./student-practicums.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentPracticumsComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly items = signal<PracticumProjectDto[]>([]);
  readonly loading = signal(false);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(12);

  /** Hero 区数据总览 */
  readonly heroStats = computed(() => {
    const items = this.items();
    const totalTasks = items.reduce((s, x) => s + (x.taskCount || 0), 0);
    const totalEnroll = items.reduce((s, x) => s + (x.enrollmentCount || 0), 0);
    const activeCount = items.filter(x => !x.isExpired).length;
    return [
      { label: '实训总数', value: this.totalCount(), suffix: '个', icon: 'experiment', color: '#1e6ce8' },
      { label: '当前页', value: items.length, suffix: '个', icon: 'appstore', color: '#0ea5e9' },
      { label: '进行中', value: activeCount, suffix: '个', icon: 'play-circle', color: '#10b981' },
      { label: '总报名', value: totalEnroll, suffix: '人次', icon: 'team', color: '#f59e0b' },
    ];
  });

  ngOnInit(): void {
    this.loadItems();
  }

  loadItems(): void {
    this.loading.set(true);
    this.practicumService.getPublished({
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
        this.message.error('加载实训项目失败');
      },
    });
  }

  onPageChange(pageIndex: number): void {
    this.pageIndex.set(pageIndex);
    this.loadItems();
  }

  openDetail(item: PracticumProjectDto): void {
    this.router.navigate(['/student/practicums', item.id]);
  }

  enroll(event: Event, item: PracticumProjectDto): void {
    event.stopPropagation();
    if (!item.id) return;
    this.practicumService.enroll(item.id).subscribe({
      next: () => {
        this.message.success('报名成功，等待教师审核');
        this.loadItems();
      },
      error: () => this.message.error('报名失败'),
    });
  }

  coverGradient(item: PracticumProjectDto): string {
    const palettes = [
      '#2563eb',
      '#1d4ed8',
      '#3b82f6',
      '#0ea5e9',
    ];
    const key = item.title || item.id || '';
    let hash = 0;
    for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
    return palettes[Math.abs(hash) % palettes.length];
  }
}
