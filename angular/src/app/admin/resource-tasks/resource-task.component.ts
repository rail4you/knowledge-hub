import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ResourceTaskDto, ResourceTaskGroupDto, ResourceTaskService, TaskStatus } from './resource-task.service';

/**
 * 资源任务：合并媒体处理、文档索引、视频索引，按资源聚合为嵌套表格。
 * 父行为资源，展开后展示该资源下的全部任务；每个任务可独立重试。
 */
@Component({
  selector: 'app-resource-tasks',
  standalone: true,
  imports: [
    CommonModule, DatePipe, FormsModule,
    NzTableModule, NzTagModule, NzButtonModule, NzIconModule, NzProgressModule, NzSelectModule, NzInputModule,
  ],
  templateUrl: './resource-task.component.html',
  styleUrls: ['./resource-task.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceTaskComponent implements OnInit, OnDestroy {
  private readonly taskService = inject(ResourceTaskService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly groups = signal<ResourceTaskGroupDto[]>([]);
  readonly totalCount = signal(0);
  readonly expandSet = signal<Set<string>>(new Set<string>());

  pageIndex = 1;
  pageSize = 10;
  filter = '';
  taskStatus: TaskStatus | '' = '';
  /** 从资源进度页跳转带入的资源筛选。 */
  resourceIdFilter = signal<string | null>(null);

  readonly statusOptions = [
    { label: '全部状态', value: '' },
    { label: '排队中', value: 'pending' as TaskStatus },
    { label: '进行中', value: 'running' as TaskStatus },
    { label: '成功', value: 'success' as TaskStatus },
    { label: '部分失败', value: 'partial' as TaskStatus },
    { label: '失败', value: 'failed' as TaskStatus },
    { label: '已取消', value: 'cancelled' as TaskStatus },
  ];

  readonly hasFailed = computed(() => this.groups().some(g => g.failedCount > 0));

  private timer?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    const resourceId = this.route.snapshot.queryParamMap.get('resourceId');
    if (resourceId) {
      this.resourceIdFilter.set(resourceId);
      this.expandSet.set(new Set([resourceId]));
    }
    this.load();
    this.timer = setInterval(() => this.load(false), 5000);
  }

  ngOnDestroy(): void {
    if (this.timer) {
      clearInterval(this.timer);
    }
  }

  load(showLoading = true): void {
    if (showLoading) {
      this.loading.set(true);
    }
    this.taskService.getGroups({
      filter: this.filter?.trim() || undefined,
      resourceId: this.resourceIdFilter() || undefined,
      taskStatus: this.taskStatus || undefined,
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize,
    }).subscribe({
      next: result => {
        this.groups.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载资源任务失败');
      },
    });
  }

  clearResourceFilter(): void {
    this.resourceIdFilter.set(null);
    this.pageIndex = 1;
    this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
    this.load();
  }

  onStatusChange(): void {
    this.pageIndex = 1;
    this.load();
  }

  onSearch(): void {
    this.pageIndex = 1;
    this.load();
  }

  onPageChange(pageIndex: number): void {
    this.pageIndex = pageIndex;
    this.load();
  }

  isExpanded(resourceId: string): boolean {
    return this.expandSet().has(resourceId);
  }

  toggleGroup(resourceId: string): void {
    const next = new Set(this.expandSet());
    if (next.has(resourceId)) {
      next.delete(resourceId);
    } else {
      next.add(resourceId);
    }
    this.expandSet.set(next);
  }

  /** 查看该资源的全链路进度。 */
  openProgress(group: ResourceTaskGroupDto): void {
    this.router.navigate(['/admin/resource-progress'], { queryParams: { resourceId: group.resourceId } });
  }

  retry(task: ResourceTaskDto): void {
    this.taskService.retry({ resourceId: task.resourceId, kind: task.kind, taskId: task.id }).subscribe({
      next: () => {
        this.message.success('已重新排队');
        this.load(false);
      },
      error: () => this.message.error('重试失败'),
    });
  }

  /** 重试该资源下所有失败任务（任务相互独立，各自重试）。 */
  retryGroup(group: ResourceTaskGroupDto): void {
    const failed = (group.tasks || []).filter(t => t.status === 'failed' || t.status === 'partial');
    if (!failed.length) {
      return;
    }
    forkJoin(failed.map(t => this.taskService.retry({ resourceId: t.resourceId, kind: t.kind, taskId: t.id })
      .pipe(catchError(() => of(null))))).subscribe(() => {
      this.message.success('失败任务已重新排队');
      this.load(false);
    });
  }

  kindLabel(kind: string): string {
    switch (kind) {
      case 'media': return '媒体处理';
      case 'document-index': return '文档索引';
      case 'video-index': return '视频索引';
      default: return kind;
    }
  }

  kindColor(kind: string): string {
    switch (kind) {
      case 'media': return 'geekblue';
      case 'document-index': return 'cyan';
      case 'video-index': return 'purple';
      default: return 'default';
    }
  }

  resourceStatusLabel(status: number): string {
    switch (status) {
      case 0: return '草稿';
      case 1: return '待审核';
      case 2: return '院校通过';
      case 3: return '联盟通过';
      case 4: return '已驳回';
      case 5: return '已隐藏';
      default: return '未知';
    }
  }

  resourceStatusColor(status: number): string {
    switch (status) {
      case 0: return 'default';
      case 1: return 'processing';
      case 2: return 'cyan';
      case 3: return 'success';
      case 4: return 'error';
      case 5: return 'default';
      default: return 'default';
    }
  }

  taskStatusLabel(status?: TaskStatus | null): string {
    switch (status) {
      case 'pending': return '排队中';
      case 'running': return '进行中';
      case 'success': return '成功';
      case 'partial': return '部分失败';
      case 'failed': return '失败';
      case 'cancelled': return '已取消';
      default: return '未知';
    }
  }

  taskStatusColor(status?: TaskStatus | null): string {
    switch (status) {
      case 'success': return 'success';
      case 'running': return 'processing';
      case 'pending': return 'default';
      case 'partial': return 'warning';
      case 'failed': return 'error';
      case 'cancelled': return 'default';
      default: return 'default';
    }
  }

  artifactLabel(kind?: number): string {
    // 0=列表封面缩略图（图片缩放 / 视频抽帧），10=Office 文档预览
    return kind === 0 ? '列表封面缩略图' : 'Office 文档预览';
  }

  /** 把后端的变体代号（w400 / full 等）翻译成可读的中文描述。 */
  artifactVariantLabel(kind?: number, variant?: string | null): string {
    if (!variant) return '';
    if (kind === 0) {
      // 缩略图：变体是宽度代号
      const m = variant.match(/^w(\d+)$/);
      if (m) return `${m[1]}px 宽`;
      return variant;
    }
    if (kind === 10) {
      // 预览 PDF：变体是版本（full=完整版）
      if (variant === 'full') return '完整版';
      return variant;
    }
    return variant;
  }

  /** 后端 ResourceArtifactState：0=Ready, 40=Failed */
  artifactStateLabel(state?: number | null): string {
    if (state === 0) return '已生成';
    if (state === 40) return '生成失败';
    return '处理中';
  }

  /**
   * 索引类任务（文档索引 / 视频索引）在“任务状态”列的统一中文文案。
   * 状态列不再显示空空的“暂无生成物”，而是讲明该资源的索引是否可用。
   */
  indexStatusLabel(status?: TaskStatus, kind?: string): string {
    const isVideo = kind === 'video-index';
    const noun = isVideo ? '视频索引' : '文档索引';
    switch (status) {
      case 'success': return `${noun}已生成`;
      case 'failed': return `${noun}生成失败`;
      case 'running': return `正在生成${noun}`;
      case 'pending': return `等待生成${noun}`;
      case 'cancelled': return `${noun}已取消`;
      default: return `${noun}状态未知`;
    }
  }

  /** 索引类任务的状态色调（颜色。 */
  indexStatusTone(status?: TaskStatus): 'ok' | 'failed' | 'running' | 'pending' {
    switch (status) {
      case 'success': return 'ok';
      case 'failed': return 'failed';
      case 'running': return 'running';
      default: return 'pending';
    }
  }
}
