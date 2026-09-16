import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ClassroomAgentTaskService } from './classroom-agent-task.service';
import {
  CLASSROOM_AGENT_PUBLISH_STATUS,
  CLASSROOM_AGENT_TARGET_TYPE,
  ClassroomAgentTask,
  CreateClassroomAgentTaskPayload,
  ExerciseOption,
  StudentOption,
  TaskCreationOptions,
  TeachingAgentOption,
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
  /** 习题集模式（targetType=exerciseSet）：所属课程 */
  courseId: string | null;
  /** 习题集模式：选定的习题 id 集合 */
  exerciseIds: string[];
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
  courseId: null,
  exerciseIds: [],
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
    NzIconModule,
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
  private readonly messageService = inject(NzMessageService);

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
      return this.options().resources.map(item => ({ id: item.id, label: item.name, disabled: false }));
    }
    return this.options().courses.map(item => ({ id: item.id, label: item.title, disabled: false }));
  });
  /** 当前任务对象下是否有可选的有效项（全空时置灰下拉并提示）。 */
  readonly targetHasUsable = computed(() => this.targetOptions().some(t => !t.disabled));
  /** 当前任务对象下可选项数量为 0 时的占位提示。 */
  readonly targetEmptyPlaceholder = computed(() => {
    const type = this.form().targetType;
    if (this.targetOptions().length === 0) {
      return type === CLASSROOM_AGENT_TARGET_TYPE.resource ? '暂无可用资源' : '暂无可绑定的课程';
    }
    if (this.targetHasUsable()) return '';
    return '暂无可用对象';
  });

  // ========= 习题集模式：点击「选择习题」弹窗，弹窗内先选课程、再选该课程习题 =========
  /** 习题集可选课程（弹窗内课程下拉用）。 */
  readonly exerciseCourseOptions = computed(() =>
    this.options().courses.map(c => ({ id: c.id, label: `${c.title}（${c.exerciseCount ?? 0} 题）`, disabled: false })),
  );
  readonly selectedExerciseCount = computed(() => this.form().exerciseIds.length);
  /** 主表单「选择习题」触发的辅助提示文字。 */
  readonly exerciseTriggerHint = computed(() => {
    if (!this.form().courseId) return '';
    return this.form().exerciseIds.length === 0 ? '该课程暂无可用习题，任务对象无效' : '';
  });

  // 习题 picker：弹窗 + 课程选择 + 习题表格复选框（复用学生选择模式）
  readonly exercisePickerVisible = signal(false);
  /** 弹窗内临时课程选择（确定时才写回表单）。 */
  readonly pickerCourseId = signal<string | null>(null);
  readonly pickerCourseExercises = signal<ExerciseOption[]>([]);
  readonly pickerCourseExercisesLoading = signal(false);
  readonly exercisePickerKeyword = signal('');
  readonly pickedExerciseIds = signal<string[]>([]);
  readonly pickedExerciseIdSet = computed(() => new Set(this.pickedExerciseIds()));
  /** 弹窗内：已选课程且该课程有习题时才展示习题表格、可继续选择。 */
  readonly pickerReady = computed(
    () => !!this.pickerCourseId() && !this.pickerCourseExercisesLoading() && this.pickerCourseExercises().length > 0,
  );
  readonly pickerEmptyHint = computed(() => {
    if (!this.pickerCourseId()) return '请选择课程';
    if (this.pickerCourseExercisesLoading()) return '习题加载中…';
    return this.pickerCourseExercises().length === 0 ? '该课程暂无习题，无法创建习题任务，请选择其他对象' : '';
  });
  readonly modalOkDisabled = computed(() => this.pickedExerciseCount() === 0);
  readonly filteredExercises = computed<ExerciseOption[]>(() => {
    const k = this.exercisePickerKeyword().trim().toLowerCase();
    const all = this.pickerCourseExercises();
    if (!k) return all;
    return all.filter(e => (e.title || '').toLowerCase().includes(k));
  });
  readonly filteredPickedExerciseCount = computed(() => {
    const set = this.pickedExerciseIdSet();
    return this.filteredExercises().filter(e => set.has(e.id)).length;
  });
  readonly allFilteredExercisesPicked = computed(() => {
    const filtered = this.filteredExercises();
    if (filtered.length === 0) return false;
    return this.filteredPickedExerciseCount() === filtered.length;
  });
  readonly someFilteredExercisesPicked = computed(() => {
    const count = this.filteredPickedExerciseCount();
    return count > 0 && count < this.filteredExercises().length;
  });
  readonly pickedExerciseCount = computed(() => this.pickedExerciseIds().length);
  // 任务创建只按智能体名称选择：同一智能体的多个已发布版本里取最新一条，
  // 避免 UI 出现 v1 / v2 等多个版本项。内部仍携带 versionId 供后端解析。
  readonly agentOptions = computed<TeachingAgentOption[]>(() => {
    const agents = this.options().agents;
    const latest = new Map<string, TeachingAgentOption>();
    for (const agent of agents) {
      const existing = latest.get(agent.id);
      if (!existing || agent.versionNumber > existing.versionNumber) {
        latest.set(agent.id, agent);
      }
    }
    return Array.from(latest.values());
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

  /** 切换「任务对象」类型：清空相关对象选择（含习题集课程/习题）。 */
  onTargetTypeChange(type: number): void {
    this.setFormField('targetType', type);
    this.setFormField('targetId', null);
    this.setFormField('courseId', null);
    this.setFormField('exerciseIds', []);
    this.resetPickerState();
  }

  /** 重置习题弹窗内的临时状态。 */
  private resetPickerState(): void {
    this.pickerCourseId.set(null);
    this.pickerCourseExercises.set([]);
    this.pickerCourseExercisesLoading.set(false);
    this.exercisePickerKeyword.set('');
    this.pickedExerciseIds.set([]);
  }

  /** 弹窗内：切换课程后拉取该课程习题（存在 0 题时提示选择其他对象）。 */
  onPickerCourseChange(courseId: string | null): void {
    this.pickerCourseId.set(courseId);
    this.pickerCourseExercises.set([]);
    this.pickerCourseExercisesLoading.set(false);
    this.exercisePickerKeyword.set('');
    this.pickedExerciseIds.set([]);
    if (!courseId) {
      return;
    }
    this.pickerCourseExercisesLoading.set(true);
    this.classroomAgentTaskService.getCourseExercises(courseId).subscribe({
      next: (list) => {
        this.pickerCourseExercises.set(list || []);
        this.pickerCourseExercisesLoading.set(false);
      },
      error: () => {
        this.pickerCourseExercises.set([]);
        this.pickerCourseExercisesLoading.set(false);
        this.messageService.error('加载习题列表失败');
      },
    });
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

  // ========= 习题 picker：弹窗 + 表格 + 复选框（复用学生选择模式） =========
  /** 打开弹窗：以表单当前课程/习题作为初始选择，课程选择也放在弹窗内。 */
  openExercisePicker(): void {
    this.resetPickerState();
    const form = this.form();
    this.pickerCourseId.set(form.courseId);
    this.pickedExerciseIds.set([...form.exerciseIds]);
    if (form.courseId) {
      // 回显已有课程时先加载其习题，并保留已勾选状态
      this.onPickerCourseChange(form.courseId);
      this.pickedExerciseIds.set([...form.exerciseIds]);
    }
    this.exercisePickerVisible.set(true);
  }

  closeExercisePicker(): void {
    this.exercisePickerVisible.set(false);
  }

  /** 确认：把弹窗内的课程与习题一起写回表单。 */
  confirmExercisePicker(): void {
    this.setFormField('courseId', this.pickerCourseId());
    this.setFormField('exerciseIds', [...this.pickedExerciseIds()]);
    this.exercisePickerVisible.set(false);
  }

  setExercisePickerKeyword(value: string): void {
    this.exercisePickerKeyword.set(value ?? '');
  }

  isExercisePicked(id: string): boolean {
    return this.pickedExerciseIdSet().has(id);
  }

  toggleExercisePicked(id: string, checked: boolean): void {
    const current = this.pickedExerciseIds();
    if (checked) {
      if (!current.includes(id)) {
        this.pickedExerciseIds.set([...current, id]);
      }
      return;
    }
    this.pickedExerciseIds.set(current.filter(x => x !== id));
  }

  toggleAllFilteredExercises(checked: boolean): void {
    const filteredIds = this.filteredExercises().map(e => e.id);
    const filteredSet = new Set(filteredIds);
    const current = this.pickedExerciseIds();
    if (checked) {
      const merged = new Set(current);
      filteredIds.forEach(id => merged.add(id));
      this.pickedExerciseIds.set(Array.from(merged));
      return;
    }
    this.pickedExerciseIds.set(current.filter(id => !filteredSet.has(id)));
  }

  invertFilteredExercisesSelection(): void {
    const filteredIds = this.filteredExercises().map(e => e.id);
    const filteredSet = new Set(filteredIds);
    const current = new Set(this.pickedExerciseIds());
    const next: string[] = [];
    current.forEach(id => {
      if (!filteredSet.has(id)) next.push(id);
    });
    filteredIds.forEach(id => {
      if (!current.has(id)) next.push(id);
    });
    this.pickedExerciseIds.set(next);
  }

  clearAllPickedExercises(): void {
    this.pickedExerciseIds.set([]);
  }

  async createTask(): Promise<void> {
    const form = this.form();
    if (!form.title.trim()) {
      this.messageService.warning('请填写任务标题');
      return;
    }
    if (!form.teachingAgentVersionId) {
      this.messageService.warning('请先选择智能体');
      return;
    }
    if (!form.taskPrompt.trim()) {
      this.messageService.warning('请填写任务提示词');
      return;
    }

    // 任务对象：课程/资源选具体对象；习题集选「课程 + 该课程习题」。
    const isExerciseSet = form.targetType === CLASSROOM_AGENT_TARGET_TYPE.exerciseSet;
    let targetId: string;
    let exerciseIds: string[] = [];
    if (isExerciseSet) {
      if (!form.courseId) {
        this.messageService.warning('请先选择习题所属课程');
        return;
      }
      if (form.exerciseIds.length === 0) {
        this.messageService.warning('当前课程暂无可用的习题，习题任务对象无效，请选择其他对象');
        return;
      }
      targetId = form.courseId;
      exerciseIds = form.exerciseIds;
    } else {
      const usable = this.targetOptions().filter(t => !t.disabled);
      if (usable.length === 0) {
        this.messageService.warning('当前任务对象无可选项，无法创建任务');
        return;
      }
      if (!form.targetId || !usable.some(t => t.id === form.targetId)) {
        this.messageService.warning('请选择有效的任务对象');
        return;
      }
      targetId = form.targetId;
    }

    const payload: CreateClassroomAgentTaskPayload = {
      title: form.title,
      description: form.description || undefined,
      teachingAgentVersionId: form.teachingAgentVersionId,
      taskPrompt: form.taskPrompt,
      targetType: form.targetType,
      targetId,
      exerciseIds,
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

  // 从 targetSnapshot 解析具体对象名（课程标题 / 资源名），
  // 没有快照时回退到通用占位，避免列里出现空白单元格。
  targetName(task: ClassroomAgentTask): string {
    const snapshot = task.targetSnapshot;
    if (!snapshot) {
      return '—';
    }
    if (snapshot.course?.title) {
      return snapshot.course.title;
    }
    if (snapshot.resource?.name) {
      return snapshot.resource.name;
    }
    return '—';
  }

  formatDate(value?: string): string {
    return formatDateTime(value);
  }

  /** 习题类型映射（与 ExerciseType 枚举一致）。 */
  exerciseTypeLabel(type: number): string {
    switch (type) {
      case 0: return '单选';
      case 1: return '多选';
      case 2: return '判断';
      case 3: return '填空';
      case 4: return '简答';
      case 5: return '论述';
      case 6: return '案例分析';
      default: return '—';
    }
  }

  trackByTaskId(_: number, task: ClassroomAgentTask): string {
    return task.id;
  }

  readonly publishStatus = CLASSROOM_AGENT_PUBLISH_STATUS;
}
