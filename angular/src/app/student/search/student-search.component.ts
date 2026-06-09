import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
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
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { SearchService, SearchResultDto, DocumentSearchResultDto, SearchQueryDto } from '../../search/search.service';

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
    NzCardModule,
    NzSpinModule,
    NzEmptyModule,
    NzTagModule,
    NzPaginationModule,
    NzDividerModule,
  ],
  templateUrl: './student-search.component.html',
  styleUrls: ['./student-search.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentSearchComponent implements OnInit {
  private readonly searchService = inject(SearchService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly message = inject(NzMessageService);

  searchQuery = '';
  pageIndex = 1;
  readonly pageSize = 20;

  loading = signal(false);
  results = signal<DocumentSearchResultDto[]>([]);
  totalCount = signal(0);
  selectedFileExtension = signal('');

  /** 已出现的文件扩展名（用作结果类型筛选） */
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

  ngOnInit() {
    // 复用教师端约定：?q=xxx 直接发起一次搜索
    const q = this.route.snapshot.queryParamMap.get('q');
    if (q) {
      this.searchQuery = q;
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
    const query: SearchQueryDto = {
      query: q,
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize,
      sorting: 'relevance',
      indexName: 'documents',
    };

    this.searchService.search(query).subscribe({
      next: (result: SearchResultDto) => {
        this.results.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('搜索失败，请稍后重试');
      }
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

  /**
   * 点击结果：跳到学生端的资源详情页。
   * 学生只看资源，资源审核、视频回放、统计等教师能力都不暴露在 UI 上。
   */
  viewDocument(result: DocumentSearchResultDto) {
    this.searchService.logView({
      resourceId: result.resourceId,
      pageNumber: result.pageNumber,
      viewDurationSeconds: 0,
      viewSource: 0
    }).subscribe({ error: () => { /* ignore */ } });

    this.router.navigate(['/student/resources', result.resourceId], {
      queryParams: { page: result.pageNumber, from: 'search' }
    });
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
}
