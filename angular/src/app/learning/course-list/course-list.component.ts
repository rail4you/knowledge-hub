import { Component, signal, inject, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription, of } from 'rxjs';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto, CreateUpdateCourseDto } from '../../proxy/courses/dtos/models';
import { CourseStatus } from '../../proxy/courses/enums/course-status.enum';
import { MajorService } from '../../proxy/majors/major.service';
import type { MajorLookupDto } from '../../proxy/majors/dtos/models';
import { OssUploadService, OssUploadResultDto } from '../../shared/oss-upload.service';

/** 本地表单类型：proxy 重新生成前（abp generate-proxy -t ng）用交叉类型承载新增的多专业字段 */
type CourseForm = CreateUpdateCourseDto & { majorIds?: string[] };

@Component({
  selector: 'app-course-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzTagModule,
    NzSpinModule,
    NzEmptyModule,
    NzTableModule,
    NzIconModule,
    NzSwitchModule,
    NzModalModule,
    NzFormModule,
    NzGridModule,
    NzUploadModule,
  ],
  templateUrl: './course-list.component.html',
  styleUrls: ['./course-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CourseListComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly majorService = inject(MajorService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly cdr = inject(ChangeDetectorRef);

  courses = signal<CourseDto[]>([]);
  loading = signal(false);
  filterText = signal('');
  readonly majors = signal<MajorLookupDto[]>([]);

  // Modal state
  isModalVisible = false;
  isEdit = false;
  editId: string | null = null;
  saving = false;

  // Cover upload state
  coverUploading = false;
  coverFileList: NzUploadFile[] = [];

  // Plain object - not a signal. ngModel mutates this directly,
  // which works reliably inside nz-modal with OnPush.
  formData: CourseForm = this.emptyForm();

  difficulties = [
    { label: '入门', value: 1 },
    { label: '初级', value: 2 },
    { label: '中级', value: 3 },
    { label: '高级', value: 4 }
  ];

  statusOptions = [
    { label: '草稿', value: CourseStatus.Draft },
    { label: '已发布', value: CourseStatus.Published }
  ];

  private emptyForm(): CourseForm {
    return {
      title: '',
      description: '',
      coverImageUrl: '',
      majorId: undefined,
      majorIds: [],
      semester: '',
      credits: undefined,
      semesterHours: undefined,
      difficulty: 1,
      categoryId: undefined,
      status: CourseStatus.Draft,
      isRecommended: false,
    } as CourseForm;
  }

  ngOnInit() {
    this.loadCourses();
    this.majorService.getLookupList().subscribe({
      next: (list) => this.majors.set(list || []),
    });
  }

  loadCourses() {
    // Only show full-page spinner on first load (no existing data)
    if (this.courses().length === 0) {
      this.loading.set(true);
    }
    this.courseService.getList({
      filter: this.filterText() || undefined,
      maxResultCount: 100,
      skipCount: 0
    } as any).subscribe({
      next: (result) => {
        this.courses.set(result.items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  getDifficultyColor(difficulty?: number): string {
    const colors: Record<number, string> = {
      1: '#52c41a',
      2: '#73d13d',
      3: '#faad14',
      4: '#fa8c16'
    };
    return colors[difficulty || 1] || '#d9d9d9';
  }

  getDifficultyLabel(difficulty?: number): string {
    return this.difficulties.find(d => d.value === difficulty)?.label || '入门';
  }

  getDifficultyClass(difficulty?: number): string {
    const classes: Record<number, string> = {
      1: 'intro',
      2: 'basic',
      3: 'mid',
      4: 'adv'
    };
    return classes[difficulty || 1] || 'intro';
  }

  getStatusLabel(status?: number): string {
    const labels: Record<number, string> = {
      0: '草稿',
      5: '已发布'
    };
    return labels[status ?? 0] || '未知';
  }

  getStatusClass(status?: number): string {
    const classes: Record<number, string> = {
      0: 'draft',
      5: 'published'
    };
    return classes[status ?? 0] || 'draft';
  }

  getStatusColor(status?: number): string {
    const colors: Record<number, string> = {
      0: 'default',
      5: 'success'
    };
    return colors[status ?? 0] || 'default';
  }

  // ═══ Cover upload ═══

  /**
   * nz-upload 的 customRequest：完全由我们接管上传流程，
   * 通过 OssUploadService 把文件 POST 到 /api/oss-upload/image，
   * 成功后把返回的 OSS 公开 URL 写回表单的 coverImageUrl。
   */
  coverCustomRequest = (item: any): Subscription => {
    const file: File | undefined = item.file;
    if (!file) {
      item.onError?.('未选择文件');
      return of(null).subscribe();
    }
    if (!file.type.startsWith('image/')) {
      this.message.error('只能上传图片文件');
      item.onError?.('仅支持图片');
      return of(null).subscribe();
    }
    if (file.size / 1024 / 1024 > 5) {
      this.message.error('图片大小不能超过 5MB');
      item.onError?.('文件过大');
      return of(null).subscribe();
    }

    this.coverUploading = true;
    return this.ossUploadService.uploadImage(file).subscribe({
      next: (res: OssUploadResultDto) => {
        this.formData.coverImageUrl = res.url;
        this.message.success('封面上传成功');
        this.coverUploading = false;
        item.onSuccess?.(res, item.file);
      },
      error: () => {
        this.coverUploading = false;
        this.message.error('封面上传失败，请重试');
        item.onError?.('上传失败');
      },
    });
  };

  /** 上传前预览：本地 blob URL 立即显示，OSS 返回后会被覆盖 */
  coverBeforeUpload = (file: NzUploadFile): boolean => {
    const raw = file as any;
    if (raw.originFileObj && raw.originFileObj instanceof File) {
      const blobUrl = URL.createObjectURL(raw.originFileObj);
      this.formData.coverImageUrl = blobUrl;
    }
    return true;
  };

  /** 用户点击"移除"按钮：清空封面 */
  onCoverRemove(): void {
    this.formData.coverImageUrl = '';
    this.coverFileList = [];
  }

  // ═══ Modal ═══

  openCreateModal() {
    this.isEdit = false;
    this.editId = null;
    this.formData = this.emptyForm();
    this.coverFileList = [];
    this.isModalVisible = true;
  }

  openEditModal(course: CourseDto) {
    this.isEdit = true;
    this.editId = course.id ?? null;
    // 后端已返回 majorIds（主专业排第一）；老数据只有 majorId 时兜底单元素数组
    const majorIds = ((course as any).majorIds as string[] | undefined)?.length
      ? [...(course as any).majorIds]
      : course.majorId ? [course.majorId] : [];
    this.formData = {
      title: course.title ?? '',
      description: course.description ?? '',
      coverImageUrl: course.coverImageUrl ?? '',
      majorId: course.majorId ?? majorIds[0] ?? undefined,
      majorIds,
      semester: course.semester ?? '',
      credits: course.credits ?? undefined,
      semesterHours: course.semesterHours ?? undefined,
      difficulty: course.difficulty ?? 1,
      categoryId: undefined,
      status: course.status ?? CourseStatus.Draft,
      isRecommended: (course as any).isRecommended ?? false,
    } as CourseForm;
    this.coverFileList = [];
    this.isModalVisible = true;
  }

  /** 多选专业变化时同步主专业：清空=公共课；主专业被移除则取第一个 */
  onMajorsChange(ids: string[]) {
    this.formData.majorIds = ids ?? [];
    if (this.formData.majorIds.length === 0) {
      this.formData.majorId = undefined;
    } else if (!this.formData.majorId || !this.formData.majorIds.includes(this.formData.majorId)) {
      this.formData.majorId = this.formData.majorIds[0];
    }
  }

  /** 发往后端的载荷：majorId=主专业，majorIds=全量归属（空=公共课） */
  private buildPayload(): CreateUpdateCourseDto {
    const majorIds = this.formData.majorIds ?? [];
    return {
      ...this.formData,
      majorIds,
      majorId: majorIds.length ? (this.formData.majorId ?? majorIds[0]) : undefined,
    } as unknown as CreateUpdateCourseDto;
  }

  /** 从已有课程构造载荷（用于推荐开关等局部更新，避免丢副专业） */
  private payloadFromCourse(course: CourseDto, patch: Partial<CreateUpdateCourseDto>): CreateUpdateCourseDto {
    const majorIds = ((course as any).majorIds as string[] | undefined)?.length
      ? [...(course as any).majorIds]
      : course.majorId ? [course.majorId] : [];
    return {
      title: course.title ?? '',
      description: course.description ?? '',
      coverImageUrl: course.coverImageUrl ?? '',
      majorId: course.majorId ?? majorIds[0] ?? undefined,
      semester: course.semester ?? '',
      credits: course.credits ?? undefined,
      semesterHours: course.semesterHours ?? undefined,
      difficulty: course.difficulty ?? 1,
      categoryId: course.categoryId ?? undefined,
      status: course.status ?? CourseStatus.Draft,
      isRecommended: (course as any).isRecommended ?? false,
      ...patch,
      majorIds: (patch as any).majorIds ?? majorIds,
    } as unknown as CreateUpdateCourseDto;
  }

  getMajorName(id?: string | null): string {
    if (!id) {
      return '-';
    }
    return this.majors().find((m) => m.id === id)?.name || '-';
  }

  /** 是否公共课：无任何专业归属 */
  isPublicCourse(course: CourseDto): boolean {
    const ids = (course as any).majorIds as string[] | undefined;
    if (ids) {
      return ids.length === 0;
    }
    return !course.majorId;
  }

  /** 专业展示：主专业排第一用"、"连接；公共课返回空（模板渲染 tag） */
  getMajorDisplay(course: CourseDto): string {
    const names = (course as any).majorNames as string[] | undefined;
    if (names?.length) {
      return names.join('、');
    }
    const ids = (course as any).majorIds as string[] | undefined;
    if (ids?.length) {
      return ids.map((id) => this.getMajorName(id)).join('、');
    }
    return course.majorName || this.getMajorName(course.majorId);
  }

  deleteCourse(course: CourseDto) {
    this.modal.confirm({
      nzTitle: '确认删除',
      nzContent: `确定要删除课程「${course.title}」吗？此操作不可撤销。`,
      nzOkText: '删除',
      nzOkDanger: true,
      nzCancelText: '取消',
      nzOnOk: () => {
        if (!course.id) return;
        this.courseService.delete(course.id).subscribe({
          next: () => {
            this.message.success('课程已删除');
            this.loadCourses();
          },
          error: () => {
            this.message.error('删除失败');
          }
        });
      }
    });
  }

  handleModalCancel() {
    this.isModalVisible = false;
  }

  handleModalOk() {
    if (!this.formData.title?.trim()) {
      this.message.warning('请输入课程名称');
      return;
    }

    this.saving = true;
    const request = this.isEdit && this.editId
      ? this.courseService.update(this.editId, this.buildPayload())
      : this.courseService.create(this.buildPayload());

    request.subscribe({
      next: () => {
        this.saving = false;
        this.isModalVisible = false;
        this.message.success(this.isEdit ? '课程更新成功' : '课程创建成功');
        this.loadCourses();
      },
      error: () => {
        this.saving = false;
        this.message.error(this.isEdit ? '更新失败' : '创建失败');
      }
    });
  }

  /** 表格内快捷开关：是否推荐课程（学生端「推荐课程」只显示开启的） */
  toggleRecommend(course: CourseDto, checked: boolean) {
    if (!course.id) return;
    // 必须带上原有专业归属，否则局部更新会把副专业清空
    const body = this.payloadFromCourse(course, { isRecommended: checked });
    this.courseService.update(course.id, body).subscribe({
      next: () => {
        this.courses.update(list =>
          list.map(c => (c.id === course.id ? { ...c, isRecommended: checked } as CourseDto : c))
        );
        this.message.success(checked ? '已设为推荐课程' : '已取消推荐');
      },
      error: () => {
        this.message.error('推荐状态更新失败');
        this.loadCourses();
      },
    });
  }

  isRecommended(course: CourseDto): boolean {
    return !!(course as any).isRecommended;
  }

  /** 弹窗内推荐开关的代理属性（模板语法不支持 as 强转） */
  get formIsRecommended(): boolean {
    return !!((this.formData as any).isRecommended);
  }

  set formIsRecommended(v: boolean) {
    (this.formData as any).isRecommended = v;
  }
}
