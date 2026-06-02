import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { CreateUpdateStudentResumeDto, EmploymentService, StudentResumeDto } from '../../employment/employment.service';

interface ResumeFormState extends CreateUpdateStudentResumeDto {
  attachmentFileName?: string;
  hasAttachment?: boolean;
}

@Component({
  selector: 'app-student-my-resumes',
  standalone: true,
  imports: [CommonModule, DatePipe, DecimalPipe, FormsModule, NzIconModule, NzSpinModule, NzModalModule],
  templateUrl: './student-my-resumes.component.html',
  styleUrls: ['./student-my-resumes.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyResumesComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly items = signal<StudentResumeDto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly uploading = signal(false);

  modalVisible = false;
  editingId: string | null = null;
  form: ResumeFormState = this.createEmptyForm();
  attachmentFile?: File;

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

  reload(): void {
    this.loading.set(true);
    this.employmentService.getMyResumeList().subscribe({
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
          this.reload();
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
        this.reload();
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
              this.reload();
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
