import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
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
    CommonModule, FormsModule, RouterModule,
    NzIconModule, NzSpinModule, NzPaginationModule,
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
  readonly pageIndex = signal(1);
  readonly pageSize = signal(12);

  readonly filter = signal('');
  readonly sortKey = signal<'default' | 'updated' | 'name'>('default');
  readonly majorFilter = signal('全部');

  /** 专业方向 chips（全部 + 去重专业） */
  readonly majorFilters = computed(() => {
    const majors = new Set<string>();
    for (const item of this.items()) {
      if (item.major) majors.add(item.major);
    }
    return ['全部', ...Array.from(majors)];
  });

  /** 过滤 + 排序 */
  readonly sortedItems = computed(() => {
    const keyword = this.filter().trim().toLowerCase();
    const major = this.majorFilter();
    let list = this.items().filter(item => {
      if (major !== '全部' && item.major !== major) return false;
      if (!keyword) return true;
      return [item.title, item.summary, item.major, item.courseTitle]
        .filter(Boolean)
        .some(v => String(v).toLowerCase().includes(keyword));
    });
    const sort = this.sortKey();
    if (sort === 'name') {
      list = [...list].sort((a, b) => (a.title || '').localeCompare(b.title || '', 'zh-CN'));
    } else if (sort === 'updated') {
      list = [...list].sort((a, b) => {
        const ka = String(a.endTime ?? a.creationTime ?? '');
        const kb = String(b.endTime ?? b.creationTime ?? '');
        return kb.localeCompare(ka);
      });
    }
    return list;
  });

  readonly pagedItems = computed(() => {
    const start = (this.pageIndex() - 1) * this.pageSize();
    return this.sortedItems().slice(start, start + this.pageSize());
  });

  ngOnInit(): void {
    this.loadItems();
  }

  loadItems(): void {
    this.loading.set(true);
    this.practicumService.getPublished({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe({
      next: result => {
        this.items.set(result.items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载实训项目失败');
      },
    });
  }

  onSearch(): void {
    this.pageIndex.set(1);
  }

  setSort(value: string): void {
    this.sortKey.set(value as 'default' | 'updated' | 'name');
    this.pageIndex.set(1);
  }

  selectMajor(major: string): void {
    this.majorFilter.set(major);
    this.pageIndex.set(1);
  }

  resetFilters(): void {
    this.filter.set('');
    this.sortKey.set('default');
    this.majorFilter.set('全部');
    this.pageIndex.set(1);
  }

  onPageChange(pageIndex: number): void {
    this.pageIndex.set(pageIndex);
  }

  openDetail(item: PracticumProjectDto): void {
    this.router.navigate(['/student/practicums', item.id]);
  }

  /** 卡片状态标签：对齐原型 tag（开放报名 / 已报名 / 已结束） */
  statusTag(item: PracticumProjectDto): { text: string; cls: string } {
    if (item.isExpired) return { text: '已结束', cls: 'tag tag--gray' };
    if (item.isCurrentUserEnrolled) return { text: '已报名', cls: 'tag tag--primary' };
    return { text: '开放报名', cls: 'tag' };
  }

  taskCountText(item: PracticumProjectDto): string {
    return `${item.taskCount || 0} 项任务`;
  }
}
