import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { SearchService } from '../../proxy/application/search/search.service';
import type {
  DocumentSearchResultDto,
  PopularSearchDto,
  SearchQueryDto,
} from '../../proxy/application/contracts/search/dtos/models';
import { getMatchInfo, stripUuids, type MatchInfo } from '../../search/search.util';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';

/** 检索范围（对应后端 Meilisearch 索引名） */
export type SearchScope = 'all' | 'documents' | 'videos';

/** 一条检索结果（后端 DTO + 前端匹配类型标注） */
interface SearchRow extends DocumentSearchResultDto {
  match: MatchInfo;
}

/**
 * 学生端 · 智能检索。
 *
 * 对齐原型 `#search`：检索条 + 结果列表。
 *
 * 诚实说明：后端 `/api/app/search/search` 目前就是**关键词检索**
 * （hybrid 入口也已整体降级为关键词检索，未接入语义模型），
 * 因此本页不宣称「语义 / AI 搜索」，只按命中片段展示真实结果。
 *
 * 数据来源（全部真实接口，服务层只有生成代理 SearchService 一处）：
 *   · 检索   POST /api/app/search/search（服务端分页）
 *   · 热门词 GET  /api/app/search/popular-searches
 *   · 访问埋点 POST /api/app/search/log-view
 *
 * 关键词 / 范围 / 页码都写进 URL query params（刷新不丢）。
 */
@Component({
  selector: 'app-student-search',
  standalone: true,
  imports: [
    CommonModule,
    DecimalPipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    StudentPaginationComponent,
  ],
  templateUrl: './student-search.component.html',
  styleUrls: ['./student-search.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentSearchComponent implements OnInit {
  private readonly searchService = inject(SearchService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  /** 关键词（与 URL 同步） */
  readonly keyword = signal('');
  /** 检索范围：全部 / 文档 / 视频 */
  readonly scope = signal<SearchScope>('all');
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly results = signal<DocumentSearchResultDto[]>([]);
  readonly totalCount = signal(0);

  /** 热门检索词（真实统计） */
  readonly hotWords = signal<PopularSearchDto[]>([]);

  /** 带匹配类型标注的结果 */
  readonly rows = computed<SearchRow[]>(() => {
    const q = this.keyword().trim();
    return (this.results() || []).map(r => ({
      ...r,
      match: getMatchInfo(
        r.resourceName ?? '',
        r.highlightedContent ?? null,
        r.eventDescription ?? null,
        q,
      ),
    }));
  });

  /** 是否已输入关键词（区分「待输入」与「无结果」） */
  readonly hasKeyword = computed(() => !!this.keyword().trim());

  ngOnInit(): void {
    this.loadHotWords();

    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        this.applyQuery(params);
        this.runSearch();
      });
  }

  /** 把 URL 上的查询参数同步到信号 */
  private applyQuery(params: ParamMap): void {
    this.keyword.set((params.get('q') || '').trim());

    const scope = params.get('scope');
    this.scope.set(scope === 'documents' || scope === 'videos' ? scope : 'all');

    const page = Number(params.get('page') || '1');
    this.pageIndex.set(Number.isFinite(page) && page > 0 ? Math.floor(page) : 1);
  }

  /** 写回 URL（null 表示移除该参数） */
  private navigate(params: Record<string, string | null>, replaceUrl = true): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: params,
      queryParamsHandling: 'merge',
      replaceUrl,
    });
  }

  /** 执行检索（服务端分页） */
  private runSearch(): void {
    const q = this.keyword().trim();
    if (!q) {
      this.results.set([]);
      this.totalCount.set(0);
      this.loading.set(false);
      this.loadFailed.set(false);
      return;
    }

    this.loading.set(true);
    this.loadFailed.set(false);

    const input: SearchQueryDto = {
      query: q,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
      sorting: 'relevance',
      // 学生端仅检索联盟审核通过的资源（与资源中心口径一致；documents 侧生效，videos 侧后端自动忽略）
      statusFilter: '3',
    };

    // 「全部」不传 indexName，后端合并 documents + videos 双索引；单选时只走单边。
    if (this.scope() !== 'all') {
      input.indexName = this.scope();
    }

    this.searchService.search(input).subscribe({
      next: data => {
        this.results.set(data?.items || []);
        this.totalCount.set(data?.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  /** 重新检索（错误态重试） */
  reload(): void {
    this.runSearch();
  }

  /** 热门检索词 */
  private loadHotWords(): void {
    this.searchService.getPopularSearches(12).subscribe({
      next: list => this.hotWords.set(list || []),
      error: () => this.hotWords.set([]),
    });
  }

  /** 提交检索条 */
  onSearch(): void {
    this.navigate({ q: this.keyword().trim() || null, page: null });
  }

  /** 切换检索范围 */
  setScope(scope: SearchScope): void {
    this.scope.set(scope);
    this.navigate({ scope: scope === 'all' ? null : scope, page: null });
  }

  /** 点击热门词 */
  onHotWord(word?: string): void {
    if (!word) return;
    this.keyword.set(word);
    this.navigate({ q: word, page: null });
  }

  /** 分页 */
  onPageChange(event: { pageIndex: number; pageSize: number }): void {
    this.pageIndex.set(event.pageIndex);
    this.navigate({ page: String(event.pageIndex) }, false);
  }

  /** 清空检索条件 */
  resetSearch(): void {
    this.keyword.set('');
    this.scope.set('all');
    this.navigate({ q: null, scope: null, page: null });
  }

  /** 打开资源详情（顺带记录一次访问） */
  openResource(row: SearchRow): void {
    const resourceId = row.resourceId;
    if (!resourceId) return;

    this.searchService
      .logView({
        resourceId,
        pageNumber: row.pageNumber,
        viewDurationSeconds: 0,
        viewSource: 0,
      })
      .subscribe({ error: () => undefined });

    this.router.navigate(['/student/resources', resourceId], {
      queryParams: { from: 'search' },
    });
  }

  /** 是否视频结果 */
  isVideo(row: SearchRow): boolean {
    return row.sourceType === 'video';
  }

  /** 结果行的图标 */
  fileIcon(row: SearchRow): string {
    if (this.isVideo(row)) return 'video-camera';
    const ext = (row.fileExtension || '').replace(/^\./, '').toLowerCase();
    switch (ext) {
      case 'pdf':
        return 'file-pdf';
      case 'doc':
      case 'docx':
        return 'file-word';
      case 'xls':
      case 'xlsx':
        return 'file-excel';
      case 'ppt':
      case 'pptx':
        return 'file-ppt';
      case 'jpg':
      case 'jpeg':
      case 'png':
      case 'gif':
        return 'file-image';
      case 'txt':
      case 'md':
        return 'file-text';
      default:
        return 'file';
    }
  }

  /** 格式标签（真实扩展名） */
  formatTag(row: SearchRow): string {
    const ext = (row.fileExtension || '').replace(/^\./, '').trim();
    if (ext) return ext.toUpperCase();
    return this.isVideo(row) ? '视频' : '文档';
  }

  /** 命中位置：文档页码 / 视频时间段 */
  hitPosition(row: SearchRow): string {
    if (this.isVideo(row)) {
      const start = row.startTime || '';
      const end = row.endTime || '';
      if (start && end) return `${start} - ${end}`;
      if (start) return start;
      return '视频片段';
    }
    if (row.pageNumber) return `第 ${row.pageNumber} 页`;
    return '资源命中';
  }

  /** 命中片段（去除 UUID，保留后端返回的高亮标签） */
  snippet(row: SearchRow): string {
    const raw = row.highlightedContent || row.eventDescription || row.content || '';
    const clean = stripUuids(raw);
    if (clean.length <= 220) return clean;
    // 截断时去掉可能被切断的标签，避免出现残缺 HTML
    return `${clean.slice(0, 220).replace(/<[^>]*$/, '')}…`;
  }

  /** 匹配类型文案 */
  matchLabel(row: SearchRow): string {
    switch (row.match.type) {
      case 'content':
        return '正文命中';
      case 'name':
        return '文件名命中';
      default:
        return '可能相关';
    }
  }

  /** 匹配类型对应的图标 */
  matchIcon(row: SearchRow): string {
    switch (row.match.type) {
      case 'content':
        return 'highlight';
      case 'name':
        return 'file-text';
      default:
        return 'question-circle';
    }
  }
}
