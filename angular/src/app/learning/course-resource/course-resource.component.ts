import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CourseContextService } from '../course-context.service';
import { take } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { Rest, RestService } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseService } from '../../proxy/courses/course.service';
import { CourseResourceService } from '../../proxy/courses/course-resource.service';
import { ChunkUploadService } from '../../proxy/controllers/chunk-upload.service';
import { ImageGenerationComponent } from '../../ai/image-generation/image-generation.component';
import { VideoGenerationComponent } from '../../ai/video-generation/video-generation.component';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import { ResourceStatus } from '../../proxy/resources/enums/resource-status.enum';
import type { CourseDto, CourseResourceDto } from '../../proxy/courses/dtos/models';

/**
 * 代理里 CourseResourceDto 还没有 Status / IsAiGenerated（后端新增字段，代理未重新生成），
 * 这里用本地扩展类型承接，避免手动改动 proxy 文件。
 */
interface CourseResourceRow extends CourseResourceDto {
  status?: ResourceStatus;
  isAiGenerated?: boolean;
  resourceCreationTime?: string;
  resourceCreatorId?: string;
  canDelete?: boolean;
}

/**
 * 课程资源：资源本身已带课程关联（Resource ↔ CourseResource），
 * 因此这里直接按课程过滤展示，不再需要「手动关联」这一步。
 * AI 生成的图片 / 短视频也从这里发起，生成后作为普通资源走审核流程（待审核 → 审核通过可用）。
 */
@Component({
  selector: 'app-course-resource',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzTagModule,
    NzIconModule,
    NzSpinModule,
    NzEmptyModule,
    NzSelectModule,
    NzTableModule,
    NzTooltipModule,
    NzModalModule,
    NzTabsModule,
    NzProgressModule,
    NzPopconfirmModule,
    ImageGenerationComponent,
    VideoGenerationComponent,
  ],
  templateUrl: './course-resource.component.html',
  styleUrls: ['./course-resource.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseResourceComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly courseContext = inject(CourseContextService);
  private readonly courseResourceService = inject(CourseResourceService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  private readonly route = inject(ActivatedRoute);
  private readonly restService = inject(RestService);
  private readonly chunkUpload = inject(ChunkUploadService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly resources = signal<CourseResourceRow[]>([]);
  /** 全量关联（前端过滤 / 分页的源数据） */
  private readonly allRows = signal<CourseResourceRow[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly keyword = signal('');
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  // ── 上传资源弹窗 ──
  readonly uploadVisible = signal(false);
  readonly uploading = signal(false);
  readonly uploadProgress = signal(0);
  readonly uploadName = signal('');
  readonly uploadDescription = signal('');
  readonly uploadFile = signal<File | null>(null);
  readonly uploadFileName = signal('');
  private static readonly CHUNK_SIZE = 1024 * 1024;

  // ── AI 生成资源弹窗（两个 tab：图片 / 短视频） ──
  readonly aiVisible = signal(false);
  readonly aiTabIndex = signal(0);

  readonly hasCourse = computed(() => !!this.selectedCourseId());
  readonly selectedCourse = computed(
    () => this.courses().find((c) => c.id === this.selectedCourseId()) ?? null,
  );

  /** 当前页状态分布：让教师一眼看到有多少资源还在等审核 */
  readonly stats = computed(() => {
    const list = this.resources();
    return {
      pending: list.filter(
        (x) => x.status === ResourceStatus.PendingReview || x.status === ResourceStatus.Draft,
      ).length,
      approved: list.filter(
        (x) =>
          x.status === ResourceStatus.SchoolApproved || x.status === ResourceStatus.LeagueApproved,
      ).length,
      ai: list.filter((x) => (x as any).isAiGenerated).length,
    };
  });

  ngOnInit(): void {
    this.loadCourses();
    // 支持从「课程工作台」带 courseId 直达：进入即选中该课程，免去再次选课
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      // 优先级：URL 上的 courseId > 全局「当前课程」
      const courseId = params.get('courseId') || this.courseContext.currentCourseId();
      if (courseId) {
        this.onCourseSelected(courseId);
      }
    });
  }

  loadCourses(): void {
    this.courseService.getList({ maxResultCount: 200, skipCount: 0 } as any).subscribe({
      next: (result) => this.courses.set(result.items || []),
      error: () => this.message.error('加载课程列表失败'),
    });
  }

  onCourseSelected(courseId: string | null): void {
    this.courseContext.setCourse(courseId);
    this.selectedCourseId.set(courseId);
    this.pageIndex.set(1);
    this.resources.set([]);
    this.totalCount.set(0);
    if (courseId) {
      this.loadResources();
    }
  }

  /**
   * 课程资源列表：直接取「课程 ↔ 资源」关联（后端已跨租户 join 资源），
   * 不再需要手动关联；关键词过滤与分页在前端完成（单课程资源量有限）。
   */
  loadResources(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.courseResourceService.getByCourse(courseId).subscribe({
      next: (data) => {
        const rows = (data || []) as unknown as CourseResourceRow[];
        this.allRows.set(rows);
        this.applyFilter();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载课程资源失败');
      },
    });
  }

  private applyFilter(): void {
    const kw = this.keyword().trim().toLowerCase();
    let rows = this.allRows();
    if (kw) {
      rows = rows.filter(
        (r) =>
          (r.resourceName || r.displayName || '').toLowerCase().includes(kw) ||
          (r.description || '').toLowerCase().includes(kw),
      );
    }
    this.totalCount.set(rows.length);
    const start = (this.pageIndex() - 1) * this.pageSize();
    this.resources.set(rows.slice(start, start + this.pageSize()));
  }

  onKeywordChange(value: string): void {
    this.keyword.set(value);
  }

  onSearch(): void {
    this.pageIndex.set(1);
    this.applyFilter();
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
    this.applyFilter();
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
    this.applyFilter();
  }

  // ── AI 生成资源：弹窗内两个 tab（图片 / 短视频），生成结果直接作为课程资源待审核 ──
  openAiGenerate(): void {
    if (!this.hasCourse()) {
      this.message.warning('请先选择课程');
      return;
    }
    this.aiTabIndex.set(0);
    this.aiVisible.set(true);
  }

  closeAiGenerate(): void {
    this.aiVisible.set(false);
    this.loadResources();
  }

  onAiGenerated(resourceId: string | null): void {
    if (!resourceId) {
      // 没有课程上下文或登记失败时，仍刷新一次，避免教师在老页面看不到结果
      return;
    }
    this.message.success('已生成并加入课程资源，等待审核通过');
    this.loadResources();
  }

  // ── 上传资源：分块上传 → 创建资源并关联本课程 → 提交审核 ──
  openUpload(): void {
    if (!this.hasCourse()) {
      this.message.warning('请先选择课程');
      return;
    }
    this.uploadName.set('');
    this.uploadDescription.set('');
    this.uploadFile.set(null);
    this.uploadFileName.set('');
    this.uploadProgress.set(0);
    this.uploadVisible.set(true);
  }

  closeUpload(): void {
    if (this.uploading()) return;
    this.uploadVisible.set(false);
  }

  onUploadFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files && input.files.length > 0 ? input.files[0] : null;
    this.uploadFile.set(file);
    this.uploadFileName.set(file ? file.name : '');
    if (file && !this.uploadName().trim()) {
      this.uploadName.set(file.name.replace(/\.[^.]+$/, ''));
    }
  }

  async submitUpload(): Promise<void> {
    const courseId = this.selectedCourseId();
    const file = this.uploadFile();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    if (!file) {
      this.message.warning('请选择要上传的文件');
      return;
    }
    const name = this.uploadName().trim() || file.name;

    this.uploading.set(true);
    this.uploadProgress.set(0);
    try {
      const info = await this.uploadChunked(file);
      const resource: any = await this.restService
        .request<any, any>({
          method: 'POST',
          url: '/api/app/resource',
          body: {
            name,
            description: this.uploadDescription().trim() || undefined,
            resourceType: this.guessResourceType(info.fileExtension || file.name),
            courseIds: [courseId],
            filePath: info.filePath,
            fileSize: info.fileSize,
            fileExtension: info.fileExtension,
            originalFileName: info.originalFileName || file.name,
            isDownloadable: true,
          },
        })
        .toPromise();

      // 上传后直接提交审核，教师不必再去资源中心点一次「提交审核」
      if (resource?.id) {
        await this.restService
          .request<any, void>({
            method: 'POST',
            url: `/api/app/resource/${resource.id}/submit-for-review`,
          })
          .toPromise()
          .catch(() => undefined);
      }

      this.uploading.set(false);
      this.uploadVisible.set(false);
      this.message.success('资源已上传并提交审核');
      this.loadResources();
    } catch (err: any) {
      this.uploading.set(false);
      const detail =
        err?.error?.error?.message || err?.error?.message || err?.message || '上传失败';
      this.message.error(detail);
    }
  }

  private async uploadChunked(
    file: File,
  ): Promise<{ filePath: string; fileSize: number; fileExtension: string; originalFileName: string }> {
    const initiated = await this.chunkUpload
      .initiateUploadByInput({
        fileName: file.name,
        totalSize: file.size,
        chunkSize: CourseResourceComponent.CHUNK_SIZE,
      })
      .toPromise();
    if (!initiated?.uploadId) {
      throw new Error('发起上传失败');
    }

    const totalChunks = initiated.totalChunks || 1;
    for (let i = 0; i < totalChunks; i++) {
      const start = i * CourseResourceComponent.CHUNK_SIZE;
      const chunk = file.slice(start, Math.min(start + CourseResourceComponent.CHUNK_SIZE, file.size));
      const formData = new FormData();
      formData.append('file', chunk, file.name);
      formData.append('uploadId', initiated.uploadId);
      formData.append('fileName', file.name);
      formData.append('chunkNumber', String(i));
      const ok = await this.restService
        .request<any, boolean>({ method: 'POST', url: '/api/app/chunk-upload/upload', body: formData })
        .toPromise();
      if (ok !== true) {
        throw new Error(`第 ${i + 1}/${totalChunks} 片上传失败，请重试`);
      }
      this.uploadProgress.set(Math.round(((i + 1) / totalChunks) * 100));
    }

    const done = await this.chunkUpload
      .completeUploadByInput({
        uploadId: initiated.uploadId,
        fileName: file.name,
        totalChunks,
      })
      .toPromise();
    if (!done?.filePath) {
      throw new Error('上传完成但未返回文件信息');
    }
    return {
      filePath: done.filePath!,
      fileSize: done.fileSize ?? file.size,
      fileExtension: done.fileExtension ?? '',
      originalFileName: done.originalFileName ?? file.name,
    };
  }

  private guessResourceType(fileExtensionOrName: string): ResourceType {
    const ext = (fileExtensionOrName || '').toLowerCase().replace(/^.*\./, '');
    if (['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp', 'svg'].includes(ext)) return ResourceType.Image;
    if (['mp4', 'mov', 'avi', 'mkv', 'webm', 'flv', 'm4v'].includes(ext)) return ResourceType.Video;
    if (['mp3', 'wav', 'aac', 'flac', 'm4a'].includes(ext)) return ResourceType.Audio;
    if (['ppt', 'pptx'].includes(ext)) return ResourceType.PPT;
    return ResourceType.Document;
  }

  // ── 删除：创建者可删除自己未审核通过的资源（无需再走审核） ──
  canDelete(row: CourseResourceRow): boolean {
    return !!row.canDelete;
  }

  deleteResource(row: CourseResourceRow): void {
    this.restService
      .request<any, void>({
        method: 'DELETE',
        url: `/api/app/resource/${row.resourceId}/own-unapproved`,
      })
      .subscribe({
        next: () => {
          this.message.success('已删除');
          this.loadResources();
        },
        error: (err: any) => {
          const detail =
            err?.error?.error?.message || err?.error?.message || '删除失败';
          this.message.error(detail);
        },
      });
  }

  // ── 展示辅助 ──
  isAiGenerated(resource: CourseResourceRow): boolean {
    return !!resource.isAiGenerated;
  }

  statusLabel(status: ResourceStatus | undefined): string {
    switch (status) {
      case ResourceStatus.Draft:
        return '草稿';
      case ResourceStatus.PendingReview:
        return '待审核';
      case ResourceStatus.SchoolApproved:
        return '本校通过';
      case ResourceStatus.LeagueApproved:
        return '联盟通过';
      case ResourceStatus.Rejected:
        return '已驳回';
      case ResourceStatus.Hidden:
        return '已下架';
      default:
        return '-';
    }
  }

  statusColor(status: ResourceStatus | undefined): string {
    switch (status) {
      case ResourceStatus.PendingReview:
        return 'orange';
      case ResourceStatus.SchoolApproved:
        return 'blue';
      case ResourceStatus.LeagueApproved:
        return 'green';
      case ResourceStatus.Rejected:
        return 'red';
      case ResourceStatus.Hidden:
        return 'default';
      default:
        return 'default';
    }
  }

  isUsable(status: ResourceStatus | undefined): boolean {
    return status === ResourceStatus.SchoolApproved || status === ResourceStatus.LeagueApproved;
  }

  getResourceTypeLabel(type: ResourceType | undefined): string {
    if (type === undefined || type === null) return '';
    const map: Record<number, string> = {
      [ResourceType.Document]: '文档',
      [ResourceType.Video]: '视频',
      [ResourceType.Audio]: '音频',
      [ResourceType.Image]: '图片',
      [ResourceType.PPT]: 'PPT',
    };
    return map[type] ?? '其他';
  }

  getResourceTypeColor(type: ResourceType | undefined): string {
    if (type === undefined || type === null) return 'default';
    const map: Record<number, string> = {
      [ResourceType.Document]: 'blue',
      [ResourceType.Video]: 'purple',
      [ResourceType.Audio]: 'cyan',
      [ResourceType.Image]: 'green',
      [ResourceType.PPT]: 'orange',
    };
    return map[type] ?? 'default';
  }
}
