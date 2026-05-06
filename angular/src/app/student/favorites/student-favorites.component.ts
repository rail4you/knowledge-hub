import { ChangeDetectionStrategy, Component, ViewChild, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import type { ResourceDto } from '../../proxy/resources/models';
import { ResourceService } from '../../proxy/resources/resource.service';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { StudentResourceCollectionService } from '../resource-collection.service';

@Component({
  selector: 'app-student-favorites',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    RouterModule,
    NzButtonModule,
    NzEmptyModule,
    NzIconModule,
    NzPaginationModule,
    NzSpinModule,
    NzTooltipModule,
    FilePreviewComponent,
  ],
  templateUrl: './student-favorites.component.html',
  styleUrls: ['./student-favorites.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentFavoritesComponent {
  private readonly resourceService = inject(ResourceService);
  private readonly resourceCollectionService = inject(StudentResourceCollectionService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  resources = signal<ResourceDto[]>([]);
  loading = signal(false);
  totalCount = signal(0);
  pageIndex = signal(1);
  pageSize = signal(12);

  constructor() {
    this.loadFavorites();
  }

  loadFavorites() {
    this.loading.set(true);
    this.resourceCollectionService.getCollectedList({
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    }).subscribe({
      next: result => {
        this.resources.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('收藏列表加载失败');
      }
    });
  }

  onPageChange(pageIndex: number) {
    this.pageIndex.set(pageIndex);
    this.loadFavorites();
  }

  previewResource(resource: ResourceDto) {
    if (!resource.id) {
      return;
    }

    this.filePreview.open(
      resource.id,
      resource.originalFileName || resource.name || '未命名',
      resource.fileExtension || '',
      resource.fileSize || 0
    );
  }

  downloadResource(resource: ResourceDto) {
    if (!resource.id) {
      return;
    }

    this.resourceService.download(resource.id).subscribe({
      next: data => {
        const arrayBuffer = this.toArrayBuffer(data);
        const blob = new Blob([arrayBuffer], { type: 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = resource.originalFileName || resource.name || 'download';
        link.click();
        URL.revokeObjectURL(url);
        this.message.success('下载已开始');
      },
      error: () => {
        this.message.error('下载失败');
      }
    });
  }

  removeFavorite(resource: ResourceDto) {
    if (!resource.id) {
      return;
    }

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

  getResourceTypeLabel(type?: number) {
    const labels: Record<number, string> = {
      [ResourceType.Document]: '文档',
      [ResourceType.Video]: '视频',
      [ResourceType.Audio]: '音频',
      [ResourceType.Image]: '图片',
      [ResourceType.PPT]: 'PPT',
    };
    return labels[type ?? ResourceType.Document] || '资源';
  }

  getResourceTypeIcon(type?: number): string {
    const icons: Record<number, string> = {
      [ResourceType.Document]: 'file-text',
      [ResourceType.Video]: 'video-camera',
      [ResourceType.Audio]: 'audio',
      [ResourceType.Image]: 'picture',
      [ResourceType.PPT]: 'file-ppt',
    };
    return icons[type ?? ResourceType.Document] || 'file-text';
  }

  private toArrayBuffer(data: string | number[] | ArrayBuffer): ArrayBuffer {
    if (typeof data === 'string') {
      const binary = atob(data);
      const buffer = new ArrayBuffer(binary.length);
      const view = new Uint8Array(buffer);
      for (let index = 0; index < binary.length; index++) {
        view[index] = binary.charCodeAt(index);
      }
      return buffer;
    }

    if (data instanceof ArrayBuffer) {
      return data;
    }

    return new Uint8Array(data).buffer.slice(0) as ArrayBuffer;
  }
}
