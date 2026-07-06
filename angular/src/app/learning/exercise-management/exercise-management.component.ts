import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseService } from '../../proxy/courses/course.service';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import { RestService } from '@abp/ng.core';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { CreateUpdateExerciseDto, ExerciseDto, ExerciseImportResultDto } from '../../proxy/exams/dtos/models';
import { ExerciseType } from '../../proxy/exams/enums/exercise-type.enum';

@Component({
  selector: 'app-exercise-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzTagModule,
    NzIconModule,
    NzModalModule,
    NzFormModule,
    NzInputNumberModule,
    NzTableModule,
    NzCheckboxModule,
    NzSpinModule,
    NzRadioModule,
  ],
  templateUrl: './exercise-management.component.html',
  styleUrls: ['./exercise-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExerciseManagementComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly exerciseService = inject(ExerciseService);
  private readonly restService = inject(RestService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly cdr = inject(ChangeDetectorRef);

  courses = signal<CourseDto[]>([]);
  selectedCourseId = signal<string | null>(null);
  exercises = signal<ExerciseDto[]>([]);
  selectedType = signal<ExerciseType | null>(null);
  loading = signal(false);

  // Modal state
  isModalVisible = false;
  isEdit = false;
  editId: string | null = null;
  saving = false;
  formData: CreateUpdateExerciseDto = this.emptyForm();

  // Import modal state
  isImportModalVisible = false;
  importing = false;
  selectedImportFile: File | null = null;

  // Options for choice questions - dynamic array approach
  readonly letters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
  options = signal<string[]>(['', '', '', '']);
  selectedAnswerIndex = signal<number>(-1);
  multiSelectedAnswers = signal<Set<number>>(new Set());

  filteredExercises = computed(() => {
    const type = this.selectedType();
    const list = this.exercises();
    if (type === null) return list;
    return list.filter(e => e.type === type);
  });

  ngOnInit() {
    this.loadCourses();
  }

  private emptyForm(): CreateUpdateExerciseDto {
    return {
      courseId: '',
      title: '',
      questionContent: '',
      type: ExerciseType.SingleChoice,
      options: null,
      answer: '',
      answerExplanation: '',
      difficulty: 2,
      score: 5,
    };
  }

  loadCourses() {
    this.courseService.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe({
      next: (result) => {
        this.courses.set(result.items || []);
      },
    });
  }

  onCourseSelected(courseId: string) {
    this.selectedCourseId.set(courseId);
    this.loadExercises();
  }

  loadExercises() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.exerciseService.getByCourse(courseId).subscribe({
      next: (data) => {
        this.exercises.set(data || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载习题失败');
      },
    });
  }

  openCreateModal() {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    this.isEdit = false;
    this.editId = null;
    this.formData = { ...this.emptyForm(), courseId };
    this.resetOptionFields();
    this.isModalVisible = true;
  }

  openEditModal(exercise: ExerciseDto) {
    this.isEdit = true;
    this.editId = exercise.id ?? null;
    this.formData = {
      courseId: exercise.courseId,
      chapterId: exercise.chapterId,
      title: exercise.title,
      questionContent: exercise.questionContent,
      type: exercise.type,
      options: exercise.options,
      answer: exercise.answer,
      answerExplanation: exercise.answerExplanation,
      difficulty: exercise.difficulty,
      score: exercise.score,
    };

    if (exercise.options && (exercise.type === ExerciseType.SingleChoice || exercise.type === ExerciseType.MultiChoice)) {
      try {
        const opts: string[] = JSON.parse(exercise.options);
        // Pad with empty strings to at least 4 options
        while (opts.length < 4) opts.push('');
        this.options.set(opts);

        // Parse answer into selected index / multi-selected set
        const answerStr = exercise.answer ?? '';
        if (exercise.type === ExerciseType.SingleChoice) {
          const letter = answerStr.trim().toUpperCase();
          const idx = letter ? this.letters.indexOf(letter) : -1;
          this.selectedAnswerIndex.set(idx >= 0 && idx < opts.length ? idx : -1);
          this.multiSelectedAnswers.set(new Set());
        } else {
          // Multi-choice: answer is like "A,C"
          this.selectedAnswerIndex.set(-1);
          const indices = answerStr.split(',').map(s => this.letters.indexOf(s.trim().toUpperCase())).filter(i => i >= 0);
          this.multiSelectedAnswers.set(new Set(indices));
        }
      } catch {
        this.resetOptionFields();
      }
    } else {
      this.resetOptionFields();
    }

    this.isModalVisible = true;
  }

  handleModalCancel() {
    this.isModalVisible = false;
  }

  handleModalOk() {
    const type = this.formData.type;

    // Build answer string for choice questions
    if (type === ExerciseType.SingleChoice || type === ExerciseType.MultiChoice) {
      this.formData.answer = this.getAnswerString();
    }

    if (!this.formData.title?.trim() || !this.formData.questionContent?.trim() || !this.formData.answer?.trim()) {
      this.message.warning('请填写必填项（标题、题目内容、答案）');
      return;
    }

    if (type === ExerciseType.SingleChoice || type === ExerciseType.MultiChoice) {
      const nonEmptyOptions = this.options().filter(o => o.trim() !== '');
      if (nonEmptyOptions.length < 2) {
        this.message.warning('选择题至少需要两个选项');
        return;
      }
      this.formData.options = JSON.stringify(this.options().map(o => o.trim()));
    } else {
      this.formData.options = null;
    }

    this.saving = true;
    const request = this.isEdit && this.editId
      ? this.exerciseService.update(this.editId, this.formData)
      : this.exerciseService.create(this.formData);

    request.subscribe({
      next: () => {
        this.saving = false;
        this.isModalVisible = false;
        this.message.success(this.isEdit ? '习题更新成功' : '习题创建成功');
        this.loadExercises();
      },
      error: () => {
        this.saving = false;
        this.message.error('保存失败');
      },
    });
  }

  deleteExercise(exercise: ExerciseDto) {
    this.modal.confirm({
      nzTitle: '确认删除',
      nzContent: `确定要删除习题「${exercise.title}」吗？`,
      nzOkText: '删除',
      nzOkDanger: true,
      nzCancelText: '取消',
      nzOnOk: () => {
        if (!exercise.id) return;
        this.exerciseService.delete(exercise.id).subscribe({
          next: () => {
            this.message.success('习题已删除');
            this.loadExercises();
          },
          error: () => {
            this.message.error('删除失败');
          },
        });
      },
    });
  }

  onTypeChange() {
    this.resetOptionFields();
  }

  private resetOptionFields() {
    this.options.set(['', '', '', '']);
    this.selectedAnswerIndex.set(-1);
    this.multiSelectedAnswers.set(new Set());
  }

  isChoiceType(): boolean {
    return this.formData.type === ExerciseType.SingleChoice || this.formData.type === ExerciseType.MultiChoice;
  }

  getAnswerString(): string {
    const form = this.formData;
    if (form.type === ExerciseType.SingleChoice) {
      const idx = this.selectedAnswerIndex();
      return idx >= 0 ? this.letters[idx] : '';
    } else if (form.type === ExerciseType.MultiChoice) {
      return Array.from(this.multiSelectedAnswers()).sort().map(i => this.letters[i]).join(',');
    }
    return form.answer || '';
  }

  updateOption(index: number, value: string): void {
    this.options.update(opts => {
      const newOpts = [...opts];
      newOpts[index] = value;
      return newOpts;
    });
  }

  toggleMultiAnswer(index: number): void {
    this.multiSelectedAnswers.update(s => {
      const newSet = new Set(s);
      if (newSet.has(index)) {
        newSet.delete(index);
      } else {
        newSet.add(index);
      }
      return newSet;
    });
  }

  addOption(): void {
    this.options.update(opts => [...opts, '']);
  }

  removeOption(index: number): void {
    if (this.options().length <= 2) return; // minimum 2 options
    this.options.update(opts => opts.filter((_, i) => i !== index));
    // Clean up selected answers
    this.multiSelectedAnswers.update(s => {
      const newSet = new Set(Array.from(s).filter(i => i < this.options().length));
      return newSet;
    });
    if (this.selectedAnswerIndex() === index) {
      this.selectedAnswerIndex.set(-1);
    } else if (this.selectedAnswerIndex() > index) {
      this.selectedAnswerIndex.update(i => i - 1);
    }
  }

  getTypeName(type: ExerciseType): string {
    const names: Record<number, string> = {
      [ExerciseType.SingleChoice]: '单选题',
      [ExerciseType.MultiChoice]: '多选题',
      [ExerciseType.TrueFalse]: '判断题',
      [ExerciseType.FillBlank]: '填空题',
      [ExerciseType.ShortAnswer]: '问答题',
      [ExerciseType.Essay]: '论述题',
      [ExerciseType.CaseAnalysis]: '案例分析',
    };
    return names[type] ?? '未知';
  }

  getTypeColor(type: ExerciseType): string {
    const colors: Record<number, string> = {
      [ExerciseType.SingleChoice]: 'blue',
      [ExerciseType.MultiChoice]: 'purple',
      [ExerciseType.TrueFalse]: 'cyan',
      [ExerciseType.FillBlank]: 'orange',
      [ExerciseType.ShortAnswer]: 'gold',
      [ExerciseType.Essay]: 'red',
      [ExerciseType.CaseAnalysis]: 'green',
    };
    return colors[type] ?? 'default';
  }

  getDifficultyName(difficulty: number): string {
    const names: Record<number, string> = { 1: '入门', 2: '简单', 3: '中等', 4: '困难', 5: '专家' };
    return names[difficulty] ?? '未知';
  }

  getDifficultyColor(difficulty: number): string {
    const colors: Record<number, string> = { 1: 'green', 2: 'lime', 3: 'gold', 4: 'orange', 5: 'red' };
    return colors[difficulty] ?? 'default';
  }

  // Batch selection
  checkedIds = signal<Set<string>>(new Set());

  get isAllChecked(): boolean {
    const filtered = this.filteredExercises();
    return filtered.length > 0 && filtered.every(e => this.checkedIds().has(e.id!));
  }

  onAllChecked(checked: boolean) {
    const ids = new Set<string>();
    if (checked) {
      this.filteredExercises().forEach(e => { if (e.id) ids.add(e.id); });
    }
    this.checkedIds.set(ids);
  }

  onRowChecked(id: string, checked: boolean) {
    this.checkedIds.update(s => {
      const newSet = new Set(s);
      if (checked) newSet.add(id);
      else newSet.delete(id);
      return newSet;
    });
  }

  deleteBatch() {
    const ids = Array.from(this.checkedIds());
    if (ids.length === 0) {
      this.message.warning('请先选择要删除的习题');
      return;
    }
    this.modal.confirm({
      nzTitle: '批量删除',
      nzContent: `确定要删除选中的 ${ids.length} 个习题吗？`,
      nzOkText: '删除',
      nzOkDanger: true,
      nzCancelText: '取消',
      nzOnOk: () => {
        this.restService.request<string[], void>({
          method: 'POST',
          url: '/api/app/exercise/batch-remove',
          body: ids,
        }, { apiName: 'KnowledgeHub' }).subscribe({
          next: () => {
            this.message.success(`成功删除 ${ids.length} 个习题`);
            this.checkedIds.set(new Set());
            this.loadExercises();
          },
          error: () => {
            this.message.error('批量删除失败');
          },
        });
      },
    });
  }

  // Import methods
  openImportModal() {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    this.isImportModalVisible = true;
    this.selectedImportFile = null;
  }

  closeImportModal() {
    this.isImportModalVisible = false;
    this.selectedImportFile = null;
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      const isExcel = file.name.endsWith('.xlsx') || file.name.endsWith('.xls');
      if (!isExcel) {
        this.message.error('只能上传 Excel 文件 (.xlsx, .xls)');
        return;
      }
      const isLt10M = file.size / 1024 / 1024 < 10;
      if (!isLt10M) {
        this.message.error('文件大小不能超过 10MB');
        return;
      }

      this.selectedImportFile = file;
      this.cdr.markForCheck();
    }
  }

  handleImport() {
    if (!this.selectedImportFile) {
      this.message.warning('请先选择文件');
      return;
    }

    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.importing = true;
    const file = this.selectedImportFile;

    const formData = new FormData();
    formData.append('file', file, file.name);

    this.restService.request<any, ExerciseImportResultDto>({
      method: 'POST',
      url: `/api/app/exercise/import-from-excel/${courseId}`,
      body: formData,
    }, { apiName: 'KnowledgeHub' }).subscribe({
      next: (result: ExerciseImportResultDto) => {
        this.importing = false;
        if (result.failCount === 0) {
          this.message.success(`导入成功！共导入 ${result.successCount} 个习题`);
          this.closeImportModal();
          this.loadExercises();
        } else {
          const errorMsg = result.errors?.slice(0, 5).join('\n') || '';
          this.message.warning(`导入完成：成功 ${result.successCount}，失败 ${result.failCount}\n${errorMsg}`);
        }
      },
      error: (err) => {
        this.importing = false;
        this.message.error('导入失败: ' + (err.message || '未知错误'));
      },
    });
  }
}
