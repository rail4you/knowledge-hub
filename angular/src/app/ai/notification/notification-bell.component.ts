import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AiTaskNotificationService } from '../services/ai-task-notification.service';
import { AiGenerationTaskDto, AiTaskService } from '../services/ai-task.service';

/**
 * 顶部工具栏 AI 任务通知铃铛：未读徽标 + 下拉列出最近完成的 AI 任务。
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
        [nzCount]="notificationService.unreadCount()"
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
          <span>AI 任务通知</span>
          @if (notificationService.unreadCount() > 0) {
            <a (click)="$event.stopPropagation(); notificationService.markAllAsRead()">全部已读</a>
          }
        </div>

        @if (notificationService.notifications().length === 0) {
          <div class="ai-bell-panel__empty">
            <nz-empty nzNotFoundContent="暂无新完成的任务"></nz-empty>
          </div>
        } @else {
          <div class="ai-bell-panel__list">
            @for (n of notificationService.notifications(); track n.id) {
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
  readonly notificationService = inject(AiTaskNotificationService);
  private readonly router = inject(Router);

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
    this.notificationService.openTaskResult(n);
  }

  goTaskCenter(): void {
    this.router.navigate(['/ai/tasks']);
  }
}
