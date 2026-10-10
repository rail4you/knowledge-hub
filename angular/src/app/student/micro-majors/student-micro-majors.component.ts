import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MicroMajorService, MicroMajorEnrollmentStatus } from '../../micro-majors/micro-major.service';
import type { MicroMajorDto, MicroMajorEnrollmentDto } from '../../micro-majors/micro-major.service';
import { ClientCacheService } from '../../shared/cache/client-cache.service';
import { hashGradient } from '../../shared/utils/color.util';
import { SectionBannerComponent } from '../shared/section-banner/section-banner.component';

@Component({
  selector: 'app-student-micro-majors',
  standalone: true,
  imports: [
    CommonModule, RouterModule,
    NzIconModule, NzSpinModule, NzPaginationModule,
    SectionBannerComponent,
  ],
  templateUrl: './student-micro-majors.component.html',
  styleUrls: ['./student-micro-majors.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMicroMajorsComponent implements OnInit {
  private readonly microMajorService = inject(MicroMajorService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly cache = inject(ClientCacheService);

  readonly items = signal<MicroMajorDto[]>([]);
  readonly enrollmentStatusMap = signal<Record<string, number>>({});
  readonly loading = signal(false);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  readonly EnrollmentStatus = MicroMajorEnrollmentStatus;

  /** 关键词搜索（在当前页数据内过滤） */
  readonly filter = signal('');
  /** 行业方向筛选 */
  readonly industryFilter = signal('全部');

  /** 行业方向选项（从当前页数据中提取，'全部' 恒在首位） */
  readonly industryFilters = computed(() => {
    const set = new Set<string>();
    for (const it of this.items()) {
      const field = (it.industryField || '').trim();
      if (field) set.add(field);
    }
    return ['全部', ...Array.from(set).sort((a, b) => a.localeCompare(b, 'zh-Hans-CN'))];
  });

  /** 过滤后的卡片（搜索 + 行业方向） */
  readonly filteredItems = computed(() => {
    const q = this.filter().trim().toLowerCase();
    const industry = this.industryFilter();
    return this.items().filter((it) => {
      if (industry !== '全部' && (it.industryField || '').trim() !== industry) return false;
      if (!q) return true;
      const haystack = `${it.title ?? ''} ${it.summary ?? ''} ${it.industryField ?? ''}`.toLowerCase();
      return haystack.includes(q);
    });
  });

  readonly hasActiveFilter = computed(() => this.filter().trim().length > 0 || this.industryFilter() !== '全部');

  /** 头部数据总览（与卡片徽标同口径：已完成含已发证，已发证即已完成） */
  readonly heroStats = computed(() => {
    const map = this.enrollmentStatusMap();
    const values = Object.values(map);
    return [
      { label: '全部微专业', value: this.totalCount(), icon: 'appstore', color: '#087f7a' },
      { label: '已报名', value: values.length, icon: 'check-circle', color: '#10b981' },
      { label: '学习中', value: values.filter(s => s === MicroMajorEnrollmentStatus.InProgress).length, icon: 'play-circle', color: '#06b6d4' },
      {
        label: '已完成',
        value: values.filter(s => s === MicroMajorEnrollmentStatus.Completed || s === MicroMajorEnrollmentStatus.Certified).length,
        icon: 'safety-certificate', color: '#f59e0b',
      },
    ];
  });

  ngOnInit(): void {
    this.loadItems();
    this.loadEnrollmentStatus();
  }

  loadEnrollmentStatus(): void {
    this.cache.load<MicroMajorEnrollmentDto[]>('student.micro-majors', 'my-enrollments', () => this.microMajorService.getMyEnrollments()).subscribe({
      next: enrollments => {
        const map: Record<string, number> = {};
        for (const e of enrollments) {
          // 后端返回 EnrolledAt 倒序（最新在前）：只保留最新一条，
          // 与后端 isCurrentUserEnrolled 取最新报名记录的口径一致。
          if (e.microMajorId && !(e.microMajorId in map)) map[e.microMajorId] = e.status;
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
    const key = `list:${this.pageIndex()}:${this.pageSize()}`;
    this.cache.load<any>('student.micro-majors', key, () => this.microMajorService.getPublished({
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    })).subscribe({
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

  resetFilters(): void {
    this.filter.set('');
    this.industryFilter.set('全部');
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
        // 报名后列表与我的报名状态需刷新：清空该业务缓存再重新拉取
        this.cache.clear('student.micro-majors');
        this.loadItems();
        this.loadEnrollmentStatus();
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
      '#087f7a',
      '#086964',
      '#087f7a',
      '#087f7a',
      '#0891b2',
    ];
    const key = item.title || item.id || '';
    return hashGradient(key, palettes);
  }

  /** 卡片头部英文标签：原型 .micro-topline 上方的 en 字段，
   *  真实数据没有英文名时降级用 industryField 拼音首字母 + 序号 */
  shortLabel(item: MicroMajorDto): string {
    const field = (item.industryField || '').trim();
    if (!field) return 'PROGRAM';
    return field.length > 16 ? field.slice(0, 16).toUpperCase() : field.toUpperCase();
  }

  /** 卡片编号（01-09）：原型 .micro-num，使用 2 位补零 */
  padIndex(idx: number): string {
    return String(idx + 1).padStart(2, '0');
  }

  /** 一句话成果（原型 .microhead p）：取 summary 前 30 字 */
  outcomeText(item: MicroMajorDto): string {
    const s = (item.summary || '').trim();
    if (!s) return '完成全部课程,获得专业能力。';
    return s.length > 30 ? s.slice(0, 30) + '…' : s;
  }

  /** 学习时长：原型 .micro-meta 第二项，无数据时显示「自主安排」 */
  durationText(item: MicroMajorDto): string {
    const hours = (item as any).estimatedHours ?? (item as any).durationHours;
    if (typeof hours === 'number' && hours > 0) return `${hours} 学时`;
    const count = item.courseCount || 0;
    if (count > 0) return `${count * 8} 学时`;
    return '自主安排';
  }

  /** 组织单位：原型 .microbottom small，使用 institution 字段或降级显示 */
  orgText(item: MicroMajorDto): string {
    const org = (item as any).institution || (item as any).orgName;
    if (org) return String(org);
    return '专业教学团队';
  }
}
