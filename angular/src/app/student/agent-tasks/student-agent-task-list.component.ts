import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { ClassroomAgentTaskService } from '../../proxy/teaching-agents/classroom-agent-task.service';
import type { StudentAgentTaskDto } from '../../proxy/teaching-agents/dtos/models';
import { ClassroomAgentAssignmentStatus } from '../../proxy/teaching-agents/enums/classroom-agent-assignment-status.enum';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentPaginationComponent } from '../shared/student-pagination/student-pagination.component';
import {
  ASSIGNMENT_STATUS_LABEL,
  assignmentStatusClass,
} from './student-agent-task-labels';

/**
 * 学生端 · 智能体课堂任务（对齐原型 #classroom-tasks 的「学生」视图）
 *
 * 原型的学生首屏不是任务列表，而是**方法论引导**：
 *   先提出自己的解释 → 再结合资源核对 → 无法解决时请求教师帮助。
 * 因此本页首屏是引导卡，任务列表在其下。
 *
 * 数据来源（真实接口，生成代理 proxy/teaching-agents）：
 *   · 任务列表 GET /api/app/classroom-agent-task/student-task-list（服务端分页）
 *   · 任务详情 GET /api/app/classroom-agent-task/{id}/task-detail
 *   · 对话     POST /api/app/agent-run/send-message/{assignmentId}
 *   · 提交     POST /api/app/agent-run/submit-assignment/{assignmentId}
 *   · 求助     POST /api/app/agent-run/mark-need-teacher-help/{assignmentId}
 *
 * 四态齐备：loading(skeleton) / error(retry) / empty / data。
 */
@Component({
  selector: 'app-student-agent-task-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
    StudentPaginationComponent,
  ],
  templateUrl: './student-agent-task-list.component.html',
  styleUrls: ['./student-agent-task-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAgentTaskListComponent implements OnInit {
  private readonly classroomAgentTaskService = inject(ClassroomAgentTaskService);
  private readonly router = inject(Router);
  private readonly actRoute = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly tasks = signal<StudentAgentTaskDto[]>([]);
  readonly totalCount = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  /** 方法论引导的三步（原型课堂任务的方法论说明） */
  readonly method = [
    { step: '01', title: '先提出自己的解释', desc: '写下你目前对任务的理解与推理过程，形成自己的初步结论。' },
    { step: '02', title: '再结合资源核对', desc: '对照关联课程、章节与资源，逐条核对判断依据与不确定之处。' },
    { step: '03', title: '无法解决时请求教师帮助', desc: '勾选「需要教师帮助」并说明卡点，教师回复后再继续推进。' },
  ];

  /** 页头数据总览（每个数字都来自真实接口 / 当前页真实数据） */
  readonly stats = computed(() => {
    const page = this.tasks();
    return [
      { label: '任务总数', value: this.totalCount(), hint: '教师发布并分配给你的课堂任务', icon: 'file' },
      {
        label: '本页进行中',
        value: page.filter(t => t.status === ClassroomAgentAssignmentStatus.InProgress).length,
        hint: '当前这一页里已开始的任务',
        icon: 'clock-circle',
      },
      {
        label: '本页待教师回复',
        value: page.filter(t => t.status === ClassroomAgentAssignmentStatus.NeedsTeacherHelp).length,
        hint: '当前这一页里已发出求助的任务',
        icon: 'alert',
      },
      {
        label: '本页已提交',
        value: page.filter(t => t.status === ClassroomAgentAssignmentStatus.Submitted).length,
        hint: '当前这一页里已提交的任务',
        icon: 'check-circle',
      },
    ];
  });

  ngOnInit(): void {
    // 页码存 URL：刷新 / 前进后退都能还原
    this.actRoute.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const page = Number(params.get('page') ?? '1');
      this.pageIndex.set(isNaN(page) || page < 1 ? 1 : page);
      this.load();
    });
  }

  /** 服务端分页加载 */
  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.classroomAgentTaskService
      .getStudentTaskList({
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
      })
      .subscribe({
        next: res => {
          this.tasks.set(res?.items || []);
          this.totalCount.set(res?.totalCount ?? 0);
          this.loading.set(false);
        },
        error: () => {
          this.tasks.set([]);
          this.totalCount.set(0);
          this.loadFailed.set(true);
          this.loading.set(false);
        },
      });
  }

  /** 重新加载 */
  reload(): void {
    this.load();
  }

  /** 翻页（服务端分页） */
  onPageChange(e: { pageIndex: number; pageSize: number }): void {
    this.pageSize.set(e.pageSize);
    this.router.navigate([], {
      relativeTo: this.actRoute,
      queryParams: { page: e.pageIndex },
      queryParamsHandling: 'merge',
    });
  }

  /** 打开任务详情 */
  openTask(assignmentId?: string): void {
    if (!assignmentId) return;
    this.router.navigate(['/student/agent-tasks', assignmentId]);
  }

  /** 去课程中心（空态引导） */
  goCourses(): void {
    this.router.navigate(['/student/courses']);
  }

  /** 状态中文 */
  statusText(status?: number | null): string {
    return ASSIGNMENT_STATUS_LABEL[status ?? 0] ?? '未知状态';
  }

  /** 状态样式 */
  statusClass(status?: number | null): string {
    return assignmentStatusClass(status);
  }

  /** 是否已超期（截止时间已过且未提交） */
  isOverdue(task: StudentAgentTaskDto): boolean {
    if (!task.dueTime) return false;
    if (task.status === ClassroomAgentAssignmentStatus.Submitted) return false;
    return new Date(task.dueTime).getTime() < Date.now();
  }

  /** 日期格式化 */
  formatDate(value?: string | null): string {
    if (!value) return '未设置';
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return value;
    return new Intl.DateTimeFormat('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).format(d);
  }
}
