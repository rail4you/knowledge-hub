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
    NzTooltipModule
  ],
  template: `
    <div class="indexing-jobs-container">
      <div class="header">
        <h2>文档索引任务</h2>
        <button nz-button nzType="default" (click)="refresh()">
          刷新
        </button>
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
      margin-bottom: 24px;
    }
    
    .header h2 {
      margin: 0;
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
  
  private refreshInterval?: Subscription;
  protected readonly IndexingJobStatus = IndexingJobStatus;

  ngOnInit() {
    this.loadJobs();
    this.refreshInterval = interval(5000).pipe(
      switchMap(() => this.searchService.getIndexingJobs({
        skipCount: (this.pageIndex - 1) * this.pageSize,
        maxResultCount: this.pageSize
      }))
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

  loadJobs() {
    this.loading.set(true);
    this.searchService.getIndexingJobs({
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize
    }).subscribe({
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
