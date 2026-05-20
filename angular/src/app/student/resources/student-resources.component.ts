import { Component, signal, computed, inject, OnInit, ChangeDetectionStrategy, ViewChild } from '@angular/core';
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
import { ResourceReviewService, type ResourceRatingSummaryDto } from '../../search/resource-review/resource-review.service';
import { RecommendationService, type RecommendedResourceDto } from '../../search/recommendation/recommendation.service';
import { AuthErrorService } from '../../core/auth/auth-error.service';

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
  private readonly reviewService = inject(ResourceReviewService);
  private readonly recommendationService = inject(RecommendationService);
  private readonly authErrorService = inject(AuthErrorService);
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
  ratingSummaries = signal<Record<string, ResourceRatingSummaryDto>>({});
  collectedResourceIds = signal<Record<string, boolean>>({});

  // Recommendation signals
  recommendedResources = signal<RecommendedResourceDto[]>([]);
  recommendationsLoading = signal(false);
  recommendationsCollapsed = signal(false);
  relatedResources = signal<RecommendedResourceDto[]>([]);
  relatedLoading = signal(false);
  relatedTab = signal<string>('all');

  relatedReasons = computed(() => {
    const reasons = new Set(this.relatedResources().map(r => r.recommendationReason));
    return [...reasons];
  });

  filteredRelatedResources = computed(() => {
    const tab = this.relatedTab();
    if (tab === 'all') return this.relatedResources();
    return this.relatedResources().filter(r => r.recommendationReason === tab);
  });

  resourceTypes = [
    { label: '全部', value: null as ResourceType | null },
    { label: '文档', value: ResourceType.Document },
    { label: '视频', value: ResourceType.Video },
  ];

  ngOnInit() {
    this.loadCategories();
    this.loadResources();
    this.loadRecommendations();
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
        this.loadRatingSummaries(result.items || []);
        this.loadCollectionStatus(result.items || []);
      },
      error: (err) => {
        this.loading.set(false);
        // 触发重新登录弹窗
        if (err.status === 401 || err.status === 403) {
          this.authErrorService.setAuthError(
            err.status,
            err.error?.error?.message || err.error?.message || '您未获得授权！'
          );
        }
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

  loadRatingSummaries(items: ResourceDto[]) {
    const summaries = { ...this.ratingSummaries() };
    items.forEach(resource => {
      if (!resource.id) return;
      this.reviewService.getRatingSummary(resource.id).subscribe({
        next: (summary) => {
          summaries[resource.id!] = summary;
          this.ratingSummaries.set({ ...summaries });
        }
      });
    });
  }

  loadCollectionStatus(items: ResourceDto[]) {
    if (items.length === 0) {
      this.collectedResourceIds.set({});
      return;
    }

    const collectedMap: Record<string, boolean> = {};

    items.forEach(resource => {
      if (!resource.id) {
        return;
      }

      this.resourceService.isCollected(resource.id).subscribe({
        next: (isCollected) => {
          collectedMap[resource.id!] = isCollected;
          this.collectedResourceIds.set({ ...this.collectedResourceIds(), ...collectedMap });
        }
      });
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
    const url = `/api/resource-file/${resource.id}/download`;
    const a = document.createElement('a');
    a.href = url;
    a.download = resource.originalFileName || resource.name || 'download';
    a.click();
    this.message.success('下载已开始');
  }

  isCollected(resourceId?: string) {
    return !!resourceId && !!this.collectedResourceIds()[resourceId];
  }

  toggleCollection(resource: ResourceDto) {
    if (!resource.id) {
      return;
    }

    const request$ = this.isCollected(resource.id)
      ? this.resourceService.uncollect(resource.id)
      : this.resourceService.collect(resource.id);

    request$.subscribe({
      next: () => {
        const nextValue = !this.isCollected(resource.id);
        this.collectedResourceIds.set({
          ...this.collectedResourceIds(),
          [resource.id!]: nextValue,
        });
        this.message.success(nextValue ? '已加入收藏' : '已取消收藏');
      },
      error: () => {
        this.message.error(this.isCollected(resource.id) ? '取消收藏失败' : '收藏失败');
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
    if (resource.id) {
      this.loadRelatedResources(resource.id);
    }
  }

  closeDrawer() {
    this.drawerVisible.set(false);
  }

  loadRecommendations() {
    this.recommendationsLoading.set(true);
    this.recommendationService.getPersonalizedRecommendations(10).subscribe({
      next: (result) => {
        this.recommendedResources.set(result);
        this.recommendationsLoading.set(false);
      },
      error: (err) => {
        this.recommendationsLoading.set(false);
        if (err.status === 401 || err.status === 403) {
          this.authErrorService.setAuthError(
            err.status,
            err.error?.error?.message || err.error?.message || '您未获得授权！'
          );
        }
      }
    });
  }

  loadRelatedResources(resourceId: string) {
    this.relatedLoading.set(true);
    this.recommendationService.getRelatedResources(resourceId, 6).subscribe({
      next: (result) => {
        this.relatedResources.set(result);
        this.relatedLoading.set(false);
      },
      error: (err) => {
        this.relatedLoading.set(false);
        if (err.status === 401 || err.status === 403) {
          this.authErrorService.setAuthError(
            err.status,
            err.error?.error?.message || err.error?.message || '您未获得授权！'
          );
        }
      }
    });
  }

  selectRelatedResource(resource: RecommendedResourceDto) {
    // Find the resource in the existing list or create a partial ResourceDto
    const existing = this.resources().find(r => r.id === resource.resourceId);
    if (existing) {
      this.selectedResource.set(existing);
    } else {
      this.selectedResource.set({
        id: resource.resourceId,
        name: resource.resourceName,
        description: resource.description,
        resourceType: resource.resourceType,
        categoryId: resource.categoryId,
        fileSize: resource.fileSize,
        fileExtension: resource.fileExtension,
        creationTime: resource.creationTime,
      } as ResourceDto);
    }
    this.loadRelatedResources(resource.resourceId);
  }

  onReviewChanged(resourceId: string) {
    this.reviewService.getRatingSummary(resourceId).subscribe({
      next: (summary) => {
        const summaries = { ...this.ratingSummaries() };
        summaries[resourceId] = summary;
        this.ratingSummaries.set(summaries);
      }
    });
  }
}
