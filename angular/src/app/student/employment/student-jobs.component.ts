import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { EmploymentJobType, EmploymentService, JobPostingDto } from '../../employment/employment.service';

interface StatItem {
  label: string;
  value: number;
  suffix: string;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-student-jobs',
  standalone: true,
  imports: [CommonModule, DatePipe, DecimalPipe, FormsModule, NzIconModule, NzSpinModule, NzEmptyModule],
  templateUrl: './student-jobs.component.html',
  styleUrls: ['./student-jobs.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentJobsComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly jobs = signal<JobPostingDto[]>([]);
  readonly loading = signal(false);
  readonly keyword = signal('');
  readonly location = signal('');
  readonly jobType = signal<EmploymentJobType | null>(null);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(12);

  readonly jobTypes = EmploymentJobType;

  readonly stats = computed<StatItem[]>(() => {
    const items = this.jobs();
    const total = this.totalCount();
    const fullTime = items.filter(x => x.jobType === EmploymentJobType.FullTime).length;
    const intern = items.filter(x => x.jobType === EmploymentJobType.Internship).length;
    return [
      { label: '在招岗位', value: total, suffix: '个', icon: 'briefcase', color: '#1e6ce8' },
      { label: '全职岗位', value: fullTime, suffix: '个', icon: 'rocket', color: '#0c4cb8' },
      { label: '实习岗位', value: intern, suffix: '个', icon: 'experiment', color: '#0891b2' },
      { label: '已投递', value: items.filter(x => x.hasApplied).length, suffix: '份', icon: 'paper-plane', color: '#10b981' },
    ];
  });

  ngOnInit(): void {
    this.loadItems();
  }

  loadItems(): void {
    this.loading.set(true);
    this.employmentService.getPublishedJobList({
      filter: this.keyword() || undefined,
      location: this.location() || undefined,
      jobType: this.jobType() ?? undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    }).subscribe({
      next: result => {
        this.jobs.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载岗位失败');
      },
    });
  }

  onSearch(): void {
    this.pageIndex.set(1);
    this.loadItems();
  }

  onPageChange(page: number): void {
    this.pageIndex.set(page);
    this.loadItems();
  }

  resetFilter(): void {
    this.keyword.set('');
    this.location.set('');
    this.jobType.set(null);
    this.pageIndex.set(1);
    this.loadItems();
  }

  openDetail(item: JobPostingDto): void {
    this.router.navigate(['/student/employment/jobs', item.id]);
  }

  getTypeLabel(type: EmploymentJobType): string {
    const labels: Record<number, string> = {
      [EmploymentJobType.FullTime]: '全职',
      [EmploymentJobType.Internship]: '实习',
      [EmploymentJobType.PartTime]: '兼职',
      [EmploymentJobType.Apprenticeship]: '学徒',
    };
    return labels[type] || '其他';
  }

  getTypeColor(type: EmploymentJobType): string {
    const colors: Record<number, string> = {
      [EmploymentJobType.FullTime]: '#1e6ce8',
      [EmploymentJobType.Internship]: '#00b7ff',
      [EmploymentJobType.PartTime]: '#10b981',
      [EmploymentJobType.Apprenticeship]: '#f59e0b',
    };
    return colors[type] || '#6b7280';
  }

  /** 封面渐变（基于 id 稳定生成） */
  coverGradient(item: JobPostingDto): string {
    const palettes = [
      '#1e6ce8',
      '#0c4cb8',
      '#1d4ed8',
      '#2563eb',
      '#0284c7',
    ];
    const key = item.id || item.title || '';
    let hash = 0;
    for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
    return palettes[Math.abs(hash) % palettes.length];
  }

  getSkillTags(item: JobPostingDto): string[] {
    if (!item.skillTags) return [];
    return item.skillTags.split(/[,,;;\n]/).map(s => s.trim()).filter(Boolean).slice(0, 4);
  }
}
