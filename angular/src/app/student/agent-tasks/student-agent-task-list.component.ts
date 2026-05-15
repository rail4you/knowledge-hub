import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTableModule } from 'ng-zorro-antd/table';
import { ClassroomAgentTaskService } from '../../teaching-agents/classroom-agent-task.service';
import { StudentAgentTask, assignmentStatusLabel, formatDateTime } from '../../teaching-agents/models';

@Component({
  selector: 'app-student-agent-task-list',
  standalone: true,
  imports: [CommonModule, RouterModule, NzButtonModule, NzCardModule, NzEmptyModule, NzTagModule, NzTableModule],
  templateUrl: './student-agent-task-list.component.html',
  styleUrls: ['./student-agent-task-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAgentTaskListComponent implements OnInit {
  private readonly classroomAgentTaskService = inject(ClassroomAgentTaskService);

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

  statusText(status: number): string {
    return assignmentStatusLabel(status);
  }

  formatDate(value?: string): string {
    return formatDateTime(value);
  }
}
