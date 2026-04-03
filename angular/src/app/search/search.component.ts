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
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { ConfigStateService, EnvironmentService } from '@abp/ng.core';
import { SearchService, SearchQueryDto, SearchResultDto, DocumentSearchResultDto, SearchHistoryDto, SearchStatsDto, PopularSearchDto, TopResourceDto, IndexStatusDto } from './search.service';
import { MeiliSearchAdminService, MeiliIndexDto } from '../admin/meilisearch/meilisearch-admin.service';

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
    NzDatePickerModule,
    NzTooltipModule,
    NzDividerModule,
    NzModalModule
  ],
  template: `
    <div class="search-container">
      <div class="search-header">
        <h1>文档搜索</h1>

        <div class="search-bar">
          <nz-input-group [nzPrefix]="prefixTemplate" nzSize="large" class="search-input-wrapper">
            <input nz-input placeholder="输入关键词搜索文档..."
                   [(ngModel)]="searchQuery"
                   (keyup.enter)="search()"
                   nzSize="large" />
          </nz-input-group>
          <ng-template #prefixTemplate>
            <span nz-icon nzType="search" style="color: #bfbfbf; font-size: 18px;"></span>
          </ng-template>
          <button nz-button nzType="primary" nzSize="large" (click)="search()" [nzLoading]="loading()" class="search-btn">
            <span nz-icon nzType="search"></span>
            搜索
          </button>
        </div>

        <div class="search-filters">
          <div class="filter-group">
            <label class="filter-label">
              <span nz-icon nzType="search" style="margin-right: 4px;"></span>
              搜索类型
            </label>
            <nz-select [(ngModel)]="searchType" class="filter-select">
              <nz-option nzValue="keyword" nzLabel="关键词搜索"></nz-option>
              <nz-option nzValue="hybrid" nzLabel="混合搜索"></nz-option>
            </nz-select>
          </div>

          <div class="filter-group">
            <label class="filter-label">
              <span nz-icon nzType="database" style="margin-right: 4px;"></span>
              搜索索引
            </label>
            <nz-select [(ngModel)]="selectedIndex" class="filter-select">
              @for (idx of availableIndexes(); track idx.uid) {
                <nz-option [nzValue]="idx.uid" [nzLabel]="idx.uid"></nz-option>
              }
            </nz-select>
          </div>

          <div class="filter-group">
            <label class="filter-label">
              <span nz-icon nzType="calendar" style="margin-right: 4px;"></span>
              时间范围
            </label>
            <div class="date-range">
              <nz-date-picker [(ngModel)]="startDate" nzPlaceHolder="开始日期" nzSize="default"></nz-date-picker>
              <span class="date-separator">至</span>
              <nz-date-picker [(ngModel)]="endDate" nzPlaceHolder="结束日期" nzSize="default"></nz-date-picker>
            </div>
          </div>
        </div>
      </div>

      <div class="search-content">
        <nz-spin [nzSpinning]="loading()">
          @if (results().length === 0 && !loading() && searchQuery) {
            <nz-empty nzNotFoundContent="未找到搜索结果"></nz-empty>
          } @else if (results().length === 0 && !loading()) {
            <div class="search-empty-hint">
              <span nz-icon nzType="search" style="font-size: 48px; color: #d9d9d9;"></span>
              <p>输入关键词开始搜索文档</p>
            </div>
          } @else {
            <div class="results-toolbar">
              <span class="results-count">找到 <strong>{{ filteredResults().length }}</strong> 个结果</span>
              <nz-divider nzType="vertical"></nz-divider>
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
                <div class="result-card" (click)="viewDocument(result)">
                  <div class="result-header">
                    <div class="result-title">
                      <span nz-icon [nzType]="result.sourceType === 'video' ? 'video-camera' : getFileIcon(result.fileExtension)" class="result-icon"></span>
                      <span class="result-name">{{ result.resourceName }}</span>
                      @if (result.sourceType === 'video') {
                        <nz-tag class="result-page-tag" nzColor="purple">
                          {{ result.startTime || '' }}
                          {{ result.startTime && result.endTime ? ' - ' : '' }}
                          {{ result.endTime || '' }}
                        </nz-tag>
                      } @else {
                        <nz-tag class="result-page-tag">第 {{ result.pageNumber }} 页</nz-tag>
                      }
                    </div>
                    <div class="result-meta">
                      <nz-tag *ngIf="result.categoryName" nzColor="blue">{{ result.categoryName }}</nz-tag>
                      <nz-tag [nzColor]="getScoreColor(result.relevanceScore)">
                        {{ (result.relevanceScore * 100).toFixed(0) }} 分
                      </nz-tag>
                      @if (result.sourceType === 'video') {
                        <span class="file-ext-badge">视频</span>
                      } @else {
                        <span class="file-ext-badge">{{ result.fileExtension }}</span>
                      }
                    </div>
                  </div>
                  <div class="result-content">
                    <p class="preview-text" [innerHTML]="result.highlightedContent || result.eventDescription || result.content"></p>
                  </div>
                </div>
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

    <nz-modal
      [nzVisible]="isVideoModalOpen()"
      [nzTitle]="currentVideoName()"
      [nzWidth]="900"
      [nzFooter]="videoModalFooter"
      (nzOnCancel)="closeVideoModal()"
      nzMaskClosable="false"
    >
      <ng-container *nzModalContent>
        <div class="video-player-container">
          <video 
            #videoPlayer
            controls 
            [src]="currentVideoUrl()" 
            style="width: 100%; max-height: 70vh;"
            (loadedmetadata)="onVideoMetadataLoaded(videoPlayer)"
          ></video>
          <div class="video-info" *ngIf="currentVideoEventDescription()">
            <h4>事件描述</h4>
            <p>{{ currentVideoEventDescription() }}</p>
          </div>
          <div class="video-timeline" *ngIf="currentVideoStartTime() && currentVideoEndTime()">
            <span class="timeline-label">时间范围：</span>
            <span class="timeline-value">{{ currentVideoStartTime() }} - {{ currentVideoEndTime() }}</span>
          </div>
        </div>
      </ng-container>
      <ng-template #videoModalFooter>
        <div class="video-modal-footer">
          <button nz-button nzType="default" (click)="returnToSearch()">
            <span nz-icon nzType="left"></span>
            返回搜索界面
          </button>
          <button nz-button nzType="primary" (click)="closeVideoModal()">
            关闭
          </button>
        </div>
      </ng-template>
    </nz-modal>
  `,
  styles: [`
    .search-container {
      padding: 32px;
      max-width: 1000px;
      margin: 0 auto;
    }

    .search-header h1 {
      margin-bottom: 28px;
      font-size: 24px;
      font-weight: 500;
      color: #1a1a1a;
    }

    .search-bar {
      display: flex;
      gap: 12px;
      margin-bottom: 20px;
    }

    .search-input-wrapper {
      flex: 1;
    }

    .search-btn {
      min-width: 100px;
    }

    .search-filters {
      display: flex;
      gap: 20px;
      flex-wrap: wrap;
      padding: 16px 20px;
      background: #fafafa;
      border-radius: 8px;
      border: 1px solid #f0f0f0;
    }

    .filter-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .filter-label {
      font-size: 13px;
      color: #8c8c8c;
      font-weight: 500;
      display: flex;
      align-items: center;
    }

    .filter-select {
      min-width: 160px;
    }

    .date-range {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .date-separator {
      color: #bbb;
      font-size: 13px;
    }

    .search-content {
      margin-top: 24px;
    }

    .search-empty-hint {
      text-align: center;
      padding: 60px 0;
      color: #bfbfbf;
    }

    .search-empty-hint p {
      margin-top: 16px;
      font-size: 14px;
      color: #999;
    }

    .results-toolbar {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
      flex-wrap: wrap;
    }

    .results-count {
      color: #666;
      font-size: 14px;
    }

    .results-count strong {
      color: #1890ff;
      font-size: 16px;
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
      gap: 12px;
    }

    .result-card {
      cursor: pointer;
      border: 1px solid #f0f0f0;
      border-radius: 8px;
      padding: 16px 20px;
      transition: all 0.2s ease;
      background: #fff;
    }

    .result-card:hover {
      border-color: #1890ff;
      box-shadow: 0 2px 12px rgba(24, 144, 255, 0.12);
    }

    .result-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 10px;
    }

    .result-title {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .result-icon {
      color: #1890ff;
      font-size: 16px;
    }

    .result-name {
      font-size: 15px;
      font-weight: 500;
      color: #333;
    }

    .result-page-tag {
      font-size: 12px;
    }

    .result-meta {
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .file-ext-badge {
      font-size: 12px;
      color: #999;
      background: #f5f5f5;
      padding: 1px 8px;
      border-radius: 4px;
    }

    .result-content {
      border-top: 1px solid #f5f5f5;
      padding-top: 10px;
    }

    .preview-text {
      max-height: 80px;
      overflow: hidden;
      text-overflow: ellipsis;
      display: -webkit-box;
      -webkit-line-clamp: 3;
      -webkit-box-orient: vertical;
      font-size: 13px;
      color: #666;
      line-height: 1.6;
      margin: 0;
    }

    .pagination {
      margin-top: 24px;
      display: flex;
      justify-content: center;
    }

    .video-player-container {
      padding: 16px 0;
    }

    .video-info {
      margin-top: 16px;
      padding: 12px;
      background: #f5f5f5;
      border-radius: 6px;
    }

    .video-info h4 {
      margin: 0 0 8px 0;
      font-size: 14px;
      color: #333;
    }

    .video-info p {
      margin: 0;
      font-size: 14px;
      color: #666;
      line-height: 1.5;
    }

    .video-timeline {
      margin-top: 12px;
      padding: 8px 12px;
      background: #e6f7ff;
      border-radius: 4px;
      font-size: 13px;
    }

    .timeline-label {
      color: #1890ff;
      font-weight: 500;
    }

    .timeline-value {
      color: #333;
    }

    .video-modal-footer {
      display: flex;
      justify-content: space-between;
      width: 100%;
    }
  `]
})
export class SearchComponent implements OnInit {
  private readonly searchService = inject(SearchService);
  private readonly adminService = inject(MeiliSearchAdminService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly configService = inject(ConfigStateService);
  private readonly environmentService = inject(EnvironmentService);

  searchQuery = '';
  selectedFileExtension = '';
  searchType: 'keyword' | 'hybrid' = 'keyword';
  selectedIndex = 'documents';
  startDate: Date | null = null;
  endDate: Date | null = null;
  
  availableIndexes = signal<MeiliIndexDto[]>([]);

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

  isVideoModalOpen = signal(false);
  currentVideoUrl = signal('');
  currentVideoStartTime = signal('00:00:00');
  currentVideoEndTime = signal('');
  currentVideoName = signal('');
  currentVideoEventDescription = signal('');

  getFileIcon(ext: string): string {
    const iconMap: Record<string, string> = {
      '.pdf': 'file-pdf',
      '.doc': 'file-word',
      '.docx': 'file-word',
      '.xls': 'file-excel',
      '.xlsx': 'file-excel',
      '.ppt': 'file-ppt',
      '.pptx': 'file-ppt',
      '.txt': 'file-text',
      '.md': 'file-text',
      '.jpg': 'file-image',
      '.jpeg': 'file-image',
      '.png': 'file-image',
    };
    return iconMap[ext?.toLowerCase()] || 'file';
  }

  getScoreColor(score: number): string {
    if (score >= 0.8) return 'green';
    if (score >= 0.5) return 'blue';
    if (score >= 0.3) return 'orange';
    return 'red';
  }

  ngOnInit() {
    this.loadIndexes();
    
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

  loadIndexes() {
    this.adminService.getIndexes().subscribe({
      next: (indexes) => {
        this.availableIndexes.set(indexes);
        if (indexes.length > 0 && !indexes.find(i => i.uid === this.selectedIndex)) {
          this.selectedIndex = indexes[0].uid;
        }
      },
      error: () => {
        this.availableIndexes.set([]);
      }
    });
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

  openVideoModal(result: DocumentSearchResultDto) {
    const env = this.environmentService.getEnvironment();
    const baseUrl = env?.apis?.default?.url || '';
    let videoUrl = result.videoUrl || '';
    if (videoUrl && !videoUrl.startsWith('http')) {
      videoUrl = baseUrl + videoUrl;
    }
    this.currentVideoUrl.set(videoUrl);
    this.currentVideoStartTime.set(result.startTime || '00:00:00');
    this.currentVideoEndTime.set(result.endTime || '');
    this.currentVideoName.set(result.videoName || result.resourceName || '视频');
    this.currentVideoEventDescription.set(result.eventDescription || '');
    this.isVideoModalOpen.set(true);
  }

  closeVideoModal() {
    this.isVideoModalOpen.set(false);
  }

  returnToSearch() {
    this.closeVideoModal();
  }

  formatTimeToSeconds(time: string): number {
    if (!time) return 0;
    const parts = time.split(':').map(Number);
    if (parts.length === 3) {
      return parts[0] * 3600 + parts[1] * 60 + parts[2];
    } else if (parts.length === 2) {
      return parts[0] * 60 + parts[1];
    }
    return 0;
  }

  onVideoMetadataLoaded(videoPlayer: HTMLVideoElement) {
    const startSeconds = this.formatTimeToSeconds(this.currentVideoStartTime());
    if (startSeconds > 0 && startSeconds < videoPlayer.duration) {
      videoPlayer.currentTime = startSeconds;
    }
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

    if (result.sourceType === 'video' && result.videoUrl) {
      this.openVideoModal(result);
      return;
    }

    if (result.sourceType === 'video') {
      this.openVideoModal(result);
    } else {
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

}
