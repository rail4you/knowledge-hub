import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { OssUploadService } from '../../shared/oss-upload.service';
import {
  CreateUpdateMicroMajorDto,
  MicroMajorDto,
  MicroMajorEnrollmentDto,
  MicroMajorService,
  MicroMajorStatus,
} from '../../micro-majors/micro-major.service';

@Component({
  selector: 'app-micro-major-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzSwitchModule,
    NzTableModule,
    NzUploadModule,
  ],
  templateUrl: './micro-major-management.component.html',
  styleUrls: ['./micro-major-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MicroMajorManagementComponent implements OnInit {
  private readonly microMajorService = inject(MicroMajorService);
  private readonly courseService = inject(CourseService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<MicroMajorDto[]>([]);
  readonly enrollments = signal<MicroMajorEnrollmentDto[]>([]);
  readonly courses = signal<CourseDto[]>([]);
  readonly statuses = MicroMajorStatus;

  modalVisible = false;
  editingId: string | null = null;
  selectedCourseIds: string[] = [];
  form: CreateUpdateMicroMajorDto = this.createEmptyForm();

  // Cover upload state
  coverUploading = false;
  coverFileList: NzUploadFile[] = [];

  ngOnInit(): void {
    this.loadCourses();
    this.reload();
  }

  createEmptyForm(): CreateUpdateMicroMajorDto {
    return {
      title: '',
      summary: '',
      description: '',
      coverImageUrl: '',
      industryField: '',
      collaborationUnit: '',
      status: MicroMajorStatus.Draft,
      requiredCompletionRate: 100,
      isCertificateEnabled: true,
      courses: [],
    };
  }

  loadCourses(): void {
    this.courseService.getList({
      maxResultCount: 200,
      skipCount: 0,
    } as any).subscribe({
      next: result => this.courses.set(result.items || []),
    });
  }

  reload(): void {
    this.microMajorService.getList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe({
      next: result => this.items.set(result.items || []),
    });

    this.microMajorService.getEnrollmentList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe({
      next: result => this.enrollments.set(result.items || []),
    });
  }

  openCreate(): void {
    this.editingId = null;
    this.selectedCourseIds = [];
    this.form = this.createEmptyForm();
    this.coverFileList = [];
    this.modalVisible = true;
  }

  openEdit(item: MicroMajorDto): void {
    this.editingId = item.id;
    this.form = {
      title: item.title,
      summary: item.summary || '',
      description: item.description || '',
      coverImageUrl: item.coverImageUrl || '',
      industryField: item.industryField || '',
      collaborationUnit: item.collaborationUnit || '',
      status: item.status,
      requiredCompletionRate: item.requiredCompletionRate,
      isCertificateEnabled: item.isCertificateEnabled,
      courses: [],
    };

    this.microMajorService.getDetail(item.id).subscribe({
      next: detail => {
        this.selectedCourseIds = detail.courses
          .sort((a, b) => a.sortOrder - b.sortOrder)
          .map(x => x.courseId);

        // Set cover file list for preview
        if (item.coverImageUrl) {
          this.coverFileList = [{
            uid: '-1',
            name: 'cover.jpg',
            status: 'done',
            url: item.coverImageUrl,
          }];
        } else {
          this.coverFileList = [];
        }

        this.modalVisible = true;
      },
    });
  }

  beforeCoverUpload = (rawFile: NzUploadFile): boolean => {
    // Validate type
    const allowedTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp'];
    if (!allowedTypes.includes(rawFile.type!)) {
      this.message.error('仅支持上传 JPG、PNG、GIF、WebP、BMP 格式的图片');
      return false;
    }
    // Validate size (10MB)
    if (rawFile.size! > 10 * 1024 * 1024) {
      this.message.error('图片大小不能超过 10MB');
      return false;
    }

    // Upload to OSS
    this.coverUploading = true;
    this.ossUploadService.uploadImage(rawFile as unknown as File).subscribe({
      next: (result) => {
        this.coverUploading = false;
        this.form.coverImageUrl = result.url;
        this.coverFileList = [{
          uid: result.objectKey,
          name: result.originalFileName,
          status: 'done',
          url: result.url,
        }];
        this.message.success('封面上传成功');
      },
      error: (err) => {
        this.coverUploading = false;
        this.coverFileList = [];
        this.message.error('封面上传失败: ' + (err?.error?.error?.message || err?.message || '未知错误'));
      },
    });
    return false; // prevent default upload behavior
  };

  removeCover = (): boolean => {
    this.form.coverImageUrl = '';
    this.coverFileList = [];
    return true;
  };

  save(): void {
    this.form.courses = this.selectedCourseIds.map((courseId, index) => ({
      courseId,
      sortOrder: index + 1,
      isCore: true,
    }));

    const request = this.editingId
      ? this.microMajorService.update(this.editingId, this.form)
      : this.microMajorService.create(this.form);

    request.subscribe({
      next: () => {
        this.modalVisible = false;
        this.message.success('微专业已保存');
        this.reload();
      },
      error: () => {
        this.message.error('微专业保存失败');
      },
    });
  }

  delete(id: string): void {
    this.microMajorService.delete(id).subscribe({
      next: () => {
        this.message.success('微专业已删除');
        this.reload();
      },
      error: () => {
        this.message.error('删除失败');
      },
    });
  }

  issueCertificate(enrollmentId: string): void {
    this.microMajorService.issueCertificate(enrollmentId).subscribe({
      next: () => {
        this.message.success('证书已发放');
        this.reload();
      },
      error: () => {
        this.message.error('发证失败，可能尚未满足完成条件');
      },
    });
  }

  getStatusLabel(status: MicroMajorStatus): string {
    const labels: Record<number, string> = {
      [MicroMajorStatus.Draft]: '草稿',
      [MicroMajorStatus.Published]: '已发布',
      [MicroMajorStatus.Archived]: '已归档',
    };
    return labels[status] || '未知';
  }
}
