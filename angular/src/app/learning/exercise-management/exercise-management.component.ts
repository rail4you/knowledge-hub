import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed } from '@angular/core';
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
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseService } from '../../proxy/courses/course.service';
import { ExerciseService } from '../../proxy/exams/exercise.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { CreateUpdateExerciseDto, ExerciseDto } from '../../proxy/exams/dtos/models';
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
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

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

  // Options for choice questions
  optionA = '';
  optionB = '';
  optionC = '';
  optionD = '';
  selectedAnswer = '';
  multiAnswerA = false;
  multiAnswerB = false;
  multiAnswerC = false;
  multiAnswerD = false;

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

    this.selectedAnswer = exercise.answer ?? '';
    this.multiAnswerA = exercise.answer?.includes('A') ?? false;
    this.multiAnswerB = exercise.answer?.includes('B') ?? false;
    this.multiAnswerC = exercise.answer?.includes('C') ?? false;
    this.multiAnswerD = exercise.answer?.includes('D') ?? false;

    if (exercise.options && (exercise.type === ExerciseType.SingleChoice || exercise.type === ExerciseType.MultiChoice)) {
      try {
        const opts = JSON.parse(exercise.options);
        this.optionA = opts[0] ?? '';
        this.optionB = opts[1] ?? '';
        this.optionC = opts[2] ?? '';
        this.optionD = opts[3] ?? '';
      } catch {
        this.resetOptionFields();
      }
    } else {
      this.optionA = '';
      this.optionB = '';
      this.optionC = '';
      this.optionD = '';
    }

    this.isModalVisible = true;
  }

  handleModalCancel() {
    this.isModalVisible = false;
  }

  handleModalOk() {
    if (!this.formData.title?.trim() || !this.formData.questionContent?.trim() || !this.formData.answer?.trim()) {
      this.message.warning('请填写必填项（标题、题目内容、答案）');
      return;
    }

    const type = this.formData.type;
    if (type === ExerciseType.SingleChoice || type === ExerciseType.MultiChoice) {
      if (!this.optionA.trim() || !this.optionB.trim()) {
        this.message.warning('选择题至少需要两个选项');
        return;
      }
      this.formData.options = JSON.stringify([
        this.optionA.trim(),
        this.optionB.trim(),
        this.optionC.trim(),
        this.optionD.trim(),
      ]);
    } else {
      this.formData.options = null;
    }

    if (type === ExerciseType.MultiChoice) {
      const answers: string[] = [];
      if (this.multiAnswerA) answers.push('A');
      if (this.multiAnswerB) answers.push('B');
      if (this.multiAnswerC) answers.push('C');
      if (this.multiAnswerD) answers.push('D');
      if (answers.length === 0) {
        this.message.warning('请选择正确答案');
        return;
      }
      this.formData.answer = answers.join(',');
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
    this.selectedAnswer = '';
    this.optionA = '';
    this.optionB = '';
    this.optionC = '';
    this.optionD = '';
    this.multiAnswerA = false;
    this.multiAnswerB = false;
    this.multiAnswerC = false;
    this.multiAnswerD = false;
  }

  isChoiceType(): boolean {
    return this.formData.type === ExerciseType.SingleChoice || this.formData.type === ExerciseType.MultiChoice;
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
}
