import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTimelineModule } from 'ng-zorro-antd/timeline';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import {
  ResourceTrackingResourceDto,
  ResourceTrackingService,
  ResourceTrackingTimelineDto,
} from './resource-tracking.service';

/**
 * 资源全链路任务跟踪：左侧资源列表 + 右侧时间轴。
 * 聚合「上传 → 生成预览 → 院校审核 → 生成索引 → 联盟审核 → 学生可见」各阶段任务，
 * 失败任务可独立重试（媒体 / 文档索引 / 视频索引）。
 */
@Component({
  selector: 'app-resource-tracking',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, FormsModule,
    NzButtonModule, NzEmptyModule, NzIconModule, NzInputModule, NzProgressModule,
    NzSelectModule, NzSpinModule, NzTagModule, NzTimelineModule, NzTooltipModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tracking">
      <header class="page-header">
        <div class="page-header__text">
          <h1>资源进度</h1>
          <p>按资源查看从上传、预览生成、两级审核到索引建立的完整链路，失败任务可单独重试。</p>
        </div>
        <div class="page-header__actions">
          <button nz-button nzType="default" (click)="refresh()">
            <i nz-icon nzType="reload" [nzSpin]="listLoading() || timelineLoading()"></i>
            刷新
          </button>
        </div>
      </header>

      <div class="tracking-body">
        <!-- 左侧资源列表 -->
        <aside class="resource-panel">
          <div class="filters">
            <input
              nz-input
              placeholder="搜索资源名称"
              [ngModel]="filter"
              (ngModelChange)="filter = $event"
              (keyup.enter)="reload()"
            />
            <nz-select
              class="status-select"
              [ngModel]="statusFilter"
              (ngModelChange)="onStatusChange($event)"
            >
              @for (opt of statusOptions; track opt.value) {
                <nz-option [nzValue]="opt.value" [nzLabel]="opt.label"></nz-option>
              }
            </nz-select>
          </div>

          <div class="resource-list">
            @for (r of resources(); track r.id) {
              <div
                class="resource-item"
                [class.active]="selectedId() === r.id"
                (click)="selectResource(r)"
              >
                <div class="resource-item__name" [nz-tooltip]="r.name">{{ r.name }}</div>
                <div class="resource-item__meta">
                  <nz-tag [nzColor]="statusColor(r.status)">{{ statusLabel(r.status) }}</nz-tag>
                  <span class="resource-item__ver">V{{ r.currentVersion }}</span>
                  @if (r.tenantName) {
                    <span class="resource-item__tenant">{{ r.tenantName }}</span>
                  }
                </div>
              </div>
            }
            @if (listLoading() && resources().length === 0) {
              <div class="list-loading">加载中…</div>
            }
            @if (!listLoading() && resources().length === 0) {
              <nz-empty nzNotFoundContent="暂无资源"></nz-empty>
            }
          </div>
        </aside>

        <!-- 右侧时间轴 -->
        <main class="timeline-panel">
          @if (timeline(); as t) {
            <div class="timeline-head">
              <div class="timeline-head__title">
                <h3>{{ t.resourceName }}</h3>
                <div class="timeline-head__tags">
                  <nz-tag [nzColor]="statusColor(t.status)">{{ statusLabel(t.status) }}</nz-tag>
                  <nz-tag [nzColor]="mediaStatusColor(t.mediaStatus)">媒体：{{ mediaStatusLabel(t.mediaStatus) }}</nz-tag>
                  <nz-tag [nzColor]="t.visibleToStudents ? 'success' : 'default'">
                    {{ t.visibleToStudents ? '学生端可见' : '学生端不可见' }}
                  </nz-tag>
                  <span class="timeline-head__meta">V{{ t.currentVersion }}</span>
                  @if (t.tenantName) {
                    <span class="timeline-head__meta">{{ t.tenantName }}</span>
                  }
                </div>
              </div>
            </div>

            <nz-spin [nzSpinning]="timelineLoading()">
              <nz-timeline>
                @for (step of t.steps; track step.key) {
                  <nz-timeline-item [nzColor]="stepDotColor(step.status)">
                    <div class="step" [class.step--failed]="step.status === 'failed'">
                      <div class="step__head">
                        <span class="step__title">{{ step.title }}</span>
                        <nz-tag [nzColor]="stepTagColor(step.status)">{{ stepStatusLabel(step.status) }}</nz-tag>
                        @if (step.time) {
                          <span class="step__time">{{ step.time | date:'yyyy-MM-dd HH:mm:ss' }}</span>
                        }
                      </div>
                      @if (step.description) {
                        <div class="step__desc">{{ step.description }}</div>
                      }
                      @if (step.errorMessage) {
                        <div class="step__error"><i nz-icon nzType="exclamation-circle"></i> {{ step.errorMessage }}</div>
                      }

                      @if (step.tasks.length) {
                        <div class="task-list">
                          @for (task of step.tasks; track task.id) {
                            <div class="task" [class.task--failed]="task.status === 'failed' || task.status === 'partial'">
                              <div class="task__head">
                                <span class="task__title">{{ task.title }}</span>
                                <nz-tag [nzColor]="taskStatusColor(task.status)">{{ taskStatusLabel(task.status) }}</nz-tag>
                                @if (task.retryCount > 0) {
                                  <span class="task__retry-count">重试 {{ task.retryCount }} 次</span>
                                }
                                <button nz-button nzType="link" nzSize="small" (click)="openTasks(t.resourceId)">
                                  <i nz-icon nzType="arrow-right"></i> 查看任务
                                </button>
                              </div>

                              @if (task.status === 'running' || (task.progress > 0 && task.progress < 100)) {
                                <nz-progress
                                  [nzPercent]="task.progress"
                                  [nzStatus]="task.status === 'failed' ? 'exception' : 'active'"
                                  nzSize="small"
                                ></nz-progress>
                              }

                              <div class="task__meta">
                                @if (task.message) { <span>{{ task.message }}</span> }
                                @if (task.startedAt) { <span>开始 {{ task.startedAt | date:'MM-dd HH:mm:ss' }}</span> }
                                @if (task.completedAt) { <span>完成 {{ task.completedAt | date:'MM-dd HH:mm:ss' }}</span> }
                                <span>创建 {{ task.creationTime | date:'MM-dd HH:mm:ss' }}</span>
                              </div>

                              @if (task.errorMessage) {
                                <div class="task__error">{{ task.errorMessage }}</div>
                              }

                              @if (task.artifacts.length) {
                                <div class="artifact-list">
                                  @for (a of task.artifacts; track a.kind + a.variant) {
                                    <span class="artifact" [class.artifact--failed]="a.state === 'failed'"
                                          [nz-tooltip]="a.errorMessage || ''">
                                      <i nz-icon [nzType]="a.state === 'failed' ? 'close-circle' : 'check-circle'"></i>
                                      {{ artifactLabel(a.kind) }}
                                      @if (a.sizeBytes) { ({{ a.sizeBytes | number }} B) }
                                    </span>
                                  }
                                </div>
                              }
                            </div>
                          }
                        </div>
                      }
                    </div>
                  </nz-timeline-item>
                }
              </nz-timeline>
            </nz-spin>
          } @else {
            <div class="timeline-empty">
              <nz-empty nzNotFoundContent="请选择左侧资源查看全链路任务"></nz-empty>
            </div>
          }
        </main>
      </div>
    </div>
  `,
  styles: [`
    .tracking { padding: 16px; }
    /* 标题容器：与其他管理页一致的卡片式头部 */
    .page-header {
      display: flex; align-items: flex-start; justify-content: space-between; gap: 16px; flex-wrap: wrap;
      flex-shrink: 0; margin-bottom: 16px; padding: 16px 20px;
      background: var(--kh-panel, #fff);
      border: 1px solid var(--kh-line, #e7edf5);
      border-radius: var(--kh-r-shell, 12px);
      box-shadow: var(--kh-shadow-card, 0 1px 4px rgba(0, 0, 0, .04));
    }
    .page-header__text h1 { margin: 0 0 4px; font-size: 20px; font-weight: 700; color: var(--kh-ink, #1f2937); line-height: 1.3; }
    .page-header__text p { margin: 0; color: var(--kh-muted, #8c8c8c); font-size: 13px; line-height: 1.5; }
    .page-header__actions { flex-shrink: 0; }

    /* 左右两栏固定为页面高度，内容超出时各栏内部滚动 */
    .tracking-body { display: flex; gap: 16px; align-items: stretch; height: calc(100vh - 215px); }
    @media (max-width: 900px) { .tracking-body { flex-direction: column; height: auto; } }

    .resource-panel { flex: 0 0 300px; min-height: 0; display: flex; flex-direction: column; overflow: hidden; background: var(--kh-card-bg, #fff); border: 1px solid var(--kh-border, #e7edf5); border-radius: 8px; padding: 12px; }
    .filters { display: flex; gap: 8px; margin-bottom: 12px; }
    .status-select { width: 120px; flex: none; }
    .resource-list { display: flex; flex-direction: column; gap: 6px; flex: 1 1 auto; min-height: 0; overflow-y: auto; padding-right: 4px; }

    /* 让滚动条在 macOS 上也可见 */
    .resource-list, .timeline-panel { scrollbar-width: thin; scrollbar-color: #c9d3e0 transparent; }
    .resource-list::-webkit-scrollbar, .timeline-panel::-webkit-scrollbar { width: 8px; height: 8px; }
    .resource-list::-webkit-scrollbar-thumb, .timeline-panel::-webkit-scrollbar-thumb { background: #c9d3e0; border-radius: 4px; }
    .resource-list::-webkit-scrollbar-thumb:hover, .timeline-panel::-webkit-scrollbar-thumb:hover { background: #a9b6c6; }
    .resource-list::-webkit-scrollbar-track, .timeline-panel::-webkit-scrollbar-track { background: transparent; }
    .resource-item { padding: 8px 10px; border-radius: 6px; cursor: pointer; border: 1px solid transparent; transition: background .15s; }
    .resource-item:hover { background: #f5f9ff; }
    .resource-item.active { background: #e6f4ff; border-color: #91caff; }
    .resource-item__name { font-weight: 500; font-size: 13px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .resource-item__meta { display: flex; align-items: center; gap: 6px; margin-top: 4px; font-size: 12px; color: #8c9aab; }
    .resource-item__tenant { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .list-loading { padding: 16px; text-align: center; color: #8c9aab; font-size: 13px; }

    .timeline-panel { flex: 1 1 auto; min-width: 0; overflow-y: auto; background: var(--kh-card-bg, #fff); border: 1px solid var(--kh-border, #e7edf5); border-radius: 8px; padding: 16px 20px; }
    .timeline-head { margin-bottom: 16px; }
    .timeline-head__title h3 { margin: 0 0 6px; font-size: 16px; font-weight: 600; }
    .timeline-head__tags { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .timeline-head__meta { color: #8c9aab; font-size: 12px; }
    .timeline-empty { padding: 60px 0; }

    .step__head { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .step__title { font-weight: 600; font-size: 14px; }
    .step__time { color: #8c9aab; font-size: 12px; }
    .step__desc { margin-top: 4px; color: #5f6f81; font-size: 13px; }
    .step__error { margin-top: 4px; color: #f5222d; font-size: 12px; }

    .task-list { display: flex; flex-direction: column; gap: 8px; margin-top: 8px; }
    .task { border: 1px solid #eef3f9; border-radius: 6px; padding: 8px 10px; background: #fafcff; }
    .task--failed { border-color: #ffccc7; background: #fff2f0; }
    .task__head { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .task__title { font-size: 13px; font-weight: 500; }
    .task__retry-count { color: #fa8c16; font-size: 12px; }
    .task__meta { display: flex; gap: 12px; flex-wrap: wrap; margin-top: 4px; color: #8c9aab; font-size: 12px; }
    .task__error { margin-top: 4px; color: #f5222d; font-size: 12px; }
    .artifact-list { display: flex; gap: 10px; flex-wrap: wrap; margin-top: 6px; }
    .artifact { color: #52c41a; font-size: 12px; }
    .artifact--failed { color: #f5222d; }
  `],
})
export class ResourceTrackingComponent implements OnInit, OnDestroy {
  private readonly service = inject(ResourceTrackingService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** 从资源任务页跳转带入的资源（URL query: resourceId）。 */
  private requestedResourceId: string | null = null;

  readonly resources = signal<ResourceTrackingResourceDto[]>([]);
  readonly totalCount = signal(0);
  readonly listLoading = signal(false);
  readonly timeline = signal<ResourceTrackingTimelineDto | null>(null);
  readonly timelineLoading = signal(false);
  readonly selectedId = signal<string | null>(null);

  filter = '';
  statusFilter = -1;

  readonly statusOptions = [
    { label: '全部状态', value: -1 },
    { label: '草稿', value: 0 },
    { label: '待审核', value: 1 },
    { label: '院校通过', value: 2 },
    { label: '联盟通过', value: 3 },
    { label: '已驳回', value: 4 },
    { label: '已隐藏', value: 5 },
  ];

  private timelineTimer?: ReturnType<typeof setInterval>;
  private listTimer?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    this.requestedResourceId = this.route.snapshot.queryParamMap.get('resourceId');
    // 带 resourceId 时直接加载该资源时间轴，无需等列表
    if (this.requestedResourceId) {
      this.selectedId.set(this.requestedResourceId);
      this.loadTimeline(this.requestedResourceId);
    }
    this.loadResources();
    // 任务进度实时刷新
    this.timelineTimer = setInterval(() => {
      const id = this.selectedId();
      if (id) {
        this.loadTimeline(id, false);
      }
    }, 5000);
    this.listTimer = setInterval(() => this.loadResources(false), 15000);
  }

  ngOnDestroy(): void {
    if (this.timelineTimer) { clearInterval(this.timelineTimer); }
    if (this.listTimer) { clearInterval(this.listTimer); }
  }

  reload(): void {
    this.loadResources();
  }

  refresh(): void {
    this.loadResources();
    const id = this.selectedId();
    if (id) {
      this.loadTimeline(id);
    }
  }

  onStatusChange(value: number): void {
    this.statusFilter = value;
    this.loadResources();
  }

  loadResources(showLoading = true): void {
    if (showLoading) { this.listLoading.set(true); }
    this.service.getResources({
      filter: this.filter?.trim() || undefined,
      status: this.statusFilter >= 0 ? this.statusFilter : undefined,
      skipCount: 0,
      maxResultCount: 500,
    }).subscribe({
      next: result => {
        this.resources.set(result.items || []);
        this.totalCount.set(result.totalCount || 0);
        this.listLoading.set(false);
        // 优先选中 URL 指定的资源，否则默认第一个，便于直接看到链路
        if (!this.selectedId() && this.resources().length > 0) {
          const requested = this.resources().find(r => r.id === this.requestedResourceId);
          this.selectResource(requested ?? this.resources()[0]);
        }
      },
      error: () => {
        this.listLoading.set(false);
        this.message.error('加载资源列表失败');
      },
    });
  }

  selectResource(r: ResourceTrackingResourceDto): void {
    this.selectedId.set(r.id);
    this.loadTimeline(r.id);
  }

  loadTimeline(resourceId: string, showLoading = true): void {
    if (showLoading) { this.timelineLoading.set(true); }
    this.service.getTimeline(resourceId).subscribe({
      next: t => {
        this.timeline.set(t);
        this.timelineLoading.set(false);
      },
      error: () => {
        this.timelineLoading.set(false);
        this.message.error('加载资源链路失败');
      },
    });
  }

  /** 跳转到「资源任务」页查看该资源下的全部任务（重试统一在该页进行）。 */
  openTasks(resourceId: string): void {
    this.router.navigate(['/admin/resource-tasks'], { queryParams: { resourceId } });
  }

  statusLabel(status: number): string {
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

  statusColor(status: number): string {
    switch (status) {
      case 2: return 'cyan';
      case 3: return 'success';
      case 4: return 'error';
      case 5: return 'default';
      case 1: return 'processing';
      default: return 'default';
    }
  }

  mediaStatusLabel(status: number): string {
    switch (status) {
      case 10: return '处理中';
      case 30: return '就绪';
      case 35: return '部分失败';
      case 40: return '失败';
      default: return '未处理';
    }
  }

  mediaStatusColor(status: number): string {
    switch (status) {
      case 30: return 'success';
      case 10: return 'processing';
      case 35: return 'warning';
      case 40: return 'error';
      default: return 'default';
    }
  }

  stepDotColor(status: string): string {
    switch (status) {
      case 'success': return 'green';
      case 'running': return 'blue';
      case 'failed': return 'red';
      default: return 'gray';
    }
  }

  /** 步骤标签色（nz-tag 预设色，避免使用 gray 造成实心灰底）。 */
  stepTagColor(status: string): string {
    switch (status) {
      case 'success': return 'success';
      case 'running': return 'processing';
      case 'failed': return 'error';
      default: return 'default';
    }
  }

  stepStatusLabel(status: string): string {
    switch (status) {
      case 'success': return '完成';
      case 'running': return '进行中';
      case 'failed': return '失败';
      case 'skipped': return '跳过';
      default: return '待处理';
    }
  }

  taskStatusColor(status: string): string {
    switch (status) {
      case 'success': return 'success';
      case 'running': return 'processing';
      case 'partial': return 'warning';
      case 'failed': return 'error';
      case 'cancelled': return 'default';
      default: return 'default';
    }
  }

  taskStatusLabel(status: string): string {
    switch (status) {
      case 'success': return '成功';
      case 'running': return '进行中';
      case 'partial': return '部分失败';
      case 'failed': return '失败';
      case 'cancelled': return '已取消';
      default: return '排队中';
    }
  }

  artifactLabel(kind: string): string {
    switch (kind) {
      case 'thumbnail': return '缩略图';
      case 'preview-pdf': return '预览PDF';
      default: return kind;
    }
  }
}
