import { ChangeDetectionStrategy, Component, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzRateModule } from 'ng-zorro-antd/rate';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import type { ResourceDto } from '../../proxy/resources/models';
import { ResourceService } from '../../proxy/resources/resource.service';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { StudentResourceCollectionService } from '../resource-collection.service';
import { ResourceReviewService, type ResourceRatingSummaryDto } from '../../search/resource-review/resource-review.service';

@Component({
  selector: 'app-student-favorites',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    RouterModule,
    NzButtonModule,
    NzIconModule,
    NzPaginationModule,
    NzRateModule,
    NzSpinModule,
    FilePreviewComponent,
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
    items.forEach(resource => {
      if (!resource.id) return;
      this.reviewService.getRatingSummary(resource.id).subscribe({
        next: summary => {
          summaries[resource.id!] = summary;
          this.ratingSummaries.set({ ...summaries });
        }
      });
    });
  }

  onPageChange(pageIndex: number) {
    this.pageIndex.set(pageIndex);
    this.loadFavorites();
  }

  openDetail(resource: ResourceDto) {
    if (!resource.id) return;
    this.router.navigate(['/student/resources', resource.id]);
  }

  previewResource(event: Event, resource: ResourceDto) {
    event.stopPropagation();
    if (!resource?.id) return;
    this.filePreview.open(
      resource.id,
      resource.originalFileName || resource.name || '未命名',
      resource.fileExtension || '',
      resource.fileSize || 0
    );
  }

  downloadResource(event: Event, resource: ResourceDto) {
    event.stopPropagation();
    if (!resource?.id) return;
    const url = `/api/resource-file/${resource.id}/download`;
    const a = document.createElement('a');
    a.href = url;
    a.download = resource.originalFileName || resource.name || 'download';
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
    this.router.navigate(['/student/resources']);
  }

  getResourceTypeIcon(type?: number): string {
    const icons: Record<number, string> = {
      [ResourceType.Document]: 'file-text',
      [ResourceType.Video]: 'video-camera',
      [ResourceType.Audio]: 'sound',
      [ResourceType.Image]: 'picture',
      [ResourceType.PPT]: 'file-ppt',
    };
    return icons[type ?? 0] || 'file-text';
  }

  getResourceTypeName(type?: number): string {
    const names: Record<number, string> = {
      [ResourceType.Document]: '文档',
      [ResourceType.Video]: '视频',
      [ResourceType.Audio]: '音频',
      [ResourceType.Image]: '图片',
      [ResourceType.PPT]: '演示文稿',
    };
    return names[type ?? 0] || '资料';
  }

  formatFileSize(size?: number): string {
    if (!size) return '未知大小';
    if (size >= 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)} MB`;
    if (size >= 1024) return `${(size / 1024).toFixed(0)} KB`;
    return `${size} B`;
  }
}
