import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { interval, Subscription } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import type { PagedResultDto } from '@abp/ng.core';
import {
  AiGenerationTaskDto,
  AiTaskService,
  AiTaskStatus,
  AiTaskType,
  aiTaskResultRoute,
} from '../services/ai-task.service';
import { AiTaskNotificationService } from '../services/ai-task-notification.service';

@Component({
  selector: 'app-ai-task-center',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzTableModule,
    NzButtonModule,
    NzTagModule,
    NzProgressModule,
    NzIconModule,
    NzInputModule,
    NzSelectModule,
    NzSwitchModule,
    NzPopconfirmModule,
    NzTooltipModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="task-center">
      <div class="page-header">
        <div class="page-header__text">
          <h2>AI 任务监控</h2>
          <p>查看教案 / 案例分析 / 职业规划 / 习题生成任务，支持切页后台执行、失败重试与取消。</p>
        </div>
        <div class="page-header__actions">
          <button nz-button nzType="default" (click)="reload()">
            <span nz-icon nzType="reload"></span>
            刷新
          </button>
        </div>
      </div>

      <div class="filter-card">
        <div class="filters">
          <nz-select
            class="filter-type"
            nzPlaceHolder="全部类型"
            [ngModel]="taskType()"
            (ngModelChange)="onTaskTypeChange($event)"
            nzAllowClear
          >
            @for (t of typeOptions; track t.value) {
              <nz-option [nzValue]="t.value" [nzLabel]="t.label"></nz-option>
            }
          </nz-select>

          <nz-select
            class="filter-status"
            nzPlaceHolder="全部状态"
            [ngModel]="status()"
            (ngModelChange)="onStatusChange($event)"
            nzAllowClear
          >
            @for (s of statusOptions; track s.value) {
              <nz-option [nzValue]="s.value" [nzLabel]="s.label"></nz-option>
            }
          </nz-select>

          <nz-input-group [nzPrefix]="searchPrefix" class="filter-search">
            <input
              nz-input
              placeholder="按标题 / 资源名搜索"
              [ngModel]="keyword()"
              (ngModelChange)="keyword.set($event)"
              (keyup.enter)="reload()"
            />
          </nz-input-group>
          <ng-template #searchPrefix>
            <span nz-icon nzType="search"></span>
          </ng-template>

          <label class="only-mine">
            <nz-switch [ngModel]="onlyMine()" (ngModelChange)="onOnlyMineChange($event)"></nz-switch>
            <span>只看我的</span>
          </label>
        </div>
      </div>

      <div class="table-card">
        <nz-table
          #table
          [nzData]="tasks()"
          [nzTotal]="totalCount()"
          [nzPageSize]="pageSize"
          [nzPageIndex]="pageIndex"
          [nzFrontPagination]="false"
          [nzLoading]="loading()"
          (nzQueryParams)="onQueryParamsChange($event)"
        >
          <thead>
            <tr>
              <th>类型</th>
              <th>标题</th>
              <th>状态</th>
              <th>进度</th>
              <th>提交人</th>
              <th>创建时间</th>
              <th nzWidth="200px">操作</th>
            </tr>
          </thead>
          <tbody>
            @for (task of table.data; track task.id) {
              <tr>
                <td>
                  <nz-tag [nzColor]="typeColor(task)">{{ typeLabel(task) }}</nz-tag>
                </td>
                <td>
                  <div class="task-title">{{ task.title }}</div>
                  @if (task.resourceName) {
                    <div class="task-sub">{{ task.resourceName }}</div>
                  }
                </td>
                <td>
                  <nz-tag [nzColor]="statusColor(task)">{{ statusLabel(task) }}</nz-tag>
                  @if (task.status === AiTaskStatus.Failed && task.errorMessage) {
                    <span
                      nz-icon
                      nzType="exclamation-circle"
                      nz-tooltip
                      [nzTooltipTitle]="task.errorMessage"
                      class="err-icon"
                    ></span>
                  }
                </td>
                <td class="progress-cell">
                  @if (task.status === AiTaskStatus.Running || task.status === AiTaskStatus.Pending) {
                    <nz-progress [nzPercent]="task.progress" [nzStrokeWidth]="8"></nz-progress>
                    @if (task.progressMessage) {
                      <div class="task-sub">{{ task.progressMessage }}</div>
                    }
                  } @else {
                    <span>{{ task.progress }}%</span>
                  }
                </td>
                <td>{{ task.creatorUserName || '—' }}</td>
                <td>{{ task.creationTime | date:'yyyy-MM-dd HH:mm' }}</td>
                <td>
                  @if (task.status === AiTaskStatus.Completed) {
                    <button nz-button nzType="link" nzSize="small" (click)="viewResult(task)">
                      查看结果
                    </button>
                  }
                  @if (task.status === AiTaskStatus.Failed || task.status === AiTaskStatus.Cancelled) {
                    <button nz-button nzType="link" nzSize="small" (click)="retry(task)">重试</button>
                  }
                  @if (task.status === AiTaskStatus.Pending || task.status === AiTaskStatus.Running) {
                    <button nz-button nzType="link" nzSize="small" (click)="cancel(task)">取消</button>
                  }
                  <button
                    nz-button
                    nzType="link"
                    nzSize="small"
                    nzDanger
                    nz-popconfirm
                    nzPopconfirmTitle="确认删除该任务记录？"
                    (nzOnConfirm)="remove(task)"
                  >
                    删除
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </nz-table>
      </div>
    </div>
  `,
  styles: [
    `
      .task-center {
        padding: 24px;
        max-width: 1400px;
        margin: 0 auto;
        display: flex;
        flex-direction: column;
        gap: 16px;
      }
      .page-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        background: var(--kh-panel);
        border: 1px solid var(--kh-line);
        border-radius: 12px;
        padding: 16px 20px;
      }
      .page-header h2 {
        margin: 0;
        font-size: 20px;
        font-weight: 700;
      }
      .page-header p {
        margin: 4px 0 0;
        color: #888;
        font-size: 13px;
      }
      .filter-card,
      .table-card {
        background: var(--kh-panel);
        border: 1px solid var(--kh-line);
        border-radius: 12px;
        padding: 16px 20px;
      }
      .filters {
        display: flex;
        align-items: center;
        gap: 12px;
        flex-wrap: wrap;
      }
      .filter-type {
        width: 160px;
      }
      .filter-status {
        width: 140px;
      }
      .filter-search {
        width: 260px;
      }
      .only-mine {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        color: #595959;
      }
      .task-title {
        font-weight: 500;
        color: #262626;
      }
      .task-sub {
        font-size: 12px;
        color: #8c8c8c;
        margin-top: 2px;
      }
      .progress-cell {
        min-width: 160px;
      }
      .err-icon {
        color: #ff4d4f;
        margin-left: 6px;
        cursor: help;
      }
    `,
  ],
})
export class AiTaskCenterComponent implements OnInit, OnDestroy {
  private readonly aiTaskService = inject(AiTaskService);
  private readonly notificationService = inject(AiTaskNotificationService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly AiTaskStatus = AiTaskStatus;

  tasks = signal<AiGenerationTaskDto[]>([]);
  totalCount = signal(0);
  loading = signal(false);
  pageIndex = 1;
  pageSize = 10;

  taskType = signal<AiTaskType | undefined>(undefined);
  status = signal<AiTaskStatus | undefined>(undefined);
  keyword = signal('');
  onlyMine = signal(false);

  readonly typeOptions = [
    { value: AiTaskType.LessonPlanSingle, label: '教案生成' },
    { value: AiTaskType.LessonPlanMulti, label: '多章节教案' },
    { value: AiTaskType.CaseAnalysis, label: '案例分析' },
    { value: AiTaskType.CareerGuidance, label: '职业规划' },
    { value: AiTaskType.ExerciseGenerate, label: '习题生成' },
  ];

  readonly statusOptions = [
    { value: AiTaskStatus.Pending, label: '排队中' },
    { value: AiTaskStatus.Running, label: '生成中' },
    { value: AiTaskStatus.Completed, label: '已完成' },
    { value: AiTaskStatus.Failed, label: '失败' },
    { value: AiTaskStatus.Cancelled, label: '已取消' },
  ];

  private refreshSub?: Subscription;

  ngOnInit(): void {
    this.load();
    this.refreshSub = interval(5000)
      .pipe(switchMap(() => this.aiTaskService.getList(this.buildInput())))
      .subscribe({
        next: (result) => {
          this.tasks.set(result.items || []);
          this.totalCount.set(result.totalCount);
        },
      });

    // ?taskId= 转发到对应功能页的结果 UI（响应式订阅，页内跳转同样生效）
    this.route.queryParamMap.subscribe(params => {
      const taskId = params.get('taskId');
      if (taskId) {
        this.aiTaskService.get(taskId).subscribe({
          next: (task) => this.viewResult(task),
        });
      }
    });
  }

  ngOnDestroy(): void {
    this.refreshSub?.unsubscribe();
  }

  typeLabel(task: AiGenerationTaskDto): string {
    return AiTaskService.typeLabel(task.taskType);
  }
  typeColor(task: AiGenerationTaskDto): string {
    return AiTaskService.typeColor(task.taskType);
  }
  statusLabel(task: AiGenerationTaskDto): string {
    return AiTaskService.statusLabel(task.status);
  }
  statusColor(task: AiGenerationTaskDto): string {
    return AiTaskService.statusColor(task.status);
  }

  onTaskTypeChange(value: AiTaskType | undefined): void {
    this.taskType.set(value);
    this.pageIndex = 1;
    this.load();
  }

  onStatusChange(value: AiTaskStatus | undefined): void {
    this.status.set(value);
    this.pageIndex = 1;
    this.load();
  }

  onOnlyMineChange(value: boolean): void {
    this.onlyMine.set(value);
    this.pageIndex = 1;
    this.load();
  }

  onQueryParamsChange(params: any): void {
    this.pageIndex = params.pageIndex;
    this.pageSize = params.pageSize;
    this.load();
  }

  buildInput() {
    return {
      taskType: this.taskType(),
      status: this.status(),
      filter: this.keyword().trim() || undefined,
      onlyMine: this.onlyMine(),
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize,
    };
  }

  load(): void {
    this.loading.set(true);
    this.aiTaskService.getList(this.buildInput()).subscribe({
      next: (result: PagedResultDto<AiGenerationTaskDto>) => {
        this.tasks.set(result.items || []);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载任务失败');
      },
    });
  }

  reload(): void {
    this.load();
  }

  viewResult(task: AiGenerationTaskDto): void {
    // 未完成也跳转：功能页会对 Running/Pending 任务跟进进度并展示
    this.notificationService.acknowledge(task.id);
    this.router.navigate([aiTaskResultRoute(task.taskType)], { queryParams: { taskId: task.id } });
  }

  retry(task: AiGenerationTaskDto): void {
    this.aiTaskService.retry(task.id).subscribe({
      next: () => {
        this.message.success('已重新提交任务');
        this.load();
      },
      error: (e) => this.message.error(e?.error?.error?.message || '重试失败'),
    });
  }

  cancel(task: AiGenerationTaskDto): void {
    this.aiTaskService.cancel(task.id).subscribe({
      next: () => {
        this.message.success('已取消任务');
        this.load();
      },
      error: (e) => this.message.error(e?.error?.error?.message || '取消失败'),
    });
  }

  remove(task: AiGenerationTaskDto): void {
    this.aiTaskService.delete(task.id).subscribe({
      next: () => {
        this.message.success('已删除');
        this.load();
      },
      error: () => this.message.error('删除失败'),
    });
  }
}
