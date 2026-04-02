import { Component, inject, signal, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
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
    NzPaginationModule,
    NzEmptyModule,
    NzButtonModule,
  ],
  template: `
    <div class="search-history-container">
      <nz-card nzTitle="搜索历史">
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
                  <th>结果数量</th>
                  <th>搜索时间</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                @for (item of history(); track item.id) {
                  <tr>
                    <td>
                      <span class="query-text">{{ item.queryText }}</span>
                    </td>
                    <td>
                      <nz-tag [nzColor]="item.resultCount > 0 ? 'blue' : 'default'">
                        {{ item.resultCount }} 条结果
                      </nz-tag>
                    </td>
                    <td>{{ item.creationTime | date:'yyyy-MM-dd HH:mm' }}</td>
                    <td>
                      <a (click)="reSearch(item.queryText)">重新搜索</a>
                    </td>
                  </tr>
                }
              </tbody>
            </nz-table>

            <div class="pagination-wrapper">
              <nz-pagination
                [(nzPageIndex)]="pageIndex"
                [nzTotal]="totalCount()"
                [nzPageSize]="pageSize"
                (nzPageIndexChange)="onPageChange($event)"
              ></nz-pagination>
            </div>
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

    .pagination-wrapper {
      margin-top: 16px;
      display: flex;
      justify-content: center;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchHistoryComponent implements OnInit {
  private readonly searchService = inject(SearchService);
  private readonly message = inject(NzMessageService);

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
        this.history.set(data);
        this.totalCount.set(data.length);
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

  reSearch(queryText: string) {
    if (!queryText.trim()) return;
    window.location.href = `/search?q=${encodeURIComponent(queryText)}`;
  }
}
