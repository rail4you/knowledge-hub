import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
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
import { SearchService } from '../../proxy/application/search/search.service';
import type { PopularSearchDto, DocumentSearchResultDto, SearchQueryDto } from '../../proxy/application/contracts/search/dtos/models';
import { filterFuzzyFallback } from '../../search/search.util';

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
  readonly pageSize = 20;

  loading = signal(false);
  results = signal<DocumentSearchResultDto[]>([]);
  totalCount = signal(0);
  selectedFileExtension = signal('');
  /** 索引选择：all=文档+视频合并，documents=仅文档，videos=仅视频。后端 IndexName 为空时合并双索引。 */
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

  filteredResults = computed(() => {
    const all = this.results();
    const ext = this.selectedFileExtension();
    const extFiltered = !ext ? all : all.filter(r => r.fileExtension === ext);
    // 与教师端保持一致的搜索行为：
    //   - 没有正文 / 文件名命中时，隐藏所有 fuzzy（近似）结果
    //   - 有正文 / 文件名命中时，fuzzy 作为陪衬保留
    return filterFuzzyFallback(extFiltered, this.searchQuery);
  });

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
      // 学生端仅搜索已审核资源（documents 侧生效，videos 侧后端自动忽略）
      statusFilter: '2,3',
    };
    // 选“全部”时不传 indexName，后端合并 documents + videos 双索引；
    // 选文档/视频时只走单边。
    if (this.selectedIndex !== 'all') {
      input.indexName = this.selectedIndex;
    }

    this.searchService.search(input).subscribe({
      next: data => {
        this.results.set(data.items ?? []);
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
    if (result.sourceType === 'video') {
      this.openVideoModal(result);
      return;
    }

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
}
