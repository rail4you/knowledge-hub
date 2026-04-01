import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { interval, Subscription } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { SearchService, IndexingJobDto, IndexingJobStatus, PagedResultDto } from '../../search/search.service';

@Component({
  selector: 'app-indexing-jobs',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzTableModule,
    NzButtonModule,
    NzTagModule,
    NzProgressModule,
    NzSpinModule,
    NzEmptyModule,
    NzTooltipModule,
    NzDropDownModule,
    NzMenuModule,
    NzDatePickerModule,
    NzIconModule
  ],
  template: `
    <div class="indexing-jobs-container">
      <div class="header">
        <h2>文档索引任务</h2>
        <div class="header-actions">
          <button nz-button nzType="default" (click)="refresh()">
            刷新
          </button>
        </div>
      </div>

      <div class="filters">
        <nz-dropdown-menu #statusMenu="nzDropdownMenu">
          <ul nz-menu>
            <li nz-menu-item [nzSelected]="selectedStatus() === null" (click)="onStatusFilter(null)">
              全部状态
            </li>
            <li nz-menu-item [nzSelected]="selectedStatus() === 0" (click)="onStatusFilter(0)">
              <nz-tag nzColor="default">等待中</nz-tag>
            </li>
            <li nz-menu-item [nzSelected]="selectedStatus() === 10" (click)="onStatusFilter(10)">
              <nz-tag nzColor="processing">解析中</nz-tag>
            </li>
            <li nz-menu-item [nzSelected]="selectedStatus() === 20" (click)="onStatusFilter(20)">
              <nz-tag nzColor="processing">索引中</nz-tag>
            </li>
            <li nz-menu-item [nzSelected]="selectedStatus() === 30" (click)="onStatusFilter(30)">
              <nz-tag nzColor="success">已完成</nz-tag>
            </li>
            <li nz-menu-item [nzSelected]="selectedStatus() === 40" (click)="onStatusFilter(40)">
              <nz-tag nzColor="error">失败</nz-tag>
            </li>
            <li nz-menu-item [nzSelected]="selectedStatus() === 50" (click)="onStatusFilter(50)">
              <nz-tag nzColor="warning">已取消</nz-tag>
            </li>
          </ul>
        </nz-dropdown-menu>
        <button nz-button [nzDropdownMenu]="statusMenu" nz-dropdown>
          <span>{{ statusFilterLabel() }}</span>
          <span nz-icon nzType="down"></span>
        </button>

        <nz-dropdown-menu #timeMenu="nzDropdownMenu">
          <ul nz-menu>
            <li nz-menu-item [nzSelected]="selectedTimeRange() === 'all'" (click)="onTimeFilter('all')">
              全部时间
            </li>
            <li nz-menu-item [nzSelected]="selectedTimeRange() === 'today'" (click)="onTimeFilter('today')">
              今天
            </li>
            <li nz-menu-item [nzSelected]="selectedTimeRange() === 'yesterday'" (click)="onTimeFilter('yesterday')">
              昨天
            </li>
            <li nz-menu-item [nzSelected]="selectedTimeRange() === 'week'" (click)="onTimeFilter('week')">
              最近7天
            </li>
            <li nz-menu-item [nzSelected]="selectedTimeRange() === 'month'" (click)="onTimeFilter('month')">
              最近30天
            </li>
            <li nz-menu-item [nzSelected]="selectedTimeRange() === 'custom'" (click)="onTimeFilter('custom')">
              自定义
            </li>
          </ul>
        </nz-dropdown-menu>
        <button nz-button [nzDropdownMenu]="timeMenu" nz-dropdown>
          <span>{{ timeFilterLabel() }}</span>
          <span nz-icon nzType="down"></span>
        </button>

        @if (selectedTimeRange() === 'custom') {
          <nz-range-picker
            [(ngModel)]="customDateRange"
            (ngModelChange)="onCustomDateChange($event)"
            nzSize="default"
          ></nz-range-picker>
        }
      </div>

      <nz-spin [nzSpinning]="loading()">
        @if (jobs().length === 0 && !loading()) {
          <nz-empty nzNotFoundContent="暂无索引任务"></nz-empty>
        } @else {
          <nz-table
            #basicTable
            [nzData]="jobs()"
            [nzTotal]="totalCount()"
            [nzPageSize]="pageSize"
            [nzPageIndex]="pageIndex"
            [nzFrontPagination]="false"
            [nzLoading]="loading()"
            (nzQueryParams)="onQueryParamsChange($event)">
            <thead>
              <tr>
                <th>资源名称</th>
                <th>状态</th>
                <th>进度</th>
                <th>页数</th>
                <th>重试次数</th>
                <th>创建时间</th>
                <th>操作</th>
              </tr>
            </thead>
            <tbody>
              @for (job of basicTable.data; track job.id) {
                <tr>
                  <td>
                    <a (click)="viewResource(job.resourceId)" class="resource-link">
                      {{ job.resourceName || job.resourceId }}
                    </a>
                  </td>
                  <td>
                    <nz-tag [nzColor]="getStatusColor(job.status)">
                      {{ getStatusText(job.status) }}
                    </nz-tag>
                  </td>
                  <td>
                    <nz-progress
                      [nzPercent]="job.progress"
                      [nzStatus]="getProgressStatus(job.status)"
                      [nzStrokeWidth]="8">
                    </nz-progress>
                  </td>
                  <td>
                    @if (job.totalPages) {
                      {{ job.processedPages ?? 0 }} / {{ job.totalPages }}
                    } @else {
                      -
                    }
                  </td>
                  <td>{{ job.retryCount }}</td>
                  <td>{{ job.creationTime | date:'short' }}</td>
                  <td>
                    <button
                      nz-button
                      nzType="link"
                      nzSize="small"
                      nz-tooltip
                      nzTooltipTitle="重新生成向量索引"
                      (click)="refreshEmbeddings(job.resourceId)">
                      刷新向量
                    </button>
                    @if (job.status === IndexingJobStatus.Failed) {
                      <button
                        nz-button
                        nzType="link"
                        nzSize="small"
                        nz-tooltip
                        [nzTooltipTitle]="job.errorMessage"
                        (click)="retryJob(job.id)">
                        重试
                      </button>
                    }
                    @if (job.status === IndexingJobStatus.Pending || job.status === IndexingJobStatus.Parsing) {
                      <button
                        nz-button
                        nzType="link"
                        nzSize="small"
                        (click)="cancelJob(job.id)">
                        取消
                      </button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </nz-table>
        }
      </nz-spin>
    </div>
  `,
  styles: [`
    .indexing-jobs-container {
      padding: 24px;
    }

    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 16px;
    }

    .header h2 {
      margin: 0;
    }

    .filters {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
    }

    .resource-link {
      color: #1890ff;
      cursor: pointer;
    }

    .resource-link:hover {
      text-decoration: underline;
    }
  `]
})
export class IndexingJobsComponent implements OnInit, OnDestroy {
  private readonly searchService = inject(SearchService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  jobs = signal<IndexingJobDto[]>([]);
  totalCount = signal(0);
  loading = signal(false);
  pageIndex = 1;
  pageSize = 20;

  selectedStatus = signal<number | null>(null);
  selectedTimeRange = signal<string>('all');
  customDateRange: Date[] | null = null;

  private refreshInterval?: Subscription;
  protected readonly IndexingJobStatus = IndexingJobStatus;

  ngOnInit() {
    this.loadJobs();
    this.refreshInterval = interval(5000).pipe(
      switchMap(() => this.searchService.getIndexingJobs(this.buildInput()))
    ).subscribe({
      next: (result: PagedResultDto<IndexingJobDto>) => {
        this.jobs.set(result.items);
        this.totalCount.set(result.totalCount);
      }
    });
  }

  ngOnDestroy() {
    this.refreshInterval?.unsubscribe();
  }

  buildInput(): any {
    const input: any = {
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize
    };
    if (this.selectedStatus() !== null) {
      input.status = this.selectedStatus();
    }
    const range = this.getDateRange();
    if (range) {
      input.startTime = range.start;
      input.endTime = range.end;
    }
    return input;
  }

  getDateRange(): { start: string; end: string } | null {
    const range = this.selectedTimeRange();
    if (range === 'all') return null;

    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());

    switch (range) {
      case 'today':
        return { start: today.toISOString(), end: new Date(today.getTime() + 86400000).toISOString() };
      case 'yesterday': {
        const yesterday = new Date(today.getTime() - 86400000);
        return { start: yesterday.toISOString(), end: today.toISOString() };
      }
      case 'week': {
        const weekAgo = new Date(today.getTime() - 7 * 86400000);
        return { start: weekAgo.toISOString(), end: new Date(today.getTime() + 86400000).toISOString() };
      }
      case 'month': {
        const monthAgo = new Date(today.getTime() - 30 * 86400000);
        return { start: monthAgo.toISOString(), end: new Date(today.getTime() + 86400000).toISOString() };
      }
      case 'custom': {
        if (this.customDateRange && this.customDateRange.length === 2) {
          return {
            start: this.customDateRange[0].toISOString(),
            end: this.customDateRange[1].toISOString()
          };
        }
        return null;
      }
      default:
        return null;
    }
  }

  statusFilterLabel(): string {
    const status = this.selectedStatus();
    if (status === null) return '全部状态';
    switch (status) {
      case 0: return '等待中';
      case 10: return '解析中';
      case 20: return '索引中';
      case 30: return '已完成';
      case 40: return '失败';
      case 50: return '已取消';
      default: return '全部状态';
    }
  }

  timeFilterLabel(): string {
    const range = this.selectedTimeRange();
    switch (range) {
      case 'all': return '全部时间';
      case 'today': return '今天';
      case 'yesterday': return '昨天';
      case 'week': return '最近7天';
      case 'month': return '最近30天';
      case 'custom': return '自定义';
      default: return '全部时间';
    }
  }

  onStatusFilter(status: number | null) {
    this.selectedStatus.set(status);
    this.pageIndex = 1;
    this.loadJobs();
  }

  onTimeFilter(range: string) {
    this.selectedTimeRange.set(range);
    if (range !== 'custom') {
      this.customDateRange = null;
    }
    this.pageIndex = 1;
    this.loadJobs();
  }

  onCustomDateChange(result: Date[]) {
    if (result && result.length === 2) {
      this.pageIndex = 1;
      this.loadJobs();
    }
  }

  loadJobs() {
    this.loading.set(true);
    this.searchService.getIndexingJobs(this.buildInput()).subscribe({
      next: (result: PagedResultDto<IndexingJobDto>) => {
        this.jobs.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载索引任务失败');
      }
    });
  }

  refresh() {
    this.loadJobs();
  }

  onQueryParamsChange(params: any) {
    this.pageIndex = params.pageIndex;
    this.pageSize = params.pageSize;
    this.loadJobs();
  }

  retryJob(id: string) {
    this.searchService.retryIndexingJob(id).subscribe({
      next: () => {
        this.message.success('已重新开始索引');
        this.loadJobs();
      },
      error: () => {
        this.message.error('重试失败');
      }
    });
  }

  cancelJob(id: string) {
    this.searchService.cancelIndexingJob(id).subscribe({
      next: () => {
        this.message.success('已取消任务');
        this.loadJobs();
      },
      error: () => {
        this.message.error('取消失败');
      }
    });
  }

  refreshEmbeddings(resourceId: string) {
    this.searchService.refreshDocumentIndex(resourceId).subscribe({
      next: () => {
        this.message.success('已开始重新生成向量索引');
      },
      error: () => {
        this.message.error('刷新向量索引失败');
      }
    });
  }

  viewResource(resourceId: string) {
    this.router.navigate(['/resources', resourceId]);
  }

  getStatusColor(status: IndexingJobStatus): string {
    switch (status) {
      case IndexingJobStatus.Pending: return 'default';
      case IndexingJobStatus.Parsing: return 'processing';
      case IndexingJobStatus.Indexing: return 'processing';
      case IndexingJobStatus.Completed: return 'success';
      case IndexingJobStatus.Failed: return 'error';
      case IndexingJobStatus.Cancelled: return 'warning';
      default: return 'default';
    }
  }

  getStatusText(status: IndexingJobStatus): string {
    switch (status) {
      case IndexingJobStatus.Pending: return '等待中';
      case IndexingJobStatus.Parsing: return '解析中';
      case IndexingJobStatus.Indexing: return '索引中';
      case IndexingJobStatus.Completed: return '已完成';
      case IndexingJobStatus.Failed: return '失败';
      case IndexingJobStatus.Cancelled: return '已取消';
      default: return '未知';
    }
  }

  getProgressStatus(status: IndexingJobStatus): string {
    switch (status) {
      case IndexingJobStatus.Completed: return 'success';
      case IndexingJobStatus.Failed: return 'exception';
      default: return 'active';
    }
  }
}
