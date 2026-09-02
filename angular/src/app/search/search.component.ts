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
import { NzRateModule } from 'ng-zorro-antd/rate';
import { ConfigStateService, EnvironmentService } from '@abp/ng.core';
import { SearchService, SearchQueryDto, SearchResultDto, DocumentSearchResultDto, SearchHistoryDto, SearchStatsDto, PopularSearchDto, TopResourceDto, IndexStatusDto } from './search.service';
import { MeiliSearchAdminService, MeiliIndexDto } from '../admin/meilisearch/meilisearch-admin.service';
import { ResourceReviewComponent } from './resource-review/resource-review.component';

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
    NzModalModule,
    NzRateModule,
    ResourceReviewComponent
  ],
  templateUrl: './search.component.html',
  styleUrl: './search.component.scss',
})
export class SearchComponent implements OnInit {
  private readonly searchService = inject(SearchService);
  private readonly adminService = inject(MeiliSearchAdminService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly configService = inject(ConfigStateService);
  private readonly environmentService = inject(EnvironmentService);

  searchQuery = '';
  selectedFileExtension = signal('');
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
    const ext = this.selectedFileExtension();
    if (!ext) return all;
    return all.filter(r => r.fileExtension === ext);
  });

  isVideoModalOpen = signal(false);
  currentVideoUrl = signal('');
  currentVideoStartTime = signal('00:00:00');
  currentVideoEndTime = signal('');
  currentVideoName = signal('');
  currentVideoEventDescription = signal('');

  isReviewModalOpen = signal(false);
  reviewResourceId = signal('');
  reviewResourceName = signal('');

  getIndexLabel(indexUid: string | null | undefined): string {
    const normalized = (indexUid || '').toLowerCase();
    if (normalized === 'documents') {
      return '文档';
    }
    if (normalized === 'videos') {
      return '视频';
    }
    if (normalized === 'images') {
      return '图片';
    }
    if (normalized === 'audios') {
      return '音频';
    }
    return indexUid || '默认索引';
  }

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
      this.selectedFileExtension.set(s.selectedFileExtension);
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
      indexName: this.selectedIndex,
      // 学生端仅搜索已审核资源
      statusFilter: this.router.url.startsWith('/student') ? '2,3' : undefined,
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

  openReviewModal(resourceId: string, resourceName: string, event: Event) {
    event.stopPropagation();
    this.reviewResourceId.set(resourceId);
    this.reviewResourceName.set(resourceName);
    this.isReviewModalOpen.set(true);
  }

  closeReviewModal() {
    this.isReviewModalOpen.set(false);
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
    if (result.resourceId && result.sourceType !== 'video') {
      this.searchService.logView({
        resourceId: result.resourceId,
        pageNumber: result.pageNumber,
        viewDurationSeconds: 0,
        viewSource: 0
      }).subscribe({
        error: () => { /* ignore log view errors */ }
      });
    }

    if (result.sourceType === 'video') {
      this.openVideoModal(result);
    } else if (this.router.url.startsWith('/student')) {
      // 学生端：跳转到资源详情页
      this.router.navigate(['/student/resources', result.resourceId]);
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
            selectedFileExtension: this.selectedFileExtension(),
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
