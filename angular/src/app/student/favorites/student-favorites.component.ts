import { ChangeDetectionStrategy, Component, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import type { ResourceDto } from '../../proxy/resources/models';
import { ResourceService } from '../../proxy/resources/resource.service';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { StudentResourceCollectionService } from '../resource-collection.service';
import { ResourceReviewService, type ResourceRatingSummaryDto } from '../../search/resource-review/resource-review.service';
import { buildDownloadFileName } from '../../shared/download/download-file.util';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';
import { fileSizeText, resourceTypeName } from '../../shared/utils/resource-format.util';

@Component({
  selector: 'app-student-favorites',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    RouterModule,
    NzIconModule,
    NzPaginationModule,
    NzSpinModule,
    FilePreviewComponent,
    StudentHeroComponent,
  ],
  templateUrl: './student-favorites.component.html',
  styleUrls: ['./student-favorites.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentFavoritesComponent implements OnInit {
  private readonly resourceService = inject(ResourceService);
  private readonly resourceCollectionService = inject(StudentResourceCollectionService);
  private readonly reviewService = inject(ResourceReviewService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  resources = signal<ResourceDto[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  pageIndex = signal(1);
  pageSize = signal(12);

  ratingSummaries = signal<Record<string, ResourceRatingSummaryDto>>({});

  /** Hero 区数据总览 */
  readonly heroStats = computed(() => {
    const total = this.totalCount();
    const items = this.resources();
    const rated = items.filter(r => (this.ratingSummaries()[r.id!]?.averageRating || 0) > 0).length;
    return [
      { label: '已收藏', value: total, suffix: '个', icon: 'heart', color: '#ef4444' },
      { label: '当前页', value: items.length, suffix: '个', icon: 'appstore', color: '#2b6cd4' },
      { label: '已评分', value: rated, suffix: '个', icon: 'star', color: '#f59e0b' },
      { label: '平均评分', value: this.avgRating(), suffix: '分', icon: 'like', color: '#10b981' },
    ];
  });

  private readonly avgRating = computed(() => {
    const summaries = this.ratingSummaries();
    const items = this.resources();
    const rated = items.filter(r => (summaries[r.id!]?.averageRating || 0) > 0);
    if (rated.length === 0) return 0;
    const sum = rated.reduce((s, r) => s + (summaries[r.id!]?.averageRating || 0), 0);
    return Math.round((sum / rated.length) * 10) / 10;
  });

  ngOnInit() {
    this.loadFavorites();
  }

  loadFavorites() {
    this.loading.set(true);
    this.resourceCollectionService.getCollectedList({
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    }).subscribe({
      next: result => {
        const items = result.items || [];
        this.resources.set(items);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
        this.loadRatingSummaries(items);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('收藏列表加载失败');
      }
    });
  }

  loadRatingSummaries(items: ResourceDto[]) {
    const summaries = { ...this.ratingSummaries() };
    // 批量拉取本页缺失的评分汇总，避免每个资源一次请求（N+1）
    const pendingIds = items
      .map(r => r.id)
      .filter((id): id is string => !!id && !summaries[id]);
    if (pendingIds.length === 0) return;

    this.reviewService.getRatingSummaries(pendingIds).subscribe({
      next: list => {
        const next = { ...this.ratingSummaries() };
        (list || []).forEach(summary => { next[summary.resourceId] = summary; });
        this.ratingSummaries.set(next);
      }
    });
  }

  onPageChange(pageIndex: number) {
    this.pageIndex.set(pageIndex);
    this.loadFavorites();
  }

  openDetail(resource: ResourceDto) {
    if (!resource.id) return;
    // 教师端没有 /resources/:id 详情页，回退到教师资源列表 /resources（至少能正常加载）。
    const url = this.router.url;
    if (url.startsWith('/student')) {
      this.router.navigate(['/student/resources', resource.id]);
    } else {
      this.router.navigate(['/resources'], { queryParams: { keyword: resource.name } });
    }
  }

  previewResource(event: Event, resource: ResourceDto) {
    event.stopPropagation();
    if (!resource?.id) return;
    // 若 fileExtension 为空，尝试从 originalFileName 或 name 提取扩展名
    let ext = resource.fileExtension;
    if (!ext) {
      const fileName = resource.originalFileName || resource.name || '';
      const dot = fileName.lastIndexOf('.');
      if (dot >= 0) {
        ext = fileName.substring(dot); // 包含 . 前缀
      }
    }
    this.filePreview.open(
      resource.id,
      resource.originalFileName || resource.name || '未命名',
      ext || '',
      resource.fileSize || 0,
      resource.isDownloadable !== false
    );
  }

  downloadResource(event: Event, resource: ResourceDto) {
    event.stopPropagation();
    if (!resource?.id) return;
    if (!resource.isDownloadable) {
      this.message.warning('该资源不允许下载，仅支持在线预览');
      return;
    }
    const url = `/api/resource-file/${resource.id}/download`;
    const a = document.createElement('a');
    a.href = url;
    const downloadName = buildDownloadFileName(resource.originalFileName, resource.name, resource.fileExtension);
    if (downloadName) a.download = downloadName;
    a.click();
    this.message.success('下载已开始');
  }

  toggleCollection(event: Event, resource: ResourceDto) {
    event.stopPropagation();
    if (!resource.id) return;

    this.resourceService.uncollect(resource.id).subscribe({
      next: () => {
        const nextResources = this.resources().filter(item => item.id !== resource.id);
        const isLastItemOnPage = nextResources.length === 0 && this.pageIndex() > 1;

        this.message.success('已取消收藏');
        if (isLastItemOnPage) {
          this.pageIndex.update(value => value - 1);
        }
        this.loadFavorites();
      },
      error: () => {
        this.message.error('取消收藏失败');
      }
    });
  }

  browseResources() {
    // 同 P1-12 修复：根据当前 URL 推断门户前缀。
    const url = this.router.url;
    if (url.startsWith('/student')) {
      this.router.navigate(['/student/resources']);
    } else {
      this.router.navigate(['/resources']);
    }
  }

  getResourceTypeName(type?: number): string {
    return resourceTypeName(type);
  }

  formatFileSize(size?: number): string {
    return fileSizeText(size);
  }

  /** 评分对应的实心星星数量（0-5，四舍五入），用于卡片评分行展示 */
  ratingStars(avg?: number | null): number[] {
    const n = Math.max(0, Math.min(5, Math.round(avg || 0)));
    return Array.from({ length: n }, (_, i) => i);
  }
}
