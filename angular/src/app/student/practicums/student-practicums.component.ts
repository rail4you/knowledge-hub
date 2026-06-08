import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzMessageService } from 'ng-zorro-antd/message';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import type { PracticumProjectDto } from '../../proxy/practicums/dtos/models';

@Component({
  selector: 'app-student-practicums',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, RouterModule,
    NzButtonModule, NzIconModule, NzSpinModule, NzPaginationModule,
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
      '#0284c7',
      '#2563eb',
      '#1e6ce8',
      '#1d4ed8',
    ];
    const key = item.title || item.id || '';
    let hash = 0;
    for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
    return palettes[Math.abs(hash) % palettes.length];
  }
}
