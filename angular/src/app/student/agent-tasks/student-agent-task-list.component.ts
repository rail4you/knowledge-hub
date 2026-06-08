import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { ClassroomAgentTaskService } from '../../teaching-agents/classroom-agent-task.service';
import { StudentAgentTask, assignmentStatusLabel, formatDateTime } from '../../teaching-agents/models';

@Component({
  selector: 'app-student-agent-task-list',
  standalone: true,
  imports: [CommonModule, RouterModule, NzButtonModule, NzIconModule, NzEmptyModule],
  templateUrl: './student-agent-task-list.component.html',
  styleUrls: ['./student-agent-task-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAgentTaskListComponent implements OnInit {
  private readonly classroomAgentTaskService = inject(ClassroomAgentTaskService);
  private readonly router = inject(Router);

  readonly tasks = signal<StudentAgentTask[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    const result = await this.classroomAgentTaskService.getStudentList({
      skipCount: 0,
      maxResultCount: 20,
    }).toPromise();

    this.tasks.set(result?.items ?? []);
  }

  openTask(assignmentId: string): void {
    this.router.navigate(['/student/agent-tasks', assignmentId]);
  }

  coverGradient(task: StudentAgentTask): string {
    const palettes = [
      '#1e6ce8',
      '#0c4cb8',
      '#2563eb',
      '#0891b2',
      '#0284c7',
    ];
    const key = task.teachingAgentName || task.title || '';
    let hash = 0;
    for (let i = 0; i < key.length; i++) {
      hash = (hash * 31 + key.charCodeAt(i)) | 0;
    }
    return palettes[Math.abs(hash) % palettes.length];
  }

  statusText(status: number): string {
    return assignmentStatusLabel(status);
  }

  formatDate(value?: string): string {
    return formatDateTime(value);
  }
}
