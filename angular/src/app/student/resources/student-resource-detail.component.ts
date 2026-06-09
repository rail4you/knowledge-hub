import { ChangeDetectionStrategy, Component, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzRateModule } from 'ng-zorro-antd/rate';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ResourceService } from '../../proxy/resources/resource.service';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import type { ResourceDto } from '../../proxy/resources/models';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { ResourceReviewComponent } from '../../search/resource-review/resource-review.component';
import { ResourceReviewService, type ResourceRatingSummaryDto } from '../../search/resource-review/resource-review.service';
import { RecommendationService, type RecommendedResourceDto } from '../../search/recommendation/recommendation.service';
import { AuthErrorService } from '../../core/auth/auth-error.service';

@Component({
  selector: 'app-student-resource-detail',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzButtonModule,
    NzSpinModule,
    NzRateModule,
    NzTagModule,
    FilePreviewComponent,
    ResourceReviewComponent,
  ],
  templateUrl: './student-resource-detail.component.html',
  styleUrls: ['./student-resource-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentResourceDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly resourceService = inject(ResourceService);
  private readonly reviewService = inject(ResourceReviewService);
  private readonly recommendationService = inject(RecommendationService);
  private readonly authErrorService = inject(AuthErrorService);
  private readonly message = inject(NzMessageService);

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  readonly resource = signal<ResourceDto | null>(null);
  readonly loading = signal(false);
  readonly isCollected = signal(false);
  readonly ratingSummary = signal<ResourceRatingSummaryDto | null>(null);

  readonly relatedResources = signal<RecommendedResourceDto[]>([]);
  readonly relatedLoading = signal(false);

  readonly copyLinkSuccess = signal(false);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadDetail(id);
      this.loadRelated(id);
    } else {
      this.router.navigate(['/student/resources']);
    }
  }

  loadDetail(id: string) {
    this.loading.set(true);
    this.resourceService.getWithVersions(id).subscribe({
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
    this.resourceService.isCollected(id).subscribe({
      next: (v) => this.isCollected.set(!!v)
    });
  }

  loadRatingSummary(id: string) {
    this.reviewService.getRatingSummary(id).subscribe({
      next: (s) => this.ratingSummary.set(s)
    });
  }

  loadRelated(id: string) {
    this.relatedLoading.set(true);
    this.recommendationService.getRelatedResources(id, 8).subscribe({
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
      r.fileSize || 0
    );
  }

  download() {
    const r = this.resource();
    if (!r?.id) return;
    const url = `/api/resource-file/${r.id}/download`;
    const a = document.createElement('a');
    a.href = url;
    a.download = r.originalFileName || r.name || 'download';
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
    if (r?.id) this.loadRatingSummary(r.id);
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

  splitKeywords(keywords?: string | null): string[] {
    if (!keywords) return [];
    return keywords
      .split(/[,，;；\s]+/)
      .map(k => k.trim())
      .filter(k => k.length > 0);
  }
}
