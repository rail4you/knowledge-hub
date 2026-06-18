import { Component, signal, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzStepsModule } from 'ng-zorro-antd/steps';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { CourseService } from '../../../proxy/courses/course.service';
import type { CreateUpdateCourseDto } from '../../../proxy/courses/dtos/models';
import { MajorService } from '../../../proxy/majors/major.service';
import type { MajorLookupDto } from '../../../proxy/majors/dtos/models';
import { FindMajorNamePipe } from '../../../shared/find-major-name.pipe';

@Component({
  selector: 'app-course-create',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzButtonModule,
    NzIconModule,
    NzStepsModule,
    NzSpinModule,
    FindMajorNamePipe,
  ],
  templateUrl: './course-create.component.html',
  styleUrls: ['./course-create.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CourseCreateComponent {
  private readonly courseService = inject(CourseService);
  private readonly majorService = inject(MajorService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  loading = signal(false);
  currentStep = signal(0);

  readonly majors = signal<MajorLookupDto[]>([]);

  semesters = [
    '2024春季',
    '2024秋季',
    '2025春季',
    '2025秋季'
  ];

  difficulties = [
    { label: '入门', value: 1 },
    { label: '初级', value: 2 },
    { label: '中级', value: 3 },
    { label: '高级', value: 4 }
  ];

  formData = signal<CreateUpdateCourseDto>({
    title: '',
    description: '',
    coverImageUrl: '',
    majorId: undefined,
    semester: '',
    credits: 3,
    semesterHours: 48,
    difficulty: 1,
    categoryId: undefined
  });

  constructor() {
    this.majorService.getLookupList().subscribe({
      next: (list) => this.majors.set(list || []),
    });
  }

  updateField<K extends keyof CreateUpdateCourseDto>(field: K, value: CreateUpdateCourseDto[K]) {
    this.formData.update(data => ({ ...data, [field]: value }));
  }

  nextStep() {
    if (this.currentStep() === 0) {
      if (!this.formData().title) {
        this.message.warning('请输入课程名称');
        return;
      }
    }
    this.currentStep.update(s => s + 1);
  }

  prevStep() {
    this.currentStep.update(s => Math.max(0, s - 1));
  }

  submit() {
    if (!this.formData().title) {
      this.message.warning('请输入课程名称');
      return;
    }
    
    this.loading.set(true);
    this.courseService.create(this.formData()).subscribe({
      next: (result) => {
        this.loading.set(false);
        this.message.success('课程创建成功');
        this.router.navigate(['/learning/my-courses']);
      },
      error: (err) => {
        this.loading.set(false);
        this.message.error('课程创建失败');
      }
    });
  }

  saveDraft() {
    this.message.info('草稿保存功能开发中');
  }

  getDifficultyLabel(): string {
    const diff = this.difficulties.find(d => d.value === this.formData().difficulty);
    return diff?.label || '入门';
  }
}