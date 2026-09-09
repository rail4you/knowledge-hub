import {
  Component,
  computed,
  inject,
  signal,
  OnInit,
  AfterViewInit,
  ViewChild,
  ElementRef,
  HostListener
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzMessageService } from 'ng-zorro-antd/message';
import { DocumentSearchResultDto } from '../search.service';
import { getFileIcon, getScoreColor, stripUuids } from '../search.util';

interface SearchStateSnapshot {
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

interface DetailPayload {
  result: DocumentSearchResultDto;
  searchState?: SearchStateSnapshot;
}

interface HitMatch {
  id: number;
  sentence: string;
  element: HTMLElement;
}

@Component({
  selector: 'app-search-detail',
  standalone: true,
  imports: [
    CommonModule,
    NzIconModule,
    NzTagModule,
    NzButtonModule,
    NzSpinModule,
    NzEmptyModule,
    NzTooltipModule,
    NzBreadCrumbModule,
    NzDividerModule
  ],
  templateUrl: './search-detail.component.html',
  styleUrl: './search-detail.component.scss'
})
export class SearchDetailComponent implements OnInit, AfterViewInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly message = inject(NzMessageService);

  @ViewChild('docPane', { static: false }) docPaneRef?: ElementRef<HTMLDivElement>;

  loading = signal(true);
  hasPayload = signal(false);

  result = signal<DocumentSearchResultDto | null>(null);
  searchState = signal<SearchStateSnapshot | null>(null);

  /** 左侧匹配列表里当前命中的索引 */
  activeIndex = computed(() => {
    const r = this.result();
    const all = this.searchState()?.results || [];
    if (!r) return -1;
    return all.findIndex(
      x => x.resourceId === r.resourceId && x.pageNumber === r.pageNumber
    );
  });

  /** 文档查看面板用的文本（清理过 UUID） */
  documentHtml = computed(() => {
    const r = this.result();
    if (!r) return '';
    const raw = r.highlightedContent || r.content || '';
    return stripUuids(raw);
  });

  /** 摘要里用的精简预览 */
  contextHtml = computed(() => {
    const r = this.result();
    if (!r) return '';
    const raw = r.highlightedContent || r.eventDescription || '';
    return stripUuids(raw);
  });

  fileIcon = computed(() => {
    const r = this.result();
    if (!r) return 'file';
    if (r.sourceType === 'video') return 'video-camera';
    return getFileIcon(r.fileExtension);
  });

  pageLabel = computed(() => {
    const r = this.result();
    if (!r) return '';
    if (r.sourceType === 'video') {
      const start = r.startTime || '';
      const end = r.endTime || '';
      if (start && end) return `${start} - ${end}`;
      return start || end || '视频片段';
    }
    return `第 ${r.pageNumber} 页`;
  });

  scorePercent = computed(() => {
    const r = this.result();
    if (!r) return 0;
    return Math.round((r.relevanceScore || 0) * 100);
  });

  scoreColor = computed(() => {
    const v = this.scorePercent();
    if (v >= 80) return 'green';
    if (v >= 50) return 'blue';
    if (v >= 30) return 'orange';
    return 'red';
  });

  isVideo = computed(() => this.result()?.sourceType === 'video');

  breadcrumb = computed(() => {
    const s = this.searchState();
    return {
      query: s?.query || '',
      hasQuery: !!s?.query
    };
  });

  // ─────────── 文档查看：高亮匹配项 ───────────
  hits = signal<HitMatch[]>([]);
  activeHit = signal<number>(-1);

  listItemIcon(ext: string, sourceType: string): string {
    if (sourceType === 'video') return 'video-camera';
    return getFileIcon(ext);
  }

  listItemPageLabel(item: DocumentSearchResultDto): string {
    if (item.sourceType === 'video') {
      if (item.startTime && item.endTime) return `${item.startTime} - ${item.endTime}`;
      return item.startTime || item.endTime || '视频片段';
    }
    return `第 ${item.pageNumber} 页`;
  }

  listItemScoreColor(score: number): string {
    return getScoreColor(score);
  }

  ngOnInit() {
    const state = (history.state || {}) as { detail?: DetailPayload };
    if (state.detail?.result) {
      this.applyPayload(state.detail);
      return;
    }
    const idFromUrl = this.route.snapshot.paramMap.get('id');
    if (idFromUrl) {
      this.hasPayload.set(false);
      this.loading.set(false);
      return;
    }
    this.loading.set(false);
    this.hasPayload.set(false);
  }

  ngAfterViewInit() {
    // 文档查看面板的高亮提取
    queueMicrotask(() => this.extractHits());
  }

  private applyPayload(payload: DetailPayload) {
    this.result.set(payload.result);
    this.searchState.set(payload.searchState || null);
    this.hasPayload.set(true);
    this.loading.set(false);
    // 切换详情后重置高亮
    queueMicrotask(() => {
      this.hits.set([]);
      this.activeHit.set(-1);
      this.extractHits();
    });
  }

  private extractHits() {
    const container = this.docPaneRef?.nativeElement;
    if (!container) return;

    const marks = container.querySelectorAll('.doc-text mark');
    if (marks.length === 0) {
      this.hits.set([]);
      return;
    }

    const matched: HitMatch[] = [];
    marks.forEach((mark, index) => {
      const el = mark as HTMLElement;
      el.setAttribute('data-hit-index', String(index + 1));
      matched.push({
        id: index + 1,
        sentence: this.extractSentence(el),
        element: el
      });
    });
    this.hits.set(matched);
  }

  private extractSentence(mark: Element): string {
    const parent = mark.parentElement;
    if (!parent) return (mark.textContent || '').trim();
    const text = parent.textContent || '';
    const markText = mark.textContent || '';
    const index = text.indexOf(markText);
    if (index === -1) return markText.trim();
    const start = Math.max(0, index - 30);
    const end = Math.min(text.length, index + markText.length + 30);
    let sentence = text.substring(start, end);
    if (start > 0) sentence = '…' + sentence;
    if (end < text.length) sentence = sentence + '…';
    return sentence.trim();
  }

  scrollToHit(id: number) {
    const container = this.docPaneRef?.nativeElement;
    if (!container) return;
    if (this.activeHit() > 0) {
      const prev = container.querySelector(`mark[data-hit-index="${this.activeHit()}"]`);
      prev?.classList.remove('hit-active');
    }
    this.activeHit.set(id);
    const mark = container.querySelector(`mark[data-hit-index="${id}"]`) as HTMLElement | null;
    if (!mark) return;
    mark.classList.add('hit-active');
    mark.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  /** 点击左侧匹配列表：切换右侧详情 */
  selectListItem(item: DocumentSearchResultDto) {
    if (
      item.resourceId === this.result()?.resourceId &&
      item.pageNumber === this.result()?.pageNumber
    ) {
      return;
    }
    this.result.set(item);

    const tree = this.router.createUrlTree(['/search/detail', item.resourceId]);
    this.router.navigateByUrl(tree, {
      replaceUrl: true,
      state: {
        detail: {
          result: item,
          searchState: this.searchState()
        }
      }
    });

    // 滚到右侧顶部，重新提取高亮
    queueMicrotask(() => {
      const el = document.querySelector('.doc-pane-scroll');
      if (el) (el as HTMLElement).scrollTo({ top: 0, behavior: 'smooth' });
      this.hits.set([]);
      this.activeHit.set(-1);
      this.extractHits();
    });
  }

  backToSearch() {
    const s = this.searchState();
    if (s) {
      this.router.navigate(['/search'], { state: { searchState: s } });
      return;
    }
    this.router.navigate(['/search']);
  }

  copySummary() {
    const r = this.result();
    if (!r) return;
    const lines: string[] = [];

    // 1. 搜索关键词与上下文
    const q = this.searchState()?.query?.trim();
    if (q) {
      lines.push(`【搜索关键词】${q}`);
    }

    // 2. 资源基本信息
    lines.push('');
    lines.push('【资源信息】');
    lines.push(`- 名称：${r.resourceName}`);
    lines.push(`- 类型：${this.isVideo() ? '视频' : '文档'}`);
    if (this.isVideo()) {
      if (r.startTime || r.endTime) {
        lines.push(`- 时间范围：${r.startTime || '-'} ~ ${r.endTime || '-'}`);
      }
      if (r.videoName) {
        lines.push(`- 视频名称：${r.videoName}`);
      }
    } else {
      lines.push(`- 页码：第 ${r.pageNumber} 页`);
      lines.push(`- 格式：${r.fileExtension || '-'}`);
    }
    if (r.categoryName) {
      lines.push(`- 分类：${r.categoryName}`);
    }
    lines.push(`- 相关度：${this.scorePercent()} / 100`);
    if (r.uploadDate) {
      const d = new Date(r.uploadDate);
      if (!Number.isNaN(d.getTime())) {
        lines.push(`- 上传时间：${this.formatDateTime(d)}`);
      }
    }

    // 3. 上下文预览（去除 HTML，保留可读文本）
    const preview = this.stripHtml(this.contextHtml() || '');
    if (preview) {
      lines.push('');
      lines.push('【匹配上下文】');
      lines.push(preview);
    }

    // 4. 视频补充
    if (this.isVideo() && r.eventDescription) {
      lines.push('');
      lines.push('【事件描述】');
      lines.push(r.eventDescription);
    }

    navigator.clipboard?.writeText(lines.join('\n')).then(
      () => this.message.success('摘要已复制到剪贴板'),
      () => this.message.error('复制失败，请手动选中复制')
    );
  }

  private formatDateTime(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  /** 把 HTML（带 <em> 高亮）转成纯文本，em 内容用 [ ] 包裹表示高亮 */
  private stripHtml(html: string): string {
    if (!html) return '';
    return html
      .replace(/<em>([\s\S]*?)<\/em>/gi, '『$1』')
      .replace(/<[^>]+>/g, '')
      .replace(/&nbsp;/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();
  }

  trackByResult = (_: number, item: DocumentSearchResultDto) =>
    item.resourceId + '-' + item.pageNumber;

  trackByHit = (_: number, hit: HitMatch) => hit.id;

  @HostListener('window:keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      this.backToSearch();
    }
  }
}
