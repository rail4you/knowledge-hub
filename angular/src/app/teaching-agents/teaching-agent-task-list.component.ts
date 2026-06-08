import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ClassroomAgentTaskService } from './classroom-agent-task.service';
import {
  CLASSROOM_AGENT_PUBLISH_STATUS,
  CLASSROOM_AGENT_TARGET_TYPE,
  ClassroomAgentTask,
  CreateClassroomAgentTaskPayload,
  TaskCreationOptions,
  formatDateTime,
  publishStatusLabel,
  targetTypeLabel,
} from './models';

interface TaskFormState {
  title: string;
  description: string;
  teachingAgentVersionId: string | null;
  taskPrompt: string;
  targetType: number;
  targetId: string | null;
  dueTime: Date | null;
  studentIds: string[];
}

const initialForm: TaskFormState = {
  title: '',
  description: '',
  teachingAgentVersionId: null,
  taskPrompt: '',
  targetType: CLASSROOM_AGENT_TARGET_TYPE.course,
  targetId: null,
  dueTime: null,
  studentIds: [],
};

@Component({
  selector: 'app-teaching-agent-task-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzDatePickerModule,
    NzEmptyModule,
    NzInputModule,
    NzModalModule,
    NzPopconfirmModule,
    NzSelectModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './teaching-agent-task-list.component.html',
  styleUrls: ['./teaching-agent-task-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeachingAgentTaskListComponent implements OnInit {
  private readonly classroomAgentTaskService = inject(ClassroomAgentTaskService);

  readonly loading = signal(false);
  readonly creating = signal(false);
  readonly createModalVisible = signal(false);
  readonly options = signal<TaskCreationOptions>({
    agents: [],
    students: [],
    courses: [],
    resources: [],
  });
  readonly tasks = signal<ClassroomAgentTask[]>([]);
  readonly studentSelectOpen = signal(false);
  readonly form = signal<TaskFormState>({ ...initialForm });

  readonly targetOptions = computed(() => {
    if (this.form().targetType === CLASSROOM_AGENT_TARGET_TYPE.resource) {
      return this.options().resources.map(item => ({ id: item.id, label: item.name }));
    }

    return this.options().courses.map(item => ({ id: item.id, label: item.title }));
  });
  // P1-16：批量选学生——暴露给模板的统计 getter，避免在模板里写三目运算。
  readonly totalStudentCount = computed(() => this.options().students.length);
  readonly selectedStudentCount = computed(() => this.form().studentIds.length);
  readonly allStudentsSelected = computed(
    () => this.totalStudentCount() > 0 && this.selectedStudentCount() === this.totalStudentCount(),
  );
  readonly summary = computed(() => {
    const tasks = this.tasks();

    return {
      totalTasks: tasks.length,
      publishedTasks: tasks.filter(task => task.publishStatus === CLASSROOM_AGENT_PUBLISH_STATUS.published).length,
      assignmentCount: tasks.reduce((sum, task) => sum + task.assignmentCount, 0),
      needsHelpCount: tasks.reduce((sum, task) => sum + task.needsHelpCount, 0),
    };
  });

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [options, taskResult] = await Promise.all([
        this.classroomAgentTaskService.getCreateOptions().toPromise(),
        this.classroomAgentTaskService.getTeacherList({
          skipCount: 0,
          maxResultCount: 20,
        }).toPromise(),
      ]);

      this.options.set(options ?? this.options());
      this.tasks.set(taskResult?.items ?? []);
    } finally {
      this.loading.set(false);
    }
  }

  openCreateModal(): void {
    this.form.set({ ...initialForm });
    this.createModalVisible.set(true);
  }

  closeCreateModal(): void {
    this.createModalVisible.set(false);
  }

  setFormField<K extends keyof TaskFormState>(key: K, value: TaskFormState[K]): void {
    this.form.update(current => ({ ...current, [key]: value }));
  }

  setStudentSelectOpen(value: boolean): void {
    this.studentSelectOpen.set(value);
  }

  setStudentIds(value: string[] | null): void {
    this.setFormField('studentIds', value ?? []);
    setTimeout(() => this.studentSelectOpen.set(true));
  }

  // P1-16：批量选学生。当学生数量大时，一个个在下拉里点选极不友好。
  // 提供全选 / 清空 / 反选三个一键操作，并在标签栏显示实时统计。
  selectAllStudents(): void {
    const allIds = this.options().students.map(s => s.id);
    this.setFormField('studentIds', allIds);
  }

  clearAllStudents(): void {
    this.setFormField('studentIds', []);
  }

  invertStudentSelection(): void {
    const selected = new Set(this.form().studentIds);
    const inverted = this.options().students
      .filter(s => !selected.has(s.id))
      .map(s => s.id);
    this.setFormField('studentIds', inverted);
  }

  async createTask(): Promise<void> {
    const form = this.form();
    if (!form.teachingAgentVersionId || !form.targetId) {
      return;
    }

    const payload: CreateClassroomAgentTaskPayload = {
      title: form.title,
      description: form.description || undefined,
      teachingAgentVersionId: form.teachingAgentVersionId,
      taskPrompt: form.taskPrompt,
      targetType: form.targetType,
      targetId: form.targetId,
      dueTime: form.dueTime ? form.dueTime.toISOString() : null,
      studentIds: form.studentIds,
    };

    this.creating.set(true);
    try {
      await this.classroomAgentTaskService.create(payload).toPromise();
      this.createModalVisible.set(false);
      await this.load();
    } finally {
      this.creating.set(false);
    }
  }

  async publishTask(taskId: string): Promise<void> {
    await this.classroomAgentTaskService.publish(taskId).toPromise();
    await this.load();
  }

  async deleteTask(taskId: string): Promise<void> {
    await this.classroomAgentTaskService.delete(taskId).toPromise();
    await this.load();
  }

  publishText(status: number): string {
    return publishStatusLabel(status);
  }

  targetText(targetType: number): string {
    return targetTypeLabel(targetType);
  }

  formatDate(value?: string): string {
    return formatDateTime(value);
  }

  trackByTaskId(_: number, task: ClassroomAgentTask): string {
    return task.id;
  }

  readonly publishStatus = CLASSROOM_AGENT_PUBLISH_STATUS;
}
