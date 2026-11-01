import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { BuildTaskService } from '../../proxy/resource-libraries/build-task.service';
import { ResourceService } from '../../proxy/resources/resource.service';
import type { BuildTaskDto, BuildTaskItemDto } from '../../proxy/resource-libraries/dtos/models';
import { BuildTaskStatus } from '../../proxy/resource-libraries/build-task-status.enum';
import { LibraryContentType } from '../../proxy/resource-libraries/library-content-type.enum';
import { ResourceStatus } from '../../proxy/resources/enums/resource-status.enum';

/**
 * 教师端「我的共建任务」。
 *
 * 只列当前用户参与的共建任务（主责 / 协作校 / 库级 Contributor）。
 * 提交交付物会同时发起库准入审核 —— 界面上必须讲清楚「交了 ≠ 发布了」。
 */
@Component({
  selector: 'app-my-build-tasks',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzEmptyModule,
    NzModalModule,
    NzProgressModule,
    NzSelectModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './my-build-tasks.component.html',
  styleUrls: ['./my-build-tasks.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyBuildTasksComponent implements OnInit {
  private readonly taskService = inject(BuildTaskService);
  private readonly resourceService = inject(ResourceService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly tasks = signal<BuildTaskDto[]>([]);
  readonly emptyHint = signal('');

  readonly submitOpen = signal(false);
  readonly submitting = signal(false);
  readonly currentTask = signal<BuildTaskDto | null>(null);
  readonly selectedItemId = signal<string | null>(null);
  readonly selectedResourceId = signal<string | null>(null);
  readonly comment = signal('');
  readonly resources = signal<Array<{ id: string; name: string }>>([]);

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.taskService
      .getTasks({ onlyMine: true, maxResultCount: 100, skipCount: 0, sorting: 'creationTime desc' } as any)
      .subscribe({
        next: res => {
          const list = (res.items ?? []).filter(
            t => t.status !== BuildTaskStatus.Draft && t.status !== BuildTaskStatus.Closed,
          );
          this.tasks.set(list);
          this.loading.set(false);
          this.emptyHint.set(
            list.length === 0
              ? '暂无进行中的共建任务。任务由主建学校（或库管理员）建好后发布，协作校教师即可在此提交交付物。'
              : '',
          );
        },
        error: () => {
          this.loading.set(false);
          this.emptyHint.set('加载失败，请稍后重试。');
        },
      });
  }

  openSubmit(task: BuildTaskDto, item?: BuildTaskItemDto): void {
    this.currentTask.set(task);
    const firstItem = item ?? (task.items ?? []).find(i => (i.acceptedCount ?? 0) < (i.requiredCount ?? 0));
    this.selectedItemId.set(firstItem?.id ?? null);
    this.selectedResourceId.set(null);
    this.comment.set('');
    this.loadMyResources();
    this.submitOpen.set(true);
  }

  /** 只列「我创建的、还未进入任何资源库」的资源，避免把已发布内容重复提交。 */
  private loadMyResources(): void {
    this.resourceService
      .getFilteredList({ skipCount: 0, maxResultCount: 200, sorting: 'creationTime desc' } as any)
      .subscribe({
        next: res => {
          const list = (res.items ?? [])
            .filter(r => !r.libraryId && r.status !== ResourceStatus.LeagueApproved)
            .map(r => ({ id: r.id!, name: r.name ?? '(未命名)' }));
          this.resources.set(list);
        },
        error: () => this.resources.set([]),
      });
  }

  submit(): void {
    const task = this.currentTask();
    if (!task?.id) return;
    if (!this.selectedItemId()) {
      this.message.warning('请选择交付项');
      return;
    }
    if (!this.selectedResourceId()) {
      this.message.warning('请选择要交付的资源');
      return;
    }

    this.submitting.set(true);
    this.taskService
      .submitDelivery({
        taskId: task.id,
        taskItemId: this.selectedItemId()!,
        contentType: LibraryContentType.Resource,
        contentId: this.selectedResourceId()!,
        comment: this.comment() || undefined,
      } as any)
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.submitOpen.set(false);
          this.message.success('已提交交付物，并同步发起库准入审核（通过后才会在库内发布）');
          this.reload();
        },
        error: err => {
          this.submitting.set(false);
          this.message.error(err?.error?.error?.message ?? '提交失败');
        },
      });
  }

  statusColor(status?: BuildTaskStatus): string {
    switch (status) {
      case BuildTaskStatus.Accepted:
        return 'success';
      case BuildTaskStatus.Returned:
        return 'error';
      default:
        return 'processing';
    }
  }

  trackById = (_: number, item: { id?: string }) => item.id;
}
