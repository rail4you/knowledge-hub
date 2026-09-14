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
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { firstValueFrom, lastValueFrom, Subject } from 'rxjs';
import { take, takeUntil, tap } from 'rxjs/operators';
import { AiGenerationTaskDto, AiTaskService, AiTaskStatus, AiTaskType } from '../services/ai-task.service';
import { AiTaskNotificationService } from '../services/ai-task-notification.service';
import { TeachingSceneCategory } from '../services/teaching-scene.service';
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
    NzSpinModule,
    NzEmptyModule,
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

  readonly imageCategory = TeachingSceneCategory.Image;
  readonly sizes = SIZES;

  readonly prompt = signal('');
  readonly size = signal('1024*1024');
  readonly negativePrompt = signal('');

  readonly generating = signal(false);
  readonly progressMessage = signal('');
  readonly imageUrl = signal('');
  readonly error = signal('');

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
  }

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
    this.progressMessage.set('正在提交任务…');

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
      this.lastPreviewTaskId = task.id;
      const done = await this.waitTask(task.id, t =>
        this.progressMessage.set(t.progressMessage || this.progressMessage()),
      );
      this.applyResult(done);
    } catch (err: any) {
      this.error.set(err?.message || '生成失败，请稍后重试');
    } finally {
      this.generating.set(false);
    }
  }

  private waitTask(taskId: string, onProgress?: (t: AiGenerationTaskDto) => void) {
    return lastValueFrom(this.aiTaskNotifications.pollTask(taskId).pipe(tap(t => onProgress?.(t))));
  }

  private loadTaskPreview(taskId: string) {
    this.aiTaskService.get(taskId).pipe(take(1)).subscribe(task => {
      if (task.status === AiTaskStatus.Completed) {
        this.applyResult(task);
      } else if (task.status === AiTaskStatus.Pending || task.status === AiTaskStatus.Running) {
        this.generating.set(true);
        this.progressMessage.set(task.progressMessage || '生成中…');
        this.waitTask(taskId, t =>
          this.progressMessage.set(t.progressMessage || this.progressMessage()),
        )
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
    try {
      const blob = await fetch(url, { mode: 'cors' }).then(r => r.blob());
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `教学图片_${Date.now()}.png`;
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