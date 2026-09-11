import { inject, provideAppInitializer } from '@angular/core';
import { ToolbarService } from '@volo/ngx-lepton-x.core';
import { AiNotificationBellComponent } from './notification-bell.component';
import { AiTaskNotificationService } from '../services/ai-task-notification.service';
import { MediaTaskNotificationService } from '../../admin/media-jobs/media-task-notification.service';

/**
 * 把 AI 任务通知铃铛注册进 LeptonX 顶部工具栏，并启动全局轮询。
 * 只在应用布局（管理端 / 教师端）出现。
 */
function initAiNotifications() {
  const toolbar = inject(ToolbarService);

  // 避免热重载 / 重复初始化导致多次注册
  toolbar.removeItem('ai-task-notification');
  toolbar.addItem({
    id: 'ai-task-notification',
    order: 100,
    component: AiNotificationBellComponent,
  } as any);

  inject(AiTaskNotificationService).start();
  inject(MediaTaskNotificationService).start();
}

export const AI_NOTIFICATION_PROVIDER = [
  provideAppInitializer(() => {
    initAiNotifications();
  }),
];
