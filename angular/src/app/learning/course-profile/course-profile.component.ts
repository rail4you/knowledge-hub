import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { take } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseInfoService } from '../../proxy/courses/course-info.service';
import { CourseTextbookService } from '../../proxy/courses/course-textbook.service';
import type { CourseDto, CourseInfoDto, SaveCourseInfoDto, CourseTextbookDto, SaveCourseTextbookDto } from '../../proxy/courses/dtos/models';
import { MyCoursesService } from '../shared/my-courses.service';
import { CourseContextService } from '../course-context.service';

/**
 * 课程信息与教材（对齐 kg-edu 的 course_infos + books）。
 *
 * 为什么单独立页而不是塞进课程编辑弹窗：
 * 这些是长文本（教学目标 / 毕业要求 / 知识逻辑结构）与 1:N 的教材清单，
 * 放在课程表单里会让「新建课程」变成一个巨大的表单；分开维护更接近老师的真实工作方式。
 * 学生端在课程详情「课程简介」Tab 里读取同一份数据。
 */
@Component({
  selector: 'app-course-profile',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzTableModule,
    NzTooltipModule,
    NzModalModule,
    NzFormModule,
    NzInputNumberModule,
    NzSwitchModule,
  ],
  templateUrl: './course-profile.component.html',
  styleUrls: ['./course-profile.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseProfileComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly infoService = inject(CourseInfoService);
  private readonly textbookService = inject(CourseTextbookService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly form = signal<SaveCourseInfoDto>({ courseId: '' });

  readonly textbooks = signal<CourseTextbookDto[]>([]);
  readonly isTextbookModalOpen = signal(false);
  readonly editingTextbook = signal<CourseTextbookDto | null>(null);
  readonly textbookForm = signal<SaveCourseTextbookDto>(this.emptyTextbookForm());

  /** 已填写字段数（页头概览用，让老师知道还差几项）。 */
  readonly filledCount = computed(() => {
    const f = this.form();
    return [
      f.background,
      f.objectives,
      f.target,
      f.highlights,
      f.introduction,
      f.structure,
      f.graduationRequirements,
    ].filter(v => !!v && !!v.trim()).length;
  });

  ngOnInit(): void {
    this.loadCourses();
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      const courseId = params.get('courseId') || this.courseContext.currentCourseId();
      if (courseId) this.onCourseSelected(courseId);
    });
  }

  loadCourses(): void {
    this.myCourses.getCourses(300).subscribe({
      next: list => this.courses.set(list as any),
      error: () => this.courses.set([]),
    });
  }

  onCourseSelected(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    if (!courseId) {
      this.form.set({ courseId: '' });
      this.textbooks.set([]);
      return;
    }
    this.courseContext.setCourse(courseId);
    this.loadInfo(courseId);
    this.loadTextbooks(courseId);
  }

  loadInfo(courseId: string): void {
    this.loading.set(true);
    this.infoService.getByCourse(courseId).subscribe({
      next: (info: CourseInfoDto) => {
        this.form.set({
          courseId,
          background: info?.background ?? '',
          objectives: info?.objectives ?? '',
          target: info?.target ?? '',
          highlights: info?.highlights ?? '',
          introduction: info?.introduction ?? '',
          structure: info?.structure ?? '',
          graduationRequirements: info?.graduationRequirements ?? '',
        });
        this.loading.set(false);
      },
      error: () => {
        this.form.set({ courseId });
        this.loading.set(false);
      },
    });
  }

  loadTextbooks(courseId: string): void {
    this.textbookService.getByCourse(courseId).subscribe({
      next: rows => this.textbooks.set(rows ?? []),
      error: () => this.textbooks.set([]),
    });
  }

  /** 模板里不能写对象展开（Angular 模板不支持 spread），统一走这里打补丁。 */
  patchForm(patch: Partial<SaveCourseInfoDto>): void {
    this.form.set({ ...this.form(), ...patch });
  }

  patchTextbook(patch: Partial<SaveCourseTextbookDto>): void {
    this.textbookForm.set({ ...this.textbookForm(), ...patch });
  }

  saveInfo(): void {
    const model = this.form();
    if (!model.courseId) return;
    this.saving.set(true);
    this.infoService.save(model as SaveCourseInfoDto).subscribe({
      next: () => {
        this.saving.set(false);
        this.message.success('课程信息已保存');
      },
      error: err => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '保存失败');
      },
    });
  }

  // ── 教材 ─────────────────────────────────────────────────────

  openCreateTextbook(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    this.editingTextbook.set(null);
    this.textbookForm.set({ ...this.emptyTextbookForm(), courseId });
    this.isTextbookModalOpen.set(true);
  }

  openEditTextbook(row: CourseTextbookDto): void {
    this.editingTextbook.set(row);
    this.textbookForm.set({
      courseId: row.courseId!,
      title: row.title ?? '',
      author: row.author ?? '',
      publisher: row.publisher ?? '',
      edition: row.edition ?? '',
      isbn: row.isbn ?? '',
      coverUrl: row.coverUrl ?? '',
      attachmentResourceId: row.attachmentResourceId ?? null,
      isPrimary: row.isPrimary ?? false,
      sortOrder: row.sortOrder ?? 0,
    });
    this.isTextbookModalOpen.set(true);
  }

  saveTextbook(): void {
    const model = this.textbookForm();
    if (!model.title?.trim()) {
      this.message.warning('请填写书名');
      return;
    }
    const current = this.editingTextbook();
    const request = current
      ? this.textbookService.update(current.id!, model as SaveCourseTextbookDto)
      : this.textbookService.create(model as SaveCourseTextbookDto);

    request.subscribe({
      next: () => {
        this.isTextbookModalOpen.set(false);
        this.message.success(current ? '已保存' : '已添加');
        const courseId = this.selectedCourseId();
        if (courseId) this.loadTextbooks(courseId);
      },
      error: err => this.message.error(err?.error?.error?.message || '保存失败'),
    });
  }

  setPrimary(row: CourseTextbookDto): void {
    if (!row.id) return;
    this.textbookService.setPrimary(row.id).subscribe({
      next: () => {
        this.message.success('已设为主教材');
        const courseId = this.selectedCourseId();
        if (courseId) this.loadTextbooks(courseId);
      },
      error: err => this.message.error(err?.error?.error?.message || '操作失败'),
    });
  }

  removeTextbook(row: CourseTextbookDto): void {
    if (!row.id) return;
    this.modal.confirm({
      nzTitle: '删除教材',
      nzContent: `确定删除「${row.title}」吗？只删除教材登记信息，不会删除附件文件。`,
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.textbookService.delete(row.id!).subscribe({
            next: () => {
              this.message.success('已删除');
              const courseId = this.selectedCourseId();
              if (courseId) this.loadTextbooks(courseId);
              resolve();
            },
            error: err => {
              this.message.error(err?.error?.error?.message || '删除失败');
              reject(err);
            },
          });
        }),
    });
  }

  private emptyTextbookForm(): SaveCourseTextbookDto {
    return {
      courseId: '',
      title: '',
      author: '',
      publisher: '',
      edition: '',
      isbn: '',
      coverUrl: '',
      attachmentResourceId: null,
      isPrimary: false,
      sortOrder: 0,
    };
  }
}
