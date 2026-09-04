import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzUploadFile, NzUploadModule } from 'ng-zorro-antd/upload';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { OssUploadService } from '../../shared/oss-upload.service';
import {
  CreateUpdatePracticumProjectDto,
  PracticumProjectDto,
  PracticumProjectStatus,
  PracticumService,
} from '../../practicum/practicum.service';

@Component({
  selector: 'app-practicum-management',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    NzButtonModule, NzCardModule, NzEmptyModule, NzInputModule, NzModalModule, NzSelectModule,
    NzSpinModule, NzSwitchModule, NzTableModule, NzTagModule, NzTooltipModule, NzIconModule, NzUploadModule,
  ],
  templateUrl: './practicum-management.component.html',
  styleUrls: ['./practicum-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumManagementComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly courseService = inject(CourseService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly projects = signal<PracticumProjectDto[]>([]);
  readonly courses = signal<CourseDto[]>([]);
  readonly statuses = PracticumProjectStatus;

  selectedProjectId: string | null = null;
  /** 是否显示内联编辑表单（新建或选中项目后显示）。 */
  formVisible = false;
  editingId: string | null = null;
  /** 编辑表单显示用:后端 detail 返回的关联课程名称(不是 ID)。 */
  selectedCourseTitle = '';

  // ===== OSS 上传状态 =====
  coverUploading = false;
  coverFileList: NzUploadFile[] = [];
  materialUploading: Record<number, boolean> = {};
  @ViewChild('coverFileInput') coverFileInputRef?: ElementRef<HTMLInputElement>;

  // ===== 资料抽屉(统一承载"查看 / 新增 / 编辑"资料) =====
  readonly drawerMode = signal<'add' | 'edit'>('add');
  readonly drawerIndex = signal(-1);
  readonly drawerVisible = signal(false);
  readonly drawerSaving = signal(false);
  readonly drawerUploading = signal(false);

  readonly materialDraft = signal<{
    title: string;
    description: string;
    materialType: number;
    resourceUrl: string;
    sortOrder: number;
  } | null>(null);

  /** 基本信息 + 资料（同一 DTO，保存时一并提交）。 */
  form: CreateUpdatePracticumProjectDto = this.freshForm();

  ngOnInit(): void {
    this.loadCourses();
    this.reload();
  }

  private freshForm(): CreateUpdatePracticumProjectDto {
    this.selectedCourseTitle = '';
    this.coverFileList = [];
    this.materialUploading = {};
    return { title: '', summary: '', description: '', coverImageUrl: '', courseId: undefined,
      major: '', className: '', status: PracticumProjectStatus.Draft,
      startTime: undefined, endTime: undefined, maxScore: 100, allowResubmission: true,
      tasks: [], materials: [] };
  }

  /** 把后端 detail 填到 form + 设置 selectedCourseTitle。 */
  private applyDetailToForm(detail: any): void {
    this.form = {
      title: detail.title,
      summary: detail.summary || '',
      description: detail.description || '',
      coverImageUrl: detail.coverImageUrl || '',
      courseId: detail.courseId,
      major: detail.major || '',
      className: detail.className || '',
      status: detail.status,
      startTime: this.toDateTimeLocal(detail.startTime),
      endTime: this.toDateTimeLocal(detail.endTime),
      maxScore: detail.maxScore,
      allowResubmission: detail.allowResubmission,
      tasks: detail.tasks || [],
      materials: (detail.materials || []).map((m: any) => ({
        taskId: m.taskId, title: m.title, description: m.description || '',
        materialType: m.materialType, resourceUrl: m.resourceUrl, sortOrder: m.sortOrder,
      })),
    };
    this.selectedCourseTitle = detail.courseTitle || '';
    this.syncCoverFileList();
    this.materialUploading = {};
    this.cdr.markForCheck();
  }

  private loadCourses(): void {
    this.courseService.getList({ skipCount: 0, maxResultCount: 200 } as any)
      .subscribe(r => { this.courses.set(r.items || []); this.cdr.markForCheck(); });
  }

  reload(): void {
    this.practicumService.getList({ skipCount: 0, maxResultCount: 100 })
      .subscribe(r => { this.projects.set(r.items || []); this.cdr.markForCheck(); });
  }

  // ─── 新建 / 编辑（内联表单） ───────────────────

  openCreate(): void {
    this.editingId = null;
    this.selectedProjectId = null;
    this.form = this.freshForm();
    this.formVisible = true;
    this.cdr.markForCheck();
  }

  openEdit(p: PracticumProjectDto): void {
    this.editingId = p.id;
    this.selectedProjectId = p.id;
    this.form = this.freshForm();
    this.formVisible = true;
    this.cdr.markForCheck();

    this.practicumService.getDetail(p.id).subscribe(detail => {
      this.applyDetailToForm(detail);
      this.cdr.markForCheck();
    });
  }

  saveForm(): void {
    if (this.form.startTime && this.form.endTime && this.form.startTime > this.form.endTime) {
      this.message.error('开始时间不能晚于结束时间');
      return;
    }
    const body = this.prepareFormPayload();
    const obs = this.editingId
      ? this.practicumService.update(this.editingId, body)
      : this.practicumService.create(body);

    obs.subscribe({
      next: r => {
        this.message.success('实训项目已保存');
        this.selectedProjectId = r.id;
        this.editingId = r.id;
        this.formVisible = true;
        this.cdr.markForCheck();
        this.practicumService.getDetail(r.id).subscribe(detail => {
          this.applyDetailToForm(detail);
          this.reload();
          this.cdr.markForCheck();
        });
      },
      error: () => this.message.error('保存失败'),
    });
  }

  deleteProject(id: string): void {
    this.practicumService.delete(id).subscribe({
      next: () => {
        this.message.success('实训项目已删除');
        this.selectedProjectId = null;
        this.editingId = null;
        this.formVisible = false;
        this.form = this.freshForm();
        this.reload();
        this.cdr.markForCheck();
      },
      error: () => this.message.error('删除失败'),
    });
  }

  // ─── 资料抽屉 ─────────────────────────────────────

  openAddMaterialDrawer(): void {
    this.drawerMode.set('add');
    this.drawerIndex.set(-1);
    this.materialDraft.set({
      title: '', description: '', materialType: 0, resourceUrl: '',
      sortOrder: this.form.materials.length + 1,
    });
    this.drawerUploading.set(false);
    this.drawerVisible.set(true);
  }

  openEditMaterialDrawer(i: number): void {
    const src = this.form.materials[i];
    if (!src) return;
    this.drawerMode.set('edit');
    this.drawerIndex.set(i);
    this.materialDraft.set({
      title: src.title ?? '', description: src.description ?? '',
      materialType: src.materialType ?? 0, resourceUrl: src.resourceUrl ?? '',
      sortOrder: src.sortOrder ?? (i + 1),
    });
    this.drawerUploading.set(false);
    this.drawerVisible.set(true);
  }

  closeDrawer(): void {
    this.drawerVisible.set(false);
    setTimeout(() => {
      this.drawerIndex.set(-1);
      this.materialDraft.set(null);
      this.drawerUploading.set(false);
    }, 200);
  }

  saveDrawer(): void {
    if (!this.validateMaterialDraft()) return;
    this.applyMaterialDraft();
    // 内联表单模式：资料先写入本地表单，由页面"保存"按钮统一提交
    this.message.success(this.drawerMode() === 'add' ? '已添加，点击"保存"生效' : '已更新，点击"保存"生效');
    this.closeDrawer();
  }

  deleteFromDrawer(): void {
    const i = this.drawerIndex();
    if (i < 0) return;
    this.removeMaterial(i);
    this.message.success('已删除，点击"保存"生效');
    this.closeDrawer();
  }

  addMaterial(): void { this.openAddMaterialDrawer(); }
  openAddMaterial(): void { this.openAddMaterialDrawer(); }
  openEditMaterial(i: number): void { this.openEditMaterialDrawer(i); }
  openMaterialDrawer(i: number): void { this.openEditMaterialDrawer(i); }
  editFromDrawer(): void { /* no-op: 抽屉本身就是编辑态 */ }

  private validateMaterialDraft(): boolean {
    const draft = this.materialDraft();
    if (!draft) return false;
    if (!(draft.title || '').trim()) {
      this.message.warning('请填写资料名称');
      return false;
    }
    const isUrlType = draft.materialType === 3;
    const resourceUrl = (draft.resourceUrl || '').trim();
    if (isUrlType && !resourceUrl) {
      this.message.warning('请填写 URL');
      return false;
    }
    if (!isUrlType && !resourceUrl) {
      this.message.warning('请上传资料文件');
      return false;
    }
    return true;
  }

  private applyMaterialDraft(): void {
    const draft = this.materialDraft()!;
    const next = {
      title: (draft.title || '').trim(),
      description: (draft.description || '').trim(),
      materialType: draft.materialType,
      resourceUrl: (draft.resourceUrl || '').trim(),
      sortOrder: draft.sortOrder ?? 1,
    };
    if (this.drawerMode() === 'add') {
      this.form.materials.push(next);
    } else {
      const i = this.drawerIndex();
      if (this.form.materials[i]) this.form.materials[i] = next;
    }
    this.form.materials.forEach((m, idx) => m.sortOrder = idx + 1);
    this.cdr.markForCheck();
  }

  private toDateTimeLocal(value: string | Date | undefined | null): string {
    if (!value) return '';
    const d = typeof value === 'string' ? new Date(value) : value;
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  private fromDateTimeLocal(input: string | undefined | null): string | undefined {
    if (!input) return undefined;
    const d = new Date(input);
    if (isNaN(d.getTime())) return undefined;
    return d.toISOString();
  }

  private prepareFormPayload() {
    const payload = { ...this.form };
    payload.tasks = (payload.tasks || []).map(t => ({
      ...t,
      dueTime: t.dueTime ? this.fromDateTimeLocal(t.dueTime) : undefined,
    }));
    // datetime-local 输入 → 后端 ISO(UTC)
    payload.startTime = this.fromDateTimeLocal(this.form.startTime);
    payload.endTime = this.fromDateTimeLocal(this.form.endTime);
    return payload;
  }

  removeMaterial(i: number): void {
    this.form.materials.splice(i, 1);
    this.form.materials.forEach((m, idx) => m.sortOrder = idx + 1);
    this.cdr.markForCheck();
  }

  materialTypeLabel(t: number | undefined): string {
    switch (t) {
      case 0: return '指南';
      case 1: return '案例';
      case 2: return '模板';
      case 3: return '链接';
      case 4: return '仿真';
      default: return '其他';
    }
  }

  materialTypeColor(t: number | undefined): string {
    switch (t) {
      case 0: return 'blue';
      case 1: return 'purple';
      case 2: return 'cyan';
      case 3: return 'green';
      case 4: return 'magenta';
      default: return 'default';
    }
  }

  statusLabel(s: PracticumProjectStatus): string {
    return ({ [PracticumProjectStatus.Draft]: '草稿', [PracticumProjectStatus.Published]: '已发布', [PracticumProjectStatus.Archived]: '已归档' } as Record<number, string>)[s] || '未知';
  }

  // ===== OSS 上传 handlers =====

  private syncCoverFileList(): void {
    const url = this.form?.coverImageUrl;
    if (url) {
      this.coverFileList = [{
        uid: 'cover-existing',
        name: this.fileNameFromUrl(url),
        status: 'done',
        url,
      }];
    } else {
      this.coverFileList = [];
    }
  }

  private fileNameFromUrl(url: string): string {
    try {
      const u = new URL(url);
      const last = u.pathname.split('/').pop() || 'cover';
      return decodeURIComponent(last);
    } catch {
      return 'cover';
    }
  }

  /** 点击“上传/替换封面”按钮：触发隐藏的文件选择。 */
  triggerCoverUpload(): void {
    this.coverFileInputRef?.nativeElement.click();
  }

  /** 选中本地文件后实际走 OSS 上传。 */
  onCoverFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    const allowed = ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp'];
    if (!allowed.includes(file.type)) {
      this.message.error('封面仅支持 JPG/PNG/GIF/WebP/BMP 格式');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      this.message.error('封面大小不能超过 10MB');
      return;
    }
    this.coverUploading = true;
    this.cdr.markForCheck();
    this.ossUploadService.uploadImage(file).subscribe({
      next: (res) => {
        this.coverUploading = false;
        this.form.coverImageUrl = res.url;
        this.coverFileList = [{ uid: res.objectKey, name: res.originalFileName, status: 'done', url: res.url }];
        this.message.success('封面上传成功');
        this.cdr.markForCheck();
      },
      error: () => {
        this.coverUploading = false;
        this.message.error('封面上传失败');
        this.cdr.markForCheck();
      },
    });
  }

  removeCoverClick(): void {
    this.form.coverImageUrl = '';
    this.coverFileList = [];
    this.cdr.markForCheck();
  }

  beforeMaterialUpload = (index: number) => (file: NzUploadFile): boolean => {
    const rawFile = file as any as File;
    if (rawFile.size > 50 * 1024 * 1024) {
      this.message.error('资料文件不能超过 50MB');
      return false;
    }
    this.materialUploading = { ...this.materialUploading, [index]: true };
    this.cdr.markForCheck();
    this.ossUploadService.uploadFile(rawFile).subscribe({
      next: (res) => {
        const m = this.form.materials[index];
        if (m) {
          m.resourceUrl = res.url;
          if (!m.title) m.title = res.originalFileName;
        }
        this.materialUploading = { ...this.materialUploading, [index]: false };
        this.message.success(`资料上传成功:${res.originalFileName}`);
        this.cdr.markForCheck();
      },
      error: () => {
        this.materialUploading = { ...this.materialUploading, [index]: false };
        this.message.error('资料上传失败');
        this.cdr.markForCheck();
      },
    });
    return false;
  };

  removeMaterialFile = (index: number) => (): boolean => {
    const m = this.form.materials[index];
    if (m) m.resourceUrl = '';
    this.cdr.markForCheck();
    return true;
  };

  beforeMaterialUploadInDrawer = (): ((file: NzUploadFile) => boolean) => {
    return (file: NzUploadFile): boolean => {
      const rawFile = file as any as File;
      if (rawFile.size > 50 * 1024 * 1024) {
        this.message.error('资料文件不能超过 50MB');
        return false;
      }
      this.drawerUploading.set(true);
      this.cdr.markForCheck();
      this.ossUploadService.uploadFile(rawFile).subscribe({
        next: (res) => {
          const draft = this.materialDraft();
          if (draft) {
            this.materialDraft.set({
              ...draft,
              resourceUrl: res.url,
              title: draft.title?.trim() ? draft.title : (res.originalFileName ?? draft.title),
            });
          }
          this.drawerUploading.set(false);
          this.message.success(`资料上传成功:${res.originalFileName}`);
          this.cdr.markForCheck();
        },
        error: () => {
          this.drawerUploading.set(false);
          this.message.error('资料上传失败');
          this.cdr.markForCheck();
        },
      });
      return false;
    };
  };

  removeMaterialFileInDrawer = (): (() => boolean) => {
    return (): boolean => {
      const draft = this.materialDraft();
      if (draft) this.materialDraft.set({ ...draft, resourceUrl: '' });
      this.cdr.markForCheck();
      return true;
    };
  };
}
