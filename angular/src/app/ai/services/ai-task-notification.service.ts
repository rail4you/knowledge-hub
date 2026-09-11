import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import { NzNotificationService } from 'ng-zorro-antd/notification';
import { Observable, Subject, Subscription, timer, forkJoin, interval, of } from 'rxjs';
import { catchError, startWith, switchMap, takeUntil, takeWhile } from 'rxjs/operators';
import { AiGenerationTaskDto, AiTaskService, AiTaskStatus, aiTaskResultRoute } from './ai-task.service';

/**
 * AI 任务全局轮询 + 完成通知。
 * - 登录后每 8s 拉取"我的未读已完成任务"。
 * - 新完成的任务弹出右上角通知，并驱动顶部铃铛未读数。
 * - 页面可订阅 completed$ 以在任务完成时刷新自己的结果表。
 */
@Injectable({ providedIn: 'root' })
export class AiTaskNotificationService {
  private readonly aiTaskService = inject(AiTaskService);
  private readonly configState = inject(ConfigStateService);
  private readonly nzNotification = inject(NzNotificationService);
  private readonly router = inject(Router);

  readonly unreadCount = signal(0);
  readonly notifications = signal<AiGenerationTaskDto[]>([]);

  private readonly destroy$ = new Subject<void>();
  private readonly completedSubject = new Subject<AiGenerationTaskDto[]>();
  /** 页面订阅：任务完成时刷新结果表 */
  readonly completed$ = this.completedSubject.asObservable();

  private pollSub?: Subscription;
  private knownCompletedIds = new Set<string>();
  private seeded = false;
  private started = false;

  start(): void {
    if (this.started) return;
    this.started = true;

    this.configState
      .getOne$('currentUser')
      .pipe(takeUntil(this.destroy$))
      .subscribe((user) => {
        if (user?.isAuthenticated) {
          this.beginPolling();
        } else {
          this.stopPolling();
        }
      });
  }

  /** 立即刷新一次（用于标记已读、任务操作后）。 */
  reload(): void {
    this.fetchOnce();
  }

  /**
   * 轮询单个任务直到进入终态（Completed / Failed / Cancelled）。
   * 会持续发出进度快照，便于页面展示进度条。
   */
  pollTask(taskId: string): Observable<AiGenerationTaskDto> {
    return interval(2500).pipe(
      startWith(0),
      switchMap(() => this.aiTaskService.get(taskId)),
      takeWhile(
        (t) => t.status === AiTaskStatus.Pending || t.status === AiTaskStatus.Running,
        true,
      ),
    );
  }

  acknowledge(id: string): void {
    this.aiTaskService.markAsRead(id).subscribe({
      next: () => {
        this.notifications.update((list) => list.filter((x) => x.id !== id));
        this.unreadCount.update((c) => Math.max(0, c - 1));
      },
    });
  }

  markAllAsRead(): void {
    if (this.unreadCount() === 0) return;
    this.aiTaskService.markAllAsRead().subscribe({
      next: () => {
        this.notifications.set([]);
        this.unreadCount.set(0);
      },
    });
  }

  /** 跳到对应功能页的结果 UI（通知 toast / 铃铛共用）。 */
  openTaskResult(task: AiGenerationTaskDto): void {
    this.acknowledge(task.id);
    this.router.navigate([aiTaskResultRoute(task.taskType)], { queryParams: { taskId: task.id } });
  }

  private beginPolling(): void {
    if (this.pollSub) return;
    this.pollSub = timer(0, 8000)
      .pipe(
        switchMap(() => this.fetchOnce$()),
        takeUntil(this.destroy$),
      )
      .subscribe();
  }

  private stopPolling(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = undefined;
    this.unreadCount.set(0);
    this.notifications.set([]);
    this.knownCompletedIds.clear();
    this.seeded = false;
  }

  private fetchOnce(): void {
    this.fetchOnce$().subscribe();
  }

  private fetchOnce$() {
    return forkJoin({
      count: this.aiTaskService.getMyUnreadCount().pipe(catchError(() => of(this.unreadCount()))),
      items: this.aiTaskService
        .getMyCompleted(true)
        .pipe(catchError(() => of(this.notifications()))),
    }).pipe(
      switchMap(({ count, items }) => {
        this.unreadCount.set(count ?? 0);
        this.notifications.set(items ?? []);
        this.detectNewCompletions(items ?? []);
        return of(null);
      }),
    );
  }

  private detectNewCompletions(items: AiGenerationTaskDto[]): void {
    const newlyCompleted: AiGenerationTaskDto[] = [];
    for (const item of items) {
      if (!this.knownCompletedIds.has(item.id)) {
        this.knownCompletedIds.add(item.id);
        if (this.seeded) {
          newlyCompleted.push(item);
        }
      }
    }
    // 首次加载只做基线，不为历史任务弹通知
    this.seeded = true;

    for (const item of newlyCompleted) {
      // 点击通知体直接跳到对应功能页的结果 UI（?taskId= 深度链接）
      const ref = this.nzNotification.success(
        'AI 任务已完成（点击查看结果）',
        `${AiTaskService.typeLabel(item.taskType)}：${item.title}`,
        { nzDuration: 8000, nzPlacement: 'topRight' },
      );
      ref.onClick.subscribe((e) => {
        // 关闭按钮的点击会冒泡到通知体，忽略避免误跳转
        const el = e.target as HTMLElement | null;
        if (el?.closest?.('.ant-notification-notice-close')) return;
        this.openTaskResult(item);
      });
    }
    if (newlyCompleted.length > 0) {
      this.completedSubject.next(newlyCompleted);
    }
  }
}
