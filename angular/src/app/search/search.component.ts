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
import { ConfigStateService } from '@abp/ng.core';
import { SearchService, SearchQueryDto, SearchResultDto, DocumentSearchResultDto, SearchHistoryDto, SearchStatsDto, PopularSearchDto, TopResourceDto, IndexStatusDto } from './search.service';
import { MeiliSearchAdminService, MeiliIndexDto } from '../admin/meilisearch/meilisearch-admin.service';
import { stripUuids, foldByResourceName, getMatchInfo, filterFuzzyFallback, MatchType } from './search.util';
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

  searchQuery = '';
  selectedFileExtension = signal('');
  searchType: 'keyword' | 'hybrid' = 'keyword';
  /**
   * 索引选择：
   *   - 'all'：全部（不传 indexName，后端合并 documents + videos 双索引）
   *   - 其它：Meili 索引 uid（单边搜索）
   * 默认 'all'，覆盖历史 'documents' 单边默认值，避免漏检视频时间轴。
   */
  selectedIndex: string = 'all';
  startDate: Date | null = null;
  endDate: Date | null = null;

  /** 用于 ngFor 渲染：'all' 作为首项固定显示，后接 availableIndexes() 动态项 */
  readonly ALL_INDEX_OPTION = 'all' as const;

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

    // 给每条结果标注匹配类型，再做"全是 fuzzy 时过滤"的兜底：
    //   - 有正文 / 文件名命中：所有 fuzzy 作为陪衬保留
    //   - 只有 fuzzy：近似结果全部隐藏，避免刷出大量弱相关资源
    const annotated = folded.map(r => ({
      ...r,
      _match: getMatchInfo(r.resourceName, r.highlightedContent, r.eventDescription, q)
    }));
    const hasRealMatch = annotated.some(
      r => r._match.type === 'content' || r._match.type === 'name'
    );
    return hasRealMatch ? annotated : annotated.filter(r => r._match.type !== 'fuzzy');
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
  currentVideoResourceId = signal('');
  /** 浏览器无法解码/加载视频时显示 fallback 提示 */
  videoPlaybackError = signal(false);

  getIndexLabel(indexUid: string | null | undefined): string {
    // 'all' 是 UI 层的哨兵值，代表“全部索引”
    if (indexUid === 'all') return '全部';
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

  /**
   * 把 Date 格式化为本地日期字符串 yyyy-MM-dd。
   * 不要用 toISOString()——它会把日期按 UTC 转换，东八区用户选 "2026-01-15"
   * 会变成 "2026-01-14"，导致后端 Meili filter 把"开始日期"提前一天、
   * "结束日期"也提前一天，最近一天的资源全部被过滤掉。
   */
  private formatLocalDate(d: Date | null | undefined): string | undefined {
    if (!d) return undefined;
    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}`;
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
      // 从详情页返回时，还原的是本地日期字符串 "yyyy-MM-dd"，
      // 拼成 T00:00:00 让 Date 解析为本地零点，避免 UTC 转换导致的日期偏移
      this.startDate = s.startDate ? new Date(`${s.startDate}T00:00:00`) : null;
      this.endDate = s.endDate ? new Date(`${s.endDate}T00:00:00`) : null;
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
        // 'all' 是固定首项，永远保留；若用户当前未选 'all'，但列表里没有对应 uid，
        // 才回退到第一个真实索引，避免用户主动选“全部”后被这里静默改成单边。
        if (
          this.selectedIndex !== 'all' &&
          indexes.length > 0 &&
          !indexes.find(i => i.uid === this.selectedIndex)
        ) {
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
    
    // 'all' = 不传 indexName，让后端走 documents + videos 双索引合并逻辑；
    // 其它值 = 走单边索引（与后端 SearchAsync/HybridSearchAsync 行为一致）。
    const isAll = this.selectedIndex === 'all';

    const query: SearchQueryDto = {
      query: this.searchQuery,
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: 'relevance',
      startDate: this.formatLocalDate(this.startDate),
      endDate: this.formatLocalDate(this.endDate),
      indexName: isAll ? undefined : this.selectedIndex,
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
    const resourceId = result.resourceId || '';
    // 固定走同源资源预览流：开发走 :4200 的 /api 代理、线上走 nginx 的 /api 代理，
    // 同源携带认证 cookie、支持 Range 定位片段。索引里的 videoUrl（如 /uploads/...）
    // 若拼成 API 绝对地址，线上 HTTPS 页会因 mixed-content 被拦截、开发环境会因
    // 自签名证书加载失败，所以不再使用。
    this.currentVideoUrl.set(resourceId ? `/api/resource-file/${resourceId}/preview` : '');
    this.currentVideoStartTime.set(result.startTime || '00:00:00');
    this.currentVideoEndTime.set(result.endTime || '');
    this.currentVideoName.set(result.videoName || result.resourceName || '视频');
    this.currentVideoEventDescription.set(result.eventDescription || '');
    this.currentVideoResourceId.set(resourceId);
    this.videoPlaybackError.set(false);
    this.isVideoModalOpen.set(true);
  }

  closeVideoModal() {
    this.isVideoModalOpen.set(false);
    // 清空 src 让弹窗关闭后立即停播，避免后台继续播放声音
    this.currentVideoUrl.set('');
    this.videoPlaybackError.set(false);
  }

  onVideoError() {
    this.videoPlaybackError.set(true);
  }

  /** 从视频弹窗跳转到该资源的资源库详情 */
  goToCurrentVideoResource(event?: Event) {
    if (event) event.stopPropagation();
    const id = this.currentVideoResourceId();
    if (!id) return;
    this.closeVideoModal();
    if (this.router.url.startsWith('/student')) {
      this.router.navigate(['/student/resources', id]);
    } else {
      this.router.navigate(['/resources'], { queryParams: { resourceId: id } });
    }
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
            startDate: this.formatLocalDate(this.startDate) ?? null,
            endDate: this.formatLocalDate(this.endDate) ?? null,
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
