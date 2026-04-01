import { Component, computed, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ConfigStateService } from '@abp/ng.core';
import { SearchService, SearchQueryDto, SearchResultDto, DocumentSearchResultDto, SearchHistoryDto, SearchStatsDto, PopularSearchDto, TopResourceDto, IndexStatusDto } from './search.service';

@Component({
  selector: 'app-search',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzInputModule,
    NzButtonModule,
    NzIconModule,
    NzSelectModule,
    NzCardModule,
    NzSpinModule,
    NzEmptyModule,
    NzTagModule,
    NzPaginationModule,
    NzDatePickerModule
  ],
  template: `
    <div class="search-container">
      <div class="search-header">
        <h1>文档搜索</h1>
        <div class="search-bar">
          <nz-input-group [nzPrefix]="prefixTemplate" nzSize="large">
            <input nz-input placeholder="输入关键词搜索文档..."
                   [(ngModel)]="searchQuery"
                   (keyup.enter)="search()"
                   nzSize="large" />
          </nz-input-group>
          <ng-template #prefixTemplate>
            <span nz-icon nzType="search"></span>
          </ng-template>
          <button nz-button nzType="primary" nzSize="large" (click)="search()" [nzLoading]="loading()">
            搜索
          </button>
        </div>

        <div class="search-filters">
          <nz-select [(ngModel)]="searchType" nzPlaceHolder="搜索类型" style="width: 150px;">
            <nz-option nzValue="keyword" nzLabel="关键词搜索"></nz-option>
            <nz-option nzValue="hybrid" nzLabel="混合搜索"></nz-option>
          </nz-select>

          <nz-select [(ngModel)]="selectedIndex" nzPlaceHolder="选择索引" style="width: 150px;">
            <nz-option nzValue="movie" nzLabel="电影"></nz-option>
            <nz-option nzValue="documents" nzLabel="文档"></nz-option>
          </nz-select>

          <nz-date-picker [(ngModel)]="startDate" nzPlaceHolder="开始日期"></nz-date-picker>
          <nz-date-picker [(ngModel)]="endDate" nzPlaceHolder="结束日期"></nz-date-picker>
        </div>
      </div>

      <div class="search-content">
        <nz-spin [nzSpinning]="loading()">
          @if (results().length === 0 && !loading()) {
            <nz-empty nzNotFoundContent="未找到搜索结果"></nz-empty>
          } @else {
            <div class="results-toolbar">
              <span class="results-count">找到 {{ filteredResults().length }} 个结果</span>
              <div class="ext-filter">
                <span class="ext-filter-label">按类型筛选：</span>
                <nz-tag
                  class="ext-tag"
                  [nzColor]="selectedFileExtension === '' ? 'blue' : ''"
                  (click)="selectedFileExtension = ''"
                >全部</nz-tag>
                @for (ext of availableExtensions(); track ext) {
                  <nz-tag
                    class="ext-tag"
                    [nzColor]="selectedFileExtension === ext ? 'blue' : ''"
                    (click)="selectedFileExtension = ext"
                  >{{ ext }}</nz-tag>
                }
              </div>
            </div>

            <div class="search-results">
              @for (result of filteredResults(); track result.resourceId + '-' + result.pageNumber) {
                <nz-card [nzTitle]="result.resourceName + ' - 第 ' + result.pageNumber + ' 页'"
                         [nzExtra]="extraTemplate"
                         class="result-card"
                         (click)="viewDocument(result)">
                  <p class="preview-text" [innerHTML]="result.highlightedContent || result.content"></p>
                  <nz-tag *ngIf="result.categoryName">{{ result.categoryName }}</nz-tag>
                  <nz-tag nzColor="green">{{ (result.relevanceScore * 100).toFixed(1) }} 分</nz-tag>
                  <ng-template #extraTemplate>
                    <span class="file-ext">{{ result.fileExtension }}</span>
                  </ng-template>
                </nz-card>
              }
            </div>

            <div class="pagination">
              <nz-pagination
                [(nzPageIndex)]="pageIndex"
                [nzTotal]="filteredResults().length"
                [nzPageSize]="pageSize"
                (nzPageIndexChange)="onPageChange()">
              </nz-pagination>
            </div>
          }
        </nz-spin>
      </div>
    </div>
  `,
  styles: [`
    .search-container {
      padding: 24px;
      max-width: 1200px;
      margin: 0 auto;
    }

    .search-header h1 {
      margin-bottom: 24px;
    }

    .search-bar {
      display: flex;
      gap: 12px;
      margin-bottom: 16px;
    }

    .search-bar nz-input-group {
      flex: 1;
    }

    .search-filters {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
    }

    .search-content {
      margin-top: 24px;
    }

    .results-toolbar {
      display: flex;
      align-items: center;
      gap: 16px;
      margin-bottom: 16px;
      flex-wrap: wrap;
    }

    .results-count {
      color: #666;
    }

    .ext-filter {
      display: flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
    }

    .ext-filter-label {
      color: #999;
      font-size: 13px;
    }

    .ext-tag {
      cursor: pointer;
    }

    .search-results {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .result-card {
      cursor: pointer;
      transition: box-shadow 0.3s;
    }

    .result-card:hover {
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }

    .preview-text {
      max-height: 100px;
      overflow: hidden;
      text-overflow: ellipsis;
      margin-bottom: 12px;
    }

    .file-ext {
      color: #999;
    }

    .pagination {
      margin-top: 24px;
      display: flex;
      justify-content: center;
    }
  `]
})
export class SearchComponent implements OnInit {
  private readonly searchService = inject(SearchService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly configService = inject(ConfigStateService);

  searchQuery = '';
  selectedFileExtension = '';
  searchType: 'keyword' | 'hybrid' = 'keyword';
  selectedIndex = 'movie';
  startDate: Date | null = null;
  endDate: Date | null = null;

  results = signal<DocumentSearchResultDto[]>([]);
  totalCount = signal(0);
  loading = signal(false);
  pageIndex = 1;
  pageSize = 20;

  availableExtensions = computed(() => {
    const exts = new Set<string>();
    for (const r of this.results()) {
      if (r.fileExtension) exts.add(r.fileExtension);
    }
    return [...exts].sort();
  });

  filteredResults = computed(() => {
    const all = this.results();
    if (!this.selectedFileExtension) return all;
    return all.filter(r => r.fileExtension === this.selectedFileExtension);
  });

  ngOnInit() {
    // Restore search state from navigation (returning from document viewer)
    const state = history.state as {
      searchState?: {
        query: string;
        results: DocumentSearchResultDto[];
        totalCount: number;
        pageIndex: number;
        selectedFileExtension: string;
        searchType: 'keyword' | 'hybrid';
        selectedIndex: string;
        startDate: string | null;
        endDate: string | null;
      }
    };

    if (state?.searchState) {
      const s = state.searchState;
      this.searchQuery = s.query;
      this.results.set(s.results);
      this.totalCount.set(s.totalCount);
      this.pageIndex = s.pageIndex;
      this.selectedFileExtension = s.selectedFileExtension;
      this.searchType = s.searchType;
      this.selectedIndex = s.selectedIndex;
      this.startDate = s.startDate ? new Date(s.startDate) : null;
      this.endDate = s.endDate ? new Date(s.endDate) : null;
      return;
    }

    const params = new URLSearchParams(window.location.search);
    const q = params.get('q');
    if (q) {
      this.searchQuery = q;
      this.search();
    }
  }

  search() {
    if (!this.searchQuery.trim()) return;

    this.loading.set(true);
    
    const query: SearchQueryDto = {
      query: this.searchQuery,
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: 'relevance',
      startDate: this.startDate ? this.startDate.toISOString() : undefined,
      endDate: this.endDate ? this.endDate.toISOString() : undefined,
      indexName: this.selectedIndex
    };

    const searchObservable = this.searchType === 'hybrid' 
      ? this.searchService.hybridSearch(query)
      : this.searchService.search(query);

    searchObservable.subscribe({
      next: (result: SearchResultDto) => {
        this.results.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('搜索失败');
      }
    });
  }

  onPageChange() {
    this.search();
  }

  viewDocument(result: DocumentSearchResultDto) {
    this.searchService.logView({
      resourceId: result.resourceId,
      pageNumber: result.pageNumber,
      viewDurationSeconds: 0,
      viewSource: 0
    }).subscribe({
      error: () => { /* ignore log view errors */ }
    });

    this.router.navigate(['/document-viewer', result.resourceId], {
      queryParams: { page: result.pageNumber },
      state: {
        content: result.highlightedContent || result.content,
        searchState: {
          query: this.searchQuery,
          results: this.results(),
          totalCount: this.totalCount(),
          pageIndex: this.pageIndex,
          selectedFileExtension: this.selectedFileExtension,
          searchType: this.searchType,
          selectedIndex: this.selectedIndex,
          startDate: this.startDate ? this.startDate.toISOString() : null,
          endDate: this.endDate ? this.endDate.toISOString() : null,
        }
      }
    });
  }

}
