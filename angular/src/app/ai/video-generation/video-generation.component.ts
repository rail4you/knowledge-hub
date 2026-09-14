import { Component, signal, inject, OnInit, OnDestroy, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { firstValueFrom, lastValueFrom, Subject } from 'rxjs';
import { take, takeUntil, tap } from 'rxjs/operators';
import { AiGenerationTaskDto, AiTaskService, AiTaskStatus, AiTaskType } from '../services/ai-task.service';
import { AiTaskNotificationService } from '../services/ai-task-notification.service';
import { TeachingSceneCategory } from '../services/teaching-scene.service';
import { TeachingScenePickerComponent } from '../components/teaching-scene-picker/teaching-scene-picker.component';

@Component({
  selector: 'app-video-generation',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzIconModule,
    NzSpinModule,
    NzEmptyModule,
    TeachingScenePickerComponent,
  ],
  templateUrl: './video-generation.component.html',
  styleUrls: ['./video-generation.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VideoGenerationComponent implements OnInit, OnDestroy {
  private readonly aiTaskService = inject(AiTaskService);
  private readonly aiTaskNotifications = inject(AiTaskNotificationService);
  private readonly message = inject(NzMessageService);
  private readonly route = inject(ActivatedRoute);

  private readonly destroy$ = new Subject<void>();

  readonly sceneCategory = TeachingSceneCategory.VideoScene;
  readonly motionCategory = TeachingSceneCategory.VideoMotion;

  readonly scenePrompt = signal('');
  readonly motionPrompt = signal('');

  readonly generatingImage = signal(false);
  readonly generatingVideo = signal(false);
  readonly progressMessage = signal('');

  readonly firstFrameUrl = signal('');
  /** 图生视频首帧源地址：优先用通义万相原始临时 URL（本地持久化地址对 DashScope 不可达）。 */
  readonly firstFrameSourceUrl = signal('');
  readonly videoUrl = signal('');
  readonly error = signal('');

  private lastPreviewTaskId: string | null = null;

  ngOnInit() {
    // ?taskId= 深度链接：视频任务或首帧图片任务结果回显。
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

  async generateFirstFrame() {
    const prompt = this.scenePrompt().trim();
    if (!prompt) {
      this.message.warning('请输入画面提示词');
      return;
    }
    if (this.generatingImage()) return;

    this.generatingImage.set(true);
    this.firstFrameUrl.set('');
    this.firstFrameSourceUrl.set('');
    this.videoUrl.set('');
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

  async generateVideo() {
    const firstFrameUrl = this.firstFrameUrl();
    const prompt = this.motionPrompt().trim();
    if (!firstFrameUrl) {
      this.message.warning('请先生成首帧图片');
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
          inputJson: JSON.stringify({
            imageUrl: this.firstFrameSourceUrl() || firstFrameUrl,
            prompt,
            duration: 5,
          }),
        }),
      );
      this.lastPreviewTaskId = task.id;
      const done = await this.waitTask(task.id, t =>
        this.progressMessage.set(t.progressMessage || this.progressMessage()),
      );
      this.applyVideo(done);
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
      if (task.taskType === AiTaskType.ImageGeneration) {
        this.applyFirstFrame(task);
      } else {
        this.applyVideo(task);
      }
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
          this.firstFrameSourceUrl.set(parsed.dashScopeUrl || parsed.imageUrl);
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
          if (parsed.imageUrl) {
            this.firstFrameSourceUrl.set(parsed.imageUrl);
            if (!this.firstFrameUrl()) this.firstFrameUrl.set(parsed.imageUrl);
          }
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

  async downloadVideo() {
    const url = this.videoUrl();
    if (!url) return;
    try {
      const blob = await fetch(url, { mode: 'cors' }).then(r => r.blob());
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `教学短视频_${Date.now()}.mp4`;
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