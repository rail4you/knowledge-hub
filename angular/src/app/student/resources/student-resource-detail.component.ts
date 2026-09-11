import { ChangeDetectionStrategy, Component, OnInit, ViewChild, inject, signal, DestroyRef } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '@abp/ng.core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzRateModule } from 'ng-zorro-antd/rate';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ResourceService } from '../../proxy/resources/resource.service';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import { ResourceStatus } from '../../proxy/resources/enums/resource-status.enum';
import type { ResourceDto } from '../../proxy/resources/models';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { ResourceCoverComponent } from '../../shared/resource-cover/resource-cover.component';
import { buildDownloadFileName } from '../../shared/download/download-file.util';
import { ResourceReviewComponent } from '../../search/resource-review/resource-review.component';
import { ResourceReviewService, type ResourceRatingSummaryDto } from '../../search/resource-review/resource-review.service';
import { RecommendationService, type RecommendedResourceDto } from '../../search/recommendation/recommendation.service';
import { AuthErrorService } from '../../core/auth/auth-error.service';
import { ClientCacheService } from '../../shared/cache/client-cache.service';
import { fileSizeText, resourceTypeName } from '../../shared/utils/resource-format.util';

/** 资源详情页缓存命名空间（通用 ClientCacheService，TTL 60s） */
const DETAIL_CACHE_NS = 'student.resource-detail';

/** 资源类型图标（模块级常量表，避免模板每次变更检测重新创建对象） */
const RESOURCE_TYPE_ICONS: Record<number, string> = {
  [ResourceType.Document]: 'file-text',
  [ResourceType.Video]: 'video-camera',
  [ResourceType.Audio]: 'sound',
  [ResourceType.Image]: 'picture',
  [ResourceType.PPT]: 'file-ppt',
};

@Component({
  selector: 'app-student-resource-detail',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzRateModule,
    FilePreviewComponent,
    ResourceReviewComponent,
    ResourceCoverComponent,
  ],
  templateUrl: './student-resource-detail.component.html',
  styleUrls: ['./student-resource-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentResourceDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly resourceService = inject(ResourceService);
  private readonly reviewService = inject(ResourceReviewService);
  private readonly recommendationService = inject(RecommendationService);
  private readonly authErrorService = inject(AuthErrorService);
  private readonly authService = inject(AuthService);
  private readonly cache = inject(ClientCacheService);
  private readonly message = inject(NzMessageService);

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;
  @ViewChild(ResourceReviewComponent) reviewForm?: ResourceReviewComponent;

  readonly resource = signal<ResourceDto | null>(null);
  readonly loading = signal(false);
  readonly isCollected = signal(false);
  readonly ratingSummary = signal<ResourceRatingSummaryDto | null>(null);

  readonly relatedResources = signal<RecommendedResourceDto[]>([]);
  readonly relatedLoading = signal(false);

  readonly copyLinkSuccess = signal(false);

  ngOnInit() {
    // 订阅 paramMap：从相关资源跳转到同一路由的不同 id 时，组件会被复用，
    // snapshot 只读一次会导致详情和评论都不刷新，这里改为响应式订阅。
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.loadDetail(id);
        this.loadRelated(id);
      } else {
        this.router.navigate(['/student/resources']);
      }
    });
  }

  loadDetail(id: string) {
    this.loading.set(true);
    this.cache.load<ResourceDto>(DETAIL_CACHE_NS, `resource:${id}`, () => this.resourceService.getWithVersions(id)).subscribe({
      next: (data) => {
        this.resource.set(data);
        this.loading.set(false);
        this.loadCollectionStatus(id);
        this.loadRatingSummary(id);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 401 || err.status === 403) {
          this.authErrorService.setAuthError(
            err.status,
            err.error?.error?.message || err.error?.message || '您未获得授权！'
          );
        } else {
          this.message.error('资源加载失败');
          this.router.navigate(['/student/resources']);
        }
      }
    });
  }

  loadCollectionStatus(id: string) {
    // 游客无法收藏，跳过收藏状态查询
    if (!this.authService.isAuthenticated) return;
    this.cache.load<boolean>(DETAIL_CACHE_NS, `collected:${id}`, () => this.resourceService.isCollected(id)).subscribe({
      next: (v) => this.isCollected.set(!!v)
    });
  }

  loadRatingSummary(id: string, force = false) {
    const loader = () => this.reviewService.getRatingSummary(id);
    const request$ = force
      ? this.cache.reload<ResourceRatingSummaryDto>(DETAIL_CACHE_NS, `rating:${id}`, loader)
      : this.cache.load<ResourceRatingSummaryDto>(DETAIL_CACHE_NS, `rating:${id}`, loader);
    request$.subscribe({
      next: (s) => this.ratingSummary.set(s)
    });
  }

  loadRelated(id: string) {
    this.relatedLoading.set(true);
    this.cache.load<RecommendedResourceDto[]>(DETAIL_CACHE_NS, `related:${id}`, () => this.recommendationService.getRelatedResources(id, 8)).subscribe({
      next: (list) => {
        this.relatedResources.set(list || []);
        this.relatedLoading.set(false);
      },
      error: () => this.relatedLoading.set(false)
    });
  }

  goBack() {
    this.router.navigate(['/student/resources']);
  }

  /** 评论区“写评价”按钮：聚焦到评价输入框，给出明确反馈 */
  focusReviewForm() {
    this.reviewForm?.focusForm();
  }

  preview() {
    const r = this.resource();
    if (!r?.id) return;
    // 若 fileExtension 为空，尝试从 originalFileName 或 name 提取扩展名
    let ext = r.fileExtension;
    if (!ext) {
      const fileName = r.originalFileName || r.name || '';
      const dot = fileName.lastIndexOf('.');
      if (dot >= 0) {
        ext = fileName.substring(dot); // 包含 . 前缀
      }
    }
    this.filePreview.open(
      r.id,
      r.originalFileName || r.name || '未命名',
      ext || '',
      r.fileSize || 0,
      r.isDownloadable !== false
    );
  }

  download() {
    const r = this.resource();
    if (!r?.id) return;
    if (!r.isDownloadable) {
      this.message.warning('该资源不允许下载，仅支持在线预览');
      return;
    }
    if (!this.isApproved()) {
      // 提示与按钮 title 一致，避免点击后看到无变化
      this.message.warning(this.getDownloadTitle() || '资源不可下载');
      return;
    }
    const url = `/api/resource-file/${r.id}/download`;
    const a = document.createElement('a');
    a.href = url;
    const downloadName = buildDownloadFileName(r.originalFileName, r.name, r.fileExtension);
    if (downloadName) a.download = downloadName;
    a.click();
    this.message.success('下载已开始');
  }

  toggleCollection() {
    const r = this.resource();
    if (!r?.id) return;
    const req$ = this.isCollected()
      ? this.resourceService.uncollect(r.id)
      : this.resourceService.collect(r.id);
    req$.subscribe({
      next: () => {
        const next = !this.isCollected();
        this.isCollected.set(next);
        this.cache.set(DETAIL_CACHE_NS, `collected:${r.id}`, next);
        this.message.success(next ? '已加入收藏' : '已取消收藏');
      },
      error: () => this.message.error('操作失败')
    });
  }

  copyLink() {
    const r = this.resource();
    if (!r?.id) return;
    const url = `${window.location.origin}/student/resources/${r.id}`;
    if (navigator.clipboard) {
      navigator.clipboard.writeText(url).then(() => {
        this.copyLinkSuccess.set(true);
        this.message.success('链接已复制到剪贴板');
        setTimeout(() => this.copyLinkSuccess.set(false), 1800);
      }).catch(() => {
        this.fallbackCopy(url);
      });
    } else {
      this.fallbackCopy(url);
    }
  }

  private fallbackCopy(url: string) {
    const input = document.createElement('input');
    input.value = url;
    document.body.appendChild(input);
    input.select();
    try {
      document.execCommand('copy');
      this.copyLinkSuccess.set(true);
      this.message.success('链接已复制');
      setTimeout(() => this.copyLinkSuccess.set(false), 1800);
    } catch {
      this.message.error('复制失败，请手动复制');
    }
    document.body.removeChild(input);
  }

  openRelated(id: string) {
    if (!id) return;
    this.router.navigate(['/student/resources', id]);
  }

  onReviewChanged() {
    const r = this.resource();
    if (r?.id) this.loadRatingSummary(r.id, true);
  }

  getResourceTypeIcon(type?: number): string {
    return RESOURCE_TYPE_ICONS[type ?? 0] || 'file-text';
  }

  /** 资源是否处于待审核状态 */
  isPendingReview(): boolean {
    return this.resource()?.status === ResourceStatus.PendingReview;
  }

  /** 资源是否被驳回 */
  isRejected(): boolean {
    return this.resource()?.status === ResourceStatus.Rejected;
  }

  /** 资源是否已通过审核（任一审核层级） */
  isApproved(): boolean {
    const s = this.resource()?.status;
    return s === ResourceStatus.SchoolApproved || s === ResourceStatus.LeagueApproved;
  }

  /** 下载按钮是否应该禁用（不允许下载 / 待审核 / 驳回 / 草稿 / 隐藏） */
  isDownloadDisabled(): boolean {
    if (!this.resource()?.isDownloadable) return true;
    const s = this.resource()?.status;
    return s !== ResourceStatus.SchoolApproved && s !== ResourceStatus.LeagueApproved;
  }

  /** 下载按钮的 title 提示 */
  getDownloadTitle(): string {
    const r = this.resource();
    if (!r) return '';
    if (!r.isDownloadable) return '该资源不允许下载，仅支持在线预览';
    const s = r.status;
    if (s === ResourceStatus.PendingReview) return '资源审核中，审核通过后开放下载';
    if (s === ResourceStatus.Rejected) return '资源未通过审核，暂不可下载';
    if (s === ResourceStatus.Draft || s === ResourceStatus.Hidden) return '资源暂未发布，暂不可下载';
    return '';
  }

  /** 下载按钮文案 */
  getDownloadButtonText(): string {
    const r = this.resource();
    if (!r) return '下载资源';
    if (!r.isDownloadable) return '不允许下载';
    const s = r.status;
    if (s === ResourceStatus.PendingReview) return '审核中，暂不可下载';
    if (s === ResourceStatus.Rejected) return '未通过审核';
    if (s === ResourceStatus.Draft || s === ResourceStatus.Hidden) return '暂未发布';
    return '下载资源';
  }

  getResourceTypeName(type?: number): string {
    return resourceTypeName(type);
  }

  formatFileSize(size?: number): string {
    return fileSizeText(size);
  }

  splitKeywords(keywords?: string | null): string[] {
    if (!keywords) return [];
    return keywords
      .split(/[,，;；\s]+/)
      .map(k => k.trim())
      .filter(k => k.length > 0);
  }
}
