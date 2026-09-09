import { Component, signal, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription, of } from 'rxjs';
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
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { CourseService } from '../../../proxy/courses/course.service';
import type { CreateUpdateCourseDto } from '../../../proxy/courses/dtos/models';
import { MajorService } from '../../../proxy/majors/major.service';
import type { MajorLookupDto } from '../../../proxy/majors/dtos/models';
import { OssUploadService, OssUploadResultDto } from '../../../shared/oss-upload.service';
import { FindMajorNamePipe } from '../../../shared/find-major-name.pipe';

/** 本地表单类型：proxy 重新生成前（abp generate-proxy -t ng）用交叉类型承载新增的多专业字段 */
type CourseCreateForm = CreateUpdateCourseDto & { majorIds?: string[] };

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
    NzUploadModule,
    FindMajorNamePipe,
  ],
  templateUrl: './course-create.component.html',
  styleUrls: ['./course-create.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CourseCreateComponent {
  private readonly courseService = inject(CourseService);
  private readonly majorService = inject(MajorService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);

  loading = signal(false);
  currentStep = signal(0);

  readonly majors = signal<MajorLookupDto[]>([]);

  // Cover upload state
  readonly coverUploading = signal(false);
  readonly coverFileList = signal<NzUploadFile[]>([]);

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

  formData = signal<CourseCreateForm>({
    title: '',
    description: '',
    coverImageUrl: '',
    majorId: undefined,
    majorIds: [],
    semester: '',
    credits: 3,
    semesterHours: 48,
    difficulty: 1,
    categoryId: undefined
  });

  updateField(field: string, value: any) {
    this.formData.update(data => ({ ...data, [field]: value }));
  }

  /** 多选专业变化时同步主专业：清空=公共课；主专业被移除则取第一个 */
  onMajorsChange(ids: string[]) {
    const list = ids ?? [];
    this.formData.update(data => ({
      ...data,
      majorIds: list,
      majorId: list.length === 0
        ? undefined
        : data.majorId && list.includes(data.majorId)
          ? data.majorId
          : list[0],
    }));
  }

  /** 专业展示文本：主专业排第一；空=公共课 */
  getMajorNamesText(): string {
    const ids = this.formData().majorIds ?? [];
    if (ids.length === 0) {
      return '公共课（所有专业可见）';
    }
    const names = ids
      .map(id => this.majors().find(m => m.id === id)?.name ?? '')
      .filter(n => !!n);
    return names.length ? names.join('、') : '未选择';
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

    this.coverUploading.set(true);
    return this.ossUploadService.uploadImage(file).subscribe({
      next: (res: OssUploadResultDto) => {
        this.updateField('coverImageUrl', res.url);
        this.message.success('封面上传成功');
        this.coverUploading.set(false);
        item.onSuccess?.(res, item.file);
      },
      error: () => {
        this.coverUploading.set(false);
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
      this.updateField('coverImageUrl', blobUrl);
    }
    return true;
  };

  /** 用户点击"移除"按钮：清空封面 */
  onCoverRemove(): void {
    this.updateField('coverImageUrl', '');
    this.coverFileList.set([]);
  }

  // ═══ Step navigation ═══

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

    const majorIds = this.formData().majorIds ?? [];
    const payload = {
      ...this.formData(),
      majorIds,
      majorId: majorIds.length ? (this.formData().majorId ?? majorIds[0]) : undefined,
    } as unknown as CreateUpdateCourseDto;

    this.loading.set(true);
    this.courseService.create(payload).subscribe({
      next: () => {
        this.loading.set(false);
        this.message.success('课程创建成功');
        this.router.navigate(['/learning/my-courses']);
      },
      error: () => {
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