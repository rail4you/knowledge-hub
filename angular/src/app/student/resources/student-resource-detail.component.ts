import { ChangeDetectionStrategy, Component, OnInit, ViewChild, computed, inject, signal, DestroyRef } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '@abp/ng.core';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzRateModule } from 'ng-zorro-antd/rate';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ResourceService } from '../../proxy/resources/resource.service';
import { CourseResourceService } from '../../proxy/courses/course-resource.service';
import { CourseService } from '../../proxy/courses/course.service';
import { KnowledgeGraphService } from '../../proxy/knowledge-graph/knowledge-graph.service';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import { ResourceStatus } from '../../proxy/resources/enums/resource-status.enum';
import type { ResourceDto, ResourceVersionDto } from '../../proxy/resources/models';
import type { KnowledgeNodeDto } from '../../proxy/knowledge-graph/dtos/models';
import { FilePreviewComponent } from '../../shared/preview/file-preview.component';
import { ResourceCoverComponent } from '../../shared/resource-cover/resource-cover.component';
import { buildDownloadFileName } from '../../shared/download/download-file.util';
import { AuthFetchService } from '../../shared/utils/auth-fetch.service';
import { ResourceReviewComponent } from '../../search/resource-review/resource-review.component';
import { ResourceReviewService, type ResourceRatingSummaryDto } from '../../search/resource-review/resource-review.service';
import { RecommendationService, type RecommendedResourceDto } from '../../search/recommendation/recommendation.service';
import { AuthErrorService } from '../../core/auth/auth-error.service';
import { ClientCacheService } from '../../shared/cache/client-cache.service';
import { fileSizeText, resourceTypeName } from '../../shared/utils/resource-format.util';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { resourceFormatTag, resourceSourceName, resourceTypeLabel } from './student-resource-labels';

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

/** 「关联学习」条目 */
interface LearningLink {
  kind: '课程' | '知识点' | '相关资源';
  title: string;
  desc: string;
  route: string[] | null;
}

/**
 * 学生端 · 资源详情。
 *
 * 对齐原型 `#resource`：文档预览区 + 关联学习（最多 3 条）+ 来源与授权 + 版本 + 评价
 * + 收藏按钮 + 下载许可说明。
 *
 * 真实接口：
 *   · 详情     GET  /api/app/resource/{id}/with-versions
 *   · 版本     GET  /api/app/resource/versions/{id}
 *   · 收藏     POST /api/app/resource/collect|uncollect、is-collected
 *   · 评价     GET  /api/app/resource-review/rating-summary/{id}
 *   · 关联资源 GET  /api/app/resource-recommendation/related-resources/{id}
 *   · 知识点   GET  /api/app/knowledge-graph/resource-graph/{id}
 *   · 关联课程 GET  /api/app/course-resource/by-resource/{id} + /api/app/course/{id}
 */
@Component({
  selector: 'app-student-resource-detail',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterModule,
    NzIconModule,
    NzRateModule,
    FilePreviewComponent,
    ResourceReviewComponent,
    ResourceCoverComponent,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
  ],
  templateUrl: './student-resource-detail.component.html',
  styleUrls: ['./student-resource-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentResourceDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly resourceService = inject(ResourceService);
  private readonly courseResourceService = inject(CourseResourceService);
  private readonly courseService = inject(CourseService);
  private readonly knowledgeGraphService = inject(KnowledgeGraphService);
  private readonly reviewService = inject(ResourceReviewService);
  private readonly recommendationService = inject(RecommendationService);
  private readonly authErrorService = inject(AuthErrorService);
  private readonly authService = inject(AuthService);
  private readonly cache = inject(ClientCacheService);
  private readonly message = inject(NzMessageService);
  private readonly authFetch = inject(AuthFetchService);

  @ViewChild('filePreview') filePreview!: FilePreviewComponent;
  @ViewChild(ResourceReviewComponent) reviewForm?: ResourceReviewComponent;

  readonly resource = signal<ResourceDto | null>(null);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly isCollected = signal(false);
  readonly ratingSummary = signal<ResourceRatingSummaryDto | null>(null);

  /** 版本历史 */
  readonly versions = signal<ResourceVersionDto[]>([]);
  /** 版本 / 关联学习的加载态（避免请求返回前闪出空态） */
  readonly versionsLoading = signal(true);
  readonly learningLoading = signal(true);
  /** 关联学习并行请求的完成计数 */
  private learningPending = 0;
  /** 关联资源（相似资源推荐） */
  readonly relatedResources = signal<RecommendedResourceDto[]>([]);
  /** 关联知识点（知识图谱） */
  readonly knowledgePoints = signal<KnowledgeNodeDto[]>([]);
  /** 关联课程 */
  readonly relatedCourses = signal<{ id: string; title: string }[]>([]);

  readonly copyLinkSuccess = signal(false);

  /** 关联学习：课程 → 知识点 → 相关资源，最多 3 条 */
  readonly learningLinks = computed<LearningLink[]>(() => {
    const links: LearningLink[] = [];

    this.relatedCourses().forEach(c => {
      links.push({
        kind: '课程',
        title: c.title,
        desc: '该资源已关联到课程，可进入课程继续学习',
        route: ['/student/courses', c.id],
      });
    });

    this.knowledgePoints().forEach(k => {
      links.push({
        kind: '知识点',
        title: k.name || '未命名知识点',
        desc: k.description || '该资源被关联到以下知识点',
        route: null,
      });
    });

    this.relatedResources().forEach(r => {
      links.push({
        kind: '相关资源',
        title: r.resourceName,
        desc: r.categoryName ? `同模块 · ${r.categoryName}` : '内容相近的教学资源',
        route: ['/student/resources', r.resourceId],
      });
    });

    return links.slice(0, 3);
  });

  /** 格式角标（真实扩展名） */
  readonly formatTag = computed(() => resourceFormatTag(this.resource()));

  /** 来源（共享来源院校 / 所属机构 / 上传人） */
  readonly sourceName = computed(() => resourceSourceName(this.resource()));

  /** 所属专业（多专业时合并展示，来源为真实字段） */
  readonly majorText = computed(() => {
    const r = this.resource();
    if (!r) return '';
    if (r.majorName) return r.majorName;
    const names = r.majorNames || [];
    return names.length ? names.join('、') : '';
  });

  /** 下载权限文案 */
  readonly downloadState = computed(() => {
    const r = this.resource();
    if (!r) return '';
    if (r.isFileMissing) return '源文件缺失，暂不可下载';
    if (!this.isApproved()) return '审核通过后开放下载';
    return r.isDownloadable ? '允许下载' : '仅在线预览';
  });

  /** 下载许可说明（由真实字段拼装，不编造授权条款） */
  readonly licenseNote = computed(() => {
    const r = this.resource();
    if (!r) return '';
    const copyright = (r.copyrightInfo || '').trim();
    if (r.isFileMissing) return '资源源文件在服务器上缺失，暂不可下载和预览，请联系管理员重新上传。';
    if (copyright) return `版权许可：${copyright}`;
    if (r.isDownloadable === false) return '该资源未开放下载，仅支持在线预览。';
    return '下载需登录并通过资源审核；下载后请在授权范围内使用，并保留来源信息。';
  });

  ngOnInit(): void {
    // 订阅 paramMap：从相关资源跳转到同一路由的不同 id 时组件会被复用，
    // snapshot 只读一次会导致详情与评价都不刷新，这里改为响应式订阅。
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.loadDetail(id);
        this.loadLearning(id);
      } else {
        this.router.navigate(['/student/resources']);
      }
    });
  }

  /** 加载详情 */
  loadDetail(id: string): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.cache
      .load<ResourceDto>(DETAIL_CACHE_NS, `resource:${id}`, () => this.resourceService.getWithVersions(id))
      .subscribe({
        next: data => {
          this.resource.set(data);
          this.loading.set(false);
          this.loadCollectionStatus(id);
          this.loadRatingSummary(id);
          this.loadVersions(id);
        },
        error: err => {
          this.loading.set(false);
          if (err.status === 401 || err.status === 403) {
            this.authErrorService.setAuthError(
              err.status,
              err.error?.error?.message || err.error?.message || '您未获得授权！',
            );
          } else {
            this.loadFailed.set(true);
          }
        },
      });
  }

  /** 重新加载（错误态重试） */
  reload(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.loadDetail(id);
  }

  /** 版本历史 */
  private loadVersions(id: string): void {
    this.versionsLoading.set(true);
    this.cache
      .load<ResourceVersionDto[]>(DETAIL_CACHE_NS, `versions:${id}`, () => this.resourceService.getVersions(id))
      .subscribe({
        next: list => {
          this.versions.set(list || []);
          this.versionsLoading.set(false);
        },
        error: () => {
          this.versions.set([]);
          this.versionsLoading.set(false);
        },
      });
  }

  /** 关联学习：相关资源 + 知识点 + 关联课程 */
  private loadLearning(id: string): void {
    this.learningLoading.set(true);
    this.learningPending = 3;
    // 相似资源
    this.cache
      .load<RecommendedResourceDto[]>(DETAIL_CACHE_NS, `related:${id}`, () =>
        this.recommendationService.getRelatedResources(id, 3),
      )
      .subscribe({
        next: list => {
          this.relatedResources.set(list || []);
          this.learningDone();
        },
        error: () => {
          this.relatedResources.set([]);
          this.learningDone();
        },
      });

    // 知识点
    this.knowledgeGraphService.getResourceGraph(id).subscribe({
      next: graph => {
        const nodes = (graph?.nodes || []).filter(n => !n.name?.startsWith('素材:'));
        this.knowledgePoints.set(nodes);
        this.learningDone();
      },
      error: () => {
        this.knowledgePoints.set([]);
        this.learningDone();
      },
    });

    // 关联课程（先取关联关系，再取课程标题）
    this.courseResourceService.getByResource(id).subscribe({
      next: list => {
        const ids = (list || [])
          .map(x => x.courseId)
          .filter((cid): cid is string => !!cid)
          .slice(0, 2);
        // 课程标题是补充信息，先结束加载态，标题到达后会补进「关联学习」
        this.learningDone();
        if (ids.length === 0) {
          this.relatedCourses.set([]);
          return;
        }
        ids.forEach(cid => {
          this.courseService.get(cid).subscribe({
            next: course => {
              const title = (course?.title || '').trim();
              if (!title) return;
              this.relatedCourses.update(rows =>
                rows.some(r => r.id === cid) ? rows : [...rows, { id: cid, title }],
              );
            },
            error: () => undefined,
          });
        });
      },
      error: () => {
        this.relatedCourses.set([]);
        this.learningDone();
      },
    });
  }

  /** 关联学习的三个并行请求全部完成 */
  private learningDone(): void {
    this.learningPending = Math.max(0, this.learningPending - 1);
    if (this.learningPending === 0) this.learningLoading.set(false);
  }

  /** 收藏状态 */
  private loadCollectionStatus(id: string): void {
    if (!this.authService.isAuthenticated) return;
    this.cache
      .load<boolean>(DETAIL_CACHE_NS, `collected:${id}`, () => this.resourceService.isCollected(id))
      .subscribe({ next: v => this.isCollected.set(!!v) });
  }

  /** 评价汇总 */
  loadRatingSummary(id: string, force = false): void {
    const loader = () => this.reviewService.getRatingSummary(id);
    const request$ = force
      ? this.cache.reload<ResourceRatingSummaryDto>(DETAIL_CACHE_NS, `rating:${id}`, loader)
      : this.cache.load<ResourceRatingSummaryDto>(DETAIL_CACHE_NS, `rating:${id}`, loader);
    request$.subscribe({ next: s => this.ratingSummary.set(s) });
  }

  goBack(): void {
    this.router.navigate(['/student/resources']);
  }

  /** 评论区「写评价」按钮：聚焦评价输入框 */
  focusReviewForm(): void {
    this.reviewForm?.focusForm();
  }

  /** 在线预览 */
  preview(): void {
    const r = this.resource();
    if (!r?.id) return;
    let ext = r.fileExtension;
    if (!ext) {
      const fileName = r.originalFileName || r.name || '';
      const dot = fileName.lastIndexOf('.');
      if (dot >= 0) ext = fileName.substring(dot);
    }
    this.filePreview.open(
      r.id,
      r.originalFileName || r.name || '未命名',
      ext || '',
      r.fileSize || 0,
      r.isDownloadable !== false,
    );
  }

  /** 下载 */
  download(): void {
    const r = this.resource();
    if (!r?.id) return;
    if (r.isFileMissing) {
      this.message.warning(this.getDownloadTitle() || '资源文件缺失，暂时无法下载');
      return;
    }
    if (!r.isDownloadable) {
      this.message.warning('该资源不允许下载，仅支持在线预览');
      return;
    }
    if (!this.isApproved()) {
      this.message.warning(this.getDownloadTitle() || '资源不可下载');
      return;
    }
    const url = this.authFetch.withAccessToken(`/api/resource-file/${r.id}/download`);
    const a = document.createElement('a');
    a.href = url;
    const downloadName = buildDownloadFileName(r.originalFileName, r.name, r.fileExtension);
    if (downloadName) a.download = downloadName;
    a.click();
    this.message.success('下载已开始');
  }

  /** 收藏 / 取消收藏 */
  toggleCollection(): void {
    const r = this.resource();
    if (!r?.id) return;
    if (!this.authService.isAuthenticated) {
      this.message.info('登录后即可收藏资源');
      return;
    }
    const request$ = this.isCollected()
      ? this.resourceService.uncollect(r.id)
      : this.resourceService.collect(r.id);
    request$.subscribe({
      next: () => {
        const next = !this.isCollected();
        this.isCollected.set(next);
        this.cache.set(DETAIL_CACHE_NS, `collected:${r.id}`, next);
        this.message.success(next ? '已加入收藏' : '已取消收藏');
      },
      error: () => this.message.error('操作失败'),
    });
  }

  /** 复制分享链接 */
  copyLink(): void {
    const r = this.resource();
    if (!r?.id) return;
    const url = `${window.location.origin}/student/resources/${r.id}`;
    if (navigator.clipboard) {
      navigator.clipboard
        .writeText(url)
        .then(() => {
          this.copyLinkSuccess.set(true);
          this.message.success('链接已复制到剪贴板');
          setTimeout(() => this.copyLinkSuccess.set(false), 1800);
        })
        .catch(() => this.fallbackCopy(url));
    } else {
      this.fallbackCopy(url);
    }
  }

  private fallbackCopy(url: string): void {
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

  /** 资源是否可被「基于资源提问」 */
  canAskAboutResource(): boolean {
    const r = this.resource();
    if (!r) return false;
    if (!r.isIndexed) return false;
    return this.isApproved();
  }

  /** 已发布但尚未完成索引 */
  isAwaitingIndex(): boolean {
    const r = this.resource();
    if (!r) return false;
    if (r.isIndexed) return false;
    return this.isApproved();
  }

  /** 跳转到 AI 聊天并以当前资源作为预选上下文 */
  askAboutResource(): void {
    const r = this.resource();
    if (!r?.id) return;
    if (!this.canAskAboutResource()) return;
    if (!this.authService.isAuthenticated) {
      this.authErrorService.setAuthError(401, '请先登录后再使用 AI 文档问答');
      return;
    }
    this.router.navigate(['/student/ai/chat'], { queryParams: { resourceId: r.id } });
  }

  onReviewChanged(): void {
    const r = this.resource();
    if (r?.id) this.loadRatingSummary(r.id, true);
  }

  getResourceTypeIcon(type?: number): string {
    return RESOURCE_TYPE_ICONS[type ?? 0] || 'file-text';
  }

  /** 预览区类型（区分于枚举值，映射到视觉变体） */
  previewKind(): 'document' | 'video' | 'image' | 'audio' {
    const t = this.resource()?.resourceType;
    switch (t) {
      case ResourceType.Video:
        return 'video';
      case ResourceType.Audio:
        return 'audio';
      case ResourceType.Image:
        return 'image';
      default:
        return 'document';
    }
  }

  previewIcon(): string {
    switch (this.previewKind()) {
      case 'video':
        return 'play-circle';
      case 'audio':
        return 'sound';
      case 'image':
        return 'picture';
      default:
        return 'file-text';
    }
  }

  /** 资源是否被驳回 */
  isRejected(): boolean {
    return this.resource()?.status === ResourceStatus.Rejected;
  }

  /** 资源是否待审核 */
  isPendingReview(): boolean {
    return this.resource()?.status === ResourceStatus.PendingReview;
  }

  /** 是否已有后端生成的真实预览（缩略图 / 服务端首页图 / 视频画封面） */
  hasRealPreview(): boolean {
    const r = this.resource();
    if (!r) return false;
    const ms = r.mediaStatus ?? 0;
    if (ms === 0 /* None */ || ms === 10 /* Processing */) return false;
    return true;
  }

  /** 资源是否已通过审核（任一审核层级） */
  isApproved(): boolean {
    const s = this.resource()?.status;
    return s === ResourceStatus.SchoolApproved || s === ResourceStatus.LeagueApproved;
  }

  /** 下载按钮是否应该禁用 */
  isDownloadDisabled(): boolean {
    if (this.resource()?.isFileMissing) return true;
    if (!this.resource()?.isDownloadable) return true;
    const s = this.resource()?.status;
    return s !== ResourceStatus.SchoolApproved && s !== ResourceStatus.LeagueApproved;
  }

  /** 下载按钮 title 提示 */
  getDownloadTitle(): string {
    const r = this.resource();
    if (!r) return '';
    if (r.isFileMissing) return '资源文件缺失，暂时无法下载，请联系管理员重新上传';
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
    if (r.isFileMissing) return '文件缺失';
    if (!r.isDownloadable) return '不允许下载';
    const s = r.status;
    if (s === ResourceStatus.PendingReview) return '审核中，暂不可下载';
    if (s === ResourceStatus.Rejected) return '未通过审核';
    if (s === ResourceStatus.Draft || s === ResourceStatus.Hidden) return '暂未发布';
    return '下载资源';
  }

  /** 缩略图填充方式 */
  readonly previewFit = computed<'cover' | 'contain'>(() => {
    const t = this.resource()?.resourceType;
    return t === ResourceType.Video || t === ResourceType.Image ? 'cover' : 'contain';
  });

  getResourceTypeName(type?: number): string {
    return resourceTypeName(type);
  }

  /** 类型文案（本地枚举映射表） */
  typeLabel(type?: number): string {
    return resourceTypeLabel(type);
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

  /** 当前版本号文案 */
  versionLabel(): string {
    const r = this.resource();
    if (!r) return '';
    const current = r.currentVersion;
    return current ? `当前发布版 v${current}` : '当前发布版';
  }
}
