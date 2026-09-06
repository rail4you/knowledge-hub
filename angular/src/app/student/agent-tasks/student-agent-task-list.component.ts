import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { ClassroomAgentTaskService } from '../../teaching-agents/classroom-agent-task.service';
import { StudentAgentTask, assignmentStatusLabel } from '../../teaching-agents/models';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';

@Component({
  selector: 'app-student-agent-task-list',
  standalone: true,
  imports: [CommonModule, RouterModule, NzIconModule, StudentHeroComponent],
  templateUrl: './student-agent-task-list.component.html',
  styleUrls: ['./student-agent-task-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentAgentTaskListComponent implements OnInit {
  private readonly classroomAgentTaskService = inject(ClassroomAgentTaskService);
  private readonly router = inject(Router);

  readonly tasks = signal<StudentAgentTask[]>([]);

  /** Hero 区数据总览 */
  readonly heroStats = computed(() => {
    const tasks = this.tasks();
    return [
      { label: '任务总数', value: tasks.length, suffix: '个', icon: 'robot', color: '#1e6ce8' },
      { label: '进行中', value: tasks.filter(t => t.status === 0).length, suffix: '个', icon: 'play-circle', color: '#10b981' },
      { label: '已完成', value: tasks.filter(t => t.status === 2 || t.status === 3).length, suffix: '个', icon: 'check-circle', color: '#0ea5e9' },
      { label: '智能体', value: new Set(tasks.map(t => t.teachingAgentName).filter(Boolean)).size, suffix: '位', icon: 'experiment', color: '#f59e0b' },
    ];
  });

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
      '#2563eb',
      '#1d4ed8',
      '#3b82f6',
      '#0ea5e9',
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
    if (!value) {
      return '';
    }
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }
    // 仅精确到年月日
    return new Intl.DateTimeFormat('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).format(date);
  }
}
