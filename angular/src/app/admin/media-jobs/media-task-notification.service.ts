import { Injectable, inject, signal } from '@angular/core';
import { ConfigStateService } from '@abp/ng.core';
import { NzNotificationService } from 'ng-zorro-antd/notification';
import { Subject, Subscription, timer, forkJoin, of } from 'rxjs';
import { catchError, switchMap, takeUntil, tap } from 'rxjs/operators';
import { ResourceMediaJobService } from '../../proxy/resources/media/resource-media-job.service';
import type { ResourceMediaJobDto } from '../../proxy/application/contracts/resources/media/models';

/**
 * 资源媒体处理任务全局轮询 + 失败通知（复用 AI 通知的思路）。
 * - 登录后每 8s 拉取"我的未读失败媒体任务"。
 * - 新失败任务弹出右上角通知，并并入顶栏铃铛未读数。
 */
@Injectable({ providedIn: 'root' })
export class MediaTaskNotificationService {
  private readonly mediaJobService = inject(ResourceMediaJobService);
  private readonly configState = inject(ConfigStateService);
  private readonly nzNotification = inject(NzNotificationService);

  readonly unreadCount = signal(0);
  readonly notifications = signal<ResourceMediaJobDto[]>([]);

  private readonly destroy$ = new Subject<void>();
  private pollSub?: Subscription;
  private knownIds = new Set<string>();
  private seeded = false;
  private started = false;
  /**
   * 本次轮询开始时间：只有在此之后失败的媒体任务才弹 toast。
   * 登录前积压的历史失败只进铃铛未读数，不再每次登录重弹。
   */
  private sessionStartedAt: Date | null = null;

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

  reload(): void {
    this.fetchOnce();
  }

  acknowledge(id: string): void {
    this.mediaJobService.markAsRead(id).subscribe({
      next: () => {
        this.notifications.update((list) => list.filter((x) => x.id !== id));
        this.unreadCount.update((c) => Math.max(0, c - 1));
      },
    });
  }

  markAllAsRead(): void {
    if (this.unreadCount() === 0) return;
    this.mediaJobService.markAllAsRead().subscribe({
      next: () => {
        this.notifications.set([]);
        this.unreadCount.set(0);
      },
    });
  }

  private beginPolling(): void {
    if (this.pollSub) return;
    this.sessionStartedAt = new Date();
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
    this.knownIds.clear();
    this.seeded = false;
  }

  private fetchOnce(): void {
    this.fetchOnce$().subscribe();
  }

  private fetchOnce$() {
    // ok 标记本次「失败任务」是否真正取到；请求失败时不得建立基线，
    // 否则下一次成功拉取会把历史失败任务全部误判为「新失败」并集中弹通知。
    let itemsOk = false;
    return forkJoin({
      count: this.mediaJobService.getMyUnreadCount().pipe(catchError(() => of(this.unreadCount()))),
      items: this.mediaJobService.getMyRecent(true).pipe(
        tap(() => { itemsOk = true; }),
        catchError(() => of(this.notifications())),
      ),
    }).pipe(
      switchMap(({ count, items }) => {
        this.unreadCount.set(count ?? 0);
        if (itemsOk) {
          this.notifications.set(items ?? []);
        }
        this.detectNewFailures(itemsOk ? (items ?? []) : [], itemsOk);
        return of(null);
      }),
    );
  }

  /**
   * 任务是否「本次轮询开始之后」失败。
   * 时间戳缺失时保守返回 false（只进铃铛，不弹历史 toast）。
   */
  private isFinishedAfterSessionStart(item: ResourceMediaJobDto, sessionStart: Date | null): boolean {
    if (!sessionStart) {
      return false;
    }
    const raw = item.completedAt || item.creationTime;
    if (!raw) {
      return false;
    }
    const finishedAt = new Date(raw);
    return !Number.isNaN(finishedAt.getTime()) && finishedAt.getTime() >= sessionStart.getTime();
  }

  private detectNewFailures(items: ResourceMediaJobDto[], itemsOk = true): void {
    // 请求失败：不建基线、不弹提示，等下一次成功拉取
    if (!itemsOk) {
      return;
    }

    const sessionStart = this.sessionStartedAt;
    const fresh: ResourceMediaJobDto[] = [];
    for (const item of items) {
      if (!item.id) continue;
      if (!this.knownIds.has(item.id)) {
        this.knownIds.add(item.id);
        if (this.seeded && this.isFinishedAfterSessionStart(item, sessionStart)) {
          fresh.push(item);
        }
      }
    }
    this.seeded = true;

    for (const item of fresh) {
      const ref = this.nzNotification.error(
        '资源媒体处理失败',
        `「${item.resourceName || item.resourceId}」生成失败，可在媒体处理任务中重试`,
        { nzDuration: 8000, nzPlacement: 'topRight' },
      );
      ref.onClick.subscribe(() => this.acknowledge(item.id!));
    }
  }
}
