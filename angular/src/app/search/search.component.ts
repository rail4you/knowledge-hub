import { Component, computed, effect, inject, signal, OnInit } from '@angular/core';
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
import { stripUuids, foldByResourceName, getMatchInfo, MatchType } from './search.util';
import { HostListener } from '@angular/core';

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

  /**
   * 用于渲染的最终结果：
   * 1. 按资源名（同 resourceId）折叠，保留最早 / pageNumber 最小的一条
   * 2. 叠加类型扩展前：折叠后仍需带来源信息，所以这里返回带 matchType 的副本
   */
  renderedResults = computed(() => {
    const base = this.filteredResults();
    const { folded } = foldByResourceName(base);
    const q = this.searchQuery;
    return folded.map(r => ({
      ...r,
      _match: getMatchInfo(r.resourceName, r.highlightedContent, r.eventDescription, q)
    }));
  });

  hiddenCount = signal(0);

  constructor() {
    // 同步折叠数（不能放在 computed 里写 signal）
    effect(() => {
      const { hiddenCount } = foldByResourceName(this.filteredResults());
      this.hiddenCount.set(hiddenCount);
    });
  }

  /** 匹配类型中文标签 */
  matchLabel(type: MatchType): string {
    if (type === 'content') return '正文命中';
    if (type === 'name') return '文件名命中';
    return '可能相关';
  }

  /** 匹配类型对应的小色标 */
  matchColor(type: MatchType): string {
    if (type === 'content') return 'green';
    if (type === 'name') return 'blue';
    return 'orange';
  }

  /** 匹配类型对应的图标 */
  matchIcon(type: MatchType): string {
    if (type === 'content') return 'highlight';
    if (type === 'name') return 'file-text';
    return 'question-circle';
  }

  // ──────────── 搜索历史 ────────────
  historyOpen = signal(false);
  historyLoading = signal(false);
  recentHistory = signal<SearchHistoryDto[]>([]);

  toggleHistory(event: MouseEvent) {
    event.stopPropagation();
    if (this.historyOpen()) {
      this.historyOpen.set(false);
    } else {
      this.historyOpen.set(true);
      this.loadRecentHistory();
    }
  }

  loadRecentHistory() {
    this.historyLoading.set(true);
    this.searchService.getMySearchHistory(0, 5).subscribe({
      next: data => {
        // 按搜索时间倒序，取前 5 条
        const sorted = [...(data.items || [])].sort(
          (a, b) => new Date(b.creationTime).getTime() - new Date(a.creationTime).getTime()
        );
        this.recentHistory.set(sorted.slice(0, 5));
        this.historyLoading.set(false);
      },
      error: () => {
        this.recentHistory.set([]);
        this.historyLoading.set(false);
      }
    });
  }

  applyHistory(query: string) {
    if (!query) return;
    this.searchQuery = query;
    this.historyOpen.set(false);
    this.search();
  }

  goToHistoryPage() {
    this.historyOpen.set(false);
    this.router.navigate(['/my/search-history']);
  }

  @HostListener('document:click')
  onDocumentClick() {
    if (this.historyOpen()) {
      this.historyOpen.set(false);
    }
  }

  isVideoModalOpen = signal(false);
  currentVideoUrl = signal('');
  currentVideoStartTime = signal('00:00:00');
  currentVideoEndTime = signal('');
  currentVideoName = signal('');
  currentVideoEventDescription = signal('');

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

  /**
   * 后端搜索结果中 CategoryName 字段实际上存的是 CategoryId（UUID）。
   * 当值是 UUID 形式时，直接判定为未填充，不渲染分类。
   */
  isCategoryId(value: string | null | undefined): boolean {
    if (!value) return true;
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim());
  }

  /**
   * 获取卡片预览文本：优先 Meili 高亮，否则显示精简的正文片段。
   * 高亮内容里可能含原始资源 ID（UUID），统一清理。
   */
  previewText(result: DocumentSearchResultDto): string {
    const raw =
      result.highlightedContent ||
      result.eventDescription ||
      this.truncateContent(result.content);
    return stripUuids(raw);
  }

  private truncateContent(content: string | null | undefined, maxLen = 240): string {
    if (!content) return '';
    const clean = content.replace(/\s+/g, ' ').trim();
    return clean.length > maxLen ? clean.slice(0, maxLen) + '…' : clean;
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

  enterDetail(result: DocumentSearchResultDto, event?: Event) {
    if (event) {
      event.stopPropagation();
    }

    // 视频：没有搜索详情页，保留原弹窗行为
    if (result.sourceType === 'video') {
      this.viewDocument(result);
      return;
    }

    // 文档：跳转到独立全屏搜索详情页 /search/detail/:id
    this.router.navigate(['/search/detail', result.resourceId], {
      state: {
        detail: {
          result,
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
      }
    });
  }

  /** 跳到资源库中该资源的详情（资源表格右侧边栏） */
  goToResource(result: DocumentSearchResultDto, event?: Event) {
    if (event) {
      event.stopPropagation();
    }
    if (!result.resourceId) return;
    if (this.router.url.startsWith('/student')) {
      this.router.navigate(['/student/resources', result.resourceId]);
    } else {
      this.router.navigate(['/resources'], {
        queryParams: { resourceId: result.resourceId }
      });
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
      // 教师/管理员端：进入搜索详情独立全屏页
      this.enterDetail(result);
    }
  }

}
