import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzOutletModule } from 'ng-zorro-antd/core/outlet';
import { MarkdownComponent, MarkdownPipe, provideMarkdown } from 'ngx-markdown';
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
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzFormModule,
    NzInputModule,
    NzPopconfirmModule,
    NzSpinModule,
    NzTagModule,
    NzTabsModule,
    NzOutletModule,
    MarkdownComponent,
    MarkdownPipe,
  ],
  providers: [provideMarkdown()],
  templateUrl: './teaching-agent-task-detail.component.html',
  styleUrls: ['./teaching-agent-task-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeachingAgentTaskDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly classroomAgentTaskService = inject(ClassroomAgentTaskService);
  private readonly agentRunService = inject(AgentRunService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly loading = signal(false);
  readonly transcriptLoading = signal(false);
  readonly task = signal<ClassroomAgentTaskDetail | null>(null);
  readonly selectedRun = signal<AgentRunDetail | null>(null);
  readonly activeTabIndex = signal(0);
  readonly expandedAssignmentId = signal<string | null>(null);
  readonly responding = signal(false);
  teacherResponseText = '';

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

  async toggleAssignment(assignment: ClassroomAgentAssignment): Promise<void> {
    if (this.expandedAssignmentId() === assignment.id) {
      this.expandedAssignmentId.set(null);
      this.selectedRun.set(null);
      return;
    }

    this.expandedAssignmentId.set(assignment.id);
    this.transcriptLoading.set(true);
    try {
      const run = await this.agentRunService.getRun(assignment.id).toPromise();
      this.selectedRun.set(run ?? null);
    } finally {
      this.transcriptLoading.set(false);
    }
  }

  async respondToHelp(assignment: ClassroomAgentAssignment): Promise<void> {
    const text = this.teacherResponseText.trim();
    if (!text) return;

    this.responding.set(true);
    try {
      await this.classroomAgentTaskService.respondToHelp(assignment.id, text).toPromise();
      this.teacherResponseText = '';
      await this.load();
    } finally {
      this.responding.set(false);
    }
  }

  onResponseInput(value: string): void {
    this.teacherResponseText = value;
    this.cdr.markForCheck();
  }

  async deleteTask(): Promise<void> {
    const currentTask = this.task();
    if (!currentTask) {
      return;
    }

    await this.classroomAgentTaskService.delete(currentTask.id).toPromise();
    await this.router.navigate(['/teaching/agent-tasks']);
  }

  getInitial(name: string): string {
    if (!name) return '?';
    return name.charAt(0).toUpperCase();
  }

  statusColor(status: number): string {
    switch (status) {
      case 2: return 'green';
      case 3: return 'red';
      case 1: return 'blue';
      default: return 'default';
    }
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
