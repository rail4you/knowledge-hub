import { Component, signal, inject, OnInit, ChangeDetectionStrategy, ViewChild } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzRateModule } from 'ng-zorro-antd/rate';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ResourceService } from '../../proxy/resources/resource.service';
import { ResourceStatus } from '../../proxy/resources/enums/resource-status.enum';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import type { ResourceDto, ResourceCategoryDto } from '../../proxy/resources/models';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { ResourceReviewComponent } from '../../search/resource-review/resource-review.component';

@Component({
  selector: 'app-student-resources',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    NzIconModule,
    NzButtonModule,
    NzInputModule,
    NzSpinModule,
    NzEmptyModule,
    NzTooltipModule,
    NzPaginationModule,
    NzDrawerModule,
    NzRateModule,
    NzDividerModule,
    FilePreviewComponent,
    ResourceReviewComponent,
  ],
  templateUrl: './student-resources.component.html',
  styleUrls: ['./student-resources.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentResourcesComponent implements OnInit {
  private readonly resourceService = inject(ResourceService);
  private readonly message = inject(NzMessageService);

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  resources = signal<ResourceDto[]>([]);
  loading = signal(false);
  filterText = signal('');
  selectedType = signal<ResourceType | null>(null);
  selectedCategoryId = signal<string | null>(null);
  expandedCategories = signal<Set<string>>(new Set());
  categories = signal<ResourceCategoryDto[]>([]);

  totalCount = signal(0);
  pageIndex = signal(1);
  pageSize = signal(12);

  drawerVisible = signal(false);
  selectedResource = signal<ResourceDto | null>(null);

  resourceTypes = [
    { label: '全部', value: null as ResourceType | null },
    { label: '文档', value: ResourceType.Document },
    { label: '视频', value: ResourceType.Video },
  ];

  ngOnInit() {
    this.loadCategories();
    this.loadResources();
  }

  loadResources() {
    this.loading.set(true);
    const input: any = {
      status: ResourceStatus.LeagueApproved,
      filter: this.filterText() || undefined,
      resourceType: this.selectedType() ?? undefined,
      categoryId: this.selectedCategoryId() || undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    };

    this.resourceService.getFilteredList(input).subscribe({
      next: (result) => {
        this.resources.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  loadCategories() {
    this.resourceService.getCategories().subscribe({
      next: (cats) => {
        this.categories.set(cats);
      }
    });
  }

  selectCategory(categoryId: string | null) {
    this.selectedCategoryId.set(categoryId);
    this.pageIndex.set(1);
    this.loadResources();
  }

  toggleCategoryExpand(categoryId: string) {
    const current = new Set(this.expandedCategories());
    if (current.has(categoryId)) {
      current.delete(categoryId);
    } else {
      current.add(categoryId);
    }
    this.expandedCategories.set(current);
  }

  selectType(type: ResourceType | null) {
    this.selectedType.set(type);
    this.pageIndex.set(1);
    this.loadResources();
  }

  onPageChange(index: number) {
    this.pageIndex.set(index);
    this.loadResources();
  }

  onSearch() {
    this.pageIndex.set(1);
    this.loadResources();
  }

  previewResource(resource: ResourceDto) {
    if (!resource?.id) return;
    this.filePreview.open(
      resource.id,
      resource.originalFileName || resource.name || '未命名',
      resource.fileExtension || '',
      resource.fileSize || 0
    );
  }

  downloadResource(resource: ResourceDto) {
    if (!resource?.id) return;
    this.resourceService.download(resource.id).subscribe({
      next: (data: any) => {
        const arrayBuffer = this.toArrayBuffer(data);
        const blob = new Blob([arrayBuffer], { type: 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = resource.originalFileName || resource.name || 'download';
        a.click();
        URL.revokeObjectURL(url);
        this.message.success('下载已开始');
      },
      error: () => {
        this.message.error('下载失败');
      }
    });
  }

  private toArrayBuffer(data: any): ArrayBuffer {
    if (typeof data === 'string') {
      const binary = atob(data);
      const buffer = new ArrayBuffer(binary.length);
      const view = new Uint8Array(buffer);
      for (let i = 0; i < binary.length; i++) {
        view[i] = binary.charCodeAt(i);
      }
      return buffer;
    }
    if (data instanceof ArrayBuffer) return data;
    return new Uint8Array(data).buffer.slice(0) as ArrayBuffer;
  }

  collectResource(id?: string) {
    if (!id) return;
    this.resourceService.collect(id).subscribe({
      next: () => {
        this.message.success('收藏成功');
      },
      error: () => {
        this.message.error('收藏失败');
      }
    });
  }

  getResourceTypeIcon(type?: number): string {
    const icons: Record<number, string> = {
      [ResourceType.Document]: 'file-text',
      [ResourceType.Video]: 'video-camera',
      [ResourceType.Audio]: 'audio',
      [ResourceType.Image]: 'picture',
      [ResourceType.PPT]: 'file-ppt',
    };
    return icons[type ?? 0] || 'file-text';
  }

  selectResource(resource: ResourceDto) {
    this.selectedResource.set(resource);
    this.drawerVisible.set(true);
  }

  closeDrawer() {
    this.drawerVisible.set(false);
  }
}
