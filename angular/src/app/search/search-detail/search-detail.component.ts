import {
  Component,
  computed,
  inject,
  signal,
  OnInit,
  OnDestroy,
  AfterViewInit,
  ViewChild,
  ElementRef,
  HostListener
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzMessageService } from 'ng-zorro-antd/message';
import { DocumentSearchResultDto, SearchService } from '../search.service';
import { getFileIcon, stripUuids, displayExt } from '../search.util';
import { AuthFetchService } from '../../shared/utils/auth-fetch.service';

interface SearchStateSnapshot {
  query: string;
  results: DocumentSearchResultDto[];
  totalCount: number;
  pageIndex: number;
  selectedFileExtension: string;
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

/** UUID 形态：搜索结果里的 categoryName 存的其实是 categoryId */
const UUID_LIKE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

@Component({
  selector: 'app-search-detail',
  standalone: true,
  imports: [
    CommonModule,
    NzIconModule,
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
export class SearchDetailComponent implements OnInit, OnDestroy, AfterViewInit {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly message = inject(NzMessageService);
  private readonly authFetch = inject(AuthFetchService);
  private readonly searchService = inject(SearchService);

  /** 文档滚动时用于判断当前命中项（左侧列表自动高亮） */
  private scrollRaf = 0;
  /** 用户是否真的滚动过正文：布局变动也会发 scroll 事件，不区分会在首屏就误选一个命中项 */
  private userScrolled = false;

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

  /** 文件格式（不带前导点）：fileExtension 在不同来源下形态不一致（"pdf" / ".pdf"） */
  formatLabel = computed(() => displayExt(this.result()?.fileExtension));

  scorePercent = computed(() => {
    const r = this.result();
    if (!r) return 0;
    return Math.round((r.relevanceScore || 0) * 100);
  });

  /** 分档后的相关度，用于配色 */
  scoreLevel = computed(() => {
    const v = this.scorePercent();
    if (v >= 80) return 'high';
    if (v >= 50) return 'medium';
    if (v >= 30) return 'low';
    return 'weak';
  });

  isVideo = computed(() => this.result()?.sourceType === 'video');

  /**
   * 分类名称。
   * 搜索索引里 categoryName 这个字段实际存的是 categoryId（UUID），不是分类名，
   * 直接展示会变成一串乱码般的 GUID —— 所以先过滤掉 UUID 形态的值。
   * 后端回填的 categoryTitle 才是真正的分类名，优先使用。
   */
  categoryLabel = computed(() => {
    const title = (this.result()?.categoryTitle || '').trim();
    if (title) return title;
    const raw = (this.result()?.categoryName || '').trim();
    if (!raw) return '';
    return UUID_LIKE.test(raw) ? '' : raw;
  });

  // ─────────── 资源背景元数据（摘要 / 描述 / 关键词 / 属性） ───────────

  /** AI 生成的资源摘要 */
  summary = computed(() => (this.result()?.summary || '').trim());

  /** 资源描述 */
  description = computed(() => (this.result()?.description || '').trim());

  /** 关键词：后端存的是逗号分隔字符串，这里拆成标签数组 */
  keywordList = computed(() => {
    const raw = (this.result()?.keywords || '').trim();
    if (!raw) return [];
    return Array.from(
      new Set(
        raw
          .split(/[,，;；、\s]+/)
          .map(x => x.trim())
          .filter(Boolean)
      )
    ).slice(0, 12);
  });

  /** 文件大小（人类可读） */
  fileSizeLabel = computed(() => this.formatFileSize(this.result()?.fileSize));

  /** 上传者 */
  creatorLabel = computed(() => (this.result()?.creatorName || '').trim());

  /** 版权信息 */
  copyrightLabel = computed(() => (this.result()?.copyrightInfo || '').trim());

  /** 版本号 */
  versionLabel = computed(() => {
    const v = this.result()?.currentVersion;
    return v && v > 0 ? `v${v}` : '';
  });

  /** 资源状态（后端回填的是枚举名） */
  statusLabel = computed(() => {
    const s = (this.result()?.statusName || '').trim();
    if (!s) return '';
    const map: Record<string, string> = {
      Draft: '草稿',
      PendingReview: '待审核',
      SchoolApproved: '院校已通过',
      LeagueApproved: '联盟已通过',
      Rejected: '已驳回',
      Hidden: '已隐藏'
    };
    return map[s] || s;
  });

  /** 热度：浏览 / 下载 / 收藏 */
  statsLabel = computed(() => {
    const r = this.result();
    if (!r) return '';
    const parts: string[] = [];
    if (r.viewCount) parts.push(`浏览 ${r.viewCount}`);
    if (r.downloadCount) parts.push(`下载 ${r.downloadCount}`);
    if (r.collectionCount) parts.push(`收藏 ${r.collectionCount}`);
    return parts.join(' · ');
  });

  /** 是否存在可展示的背景元数据 */
  hasMetaInfo = computed(
    () =>
      !!this.summary() ||
      !!this.description() ||
      this.keywordList().length > 0 ||
      !!this.fileSizeLabel() ||
      !!this.creatorLabel() ||
      !!this.copyrightLabel() ||
      !!this.versionLabel() ||
      !!this.statusLabel() ||
      !!this.statsLabel()
  );

  /** 元信息面板默认收起，只留标题栏；用户点“展开”才占用正文空间 */
  metaCollapsed = signal(true);

  /** 文件名（后端 resourceName 可能为空，退回文件名） */
  fileName = computed(() => {
    const r = this.result();
    if (!r) return '';
    const name = (r.resourceName || '').trim();
    if (name) return name;
    return r.videoName || '未命名资源';
  });

  /**
   * 同一次搜索里的结果列表（用于详情页上下条切换）。
   * 列表页把完整 results 快照塞在 history.state，这里直接复用，
   * 不再发请求 —— 避免翻页时丢失当前筛选条件。
   */
  siblingResults = computed<DocumentSearchResultDto[]>(() => {
    const all = this.searchState()?.results || [];
    const cur = this.result();
    if (!cur || all.length <= 1) return [];
    // 同一资源的多页命中先去重排序，当前项排最前，方便 prev/next 跨页浏览
    return [...all].sort((a, b) => {
      if (a.resourceId === cur.resourceId && b.resourceId !== cur.resourceId) return -1;
      if (b.resourceId === cur.resourceId && a.resourceId !== cur.resourceId) return 1;
      return (b.relevanceScore || 0) - (a.relevanceScore || 0);
    });
  });

  /** 当前结果在列表中的位置（1 基），0 表示无上下文 */
  positionInResults = computed(() => {
    const cur = this.result();
    const list = this.siblingResults();
    if (!cur || !list.length) return 0;
    const i = list.findIndex(
      x => x.resourceId === cur.resourceId && x.pageNumber === cur.pageNumber
    );
    return i === -1 ? 0 : i + 1;
  });

  canPrev = computed(() => this.positionInResults() > 1);
  canNext = computed(() => {
    const p = this.positionInResults();
    return p > 0 && p < this.siblingResults().length;
  });

  breadcrumb = computed(() => {
    const s = this.searchState();
    return {
      query: s?.query || '',
      hasQuery: !!s?.query,
      total: s?.totalCount || 0
    };
  });

  // ─────────── 文档查看：高亮匹配项 ───────────
  hits = signal<HitMatch[]>([]);
  activeHit = signal<number>(-1);

  /** 视频预览地址（与搜索列表页保持一致：同源 /api 流 + access_token，支持 Range 定位） */
  videoStreamUrl = computed(() => {
    const r = this.result();
    if (!r || r.sourceType !== 'video' || !r.resourceId) return '';
    return this.authFetch.withAccessToken(`/api/resource-file/${r.resourceId}/preview`);
  });

  /** "00:01:23" → 83（秒），无法解析时返回 0 */
  private timeToSeconds(time: string | undefined | null): number {
    if (!time) return 0;
    const parts = time.split(':').map(Number);
    if (parts.some(n => Number.isNaN(n))) return 0;
    if (parts.length === 3) return parts[0] * 3600 + parts[1] * 60 + parts[2];
    if (parts.length === 2) return parts[0] * 60 + parts[1];
    return 0;
  }

  /** 视频元数据就绪后跳到命中片段起点，让用户直接看到内容而不是片头 */
  seekToHitSegment(video: HTMLVideoElement) {
    const start = this.timeToSeconds(this.result()?.startTime);
    if (start > 0 && (!video.duration || start < video.duration)) {
      video.currentTime = start;
    }
  }

  /** 片段时长（秒），用于展示"时长 1 分 12 秒" */
  segmentDurationLabel = computed(() => {
    const r = this.result();
    if (!r || r.sourceType !== 'video') return '';
    const d = this.timeToSeconds(r.endTime) - this.timeToSeconds(r.startTime);
    if (!Number.isFinite(d) || d <= 0) return '';
    const m = Math.floor(d / 60);
    const s = Math.round(d % 60);
    return m > 0 ? `${m} 分 ${s} 秒` : `${s} 秒`;
  });

  /** 打开原文件预览（复用全站资源页入口，走 /preview 支持 Range） */
  previewOriginal() {
    const r = this.result();
    if (!r) return;
    this.router.navigate(['/resources'], {
      queryParams: { resourceId: r.resourceId }
    });
  }

  ngOnInit() {
    const state = (history.state || {}) as { detail?: DetailPayload };
    if (state.detail?.result) {
      this.applyPayload(state.detail);
      return;
    }
    const idFromUrl = this.route.snapshot.paramMap.get('id');
    if (idFromUrl) {
      // 直接通过 URL 打开（刷新 / 分享链接）：没有列表页传来的上下文，
      // 向后端要一次该资源的最佳命中，把命中片段与元数据都补齐。
      this.loadResultByResourceId(idFromUrl);
      return;
    }
    this.loading.set(false);
    this.hasPayload.set(false);
  }

  /** 按资源 Id 拉取搜索命中（详情页独立打开时的兜底路径） */
  private loadResultByResourceId(resourceId: string) {
    this.loading.set(true);
    this.searchService.getResourceResult(resourceId).subscribe({
      next: item => {
        this.loading.set(false);
        if (!item) {
          this.hasPayload.set(false);
          this.message.warning('未找到该资源的搜索索引内容');
          return;
        }
        this.result.set(item);
        this.searchState.set(null);
        this.hasPayload.set(true);
        queueMicrotask(() => this.extractHits());
      },
      error: () => {
        this.loading.set(false);
        this.hasPayload.set(false);
        this.message.error('加载搜索结果失败');
      }
    });
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
      this.userScrolled = false;
      this.extractHits();
    });
  }

  private extractHits() {
    const container = this.docPaneRef?.nativeElement;
    if (!container) return;

    const marks = container.querySelectorAll('.doc-text mark');
    if (marks.length === 0) {
      this.hits.set([]);
      this.activeHit.set(-1);
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
    // 默认选中第一处命中：左侧列表给出选中效果，正文里的第一处标记同时高亮。
    // 不自动滚动正文，避免一进页面就把用户甩到文档中间。
    this.selectHit(matched[0].id, false);
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
    this.selectHit(id, true);
  }

  /**
   * 选中某处命中：左侧列表高亮 + 正文对应标记高亮。
   * scroll=false 时不滚动正文（用于默认选中第一处命中的场景）。
   */
  private selectHit(id: number, scroll: boolean) {
    const container = this.docPaneRef?.nativeElement;
    if (!container) return;
    container.querySelectorAll('mark.hit-active').forEach(m => m.classList.remove('hit-active'));
    this.activeHit.set(id);
    const mark = container.querySelector(`mark[data-hit-index="${id}"]`) as HTMLElement | null;
    if (!mark) return;
    mark.classList.add('hit-active');
    if (scroll) {
      mark.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }

  /**
   * 切换到同一次搜索里的相邻结果。
   * 用 replaceUrl 更新 URL 并携带完整 searchState，
   * 这样刷新 / 前进后退都能还原上下文（列表页也是靠 history.state 传参）。
   */
  private goToResult(item: DocumentSearchResultDto) {
    if (
      item.resourceId === this.result()?.resourceId &&
      item.pageNumber === this.result()?.pageNumber
    ) {
      return;
    }
    this.result.set(item);
    this.router.navigateByUrl(this.router.createUrlTree(['/search/detail', item.resourceId]), {
      replaceUrl: true,
      state: { detail: { result: item, searchState: this.searchState() } }
    });
    // 切内容后重置命中高亮并回到顶部
    queueMicrotask(() => {
      const el = document.querySelector('.doc-pane-scroll');
      if (el) (el as HTMLElement).scrollTo({ top: 0 });
      this.hits.set([]);
      this.activeHit.set(-1);
      this.userScrolled = false;
      this.extractHits();
      // 视频换源后重新定位到新片段
      if (this.isVideo()) {
        const v = document.querySelector('.doc-pane-scroll video') as HTMLVideoElement | null;
        if (v) this.seekToHitSegment(v);
      }
    });
  }

  /** 上一条搜索结果（列表按相关度降序） */
  prevResult() {
    const list = this.siblingResults();
    const p = this.positionInResults();
    if (p <= 1) return;
    this.goToResult(list[p - 2]);
  }

  /** 下一条搜索结果 */
  nextResult() {
    const list = this.siblingResults();
    const p = this.positionInResults();
    if (p <= 0 || p >= list.length) return;
    this.goToResult(list[p]);
  }

  /** 上一处命中（Figma / PDF 阅读器的 j 键习惯） */
  prevHit() {
    const list = this.hits();
    if (!list.length) return;
    const cur = this.activeHit();
    // -1 表示尚未选中，跳到最后一条
    const next = cur <= 1 ? list.length : cur - 1;
    this.scrollToHit(next);
  }

  /** 下一处命中 */
  nextHit() {
    const list = this.hits();
    if (!list.length) return;
    const cur = this.activeHit();
    // 到末尾后回绕到第一条
    const next = cur >= list.length ? 1 : cur + 1;
    this.scrollToHit(next);
  }

  /** 文档滚动时同步左侧高亮：找出当前视口中第一个命中项 */
  private syncActiveHitOnScroll() {
    const container = this.docPaneRef?.nativeElement;
    if (!container) return;
    const paneRect = container.getBoundingClientRect();
    // 以视口上沿往下 1/4 处为判定线，避免刚滚过去就切换
    const probeY = paneRect.top + paneRect.height * 0.25;

    let current = 0;
    for (const hit of this.hits()) {
      const rect = hit.element.getBoundingClientRect();
      if (rect.bottom <= probeY) {
        current = hit.id;
      } else {
        break;
      }
    }
    if (current && current !== this.activeHit()) {
      this.hits().forEach(h => h.element.classList.remove('hit-active'));
      this.hits().find(h => h.id === current)?.element.classList.add('hit-active');
      this.activeHit.set(current);
    }
  }

  onDocScroll() {
    // 用 rAF 节流：文档内容长时 scroll 事件触发很密集
    if (this.scrollRaf) return;
    this.scrollRaf = requestAnimationFrame(() => {
      this.scrollRaf = 0;
      if (this.userScrolled) {
        this.syncActiveHitOnScroll();
        return;
      }
      // 首次滚动：只有确实滚动了才开启联动，避免首屏直接落到最后一条命中
      const el = this.docPaneRef?.nativeElement;
      if (el && el.scrollTop > 8) {
        this.userScrolled = true;
        this.syncActiveHitOnScroll();
      }
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
    if (this.categoryLabel()) {
      lines.push(`- 分类：${this.categoryLabel()}`);
    }
    lines.push(`- 相关度：${this.scorePercent()} / 100`);
    if (r.uploadDate) {
      const d = new Date(r.uploadDate);
      if (!Number.isNaN(d.getTime())) {
        lines.push(`- 上传时间：${this.formatDateTime(d)}`);
      }
    }
    if (this.creatorLabel()) {
      lines.push(`- 上传者：${this.creatorLabel()}`);
    }
    if (this.fileSizeLabel()) {
      lines.push(`- 文件大小：${this.fileSizeLabel()}`);
    }
    if (this.versionLabel()) {
      lines.push(`- 版本：${this.versionLabel()}`);
    }
    if (this.statusLabel()) {
      lines.push(`- 状态：${this.statusLabel()}`);
    }
    if (this.statsLabel()) {
      lines.push(`- 热度：${this.statsLabel()}`);
    }
    if (this.copyrightLabel()) {
      lines.push(`- 版权：${this.copyrightLabel()}`);
    }

    // 2.1 资源背景：摘要 / 描述 / 关键词
    if (this.summary()) {
      lines.push('');
      lines.push('【摘要】');
      lines.push(this.summary());
    }
    if (this.description()) {
      lines.push('');
      lines.push('【简介】');
      lines.push(this.description());
    }
    if (this.keywordList().length) {
      lines.push('');
      lines.push(`【关键词】${this.keywordList().join('、')}`);
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

  /** 字节数 → 人类可读（KB/MB/GB） */
  private formatFileSize(bytes?: number | null): string {
    if (!bytes || bytes <= 0) return '';
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let value = bytes;
    let i = 0;
    while (value >= 1024 && i < units.length - 1) {
      value = value / 1024;
      i++;
    }
    return `${i === 0 ? value : value.toFixed(1)} ${units[i]}`;
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

  trackByHit = (_: number, hit: HitMatch) => hit.id;

  @HostListener('window:keydown', ['$event'])
  onKeydown(event: KeyboardEvent) {
    // 在输入框里打字时不抢快捷键
    const t = event.target as HTMLElement | null;
    if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable)) return;

    switch (event.key) {
      case 'Escape':
        this.backToSearch();
        break;
      // 上下键：翻搜索结果（与 Glean / Acrobat 的结果导航一致）
      case 'ArrowDown':
      case 'j':
        event.preventDefault();
        this.nextResult();
        break;
      case 'ArrowUp':
      case 'k':
        event.preventDefault();
        this.prevResult();
        break;
      // n / p：跳下一处 / 上一处文本命中
      case 'n':
        event.preventDefault();
        this.nextHit();
        break;
      case 'p':
        event.preventDefault();
        this.prevHit();
        break;
      default:
        break;
    }
  }

  ngOnDestroy() {
    if (this.scrollRaf) cancelAnimationFrame(this.scrollRaf);
  }
}
