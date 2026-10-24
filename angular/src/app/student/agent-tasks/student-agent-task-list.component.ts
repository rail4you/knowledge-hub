import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { ClassroomAgentTaskService } from '../../teaching-agents/classroom-agent-task.service';
import { StudentAgentTask, assignmentStatusLabel } from '../../teaching-agents/models';

@Component({
  selector: 'app-student-agent-task-list',
  standalone: true,
  imports: [CommonModule, RouterModule, NzIconModule, NzSpinModule, NzPaginationModule],
  templateUrl: './student-agent-task-list.component.html',
  styleUrls: ['./student-agent-task-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAgentTaskListComponent implements OnInit {
  private readonly classroomAgentTaskService = inject(ClassroomAgentTaskService);
  private readonly router = inject(Router);

  readonly tasks = signal<StudentAgentTask[]>([]);
  readonly loading = signal(false);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  /** 当前页任务 */
  readonly pagedTasks = computed(() => {
    const start = (this.pageIndex() - 1) * this.pageSize();
    return this.tasks().slice(start, start + this.pageSize());
  });

  /** 数据总览（对齐原型 metrics：全部 / 已完成 / 进行中 / 需处理） */
  readonly metrics = computed(() => {
    const tasks = this.tasks();
    return [
      { label: '全部任务', value: tasks.length, hint: '课堂任务总数', icon: 'file' },
      { label: '已完成', value: tasks.filter(t => t.status === 2).length, hint: '可查看结果', icon: 'check' },
      {
        label: '进行中',
        value: tasks.filter(t => t.status === 0 || t.status === 1).length,
        hint: '待开始与对话中',
        icon: 'clock-circle',
      },
      { label: '需处理', value: tasks.filter(t => t.status === 3).length, hint: '需教师介入', icon: 'alert' },
    ];
  });

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.classroomAgentTaskService.getStudentList({
        skipCount: 0,
        maxResultCount: 100,
      }).toPromise();
      this.tasks.set(result?.items ?? []);
    } finally {
      this.loading.set(false);
    }
  }

  openTask(assignmentId: string): void {
    this.router.navigate(['/student/agent-tasks', assignmentId]);
  }

  statusText(status: number): string {
    return assignmentStatusLabel(status);
  }

  /** 状态 tag 色：对齐原型 tag（gold 高亮异常态） */
  statusClass(status: number): string {
    switch (status) {
      case 1: return 'primary';
      case 2: return 'green';
      case 3: return 'gold';
      default: return 'gray';
    }
  }

  formatDate(value?: string): string {
    if (!value) {
      return '—';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }
    return new Intl.DateTimeFormat('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).format(date);
  }

  onPageChange(pageIndex: number): void {
    this.pageIndex.set(pageIndex);
  }
}
