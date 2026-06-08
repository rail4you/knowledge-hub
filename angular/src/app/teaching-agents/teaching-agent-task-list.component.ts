import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ClassroomAgentTaskService } from './classroom-agent-task.service';
import {
  CLASSROOM_AGENT_PUBLISH_STATUS,
  CLASSROOM_AGENT_TARGET_TYPE,
  ClassroomAgentTask,
  CreateClassroomAgentTaskPayload,
  StudentOption,
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
    NzCheckboxModule,
    NzDatePickerModule,
    NzEmptyModule,
    NzInputModule,
    NzModalModule,
    NzPopconfirmModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
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
  // P1-17：学生选择改为「弹窗 + 表格 + 复选框」模式。
  // 表单里仍然保留 studentIds（最终提交用），picker 自己维护一份临时
  // pickedIds，确认时才同步到 form，取消则丢弃，避免半成品状态污染表单。
  readonly studentPickerVisible = signal(false);
  readonly pickerKeyword = signal('');
  readonly pickedIds = signal<string[]>([]);
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

  // P1-17：picker 内部状态计算
  // - pickedIdSet：把 pickedIds 转成 Set，方便表格逐行 O(1) 判断是否勾选。
  // - filteredStudents：按 pickerKeyword（姓名 + 学号 不区分大小写）过滤。
  // - allFiltered / someFiltered：用于表头 master checkbox 的 checked / indeterminate。
  readonly pickedIdSet = computed(() => new Set(this.pickedIds()));
  readonly filteredStudents = computed<StudentOption[]>(() => {
    const k = this.pickerKeyword().trim().toLowerCase();
    const students = this.options().students;
    if (!k) {
      return students;
    }
    return students.filter(s =>
      (s.name || '').toLowerCase().includes(k) ||
      (s.userName || '').toLowerCase().includes(k),
    );
  });
  readonly filteredPickedCount = computed(() => {
    const set = this.pickedIdSet();
    return this.filteredStudents().filter(s => set.has(s.id)).length;
  });
  readonly allFilteredPicked = computed(() => {
    const filtered = this.filteredStudents();
    if (filtered.length === 0) {
      return false;
    }
    return this.filteredPickedCount() === filtered.length;
  });
  readonly someFilteredPicked = computed(() => {
    const count = this.filteredPickedCount();
    return count > 0 && count < this.filteredStudents().length;
  });
  readonly pickedCount = computed(() => this.pickedIds().length);

  // P1-17：在表单主区域展示已选学生的「姓名 (学号)」摘要。学生很多时
  // 限制最多展示 6 条，多余的用「+N 人」省略，避免一屏被压爆。
  readonly selectedStudentSummary = computed<string[]>(() => {
    const ids = new Set(this.form().studentIds);
    return this.options().students
      .filter(s => ids.has(s.id))
      .map(s => `${s.name} (${s.userName})`);
  });
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

  // P1-17：student picker —— 打开弹窗时把当前 form.studentIds 拷贝到临时
  // pickedIds，确保进入时反映已选状态；关键字也清空，避免上次过滤残留。
  openStudentPicker(): void {
    this.pickedIds.set([...this.form().studentIds]);
    this.pickerKeyword.set('');
    this.studentPickerVisible.set(true);
  }

  closeStudentPicker(): void {
    this.studentPickerVisible.set(false);
  }

  // 确认：把 pickedIds 写回表单。取消：直接关闭，丢弃 pickedIds。
  confirmStudentPicker(): void {
    this.setFormField('studentIds', [...this.pickedIds()]);
    this.studentPickerVisible.set(false);
  }

  setPickerKeyword(value: string): void {
    this.pickerKeyword.set(value ?? '');
  }

  isPicked(id: string): boolean {
    return this.pickedIdSet().has(id);
  }

  // 单行勾选：在 pickedIds 数组里增减 id。
  togglePicked(id: string, checked: boolean): void {
    const current = this.pickedIds();
    if (checked) {
      if (!current.includes(id)) {
        this.pickedIds.set([...current, id]);
      }
      return;
    }
    this.pickedIds.set(current.filter(x => x !== id));
  }

  // 表头复选框：勾选 -> 把当前过滤结果全部并入 pickedIds（保留过滤外已勾选项）；
  // 取消 -> 仅把当前过滤结果从 pickedIds 中移除（同样保留过滤外已勾选项）。
  // 这样在搜索后再点全选只影响可见行，符合直觉。
  toggleAllFiltered(checked: boolean): void {
    const filteredIds = this.filteredStudents().map(s => s.id);
    const filteredSet = new Set(filteredIds);
    const current = this.pickedIds();
    if (checked) {
      const merged = new Set(current);
      filteredIds.forEach(id => merged.add(id));
      this.pickedIds.set(Array.from(merged));
      return;
    }
    this.pickedIds.set(current.filter(id => !filteredSet.has(id)));
  }

  // 反选：只反转当前过滤结果，过滤外的已勾选项保持不动。
  invertFilteredSelection(): void {
    const filteredIds = this.filteredStudents().map(s => s.id);
    const filteredSet = new Set(filteredIds);
    const current = new Set(this.pickedIds());
    const next: string[] = [];
    // 先保留所有过滤外的已勾选项
    current.forEach(id => {
      if (!filteredSet.has(id)) {
        next.push(id);
      }
    });
    // 再加入过滤内未勾选的项
    filteredIds.forEach(id => {
      if (!current.has(id)) {
        next.push(id);
      }
    });
    this.pickedIds.set(next);
  }

  clearAllPicked(): void {
    this.pickedIds.set([]);
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
