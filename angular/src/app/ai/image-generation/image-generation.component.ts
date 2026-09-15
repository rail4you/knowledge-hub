import { Component, signal, inject, OnInit, OnDestroy, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzMessageService } from 'ng-zorro-antd/message';
import { firstValueFrom, lastValueFrom, Subject } from 'rxjs';
import { take, takeUntil, tap } from 'rxjs/operators';
import {
  AiGenerationTaskDto,
  AiMediaHistoryDto,
  AiTaskService,
  AiTaskStatus,
  AiTaskType,
} from '../services/ai-task.service';
import { AiTaskNotificationService } from '../services/ai-task-notification.service';
import { TeachingScene, TeachingSceneCategory } from '../services/teaching-scene.service';
import { TeachingScenePickerComponent } from '../components/teaching-scene-picker/teaching-scene-picker.component';

const SIZES = [
  { label: '方形 1:1（1024×1024）', value: '1024*1024' },
  { label: '横版 16:9（1280×720）', value: '1280*720' },
  { label: '竖版 9:16（720×1280）', value: '720*1280' },
];

@Component({
  selector: 'app-image-generation',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzIconModule,
    NzEmptyModule,
    NzTableModule,
    NzTagModule,
    NzModalModule,
    NzTabsModule,
    NzProgressModule,
    TeachingScenePickerComponent,
  ],
  templateUrl: './image-generation.component.html',
  styleUrls: ['./image-generation.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImageGenerationComponent implements OnInit, OnDestroy {
  private readonly aiTaskService = inject(AiTaskService);
  private readonly aiTaskNotifications = inject(AiTaskNotificationService);
  private readonly message = inject(NzMessageService);
  private readonly route = inject(ActivatedRoute);

  private readonly destroy$ = new Subject<void>();

  // ── Tab ──
  readonly activeTab = signal(0);

  // ── 生成 ──
  readonly imageCategory = TeachingSceneCategory.Image;
  readonly sizes = SIZES;

  readonly prompt = signal('');
  readonly size = signal('1024*1024');
  readonly negativePrompt = signal('');

  readonly generating = signal(false);
  readonly progress = signal(0);
  readonly progressMessage = signal('');
  readonly imageUrl = signal('');
  readonly error = signal('');

  /** 本次生成实际使用的提示词（生成后回显）。 */
  readonly usedPrompt = signal('');
  /** 点击「使用」选择的场景名称（生成后回显）。 */
  readonly selectedSceneName = signal('');

  /** 当前后台任务 ID（用于取消 / 恢复）。 */
  private currentTaskId: string | null = null;

  // ── 历史记录 ──
  readonly history = signal<AiMediaHistoryDto[]>([]);
  readonly historyTotal = signal(0);
  readonly historyLoading = signal(false);
  readonly historyPageIndex = signal(1);
  readonly historyPageSize = 10;
  readonly previewVisible = signal(false);
  readonly previewUrl = signal('');

  private lastPreviewTaskId: string | null = null;

  ngOnInit() {
    // ?taskId= 深度链接（通知 / 任务中心跳转）：加载后台任务结果。
    this.route.queryParamMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const taskId = params.get('taskId');
        if (taskId && taskId !== this.lastPreviewTaskId) {
          this.lastPreviewTaskId = taskId;
          this.loadTaskPreview(taskId);
        }
      });

    // 无深度链接时：恢复我名下正在跑的图片生成任务（切页回来也能看到进度）。
    if (!this.route.snapshot.queryParamMap.get('taskId')) {
      this.resumeRunningTask();
    }
  }

  /** 点击场景「使用」：填入提示词并记录场景名，用于生成后回显。 */
  onSceneSelected(scene: TeachingScene) {
    this.selectedSceneName.set(scene.name);
    this.prompt.set(scene.prompt);
  }

  onTabChange(index: number) {
    this.activeTab.set(index);
    if (index === 1) this.loadHistory(this.historyPageIndex());
  }

  // ==================== 生成 ====================

  async generate() {
    const prompt = this.prompt().trim();
    if (!prompt) {
      this.message.warning('请输入图片提示词');
      return;
    }
    if (this.generating()) return;

    this.generating.set(true);
    this.imageUrl.set('');
    this.error.set('');
    this.progress.set(0);
    this.progressMessage.set('正在提交任务…');
    this.usedPrompt.set(prompt);

    try {
      const task = await firstValueFrom(
        this.aiTaskService.create({
          taskType: AiTaskType.ImageGeneration,
          title: prompt.slice(0, 50),
          inputJson: JSON.stringify({
            prompt,
            size: this.size(),
            negativePrompt: this.negativePrompt().trim() || undefined,
          }),
        }),
      );
      this.currentTaskId = task.id;
      this.lastPreviewTaskId = task.id;
      const done = await this.waitTask(task.id, t =>
        this.applyProgress(t),
      );
      this.applyResult(done);
      this.historyPageIndex.set(1);
      this.loadHistory(1);
    } catch (err: any) {
      this.error.set(err?.message || '生成失败，请稍后重试');
    } finally {
      this.generating.set(false);
    }
  }

  /** 更新进度条与进度文案（轮询快照共用）。 */
  private applyProgress(t: AiGenerationTaskDto) {
    if (typeof t.progress === 'number') this.progress.set(t.progress);
    this.progressMessage.set(t.progressMessage || this.progressMessage());
  }

  private waitTask(taskId: string, onProgress?: (t: AiGenerationTaskDto) => void) {
    return lastValueFrom(this.aiTaskNotifications.pollTask(taskId).pipe(tap(t => onProgress?.(t))));
  }

  cancel() {
    const id = this.currentTaskId;
    if (id) {
      this.aiTaskService.cancel(id).subscribe({ next: () => {}, error: () => {} });
    }
    this.generating.set(false);
    this.progress.set(0);
    this.progressMessage.set('');
    this.message.info('已取消生成');
  }

  /**
   * 恢复进行中的任务：用户中途切走再回来，生成中面板继续显示进度。
   * 取我名下最新的 Pending / Running 图片生成任务重建跟进。
   */
  private resumeRunningTask(): void {
    if (this.generating()) return;
    this.aiTaskService
      .getList({ onlyMine: true, maxResultCount: 20 })
      .pipe(take(1))
      .subscribe({
        next: res => {
          const running = (res.items || [])
            .filter(t =>
              t.taskType === AiTaskType.ImageGeneration &&
              (t.status === AiTaskStatus.Pending || t.status === AiTaskStatus.Running))
            .sort((a, b) => +new Date(b.creationTime) - +new Date(a.creationTime))[0];
          if (!running || this.generating()) return;
          this.followResumedTask(running);
        },
      });
  }

  private followResumedTask(task: AiGenerationTaskDto): void {
    const input = this.parseInput(task.inputJson);
    if (input.prompt) {
      this.prompt.set(input.prompt);
      this.usedPrompt.set(input.prompt);
    }
    if (input.size) this.size.set(input.size);
    if (input.negativePrompt) this.negativePrompt.set(input.negativePrompt);

    this.currentTaskId = task.id;
    this.generating.set(true);
    this.imageUrl.set('');
    this.error.set('');
    this.progress.set(task.progress ?? 0);
    this.progressMessage.set(task.progressMessage || '后台生成中…');

    this.waitTask(task.id, t => this.applyProgress(t))
      .then(done => {
        this.applyResult(done);
        this.generating.set(false);
        this.historyPageIndex.set(1);
        this.loadHistory(1);
      })
      .catch((err: any) => {
        this.error.set(err?.message || '加载任务失败');
        this.generating.set(false);
      });
  }

  private parseInput(inputJson?: string): { prompt?: string; size?: string; negativePrompt?: string } {
    if (!inputJson) return {};
    try {
      return JSON.parse(inputJson);
    } catch {
      return {};
    }
  }

  private loadTaskPreview(taskId: string) {
    this.aiTaskService.get(taskId).pipe(take(1)).subscribe(task => {
      if (task.status === AiTaskStatus.Completed) {
        this.applyResult(task);
      } else if (task.status === AiTaskStatus.Pending || task.status === AiTaskStatus.Running) {
        const input = this.parseInput(task.inputJson);
        if (input.prompt) {
          this.prompt.set(input.prompt);
          this.usedPrompt.set(input.prompt);
        }
        this.currentTaskId = task.id;
        this.generating.set(true);
        this.progress.set(task.progress ?? 0);
        this.progressMessage.set(task.progressMessage || '生成中…');
        this.waitTask(taskId, t => this.applyProgress(t))
          .then(done => {
            this.applyResult(done);
            this.generating.set(false);
          })
          .catch((err: any) => {
            this.error.set(err?.message || '加载任务失败');
            this.generating.set(false);
          });
      } else if (task.status === AiTaskStatus.Failed) {
        this.error.set(task.errorMessage || '生成失败');
      }
    });
  }

  private applyResult(task: AiGenerationTaskDto) {
    if (task.status === AiTaskStatus.Completed) {
      try {
        const parsed = JSON.parse(task.resultJson || '{}');
        const url = parsed.imageUrl || '';
        if (!url) throw new Error('result empty');
        this.imageUrl.set(url);
        this.progress.set(100);
        this.progressMessage.set('生成完成');
      } catch {
        this.error.set('解析生成结果失败');
      }
    } else if (task.status === AiTaskStatus.Failed) {
      this.error.set(task.errorMessage || '生成失败');
    } else {
      this.error.set('任务已取消');
    }
  }

  async downloadImage() {
    const url = this.imageUrl();
    if (!url) return;
    await this.downloadUrl(url, `教学图片_${Date.now()}.png`);
  }

  // ==================== 历史记录 ====================

  loadHistory(page: number) {
    this.historyLoading.set(true);
    this.aiTaskService
      .getMediaHistory(AiTaskType.ImageGeneration, {
        skipCount: (page - 1) * this.historyPageSize,
        maxResultCount: this.historyPageSize,
      })
      .subscribe({
        next: res => {
          this.history.set(res.items ?? []);
          this.historyTotal.set(res.totalCount ?? 0);
          this.historyLoading.set(false);
        },
        error: () => {
          this.history.set([]);
          this.historyLoading.set(false);
        },
      });
  }

  onHistoryPageChange(page: number) {
    this.historyPageIndex.set(page);
    this.loadHistory(page);
  }

  statusLabel(status: AiTaskStatus): string {
    return AiTaskService.statusLabel(status);
  }

  statusColor(status: AiTaskStatus): string {
    return AiTaskService.statusColor(status);
  }

  openPreview(url: string) {
    this.previewUrl.set(url);
    this.previewVisible.set(true);
  }

  async downloadUrl(url: string, fileName: string) {
    try {
      const blob = await fetch(url, { mode: 'cors' }).then(r => r.blob());
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(a.href);
    } catch {
      window.open(url, '_blank');
    }
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }
}