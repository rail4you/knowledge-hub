import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ResourceMediaJobService } from '../../proxy/resources/media/resource-media-job.service';
import { ResourceMediaJobStatus } from '../../proxy/resources/media/resource-media-job-status.enum';
import type { ResourceMediaJobDto } from '../../proxy/application/contracts/resources/media/models';

/**
 * 资源媒体处理任务管理：跟踪缩略图/预览生成状态，支持重试/取消。
 */
@Component({
  selector: 'app-media-jobs',
  standalone: true,
  imports: [
    CommonModule, DatePipe, FormsModule,
    NzTableModule, NzTagModule, NzButtonModule, NzIconModule, NzProgressModule, NzSelectModule, NzInputModule,
  ],
  templateUrl: './media-jobs.component.html',
  styleUrls: ['./media-jobs.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MediaJobsComponent implements OnInit, OnDestroy {
  private readonly service = inject(ResourceMediaJobService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly jobs = signal<ResourceMediaJobDto[]>([]);
  readonly totalCount = signal(0);

  pageIndex = 1;
  pageSize = 20;
  statusFilter: ResourceMediaJobStatus | null = null;
  filter = '';

  readonly statusOptions = [
    { label: '全部', value: null },
    { label: '排队中', value: ResourceMediaJobStatus.Pending },
    { label: '处理中', value: ResourceMediaJobStatus.Running },
    { label: '已完成', value: ResourceMediaJobStatus.Completed },
    { label: '部分失败', value: ResourceMediaJobStatus.PartialFailed },
    { label: '失败', value: ResourceMediaJobStatus.Failed },
    { label: '已取消', value: ResourceMediaJobStatus.Cancelled },
  ];

  readonly hasFailed = computed(() =>
    this.jobs().some(j =>
      j.status === ResourceMediaJobStatus.Failed || j.status === ResourceMediaJobStatus.PartialFailed
    )
  );

  private timer?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    this.load();
    // 每 5s 轮询，处理进度实时可见
    this.timer = setInterval(() => this.load(), 5000);
  }

  ngOnDestroy(): void {
    if (this.timer) {
      clearInterval(this.timer);
    }
  }

  load(): void {
    this.loading.set(true);
    this.service.getList({
      status: this.statusFilter ?? undefined,
      filter: this.filter?.trim() || undefined,
      sorting: 'creationTime desc',
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize,
    } as any).subscribe({
      next: result => {
        this.jobs.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载媒体处理任务失败');
      },
    });
  }

  onStatusChange(): void {
    this.pageIndex = 1;
    this.load();
  }

  onSearch(): void {
    this.pageIndex = 1;
    this.load();
  }

  onPageChange(pageIndex: number): void {
    this.pageIndex = pageIndex;
    this.load();
  }

  retry(job: ResourceMediaJobDto): void {
    this.service.retry(job.id).subscribe({
      next: () => {
        this.message.success('已重新排队');
        this.load();
      },
      error: () => this.message.error('重试失败'),
    });
  }

  cancel(job: ResourceMediaJobDto): void {
    this.service.cancel(job.id).subscribe({
      next: () => {
        this.message.success('已取消');
        this.load();
      },
      error: () => this.message.error('取消失败'),
    });
  }

  retryAllFailed(): void {
    this.service.retryAllFailed().subscribe({
      next: () => {
        this.message.success('失败任务已重新排队');
        this.load();
      },
      error: () => this.message.error('操作失败'),
    });
  }

  statusLabel(status: ResourceMediaJobStatus): string {
    switch (status) {
      case ResourceMediaJobStatus.Pending: return '排队中';
      case ResourceMediaJobStatus.Running: return '处理中';
      case ResourceMediaJobStatus.Completed: return '已完成';
      case ResourceMediaJobStatus.PartialFailed: return '部分失败';
      case ResourceMediaJobStatus.Failed: return '失败';
      case ResourceMediaJobStatus.Cancelled: return '已取消';
      default: return '未知';
    }
  }

  statusColor(status: ResourceMediaJobStatus): string {
    switch (status) {
      case ResourceMediaJobStatus.Completed: return 'success';
      case ResourceMediaJobStatus.Running: return 'processing';
      case ResourceMediaJobStatus.PartialFailed: return 'warning';
      case ResourceMediaJobStatus.Failed: return 'error';
      case ResourceMediaJobStatus.Cancelled: return 'default';
      default: return 'default';
    }
  }

  isRunning(job: ResourceMediaJobDto): boolean {
    return job.status === ResourceMediaJobStatus.Pending || job.status === ResourceMediaJobStatus.Running;
  }
}
