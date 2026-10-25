import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { EmploymentService } from '../../proxy/employment/employment.service';
import type { CreateUpdateStudentResumeDto, StudentResumeDto } from '../../proxy/employment/dtos/models';
import { StudentSkeletonComponent } from '../shared/student-skeleton/student-skeleton.component';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import { StudentErrorComponent } from '../shared/student-error/student-error.component';
import { StudentEmploymentFilesService } from './student-employment-files.service';
import { ResumeUploadService } from '../../proxy/controllers/resume-upload.service';
import { splitTagList } from './employment-labels';

/** 简历表单模型 */
interface ResumeForm {
  title: string;
  fullName: string;
  phoneNumber: string;
  email: string;
  schoolName: string;
  major: string;
  grade: string;
  className: string;
  studentNumber: string;
  summary: string;
  skills: string;
  educationExperience: string;
  internshipExperience: string;
  projectExperience: string;
  certificateText: string;
  attachmentUrl: string;
  isDefault: boolean;
}

/**
 * 简历完整度的计分字段（与后端 StudentResume 的可填字段一一对应）。
 * 完整度 = 已填写字段数 / 字段总数 × 100%，不写死任何数值。
 */
const COMPLETENESS_FIELDS: { key: keyof ResumeForm; label: string }[] = [
  { key: 'title', label: '简历标题' },
  { key: 'fullName', label: '姓名' },
  { key: 'phoneNumber', label: '联系电话' },
  { key: 'email', label: '电子邮箱' },
  { key: 'schoolName', label: '学校' },
  { key: 'major', label: '专业 / 求职方向' },
  { key: 'grade', label: '年级' },
  { key: 'className', label: '班级' },
  { key: 'studentNumber', label: '学号' },
  { key: 'summary', label: '自我评价' },
  { key: 'skills', label: '技能特长' },
  { key: 'educationExperience', label: '教育经历' },
  { key: 'internshipExperience', label: '实习经历' },
  { key: 'projectExperience', label: '项目经历' },
  { key: 'certificateText', label: '证书获奖' },
  { key: 'attachmentUrl', label: '简历附件' },
];

/**
 * 学生端 · 我的简历（对齐原型 `#resume-center`，并合并 `#career` 的简历编辑）。
 *
 * 原型的 `#career`（单份简历 + 固定 70% 完整度）与 `#resume-center`（多版本简历）
 * 是两套并行实现，本页合并为一套：**沿用后台既有的 StudentResume 模型**
 * （一行一份简历，带 VersionNo），不改动后端。
 *
 * 数据来源（全部真实接口）：
 *   · 简历列表 GET    /api/app/employment/my-resume-list
 *   · 新建 / 编辑     POST /api/app/employment/resume、PUT /api/app/employment/{id}/resume
 *   · 设为默认        POST /api/app/employment/{id}/set-default-resume
 *   · 删除            DELETE /api/app/employment/{id}/resume
 *   · 附件上传        POST /api/app/resume-upload（独立 Controller，走生成代理 ResumeUploadService）
 */
@Component({
  selector: 'app-student-my-resumes',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    FormsModule,
    NzIconModule,
    NzModalModule,
    NzButtonModule,
    NzInputModule,
    NzCheckboxModule,
    StudentSkeletonComponent,
    StudentEmptyComponent,
    StudentErrorComponent,
  ],
  templateUrl: './student-my-resumes.component.html',
  styleUrls: ['./student-my-resumes.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyResumesComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly filesService = inject(StudentEmploymentFilesService);
  /** 附件上传走生成代理（独立 Controller 已在代理里） */
  private readonly resumeUploadService = inject(ResumeUploadService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly items = signal<StudentResumeDto[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly saving = signal(false);
  readonly uploading = signal(false);

  /** 新建 / 编辑弹窗 */
  readonly modalVisible = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly form = signal<ResumeForm>(this.emptyForm());

  /** 待上传的新附件 */
  readonly attachmentFile = signal<File | null>(null);

  /** 详情 / 附件预览 */
  readonly previewItem = signal<StudentResumeDto | null>(null);
  readonly previewVisible = signal(false);

  /** 预览类型：图片直接渲染；PDF / Word 走后端 resume-preview 中转 */
  readonly previewKind = computed<'image' | 'document' | null>(() => {
    const url = this.previewItem()?.attachmentUrl;
    if (!url) return null;
    return this.fileIcon(url) === 'file-image' ? 'image' : 'document';
  });

  /** 文档预览地址 */
  readonly safePreviewUrl = computed<SafeResourceUrl | null>(() => {
    const url = this.previewItem()?.attachmentUrl;
    if (!url || this.previewKind() !== 'document') return null;
    return this.sanitizer.bypassSecurityTrustResourceUrl(this.filesService.resumePreviewUrl(url));
  });

  /** 预览里的简历字段明细 */
  readonly previewRows = computed<{ label: string; value: string }[]>(() => {
    const item = this.previewItem();
    if (!item) return [];
    const rows: { label: string; value: string }[] = [];
    const push = (label: string, value?: string | null) => {
      const v = (value || '').trim();
      if (v) rows.push({ label, value: v });
    };
    push('姓名', item.fullName);
    push('联系电话', item.phoneNumber);
    push('电子邮箱', item.email);
    push('学校', item.schoolName);
    push('专业 / 求职方向', item.major);
    push('年级', item.grade);
    push('班级', item.className);
    push('学号', item.studentNumber);
    push('技能特长', item.skills);
    push('自我评价', item.summary);
    push('教育经历', item.educationExperience);
    push('实习经历', item.internshipExperience);
    push('项目经历', item.projectExperience);
    push('证书获奖', item.certificateText);
    push('最近使用', this.toText(item.lastUsedAt));
    push('更新时间', this.toText(item.creationTime));
    return rows;
  });

  /** 页头数据总览（数字全部来自真实数据） */
  readonly stats = computed(() => {
    const list = this.items();
    const defaultCount = list.filter(r => r.isDefault).length;
    const avg = list.length
      ? Math.round(list.reduce((sum, r) => sum + this.completeness(r), 0) / list.length)
      : 0;
    return [
      { label: '简历版本', value: list.length, hint: '可分别用于不同岗位' },
      { label: '默认版本', value: defaultCount, hint: '投递时优先选中' },
      { label: '平均完整度', value: avg, suffix: '%', hint: '按已填写字段计算' },
    ];
  });

  ngOnInit(): void {
    this.reload();
  }

  /** 加载简历列表 */
  reload(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.employmentService.getMyResumeList().subscribe({
      next: list => {
        this.items.set(list || []);
        this.loading.set(false);
      },
      error: () => {
        this.items.set([]);
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  /** 空表单 */
  private emptyForm(): ResumeForm {
    return {
      title: '',
      fullName: '',
      phoneNumber: '',
      email: '',
      schoolName: '',
      major: '',
      grade: '',
      className: '',
      studentNumber: '',
      summary: '',
      skills: '',
      educationExperience: '',
      internshipExperience: '',
      projectExperience: '',
      certificateText: '',
      attachmentUrl: '',
      isDefault: false,
    };
  }

  /** 更新表单字段 */
  patchForm(patch: Partial<ResumeForm>): void {
    this.form.update(f => ({ ...f, ...patch }));
  }

  /** 打开新建 */
  openCreate(): void {
    this.editingId.set(null);
    this.form.set(this.emptyForm());
    this.attachmentFile.set(null);
    this.modalVisible.set(true);
  }

  /** 打开编辑 */
  openEdit(item: StudentResumeDto): void {
    this.editingId.set(item.id ?? null);
    this.form.set({
      title: item.title || '',
      fullName: item.fullName || '',
      phoneNumber: item.phoneNumber || '',
      email: item.email || '',
      schoolName: item.schoolName || '',
      major: item.major || '',
      grade: item.grade || '',
      className: item.className || '',
      studentNumber: item.studentNumber || '',
      summary: item.summary || '',
      skills: item.skills || '',
      educationExperience: item.educationExperience || '',
      internshipExperience: item.internshipExperience || '',
      projectExperience: item.projectExperience || '',
      certificateText: item.certificateText || '',
      attachmentUrl: item.attachmentUrl || '',
      isDefault: !!item.isDefault,
    });
    this.attachmentFile.set(null);
    this.modalVisible.set(true);
  }

  closeModal(): void {
    this.modalVisible.set(false);
    this.editingId.set(null);
    this.attachmentFile.set(null);
  }

  /** 选择附件 */
  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (file.size > 10 * 1024 * 1024) {
      this.message.error('文件大小不能超过 10MB');
      input.value = '';
      return;
    }
    if (!/\.(pdf|doc|docx|rtf|jpg|jpeg|png|webp)$/i.test(file.name)) {
      this.message.error('仅支持 PDF / Word / 图片格式');
      input.value = '';
      return;
    }
    this.attachmentFile.set(file);
  }

  /** 移除附件 */
  removeAttachment(): void {
    this.attachmentFile.set(null);
    this.patchForm({ attachmentUrl: '' });
  }

  /** 保存（新建或编辑） */
  save(): void {
    const form = this.form();
    if (!form.title.trim()) {
      this.message.warning('请填写简历标题');
      return;
    }
    if (!form.fullName.trim()) {
      this.message.warning('请填写姓名');
      return;
    }

    const payload: CreateUpdateStudentResumeDto = {
      title: form.title.trim(),
      fullName: form.fullName.trim(),
      phoneNumber: form.phoneNumber.trim() || undefined,
      email: form.email.trim() || undefined,
      schoolName: form.schoolName.trim() || undefined,
      major: form.major.trim() || undefined,
      grade: form.grade.trim() || undefined,
      className: form.className.trim() || undefined,
      studentNumber: form.studentNumber.trim() || undefined,
      summary: form.summary.trim() || undefined,
      skills: form.skills.trim() || undefined,
      educationExperience: form.educationExperience.trim() || undefined,
      internshipExperience: form.internshipExperience.trim() || undefined,
      projectExperience: form.projectExperience.trim() || undefined,
      certificateText: form.certificateText.trim() || undefined,
      attachmentUrl: form.attachmentUrl.trim() || undefined,
      isDefault: form.isDefault,
    };

    const editingId = this.editingId();
    const save = (attachmentUrl?: string) => {
      this.saving.set(true);
      const body = attachmentUrl ? { ...payload, attachmentUrl } : payload;
      const request = editingId
        ? this.employmentService.updateResume(editingId, body)
        : this.employmentService.createResume(body);

      request.subscribe({
        next: () => {
          this.saving.set(false);
          this.message.success('简历已保存');
          this.closeModal();
          this.reload();
        },
        error: err => {
          this.saving.set(false);
          this.message.error(this.serverMessage(err, '保存失败，请稍后重试'));
        },
      });
    };

    const file = this.attachmentFile();
    if (file) {
      this.uploading.set(true);
      this.resumeUploadService.upload(file).subscribe({
        next: result => {
          this.uploading.set(false);
          save(result.url);
        },
        error: () => {
          this.uploading.set(false);
          this.message.error('附件上传失败，请重试');
        },
      });
    } else {
      save();
    }
  }

  /** 设为默认版本 */
  setDefault(item: StudentResumeDto): void {
    if (item.isDefault || !item.id) return;
    this.employmentService.setDefaultResume(item.id).subscribe({
      next: () => {
        this.message.success('已设为默认简历');
        this.reload();
      },
      error: err => this.message.error(this.serverMessage(err, '设置失败，请稍后重试')),
    });
  }

  /** 删除版本 */
  delete(item: StudentResumeDto): void {
    if (!item.id) return;
    this.modal.confirm({
      nzTitle: '确认删除该简历版本？',
      nzContent: `简历【${item.title || '未命名'}】删除后无法恢复。`,
      nzOkText: '确认删除',
      nzOkType: 'primary',
      nzOkDanger: true,
      nzCancelText: '取消',
      nzOnOk: () =>
        new Promise<void>(resolve => {
          this.employmentService.deleteResume(item.id).subscribe({
            next: () => {
              this.message.success('简历已删除');
              this.reload();
              resolve();
            },
            error: () => {
              this.message.error('删除失败，请稍后重试');
              resolve();
            },
          });
        }),
    });
  }

  /** 打开预览 */
  openPreview(item: StudentResumeDto): void {
    this.previewItem.set(item);
    this.previewVisible.set(true);
  }

  closePreview(): void {
    this.previewVisible.set(false);
    this.previewItem.set(null);
  }

  /** 新窗口打开附件 */
  previewOpenUrl(): string {
    const url = this.previewItem()?.attachmentUrl || '';
    if (!url) return '';
    return this.previewKind() === 'document' ? this.filesService.resumePreviewUrl(url) : url;
  }

  /** 下载原件地址 */
  previewDownloadUrl(): string {
    return this.previewItem()?.attachmentUrl || '';
  }

  /** 附件文件名 */
  previewFileName(): string {
    const url = this.previewItem()?.attachmentUrl || '';
    if (!url) return '简历附件';
    const name = url.substring(url.lastIndexOf('/') + 1);
    return name && name.length <= 20 ? name : `${this.previewItem()?.title || '简历'}-附件`;
  }

  /** 文件图标 */
  fileIcon(url?: string | null): string {
    if (!url) return 'file';
    const ext = url.toLowerCase();
    if (ext.endsWith('.pdf')) return 'file-pdf';
    if (ext.endsWith('.doc') || ext.endsWith('.docx') || ext.endsWith('.rtf')) return 'file-word';
    if (ext.endsWith('.jpg') || ext.endsWith('.jpeg') || ext.endsWith('.png') || ext.endsWith('.webp'))
      return 'file-image';
    return 'file';
  }

  /** 技能标签 */
  skillTags(item: StudentResumeDto): string[] {
    return splitTagList(item.skills).slice(0, 6);
  }

  /**
   * 简历完整度（%）：按实际填写字段计算，不使用任何固定数值。
   */
  completeness(item: StudentResumeDto): number {
    const filled = COMPLETENESS_FIELDS.filter(f => !!this.fieldValue(item, f.key)).length;
    return Math.round((filled / COMPLETENESS_FIELDS.length) * 100);
  }

  /** 已填写字段数 / 总字段数 */
  completenessDetail(item: StudentResumeDto): string {
    const filled = COMPLETENESS_FIELDS.filter(f => !!this.fieldValue(item, f.key)).length;
    return `${filled}/${COMPLETENESS_FIELDS.length} 项已填写`;
  }

  /** 读取简历上的字符串字段 */
  private fieldValue(item: StudentResumeDto, key: keyof ResumeForm): string {
    const value = item[key];
    return typeof value === 'string' ? value.trim() : '';
  }

  /** 后端时间字段（string | Date）统一转成可读文本 */
  private toText(value?: string | Date | null): string {
    if (!value) return '';
    const date = value instanceof Date ? value : new Date(value);
    if (isNaN(date.getTime())) return '';
    return date.toISOString().slice(0, 10);
  }

  /** 求职方向：后台没有独立字段，用专业 + 年级组合展示 */
  targetText(item: StudentResumeDto): string {
    const parts = [item.major, item.grade].filter(p => !!p?.trim());
    return parts.length ? parts.join(' · ') : '未填写求职方向';
  }

  /** 抽取后端返回的业务错误信息 */
  private serverMessage(err: any, fallback: string): string {
    const raw: string = err?.error?.error?.message || err?.error?.message || err?.message || '';
    const text = (raw || '').trim();
    if (!text) return fallback;
    if (text.length <= 120 && /[\u4e00-\u9fa5]/.test(text)) return text;
    return fallback;
  }
}
