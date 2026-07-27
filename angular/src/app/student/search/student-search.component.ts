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
import type { PopularSearchDto, DocumentSearchResultDto } from '../../proxy/application/contracts/search/dtos/models';

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
    if (!ext) return all;
    return all.filter(r => r.fileExtension === ext);
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
    fetch('/api/app/search/popular-searches?count=30', { credentials: 'include' })
      .then(r => r.json())
      .then(data => {
        this.hotWords.set(data ?? []);
        this.isHotWordsLoading.set(false);
      })
      .catch(() => {
        this.hotWords.set([]);
        this.isHotWordsLoading.set(false);
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

    fetch('/api/app/search/search', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        query: q,
        skipCount: (this.pageIndex - 1) * this.pageSize,
        maxResultCount: this.pageSize,
        sorting: 'relevance',
        indexName: 'documents',
      }),
    })
      .then(r => {
        console.log('[search] status:', r.status);
        return r.json();
      })
      .then(data => {
        console.log('[search] data:', data, 'items:', data.items);
        this.results.set(data.items ?? []);
        this.totalCount.set(data.totalCount ?? 0);
        this.loading.set(false);
      })
      .catch(err => {
        console.error('[search] error:', err);
        this.loading.set(false);
        this.message.error('搜索失败');
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
    fetch('/api/app/search/log-view', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({
        resourceId: result.resourceId ?? '',
        pageNumber: result.pageNumber,
        viewDurationSeconds: 0,
        viewSource: 0
      }),
    }).catch(() => {});

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
