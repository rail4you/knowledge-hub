import { Component, signal, inject, OnInit, OnDestroy, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
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
import { AiMediaUploadService } from '../services/ai-media-upload.service';
import { TeachingSceneCategory } from '../services/teaching-scene.service';
import { TeachingScenePickerComponent } from '../components/teaching-scene-picker/teaching-scene-picker.component';

@Component({
  selector: 'app-video-generation',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzButtonModule,
    NzInputModule,
    NzIconModule,
    NzSpinModule,
    NzEmptyModule,
    NzTableModule,
    NzTagModule,
    NzModalModule,
    NzTabsModule,
    NzProgressModule,
    TeachingScenePickerComponent,
  ],
  templateUrl: './video-generation.component.html',
  styleUrls: ['./video-generation.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VideoGenerationComponent implements OnInit, OnDestroy {
  private readonly aiTaskService = inject(AiTaskService);
  private readonly aiTaskNotifications = inject(AiTaskNotificationService);
  private readonly uploadService = inject(AiMediaUploadService);
  private readonly message = inject(NzMessageService);
  private readonly route = inject(ActivatedRoute);

  private readonly destroy$ = new Subject<void>();

  // ── Tab ──
  readonly activeTab = signal(0);

  // ── 第 1 步：教学场景图片 ──
  readonly sceneCategory = TeachingSceneCategory.VideoScene;
  readonly sourceMode = signal<'ai' | 'library' | 'upload'>('ai');
  readonly scenePrompt = signal('');
  readonly generatingImage = signal(false);
  readonly imageProgress = signal(0);
  readonly uploading = signal(false);
  readonly firstFrameUrl = signal('');
  /** 本次教学场景图片实际使用的画面提示词（生成后回显）。 */
  readonly usedScenePrompt = signal('');

  // ── 教学场景图片来源：「已生成的图片」库 ──
  readonly libraryImages = signal<AiMediaHistoryDto[]>([]);
  readonly libraryLoading = signal(false);

  // ── 第 2 步：生成视频 ──
  readonly motionCategory = TeachingSceneCategory.VideoMotion;
  readonly motionPrompt = signal('');
  readonly generatingVideo = signal(false);
  readonly videoProgress = signal(0);
  readonly progressMessage = signal('');
  readonly videoUrl = signal('');
  readonly error = signal('');
  /** 本次视频实际使用的运镜 / 动作提示词（生成后回显）。 */
  readonly usedMotionPrompt = signal('');

  /** 当前后台任务 ID（用于取消 / 恢复）。 */
  private currentImageTaskId: string | null = null;
  private currentVideoTaskId: string | null = null;

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
    this.route.queryParamMap
      .pipe(takeUntil(this.destroy$))
      .subscribe(params => {
        const taskId = params.get('taskId');
        if (taskId && taskId !== this.lastPreviewTaskId) {
          this.lastPreviewTaskId = taskId;
          this.loadTaskPreview(taskId);
        }
      });

    // 无深度链接时：恢复我名下正在跑的教学场景图片 / 视频任务（切页回来也能看到进度）。
    if (!this.route.snapshot.queryParamMap.get('taskId')) {
      this.resumeRunningTask();
    }
  }

  onTabChange(index: number) {
    this.activeTab.set(index);
    if (index === 1) this.loadHistory(this.historyPageIndex());
  }

  // ==================== 第 1 步：教学场景图片 ====================

  /** 切换图片来源：AI 生成 / 已生成的图片 / 上传。进入「已生成的图片」时懒加载图片库。 */
  setSourceMode(mode: 'ai' | 'library' | 'upload') {
    this.sourceMode.set(mode);
    if (mode === 'library') this.loadLibraryImages();
  }

  private loadLibraryImages() {
    if (this.libraryLoading()) return;
    this.libraryLoading.set(true);
    this.aiTaskService
      .getMediaHistory(AiTaskType.ImageGeneration, { skipCount: 0, maxResultCount: 24 })
      .subscribe({
        next: res => {
          this.libraryImages.set((res.items ?? []).filter(i => !!i.imageUrl));
          this.libraryLoading.set(false);
        },
        error: () => {
          this.libraryImages.set([]);
          this.libraryLoading.set(false);
        },
      });
  }

  /** 选用图片库中某张已生成的图片作为教学场景图片。 */
  useLibraryImage(img: AiMediaHistoryDto) {
    if (!img.imageUrl) return;
    this.firstFrameUrl.set(img.imageUrl);
    this.usedScenePrompt.set(img.prompt || '');
    this.message.success('已选用该图片作为教学场景图片');
  }

  async generateFirstFrame() {
    const prompt = this.scenePrompt().trim();
    if (!prompt) {
      this.message.warning('请输入画面提示词');
      return;
    }
    if (this.generatingImage()) return;

    this.generatingImage.set(true);
    this.firstFrameUrl.set('');
    this.error.set('');
    this.imageProgress.set(0);
    this.progressMessage.set('正在提交教学场景图片任务…');
    this.usedScenePrompt.set(prompt);

    try {
      const task = await firstValueFrom(
        this.aiTaskService.create({
          taskType: AiTaskType.ImageGeneration,
          title: `教学场景图片：${prompt.slice(0, 30)}`,
          inputJson: JSON.stringify({ prompt, size: '1280*720' }),
        }),
      );
      this.currentImageTaskId = task.id;
      this.lastPreviewTaskId = task.id;
      const done = await this.waitTask(task.id, t => this.applyImageProgress(t));
      this.applyFirstFrame(done);
    } catch (err: any) {
      this.error.set(this.friendlyMediaError(err?.message, '教学场景图片生成失败'));
    } finally {
      this.generatingImage.set(false);
    }
  }

  /** 更新教学场景图片进度条与文案（轮询快照共用）。 */
  private applyImageProgress(t: AiGenerationTaskDto) {
    if (typeof t.progress === 'number') this.imageProgress.set(t.progress);
    this.progressMessage.set(t.progressMessage || this.progressMessage());
  }

  cancelFirstFrame() {
    const id = this.currentImageTaskId;
    if (id) {
      this.aiTaskService.cancel(id).subscribe({ next: () => {}, error: () => {} });
    }
    this.generatingImage.set(false);
    this.imageProgress.set(0);
    this.progressMessage.set('');
    this.message.info('已取消教学场景图片生成');
  }

  async onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    this.uploading.set(true);
    this.error.set('');
    this.progressMessage.set('正在上传图片…');
    try {
      const result = await firstValueFrom(this.uploadService.uploadFirstFrame(file));
      this.firstFrameUrl.set(result.url);
      this.progressMessage.set('图片上传完成');
    } catch (err: any) {
      this.error.set(err?.error?.error?.message || err?.message || '上传失败');
    } finally {
      this.uploading.set(false);
    }
  }

  clearFirstFrame() {
    this.firstFrameUrl.set('');
  }

  // ==================== 第 2 步：生成视频 ====================

  async generateVideo() {
    const firstFrameUrl = this.firstFrameUrl();
    const prompt = this.motionPrompt().trim();
    if (!firstFrameUrl) {
      this.message.warning('请先生成或上传教学场景图片');
      return;
    }
    if (!prompt) {
      this.message.warning('请输入运镜 / 动作提示词');
      return;
    }
    if (this.generatingVideo()) return;

    this.generatingVideo.set(true);
    this.videoUrl.set('');
    this.error.set('');
    this.videoProgress.set(0);
    this.progressMessage.set('正在提交视频任务…');
    this.usedMotionPrompt.set(prompt);

    try {
      const task = await firstValueFrom(
        this.aiTaskService.create({
          taskType: AiTaskType.VideoGeneration,
          title: `短视频：${prompt.slice(0, 30)}`,
          inputJson: JSON.stringify({ imageUrl: firstFrameUrl, prompt, duration: 5 }),
        }),
      );
      this.currentVideoTaskId = task.id;
      this.lastPreviewTaskId = task.id;
      const done = await this.waitTask(task.id, t => this.applyVideoProgress(t));
      this.applyVideo(done);
      this.historyPageIndex.set(1);
      this.loadHistory(1);
    } catch (err: any) {
      this.error.set(this.friendlyMediaError(err?.message, '视频生成失败'));
    } finally {
      this.generatingVideo.set(false);
    }
  }

  /** 更新视频进度条与文案（轮询快照共用）。 */
  private applyVideoProgress(t: AiGenerationTaskDto) {
    if (typeof t.progress === 'number') this.videoProgress.set(t.progress);
    this.progressMessage.set(t.progressMessage || this.progressMessage());
  }

  cancelVideo() {
    const id = this.currentVideoTaskId;
    if (id) {
      this.aiTaskService.cancel(id).subscribe({ next: () => {}, error: () => {} });
    }
    this.generatingVideo.set(false);
    this.videoProgress.set(0);
    this.progressMessage.set('');
    this.message.info('已取消视频生成');
  }

  private waitTask(taskId: string, onProgress?: (t: AiGenerationTaskDto) => void) {
    return lastValueFrom(this.aiTaskNotifications.pollTask(taskId).pipe(tap(t => onProgress?.(t))));
  }

  /**
   * 恢复进行中的任务：用户中途切走再回来，生成中面板继续显示进度。
   * 取我名下最新的 Pending / Running 教学场景图片或视频任务重建跟进。
   */
  private resumeRunningTask(): void {
    if (this.generatingImage() || this.generatingVideo()) return;
    this.aiTaskService
      .getList({ onlyMine: true, maxResultCount: 20 })
      .pipe(take(1))
      .subscribe({
        next: res => {
          const items = (res.items || []).filter(t =>
            (t.status === AiTaskStatus.Pending || t.status === AiTaskStatus.Running) &&
            (t.taskType === AiTaskType.ImageGeneration || t.taskType === AiTaskType.VideoGeneration));
          const newest = items
            .sort((a, b) => +new Date(b.creationTime) - +new Date(a.creationTime))[0];
          if (!newest || this.generatingImage() || this.generatingVideo()) return;
          this.followResumedTask(newest);
        },
      });
  }

  private followResumedTask(task: AiGenerationTaskDto): void {
    if (task.taskType === AiTaskType.ImageGeneration) {
      const input = this.parseInput(task.inputJson);
      if (input.prompt) {
        this.scenePrompt.set(input.prompt);
        this.usedScenePrompt.set(input.prompt);
      }
      this.currentImageTaskId = task.id;
      this.generatingImage.set(true);
      this.firstFrameUrl.set('');
      this.error.set('');
      this.imageProgress.set(task.progress ?? 0);
      this.progressMessage.set(task.progressMessage || '后台生成教学场景图片中…');
      this.waitTask(task.id, t => this.applyImageProgress(t))
        .then(done => {
          this.applyFirstFrame(done);
          this.generatingImage.set(false);
        })
        .catch((err: any) => {
          this.error.set(this.friendlyMediaError(err?.message, '加载任务失败'));
          this.generatingImage.set(false);
        });
    } else {
      const input = this.parseInput(task.inputJson);
      if (input.prompt) {
        this.motionPrompt.set(input.prompt);
        this.usedMotionPrompt.set(input.prompt);
      }
      if (input.imageUrl) this.firstFrameUrl.set(input.imageUrl);
      this.currentVideoTaskId = task.id;
      this.generatingVideo.set(true);
      this.videoUrl.set('');
      this.error.set('');
      this.videoProgress.set(task.progress ?? 0);
      this.progressMessage.set(task.progressMessage || '后台生成视频中…');
      this.waitTask(task.id, t => this.applyVideoProgress(t))
        .then(done => {
          this.applyVideo(done);
          this.generatingVideo.set(false);
          this.historyPageIndex.set(1);
          this.loadHistory(1);
        })
        .catch((err: any) => {
          this.error.set(this.friendlyMediaError(err?.message, '加载任务失败'));
          this.generatingVideo.set(false);
        });
    }
  }

  private parseInput(inputJson?: string): { prompt?: string; imageUrl?: string } {
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
        if (task.taskType === AiTaskType.ImageGeneration) this.applyFirstFrame(task);
        else this.applyVideo(task);
        return;
      }
      if (task.status === AiTaskStatus.Pending || task.status === AiTaskStatus.Running) {
        if (task.taskType === AiTaskType.ImageGeneration) {
          const input = this.parseInput(task.inputJson);
          if (input.prompt) this.usedScenePrompt.set(input.prompt);
          this.currentImageTaskId = task.id;
          this.generatingImage.set(true);
          this.imageProgress.set(task.progress ?? 0);
        } else {
          const input = this.parseInput(task.inputJson);
          if (input.prompt) this.usedMotionPrompt.set(input.prompt);
          this.currentVideoTaskId = task.id;
          this.generatingVideo.set(true);
          this.videoProgress.set(task.progress ?? 0);
        }
        this.progressMessage.set(task.progressMessage || '生成中…');
        this.waitTask(taskId, t => {
          if (task.taskType === AiTaskType.ImageGeneration) this.applyImageProgress(t);
          else this.applyVideoProgress(t);
        })
          .then(done => {
            if (done.taskType === AiTaskType.ImageGeneration) this.applyFirstFrame(done);
            else this.applyVideo(done);
            this.generatingImage.set(false);
            this.generatingVideo.set(false);
          })
          .catch((err: any) => {
            this.error.set(this.friendlyMediaError(err?.message, '加载任务失败'));
            this.generatingImage.set(false);
            this.generatingVideo.set(false);
          });
      }
    });
  }

  /**
   * DashScope 内容安全拦截的英文错误（Input data may contain inappropriate content 等）
   * 统一转成中文说明，避免直接把英文细节展示给用户。后端已转换时原样返回。
   */
  private friendlyMediaError(raw?: string, fallback = '生成失败'): string {
    const msg = raw?.trim() ? raw : fallback;
    if (/inappropriate content|DataInspection|content[_ ]filter|sensitive content/i.test(msg)) {
      return '生成被平台安全策略拦截：图片或提示词包含不适当内容，无法生成。请更换一张更合适的图片，并检查提示词中是否包含敏感或违规表述后重试。';
    }
    return msg;
  }

  private applyFirstFrame(task: AiGenerationTaskDto) {
    if (task.status === AiTaskStatus.Completed) {
      try {
        const parsed = JSON.parse(task.resultJson || '{}');
        if (parsed.imageUrl) {
          this.firstFrameUrl.set(parsed.imageUrl);
          this.imageProgress.set(100);
          this.progressMessage.set('教学场景图片生成完成');
          return;
        }
        throw new Error('result empty');
      } catch {
        this.error.set('解析教学场景图片结果失败');
      }
    } else if (task.status === AiTaskStatus.Failed) {
      this.error.set(this.friendlyMediaError(task.errorMessage, '教学场景图片生成失败'));
    }
  }

  private applyVideo(task: AiGenerationTaskDto) {
    if (task.status === AiTaskStatus.Completed) {
      try {
        const parsed = JSON.parse(task.resultJson || '{}');
        if (parsed.videoUrl) {
          this.videoUrl.set(parsed.videoUrl);
          this.videoProgress.set(100);
          this.progressMessage.set('视频生成完成');
          return;
        }
        throw new Error('result empty');
      } catch {
        this.error.set('解析视频结果失败');
      }
    } else if (task.status === AiTaskStatus.Failed) {
      this.error.set(this.friendlyMediaError(task.errorMessage, '视频生成失败'));
    }
  }

  // ==================== 历史记录 ====================

  loadHistory(page: number) {
    this.historyLoading.set(true);
    this.aiTaskService
      .getMediaHistory(AiTaskType.VideoGeneration, {
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