import { Component, ChangeDetectionStrategy, computed, effect, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { SearchService } from '../../proxy/application/search/search.service';
import { type DocumentSearchResultDto, type SearchQueryDto } from '../../search/search.service';
import type { PopularSearchDto } from '../../proxy/application/contracts/search/dtos/models';
import {
  stripUuids,
  foldByResourceName,
  getMatchInfo,
  MatchType
} from '../../search/search.util';

/** 文件扩展名 -> 图标（模块级常量表，避免模板每次变更检测重新创建对象） */
const FILE_ICONS: Record<string, string> = {
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
  '.mp4': 'video-camera',
  '.webm': 'video-camera',
  '.mov': 'video-camera',
  '.avi': 'video-camera',
};

@Component({
  selector: 'app-student-search',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzInputModule,
    NzButtonModule,
    NzIconModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzTagModule,
    NzPaginationModule,
    NzDividerModule,
    NzModalModule,
    NzTooltipModule,
  ],
  templateUrl: './student-search.component.html',
  styleUrls: ['./student-search.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentSearchComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly message = inject(NzMessageService);
  private readonly searchService = inject(SearchService);

  searchQuery = '';
  pageIndex = 1;
  readonly pageSize = 10;

  loading = signal(false);
  results = signal<DocumentSearchResultDto[]>([]);
  totalCount = signal(0);
  selectedFileExtension = signal('');
  /** 索引选择：all=文档+视频合并，documents=仅文档，videos=仅视频。后端 IndexName 为空时合并双索引。默认 'all'。 */
  selectedIndex: 'all' | 'documents' | 'videos' = 'all';

  // 视频播放弹窗
  isVideoModalOpen = signal(false);
  currentVideoUrl = signal('');
  currentVideoStartTime = signal('00:00:00');
  currentVideoEndTime = signal('');
  currentVideoName = signal('');
  currentVideoEventDescription = signal('');
  currentVideoResourceId = signal('');
  /** 浏览器无法解码/加载视频时显示 fallback 提示 */
  videoPlaybackError = signal(false);
  /** 大视频缓冲/加载中：显示"完整窗口 + loading"，就绪/播放后隐藏 */
  videoLoading = signal(false);

  // Hot words
  hotWords = signal<PopularSearchDto[]>([]);
  isHotWordsLoading = signal(false);

  availableExtensions = computed(() => {
    const exts = new Set<string>();
    for (const r of this.results()) {
      if (r.fileExtension) exts.add(r.fileExtension);
    }
    return [...exts].sort();
  });

  /**
   * 用于渲染的最终结果（与教师端保持一致）：
   * 1. 按类型扩展名过滤
   * 2. 按资源名折叠（同一资源只保留 pageNumber 最小的一条）
   * 3. 给每条结果标注匹配类型（content / name / fuzzy）
   * 4. 兜底：全是 fuzzy 时全部隐藏，避免弱相关结果淹没列表
   */
  renderedResults = computed(() => {
    const ext = this.selectedFileExtension();
    const all = this.results();
    const extFiltered = !ext ? all : all.filter(r => r.fileExtension === ext);

    const { folded } = foldByResourceName(extFiltered);
    const q = this.searchQuery;

    const annotated = folded.map(r => ({
      ...r,
      _match: getMatchInfo(r.resourceName, r.highlightedContent, r.eventDescription, q)
    }));

    const hasRealMatch = annotated.some(
      r => r._match.type === 'content' || r._match.type === 'name'
    );
    return hasRealMatch ? annotated : annotated.filter(r => r._match.type !== 'fuzzy');
  });

  /** 因同名而被折叠掉的条数（用于工具栏 "已折叠 X 条" 提示） */
  hiddenCount = signal(0);

  constructor() {
    // 同步折叠数（不能放在 computed 里写 signal）
    effect(() => {
      const ext = this.selectedFileExtension();
      const all = this.results();
      const extFiltered = !ext ? all : all.filter(r => r.fileExtension === ext);
      const { hiddenCount } = foldByResourceName(extFiltered);
      this.hiddenCount.set(hiddenCount);
    });
  }

  /** 匹配类型中文标签 */
  matchLabel(type: MatchType): string {
    if (type === 'content') return '正文命中';
    if (type === 'name') return '文件名命中';
    return '可能相关';
  }

  /** 匹配类型对应的图标 */
  matchIcon(type: MatchType): string {
    if (type === 'content') return 'highlight';
    if (type === 'name') return 'file-text';
    return 'question-circle';
  }

  ngOnInit() {
    this.loadHotWords();

    const q = this.route.snapshot.queryParamMap.get('q');
    if (q) {
      this.searchQuery = q;
      this.search();
    }
  }

  private loadHotWords() {
    this.isHotWordsLoading.set(true);
    this.searchService.getPopularSearches(30).subscribe({
      next: data => {
        this.hotWords.set(data ?? []);
        this.isHotWordsLoading.set(false);
      },
      error: () => {
        this.hotWords.set([]);
        this.isHotWordsLoading.set(false);
      },
    });
  }

  /** 点击热门词：直接触发搜索 */
  onHotWordClick(word: string | undefined) {
    if (word) {
      this.searchQuery = word;
      this.pageIndex = 1;
      this.search();
    }
  }

  search() {
    const q = this.searchQuery.trim();
    if (!q) {
      this.message.warning('请输入搜索关键词');
      return;
    }

    this.loading.set(true);

    const input: SearchQueryDto = {
      query: q,
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: 'relevance',
      // 学生端仅搜索联盟审核通过的资源（与资源列表口径一致；documents 侧生效，videos 侧后端自动忽略）
      statusFilter: '3',
    };
    // 选"全部"时不传 indexName，后端合并 documents + videos 双索引；
    // 选文档/视频时只走单边。
    if (this.selectedIndex !== 'all') {
      input.indexName = this.selectedIndex;
    }

    this.searchService.search(input).subscribe({
      next: data => {
        // 后端 items 使用生成代理的可选字段类型，本组件内部的 renderedResults / foldByResourceName
        // 要求 resourceId / pageNumber 为必填，这里按手写的 DTO 形状断言以满足类型检查。
        this.results.set((data.items ?? []) as unknown as DocumentSearchResultDto[]);
        this.totalCount.set(data.totalCount ?? 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('搜索失败');
      },
    });
  }

  onKeyEnter(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.pageIndex = 1;
      this.search();
    }
  }

  onPageChange() {
    this.search();
  }

  viewDocument(result: DocumentSearchResultDto) {
    // 视频：保留弹窗播放（学生端也支持在线预览片段）
    if (result.sourceType === 'video') {
      this.openVideoModal(result);
      return;
    }

    // 文档：仅跳转资源详情页（学生端不做高亮搜索详情）
    this.goToResource(result);
  }

  /** 跳转到学生端资源详情（文档 / 视频共用，视频详情页支持在线预览播放） */
  goToResource(result: DocumentSearchResultDto, event?: Event) {
    if (event) event.stopPropagation();
    if (!result.resourceId) return;

    this.searchService.logView({
      resourceId: result.resourceId,
      pageNumber: result.pageNumber,
      viewDurationSeconds: 0,
      viewSource: 0,
    }).subscribe({ error: () => {} });

    this.router.navigate(['/student/resources', result.resourceId], {
      queryParams: { page: result.pageNumber, from: 'search' }
    });
  }

  getFileIcon(ext: string): string {
    return FILE_ICONS[ext?.toLowerCase()] || 'file';
  }

  isVideoResult(result: DocumentSearchResultDto): boolean {
    return result.sourceType === 'video';
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

  openVideoModal(result: DocumentSearchResultDto) {
    const resourceId: string = result.resourceId || '';
    // 固定走同源资源预览流：经 /api 代理携带认证 cookie、支持 Range 定位片段，
    // 实测 200 + video/mp4 + 206。索引里的 videoUrl（如 /uploads/...）在 Angular
    // 开发服务器下没有代理会 404 黑屏，线上也可能跨域无 cookie，所以不再使用。
    this.currentVideoUrl.set(resourceId ? `/api/resource-file/${resourceId}/preview` : '');
    this.currentVideoStartTime.set(result.startTime || '00:00:00');
    this.currentVideoEndTime.set(result.endTime || '');
    this.currentVideoName.set(result.videoName || result.resourceName || '视频');
    this.currentVideoEventDescription.set(result.eventDescription || result.highlightedContent || '');
    this.currentVideoResourceId.set(resourceId);
    this.videoPlaybackError.set(false);
    // 大视频 metadata/首帧未就绪前先显示"完整窗口 + loading"，避免部分窗口闪烁
    this.videoLoading.set(true);
    this.isVideoModalOpen.set(true);
  }

  closeVideoModal() {
    this.isVideoModalOpen.set(false);
    // 清空 src 让弹窗关闭后立即停播，避免后台继续播放声音
    this.currentVideoUrl.set('');
    this.videoPlaybackError.set(false);
    this.videoLoading.set(false);
  }

  onVideoError() {
    this.videoPlaybackError.set(true);
  }

  /** 视频已缓冲到可播放的数据（loadeddata/canplay）→ 收起 loading */
  onVideoReady() {
    this.videoLoading.set(false);
  }

  /** 视频进入缓冲等待（waiting/stalled）→ 显示 loading，避免"一直卡着"没有反馈 */
  onVideoWaiting() {
    this.videoLoading.set(true);
  }

  /** 视频真正开始播放（playing）→ 收起 loading */
  onVideoPlaying() {
    this.videoLoading.set(false);
  }

  /** 从视频弹窗跳转到学生端视频资源详情（在线预览/收藏/评价） */
  goToVideoResource(event?: Event) {
    if (event) event.stopPropagation();
    const id = this.currentVideoResourceId();
    if (!id) return;
    this.closeVideoModal();
    this.router.navigate(['/student/resources', id], {
      queryParams: { from: 'search' }
    });
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

  getScoreColor(score: number): string {
    if (score >= 0.8) return 'green';
    if (score >= 0.5) return 'blue';
    if (score >= 0.3) return 'orange';
    return 'red';
  }

  /**
   * 后端搜索结果中的 CategoryName 实际存的是 CategoryId（UUID）。
   * 值是 UUID 形式时视为未填充，不渲染分类标签。
   */
  isCategoryId(value: string | null | undefined): boolean {
    if (!value) return true;
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim());
  }

  /** 视频结果辅助：从任意带 _match 字段的结果上安全取出 startTime / endTime */
  asVideoTime(result: DocumentSearchResultDto): { startTime?: string; endTime?: string } {
    return {
      startTime: (result as { startTime?: string }).startTime,
      endTime: (result as { endTime?: string }).endTime,
    };
  }
}
