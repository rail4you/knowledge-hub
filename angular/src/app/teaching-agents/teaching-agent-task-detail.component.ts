import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AgentRunService } from './agent-run.service';
import { ClassroomAgentTaskService } from './classroom-agent-task.service';
import {
  AgentRunDetail,
  ClassroomAgentAssignment,
  ClassroomAgentTaskDetail,
  assignmentStatusLabel,
  formatDateTime,
  publishStatusLabel,
  targetTypeLabel,
} from './models';

@Component({
  selector: 'app-teaching-agent-task-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzPopconfirmModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './teaching-agent-task-detail.component.html',
  styleUrls: ['./teaching-agent-task-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeachingAgentTaskDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly classroomAgentTaskService = inject(ClassroomAgentTaskService);
  private readonly agentRunService = inject(AgentRunService);

  readonly loading = signal(false);
  readonly transcriptLoading = signal(false);
  readonly task = signal<ClassroomAgentTaskDetail | null>(null);
  readonly selectedAssignment = signal<ClassroomAgentAssignment | null>(null);
  readonly selectedRun = signal<AgentRunDetail | null>(null);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    const taskId = this.route.snapshot.paramMap.get('id');
    if (!taskId) {
      return;
    }

    this.loading.set(true);
    try {
      const task = await this.classroomAgentTaskService.get(taskId).toPromise();
      this.task.set(task ?? null);
    } finally {
      this.loading.set(false);
    }
  }

  async inspectAssignment(assignment: ClassroomAgentAssignment): Promise<void> {
    this.selectedAssignment.set(assignment);
    this.transcriptLoading.set(true);
    try {
      const run = await this.agentRunService.getRun(assignment.id).toPromise();
      this.selectedRun.set(run ?? null);
    } finally {
      this.transcriptLoading.set(false);
    }
  }

  async deleteTask(): Promise<void> {
    const currentTask = this.task();
    if (!currentTask) {
      return;
    }

    await this.classroomAgentTaskService.delete(currentTask.id).toPromise();
    await this.router.navigate(['/teaching/agent-tasks']);
  }

  publishText(value: number): string {
    return publishStatusLabel(value);
  }

  assignmentText(value: number): string {
    return assignmentStatusLabel(value);
  }

  targetText(value: number): string {
    return targetTypeLabel(value);
  }

  formatDate(value?: string): string {
    return formatDateTime(value);
  }
}
