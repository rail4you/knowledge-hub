import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AiTaskNotificationService } from '../services/ai-task-notification.service';
import { AiGenerationTaskDto, AiTaskService } from '../services/ai-task.service';
import { MediaTaskNotificationService } from '../../admin/media-jobs/media-task-notification.service';
import type { ResourceMediaJobDto } from '../../proxy/application/contracts/resources/media/models';

/**
 * 顶部工具栏任务通知铃铛：AI 任务完成 + 资源媒体处理失败。
 * 通过 LeptonX ToolbarService 注册到顶栏。
 */
@Component({
  selector: 'app-ai-notification-bell',
  standalone: true,
  imports: [
    CommonModule,
    NzBadgeModule,
    NzIconModule,
    NzDropDownModule,
    NzEmptyModule,
    NzTagModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="ai-bell" nz-dropdown [nzDropdownMenu]="menu" nzTrigger="click" nzPlacement="bottomRight">
      <nz-badge
        [nzCount]="totalUnread()"
        [nzOverflowCount]="99"
        nzSize="small"
        [nzOffset]="[2, -2]"
      >
        <span nz-icon nzType="bell" class="ai-bell__icon"></span>
      </nz-badge>
    </div>

    <nz-dropdown-menu #menu="nzDropdownMenu">
      <div class="ai-bell-panel">
        <div class="ai-bell-panel__header">
          <span>任务通知</span>
          @if (totalUnread() > 0) {
            <a (click)="$event.stopPropagation(); markAllAsRead()">全部已读</a>
          }
        </div>

        @if (aiService.notifications().length === 0 && mediaService.notifications().length === 0) {
          <div class="ai-bell-panel__empty">
            <nz-empty nzNotFoundContent="暂无新通知"></nz-empty>
          </div>
        } @else {
          <div class="ai-bell-panel__list">
            @for (n of aiService.notifications(); track n.id) {
              <div class="ai-bell-item" (click)="openTask(n)">
                <nz-tag [nzColor]="typeColor(n)">{{ typeLabel(n) }}</nz-tag>
                <div class="ai-bell-item__body">
                  <div class="ai-bell-item__title">{{ n.title }}</div>
                  <div class="ai-bell-item__meta">
                    {{ n.resourceName || '—' }} · {{ formatDate(n.completedAt || n.creationTime) }}
                  </div>
                </div>
              </div>
            }
            @for (m of mediaService.notifications(); track m.id) {
              <div class="ai-bell-item" (click)="openMediaJob(m)">
                <nz-tag nzColor="error">媒体处理</nz-tag>
                <div class="ai-bell-item__body">
                  <div class="ai-bell-item__title">{{ m.resourceName || '资源媒体生成失败' }}</div>
                  <div class="ai-bell-item__meta">
                    {{ m.errorMessage || '生成失败' }} · {{ formatDate(m.completedAt || m.creationTime) }}
                  </div>
                </div>
              </div>
            }
          </div>
        }

        <div class="ai-bell-panel__footer" (click)="goTaskCenter()">查看全部任务</div>
      </div>
    </nz-dropdown-menu>
  `,
  styles: [
    `
      .ai-bell {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        cursor: pointer;
        padding: 0 6px;
      }
      .ai-bell__icon {
        font-size: 18px;
      }
      .ai-bell-panel {
        width: 340px;
        max-width: 90vw;
        background: var(--kh-panel, #fff);
      }
      .ai-bell-panel__header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 10px 14px;
        font-weight: 600;
        border-bottom: 1px solid var(--kh-line, #f0f0f0);
      }
      .ai-bell-panel__list {
        max-height: 360px;
        overflow-y: auto;
      }
      .ai-bell-panel__empty {
        padding: 16px 0;
      }
      .ai-bell-item {
        display: flex;
        gap: 8px;
        padding: 10px 14px;
        cursor: pointer;
        border-bottom: 1px solid var(--kh-line, #f5f5f5);
        transition: background 0.15s;
      }
      .ai-bell-item:hover {
        background: rgba(24, 144, 255, 0.06);
      }
      .ai-bell-item__body {
        min-width: 0;
      }
      .ai-bell-item__title {
        font-size: 13px;
        color: #262626;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .ai-bell-item__meta {
        margin-top: 2px;
        font-size: 12px;
        color: #8c8c8c;
      }
      .ai-bell-panel__footer {
        padding: 8px 14px;
        text-align: center;
        color: #1890ff;
        cursor: pointer;
        border-top: 1px solid var(--kh-line, #f0f0f0);
      }
    `,
  ],
})
export class AiNotificationBellComponent {
  readonly aiService = inject(AiTaskNotificationService);
  readonly mediaService = inject(MediaTaskNotificationService);
  private readonly router = inject(Router);

  readonly totalUnread = computed(() => this.aiService.unreadCount() + this.mediaService.unreadCount());

  markAllAsRead(): void {
    this.aiService.markAllAsRead();
    this.mediaService.markAllAsRead();
  }

  typeLabel(n: AiGenerationTaskDto): string {
    return AiTaskService.typeLabel(n.taskType);
  }

  typeColor(n: AiGenerationTaskDto): string {
    return AiTaskService.typeColor(n.taskType);
  }

  formatDate(iso?: string): string {
    if (!iso) return '';
    try {
      const d = new Date(iso);
      const mm = String(d.getMonth() + 1).padStart(2, '0');
      const dd = String(d.getDate()).padStart(2, '0');
      const hh = String(d.getHours()).padStart(2, '0');
      const mi = String(d.getMinutes()).padStart(2, '0');
      return `${mm}-${dd} ${hh}:${mi}`;
    } catch {
      return iso;
    }
  }

  openTask(n: AiGenerationTaskDto): void {
    this.aiService.openTaskResult(n);
  }

  openMediaJob(m: ResourceMediaJobDto): void {
    if (m.id) {
      this.mediaService.acknowledge(m.id);
    }
    this.router.navigate(['/admin/media-jobs']);
  }

  goTaskCenter(): void {
    this.router.navigate(['/ai/tasks']);
  }
}
