import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzUploadModule } from 'ng-zorro-antd/upload';
import { CreateUpdateStudentResumeDto, EmploymentService, StudentResumeDto } from '../../employment/employment.service';
import { ClientCacheService } from '../../shared/cache/client-cache.service';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';
import { SectionBannerComponent } from '../shared/section-banner/section-banner.component';

interface ResumeFormState extends CreateUpdateStudentResumeDto {
  attachmentFileName?: string;
  hasAttachment?: boolean;
}

@Component({
  selector: 'app-student-my-resumes',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, NzIconModule, NzSpinModule, NzModalModule, NzButtonModule, NzInputModule, NzCheckboxModule, NzUploadModule, StudentHeroComponent, SectionBannerComponent],
  templateUrl: './student-my-resumes.component.html',
  styleUrls: ['./student-my-resumes.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyResumesComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly cache = inject(ClientCacheService);
  private readonly sanitizer = inject(DomSanitizer);

  readonly items = signal<StudentResumeDto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly uploading = signal(false);

  /** Hero 区数据总览 */
  readonly heroStats = computed(() => {
    const items = this.items();
    const def = items.find(x => x.isDefault);
    return [
      { label: '简历总数', value: items.length, suffix: '份', icon: 'file-text', color: '#087f7a' },
      { label: '已设默认', value: def ? 1 : 0, suffix: '份', icon: 'star', color: '#f59e0b' },
    ];
  });

  modalVisible = false;
  editingId: string | null = null;
  form: ResumeFormState = this.createEmptyForm();
  attachmentFile?: File;

  /** 附件预览弹窗：当前选中的含附件简历 */
  readonly previewItem = signal<StudentResumeDto | null>(null);
  readonly previewVisible = signal(false);
  /** 预览 iframe 疑似渲染了错误体（转换服务不可用）时给出兑底提示 */
  readonly previewFrameHint = signal(false);

  /** 预览类型：图片直接渲染；PDF/Word 走后端 resume-preview（与资源预览同一 Gotenberg 转 PDF 链路） */
  readonly previewKind = computed<'image' | 'document' | null>(() => {
    const url = this.previewItem()?.attachmentUrl;
    if (!url) return null;
    return this.getFileIcon(url) === 'file-image' ? 'image' : 'document';
  });

  /** 文档预览地址（后端中转：PDF 内联返回，Word 转 PDF 后返回；URL 上已附 access_token，iframe 可用） */
  readonly safePreviewUrl = computed<SafeResourceUrl | null>(() => {
    const url = this.previewItem()?.attachmentUrl;
    if (!url || this.previewKind() !== 'document') return null;
    return this.sanitizer.bypassSecurityTrustResourceUrl(this.employmentService.getResumePreviewUrl(url));
  });

  /** 新窗口打开地址：文档走后端预览地址，图片走原地址 */
  previewOpenUrl(): string {
    const url = this.previewItem()?.attachmentUrl || '';
    if (!url) return '';
    return this.previewKind() === 'document' ? this.employmentService.getResumePreviewUrl(url) : url;
  }

  /** 下载原件地址：直接用 OSS 原始地址（带 token 无必要，OSS 公网地址可直接下载） */
  previewDownloadUrl(): string {
    return this.previewItem()?.attachmentUrl || '';
  }

  /**
   * 右侧元数据：与简历卡片同口径，最多展示有值的字段。
   * 编辑界面从 form 构造预览项时同样走这里（openFormAttachmentPreview）。
   */
  readonly previewMeta = computed<Array<{ label: string; value: string }>>(() => {
    const item = this.previewItem();
    if (!item) return [];
    const rows: Array<{ label: string; value: string }> = [];
    const push = (label: string, value?: string | null) => {
      const v = (value || '').trim();
      if (v) rows.push({ label, value: v });
    };
    push('姓名', item.fullName);
    push('联系电话', item.phoneNumber);
    push('电子邮箱', item.email);
    push('学校', item.schoolName);
    push('专业', item.major);
    push('年级', item.grade);
    push('班级', item.className);
    push('学号', item.studentNumber);
    push('技能特长', item.skills);
    push('自我评价', item.summary);
    push('最近使用', item.lastUsedAt ? item.lastUsedAt.slice(0, 10) : '');
    push('更新时间', item.creationTime ? item.creationTime.slice(0, 10) : '');
    return rows;
  });

  /**
   * 编辑表单内「查看附件」：把表单当前内容构造为预览项，
   * 复用查看界面同一套双栏预览（左侧预览窗口 + 右侧简历元数据）。
   * 编辑中若选了新文件还未上传，仍以已保存的 attachmentUrl 为准。
   */
  openFormAttachmentPreview(): void {
    const url = this.form.attachmentUrl;
    if (!url) return;
    const nowIso = new Date().toISOString();
    this.previewFrameHint.set(false);
    this.previewItem.set({
      id: this.editingId || 'form-preview',
      studentId: '',
      title: this.form.title || '简历预览',
      fullName: this.form.fullName || '',
      phoneNumber: this.form.phoneNumber,
      email: this.form.email,
      schoolName: this.form.schoolName,
      major: this.form.major,
      grade: this.form.grade,
      className: this.form.className,
      studentNumber: this.form.studentNumber,
      summary: this.form.summary,
      skills: this.form.skills,
      educationExperience: this.form.educationExperience,
      internshipExperience: this.form.internshipExperience,
      projectExperience: this.form.projectExperience,
      certificateText: this.form.certificateText,
      attachmentUrl: url,
      isDefault: !!this.form.isDefault,
      versionNo: 0,
      creationTime: nowIso,
    });
    this.previewVisible.set(true);
  }

  /** 附件文件名：URL 尾段解析，解析不出时回退标题 */
  previewFileName(): string {
    const item = this.previewItem();
    if (!item?.attachmentUrl) return '简历附件';
    const name = this.extractFileNameFromUrl(item.attachmentUrl);
    return name || `${item.title || '简历'}-附件`;
  }

  openPreview(item: StudentResumeDto): void {
    if (!item.attachmentUrl) return;
    this.previewFrameHint.set(false);
    this.previewItem.set(item);
    this.previewVisible.set(true);
  }

  closePreview(): void {
    this.previewVisible.set(false);
    this.previewFrameHint.set(false);
    this.previewItem.set(null);
  }

  /**
   * 预览 iframe 加载完成：探测是否渲染了后端的 JSON 错误体。
   * iframe 同源（/api/app/resume-preview），错误时后端返回 {"message":"..."}，
   * PDF 由浏览器内置 viewer 渲染（body 非 JSON），据此区分给出兑底提示。
   */
  onPreviewFrameLoad(): void {
    this.previewFrameHint.set(false);
    try {
      const frame = document.querySelector<HTMLIFrameElement>('.resume-preview__viewer .preview-frame');
      const text = (frame?.contentDocument?.body?.innerText || '').trim();
      if (text.startsWith('{') && text.includes('message')) {
        this.previewFrameHint.set(true);
      }
    } catch {
      // 跨源或安全限制下不可读，忽略（不阻断预览）
    }
  }

  ngOnInit(): void {
    this.reload();
  }

  createEmptyForm(): ResumeFormState {
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
      isDefault: true,
      attachmentFileName: '',
      hasAttachment: false,
    };
  }

  reload(force = false): void {
    this.loading.set(true);
    const loader = () => this.employmentService.getMyResumeList();
    const request$ = force
      ? this.cache.reload<StudentResumeDto[]>('student.employment', 'resumes', loader)
      : this.cache.load<StudentResumeDto[]>('student.employment', 'resumes', loader);
    request$.subscribe({
      next: items => {
        this.items.set(items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载简历失败');
      },
    });
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.createEmptyForm();
    this.attachmentFile = undefined;
    this.modalVisible = true;
  }

  openEdit(item: StudentResumeDto): void {
    this.editingId = item.id;
    this.form = {
      title: item.title,
      fullName: item.fullName,
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
      isDefault: item.isDefault,
      attachmentFileName: item.attachmentUrl ? this.extractFileNameFromUrl(item.attachmentUrl) : '',
      hasAttachment: !!item.attachmentUrl,
    };
    this.attachmentFile = undefined;
    this.modalVisible = true;
  }

  closeModal(): void {
    this.modalVisible = false;
    this.editingId = null;
    this.attachmentFile = undefined;
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const maxSize = 10 * 1024 * 1024;
    if (file.size > maxSize) {
      this.message.error('文件大小不能超过 10MB');
      input.value = '';
      return;
    }
    const allowed = /\.(pdf|doc|docx|rtf|jpg|jpeg|png|webp)$/i;
    if (!allowed.test(file.name)) {
      this.message.error('仅支持 PDF / Word / 图片 格式');
      input.value = '';
      return;
    }

    this.attachmentFile = file;
    this.form.attachmentFileName = file.name;
    this.form.hasAttachment = true;
  }

  removeAttachment(): void {
    this.attachmentFile = undefined;
    this.form.attachmentUrl = '';
    this.form.attachmentFileName = '';
    this.form.hasAttachment = false;
  }

  save(): void {
    if (!this.form.title.trim()) {
      this.message.warning('请填写简历标题');
      return;
    }
    if (!this.form.fullName.trim()) {
      this.message.warning('请填写姓名');
      return;
    }

    this.saving.set(true);

    const proceed = (attachmentUrl?: string) => {
      const payload: CreateUpdateStudentResumeDto = {
        title: this.form.title.trim(),
        fullName: this.form.fullName.trim(),
        phoneNumber: this.form.phoneNumber?.trim() || undefined,
        email: this.form.email?.trim() || undefined,
        schoolName: this.form.schoolName?.trim() || undefined,
        major: this.form.major?.trim() || undefined,
        grade: this.form.grade?.trim() || undefined,
        className: this.form.className?.trim() || undefined,
        studentNumber: this.form.studentNumber?.trim() || undefined,
        summary: this.form.summary?.trim() || undefined,
        skills: this.form.skills?.trim() || undefined,
        educationExperience: this.form.educationExperience?.trim() || undefined,
        internshipExperience: this.form.internshipExperience?.trim() || undefined,
        projectExperience: this.form.projectExperience?.trim() || undefined,
        certificateText: this.form.certificateText?.trim() || undefined,
        attachmentUrl: attachmentUrl || this.form.attachmentUrl?.trim() || undefined,
        isDefault: this.form.isDefault,
      };

      const request = this.editingId
        ? this.employmentService.updateResume(this.editingId, payload)
        : this.employmentService.createResume(payload);

      request.subscribe({
        next: () => {
          this.saving.set(false);
          this.message.success('简历已保存');
          this.closeModal();
          this.reload(true);
        },
        error: () => {
          this.saving.set(false);
          this.message.error('保存失败');
        },
      });
    };

    if (this.attachmentFile) {
      this.uploading.set(true);
      this.employmentService.uploadResumeAttachment(this.attachmentFile).subscribe({
        next: result => {
          this.uploading.set(false);
          proceed(result.url);
        },
        error: () => {
          this.uploading.set(false);
          this.message.error('附件上传失败');
        },
      });
    } else {
      proceed();
    }
  }

  setDefault(item: StudentResumeDto): void {
    if (item.isDefault) return;
    this.employmentService.setDefaultResume(item.id).subscribe({
      next: () => {
        this.message.success('已设为默认简历');
        this.reload(true);
      },
      error: () => this.message.error('设置失败'),
    });
  }

  delete(item: StudentResumeDto): void {
    this.modal.confirm({
      nzTitle: '确认删除该简历？',
      nzContent: `简历【${item.title}】删除后无法恢复。`,
      nzOkText: '确认删除',
      nzOkType: 'primary',
      nzOkDanger: true,
      nzCancelText: '取消',
      nzOnOk: () => {
        return new Promise<void>(resolve => {
          this.employmentService.deleteResume(item.id).subscribe({
            next: () => {
              this.message.success('简历已删除');
              this.reload(true);
              resolve();
            },
            error: () => {
              this.message.error('删除失败');
              resolve();
            },
          });
        });
      },
    });
  }

  /** 表单当前是否为新选择的文件（尚未保存） */
  isNewAttachment(): boolean {
    return !!this.attachmentFile;
  }

  /** 表单附件显示名：新文件显示文件名，已有附件显示原文件名或占位 */
  attachmentDisplayName(): string {
    if (this.form.attachmentFileName) return this.form.attachmentFileName;
    return this.isNewAttachment() ? '已选择文件' : '已上传附件';
  }

  /** 表单附件图标：新文件按文件名，已有附件按线上地址判断 */
  attachmentIcon(): string {
    const src = this.isNewAttachment() ? this.form.attachmentFileName : this.form.attachmentUrl;
    return this.getFileIcon(src || '');
  }

  getFileIcon(attachmentUrl?: string): string {
    if (!attachmentUrl) return 'file';
    const ext = attachmentUrl.toLowerCase();
    if (ext.endsWith('.pdf')) return 'file-pdf';
    if (ext.endsWith('.doc') || ext.endsWith('.docx') || ext.endsWith('.rtf')) return 'file-word';
    if (ext.endsWith('.jpg') || ext.endsWith('.jpeg') || ext.endsWith('.png') || ext.endsWith('.webp')) return 'file-image';
    return 'file';
  }

  private extractFileNameFromUrl(url: string): string {
    if (!url) return '';
    const idx = url.lastIndexOf('/');
    if (idx < 0) return url;
    const name = url.substring(idx + 1);
    // 去掉可能的时间戳前缀 / 32 位哈希
    return name.length > 20 ? '' : name;
  }

  /** 联系方式行文本：手机 · 邮箱，双无时返回“暂无” */
  contactText(item: StudentResumeDto): string {
    const p = (item.phoneNumber || '').trim();
    const e = (item.email || '').trim();
    if (p && e) return `${p} · ${e}`;
    return p || e || '暂无';
  }

  getSkillTags(skillString?: string): string[] {
    if (!skillString) return [];
    return skillString
      .split(/[,,;;\n]/)
      .map(s => s.trim())
      .filter(Boolean)
      .slice(0, 6);
  }

  getDefaultName(): string {
    const def = this.items().find(x => x.isDefault);
    return def?.title || '（未设置）';
  }
}
