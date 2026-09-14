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

  // ── 第 1 步：首帧图片 ──
  readonly sceneCategory = TeachingSceneCategory.VideoScene;
  readonly sourceMode = signal<'ai' | 'upload'>('ai');
  readonly scenePrompt = signal('');
  readonly generatingImage = signal(false);
  readonly uploading = signal(false);
  readonly firstFrameUrl = signal('');

  // ── 第 2 步：生成视频 ──
  readonly motionCategory = TeachingSceneCategory.VideoMotion;
  readonly motionPrompt = signal('');
  readonly generatingVideo = signal(false);
  readonly progressMessage = signal('');
  readonly videoUrl = signal('');
  readonly error = signal('');

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
  }

  onTabChange(index: number) {
    this.activeTab.set(index);
    if (index === 1) this.loadHistory(this.historyPageIndex());
  }

  // ==================== 第 1 步：首帧图片 ====================

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
    this.progressMessage.set('正在提交首帧图片任务…');

    try {
      const task = await firstValueFrom(
        this.aiTaskService.create({
          taskType: AiTaskType.ImageGeneration,
          title: `首帧：${prompt.slice(0, 30)}`,
          inputJson: JSON.stringify({ prompt, size: '1280*720' }),
        }),
      );
      this.lastPreviewTaskId = task.id;
      const done = await this.waitTask(task.id, t =>
        this.progressMessage.set(t.progressMessage || this.progressMessage()),
      );
      this.applyFirstFrame(done);
    } catch (err: any) {
      this.error.set(err?.message || '首帧图片生成失败');
    } finally {
      this.generatingImage.set(false);
    }
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
      this.message.warning('请先生成或上传首帧图片');
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
    this.progressMessage.set('正在提交视频任务…');

    try {
      const task = await firstValueFrom(
        this.aiTaskService.create({
          taskType: AiTaskType.VideoGeneration,
          title: `短视频：${prompt.slice(0, 30)}`,
          inputJson: JSON.stringify({ imageUrl: firstFrameUrl, prompt, duration: 5 }),
        }),
      );
      this.lastPreviewTaskId = task.id;
      const done = await this.waitTask(task.id, t =>
        this.progressMessage.set(t.progressMessage || this.progressMessage()),
      );
      this.applyVideo(done);
      this.historyPageIndex.set(1);
      this.loadHistory(1);
    } catch (err: any) {
      this.error.set(err?.message || '视频生成失败');
    } finally {
      this.generatingVideo.set(false);
    }
  }

  private waitTask(taskId: string, onProgress?: (t: AiGenerationTaskDto) => void) {
    return lastValueFrom(this.aiTaskNotifications.pollTask(taskId).pipe(tap(t => onProgress?.(t))));
  }

  private loadTaskPreview(taskId: string) {
    this.aiTaskService.get(taskId).pipe(take(1)).subscribe(task => {
      if (task.taskType === AiTaskType.ImageGeneration) this.applyFirstFrame(task);
      else this.applyVideo(task);

      if (task.status === AiTaskStatus.Pending || task.status === AiTaskStatus.Running) {
        this.generatingImage.set(true);
        this.generatingVideo.set(true);
        this.progressMessage.set(task.progressMessage || '生成中…');
        this.waitTask(taskId, t =>
          this.progressMessage.set(t.progressMessage || this.progressMessage()),
        )
          .then(done => {
            if (done.taskType === AiTaskType.ImageGeneration) this.applyFirstFrame(done);
            else this.applyVideo(done);
            this.generatingImage.set(false);
            this.generatingVideo.set(false);
          })
          .catch((err: any) => {
            this.error.set(err?.message || '加载任务失败');
            this.generatingImage.set(false);
            this.generatingVideo.set(false);
          });
      }
    });
  }

  private applyFirstFrame(task: AiGenerationTaskDto) {
    if (task.status === AiTaskStatus.Completed) {
      try {
        const parsed = JSON.parse(task.resultJson || '{}');
        if (parsed.imageUrl) {
          this.firstFrameUrl.set(parsed.imageUrl);
          this.progressMessage.set('首帧图片生成完成');
          return;
        }
        throw new Error('result empty');
      } catch {
        this.error.set('解析首帧结果失败');
      }
    } else if (task.status === AiTaskStatus.Failed) {
      this.error.set(task.errorMessage || '首帧图片生成失败');
    }
  }

  private applyVideo(task: AiGenerationTaskDto) {
    if (task.status === AiTaskStatus.Completed) {
      try {
        const parsed = JSON.parse(task.resultJson || '{}');
        if (parsed.videoUrl) {
          this.videoUrl.set(parsed.videoUrl);
          this.progressMessage.set('视频生成完成');
          return;
        }
        throw new Error('result empty');
      } catch {
        this.error.set('解析视频结果失败');
      }
    } else if (task.status === AiTaskStatus.Failed) {
      this.error.set(task.errorMessage || '视频生成失败');
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