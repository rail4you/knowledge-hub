import { Component, inject, signal, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Router } from '@angular/router';
import { SearchService, SearchHistoryDto } from '../search.service';

@Component({
  selector: 'app-search-history',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
    NzEmptyModule,
    NzButtonModule,
    NzIconModule,
    NzTooltipModule,
    NzPopconfirmModule,
  ],
  template: `
    <div class="search-history-container">
      <nz-card
        nzTitle="搜索历史"
        [nzExtra]="historyToolbar"
      >
        <ng-template #historyToolbar>
          @if (history().length > 0) {
            <button
              nz-button
              nzSize="small"
              nzType="default"
              nzDanger
              nz-popconfirm
              nzPopconfirmTitle="确认清空全部搜索历史？"
              nzOkText="清空"
              nzCancelText="取消"
              (nzOnConfirm)="clearHistory()"
            >
              清空全部
            </button>
          }
        </ng-template>
        <nz-spin [nzSpinning]="loading()">
          @if (history().length === 0 && !loading()) {
            <nz-empty nzNotFoundContent="暂无搜索历史"></nz-empty>
          } @else {
            <nz-table
              #basicTable
              [nzData]="history()"
              [nzPageSize]="pageSize"
              [nzTotal]="totalCount()"
              [nzPageIndex]="pageIndex"
              [nzFrontPagination]="false"
              [nzLoading]="loading()"
              (nzPageIndexChange)="onPageChange($event)"
              nzSize="middle"
            >
              <thead>
                <tr>
                  <th>搜索关键词</th>
                  <th nzAlign="center">结果数量</th>
                  <th nzAlign="center">搜索时间</th>
                  <th nzAlign="center">操作</th>
                </tr>
              </thead>
              <tbody>
                @for (item of history(); track item.id) {
                  <tr>
                    <td>
                      <span class="query-text">{{ item.queryText }}</span>
                    </td>
                    <td nzAlign="center">
                      <nz-tag [nzColor]="item.resultCount > 0 ? 'blue' : 'default'">
                        {{ item.resultCount }} 条结果
                      </nz-tag>
                    </td>
                    <td nzAlign="center">{{ item.creationTime | date:'yyyy-MM-dd HH:mm' }}</td>
                    <td nzAlign="center">
                      <div class="row-actions">
                        <button
                          nz-button
                          nzType="text"
                          nzSize="small"
                          nz-tooltip
                          nzTooltipTitle="重新搜索"
                          (click)="reSearch(item.queryText)"
                          class="action-btn"
                          aria-label="重新搜索"
                        >
                          <span nz-icon nzType="redo" nzTheme="outline"></span>
                        </button>
                        <span nz-tooltip nzTooltipTitle="删除">
                          <button
                            nz-button
                            nzType="text"
                            nzSize="small"
                            nzDanger
                            nz-popconfirm
                            nzPopconfirmTitle="确认删除这条搜索记录？"
                            nzOkText="删除"
                            nzCancelText="取消"
                            (nzOnConfirm)="deleteHistoryItem(item.id)"
                            class="action-btn"
                            aria-label="删除"
                          >
                            <span nz-icon nzType="delete" nzTheme="outline"></span>
                          </button>
                        </span>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </nz-table>
          }
        </nz-spin>
      </nz-card>
    </div>
  `,
  styles: [`
    .search-history-container {
      padding: 24px;
    }

    .query-text {
      font-weight: 500;
      color: #333;
    }

    .row-actions {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 4px;
    }

    .action-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchHistoryComponent implements OnInit {
  private readonly searchService = inject(SearchService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  readonly history = signal<SearchHistoryDto[]>([]);
  readonly loading = signal(false);
  readonly totalCount = signal(0);

  pageIndex = 1;
  readonly pageSize = 20;

  ngOnInit() {
    this.loadHistory();
  }

  loadHistory() {
    this.loading.set(true);
    const skipCount = (this.pageIndex - 1) * this.pageSize;

    this.searchService.getMySearchHistory(skipCount, this.pageSize).subscribe({
      next: (data) => {
        this.history.set(data.items);
        this.totalCount.set(data.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载搜索历史失败');
      },
    });
  }

  onPageChange(index: number) {
    this.pageIndex = index;
    this.loadHistory();
  }

  deleteHistoryItem(id: string) {
    this.searchService.deleteMySearchHistory(id).subscribe({
      next: () => {
        this.message.success('删除成功');
        this.loadHistory();
      },
      error: () => {
        this.message.error('删除失败');
      },
    });
  }

  clearHistory() {
    this.searchService.clearMySearchHistory().subscribe({
      next: () => {
        this.message.success('搜索历史已清空');
        this.pageIndex = 1;
        this.loadHistory();
      },
      error: () => {
        this.message.error('清空失败');
      },
    });
  }

  reSearch(queryText: string) {
    if (!queryText.trim()) return;
    const target = this.router.url.startsWith('/student') ? '/student/search' : '/search';
    this.router.navigate([target], {
      queryParams: { q: queryText },
    });
  }
}
