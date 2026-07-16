import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject, signal } from '@angular/core';
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
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { OssUploadService } from '../../shared/oss-upload.service';
import {
  CreatePracticumAssessmentDto,
  CreatePracticumGuidanceRecordDto,
  CreateUpdatePracticumProjectDto,
  PracticumEnrollmentDto,
  PracticumProjectDto,
  PracticumProjectStatus,
  PracticumService,
  PracticumSubmissionDto,
  PracticumSubmissionStatus,
} from '../../practicum/practicum.service';
import { PracticumChatService } from '../../practicum/practicum-chat.service';
import type { PracticumAgentConfigDto } from '../../practicum/practicum-chat.service';

@Component({
  selector: 'app-practicum-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzSelectModule,
    NzSwitchModule,
    NzTableModule,
    NzTagModule,
    NzTooltipModule,
    NzIconModule,
    NzDrawerModule,
    NzUploadModule,
  ],
  templateUrl: './practicum-management.component.html',
  styleUrls: ['./practicum-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumManagementComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly pratChatService = inject(PracticumChatService);
  private readonly courseService = inject(CourseService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly projects = signal<PracticumProjectDto[]>([]);
  readonly enrollments = signal<PracticumEnrollmentDto[]>([]);
  readonly submissions = signal<PracticumSubmissionDto[]>([]);
  readonly courses = signal<CourseDto[]>([]);
  readonly statuses = PracticumProjectStatus;
  readonly submissionStatuses = PracticumSubmissionStatus;

  activeTab = 0;
  selectedProjectId: string | null = null;
  modalVisible = false;
  editingId: string | null = null;
  /** 基本信息 Tab 显示用：后端 detail 返回的关联课程名称（不是 ID）。 */
  selectedCourseTitle = '';

  // ===== OSS 上传状态 =====
  /** 封面图片上传进度 + 已上传文件列表（picture-card 模式）。 */
  coverUploading = false;
  coverFileList: NzUploadFile[] = [];
  /** 资料上传进度：key=material index, value=true 表示上传中。 */
  materialUploading: Record<number, boolean> = {};

  // ===== 右侧抽屉（统一承载"查看 / 新增 / 编辑"任务与资料） =====
  /** 抽屉中的资源类型：'task' | 'material' | null */
  readonly drawerKind = signal<'task' | 'material' | null>(null);
  /** 抽屉模式：'add' 新增 | 'edit' 编辑现有 */
  readonly drawerMode = signal<'add' | 'edit'>('add');
  /** 抽屉中编辑/查看的下标（新增时为 -1） */
  readonly drawerIndex = signal(-1);
  /** 抽屉是否可见 */
  readonly drawerVisible = signal(false);
  /** 保存中（抽屉底部"保存"按钮的 loading） */
  readonly drawerSaving = signal(false);
  /** 抽屉内文件上传中 */
  readonly drawerUploading = signal(false);

  /** 任务草稿 */
  readonly taskDraft = signal<{
    title: string;
    description: string;
    requirement: string;
    scoreWeight: number;
    sortOrder: number;
    dueTimeInput: string;
  } | null>(null);

  /** 资料草稿 */
  readonly materialDraft = signal<{
    title: string;
    description: string;
    materialType: number;
    resourceUrl: string;
    sortOrder: number;
  } | null>(null);

  /** Form for basic info, tasks, and materials (all in one DTO) */
  form: CreateUpdatePracticumProjectDto = this.freshForm();

  guidanceVisible = false;
  scoreVisible = false;
  guidanceTarget: PracticumSubmissionDto | null = null;
  scoreTarget: PracticumSubmissionDto | null = null;
  guidanceForm: CreatePracticumGuidanceRecordDto = this.emptyGuidance();
  scoreForm: CreatePracticumAssessmentDto = this.emptyScore();
  agentConfigForm: PracticumAgentConfigDto = {};

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

  /** 把后端 detail 填到 form + 设置 selectedCourseTitle（用于 Tab 0 显示课程名称）。 */
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
      startTime: detail.startTime,
      endTime: detail.endTime,
      maxScore: detail.maxScore,
      allowResubmission: detail.allowResubmission,
      tasks: (detail.tasks || []).map((t: any) => ({
        title: t.title, description: t.description || '', requirement: t.requirement || '',
        dueTime: t.dueTime, scoreWeight: t.scoreWeight, sortOrder: t.sortOrder,
      })),
      materials: (detail.materials || []).map((m: any) => ({
        taskId: m.taskId, title: m.title, description: m.description || '',
        materialType: m.materialType, resourceUrl: m.resourceUrl, sortOrder: m.sortOrder,
      })),
    };
    this.selectedCourseTitle = detail.courseTitle || '';
    // 同步封面 / 资料上传组件的显示状态（如果后端已有 URL，回显成"已上传"卡片）
    this.syncCoverFileList();
    this.materialUploading = {};
  }

  private emptyGuidance(): CreatePracticumGuidanceRecordDto {
    return { enrollmentId: '', taskId: undefined, content: '', isVisibleToStudent: true };
  }

  private emptyScore(): CreatePracticumAssessmentDto {
    return { submissionId: undefined, score: 0, gradeLevel: '', comment: '', rubricJson: '' };
  }

  private loadCourses(): void {
    this.courseService.getList({ skipCount: 0, maxResultCount: 200 } as any)
      .subscribe(r => { this.courses.set(r.items || []); this.cdr.markForCheck(); });
  }

  reload(): void {
    this.practicumService.getList({ skipCount: 0, maxResultCount: 100 })
      .subscribe(r => { this.projects.set(r.items || []); this.cdr.markForCheck(); });
  }

  // --- Modal create / edit -------------------------------------

  openCreate(): void {
    this.editingId = null;
    this.form = this.freshForm();
    this.modalVisible = true;
    this.cdr.markForCheck();
  }

  openEdit(p: PracticumProjectDto): void {
    this.editingId = p.id;
    this.form = this.freshForm();
    this.modalVisible = true;
    this.cdr.markForCheck();

    this.practicumService.getDetail(p.id).subscribe(detail => {
      this.applyDetailToForm(detail);
      this.selectedProjectId = p.id;
      this.activeTab = 1;
      this.modalVisible = false;
      this.loadEnrollmentsAndSubmissions(p.id);
      this.cdr.markForCheck();
    });
  }

  saveModal(): void {
    // P1-15：保存时同时提交任务（原先在 saveModal 里把 tasks/materials 强制清空，迫使用户"先保存基本信息，再到 Tab1 保存任务"——分两步走容易漏）。
    // 现在 modal 内的"基本信息 + 任务配置"是同一个表单，一次性提交。
    const body: CreateUpdatePracticumProjectDto = { ...this.form };
    const obs = this.editingId
      ? this.practicumService.update(this.editingId, body)
      : this.practicumService.create(body);

    obs.subscribe({
      next: r => {
        this.message.success('实训项目已保存');
        this.modalVisible = false;
        this.selectedProjectId = r.id;
        this.activeTab = 1;
        this.cdr.markForCheck();
        // Reload detail so tabs have tasks/materials data
        this.practicumService.getDetail(r.id).subscribe(detail => {
          this.applyDetailToForm(detail);
          this.reload();
          this.loadEnrollmentsAndSubmissions(r.id);
          this.cdr.markForCheck();
        });
      },
      error: () => this.message.error('保存失败'),
    });
  }

  /**
   * P1-15：保存按钮的启用条件——至少要有一条任务。
   * 没任务时点保存没意义，禁用 + tooltip 提示用户先加任务。
   */
  get canSaveModal(): boolean {
    return Array.isArray(this.form?.tasks) && this.form.tasks.length > 0;
  }

  get saveModalTooltip(): string {
    return this.canSaveModal ? '' : '请先添加至少一个任务（点击上方"添加任务"按钮）';
  }

  /** 从基本信息 Tab 直接打开编辑 modal（不再走 openEdit → 切到任务 Tab 的路径）。 */
  openEditFromTab0(): void {
    if (!this.selectedProjectId) return;
    this.editingId = this.selectedProjectId;
    this.modalVisible = true;
    this.cdr.markForCheck();
  }

  deleteProject(id: string): void {
    this.practicumService.delete(id).subscribe({
      next: () => {
        this.message.success('实训项目已删除');
        this.selectedProjectId = null;
        this.form = this.freshForm();
        this.activeTab = 0;
        this.enrollments.set([]);
        this.submissions.set([]);
        this.reload();
        this.cdr.markForCheck();
      },
      error: () => this.message.error('删除失败'),
    });
  }

  // --- Tab 1 / Tab 2：统一用右侧抽屉承载 ----

  /** 打开新增任务抽屉（直接是编辑态） */
  openAddTaskDrawer(): void {
    this.drawerKind.set('task');
    this.drawerMode.set('add');
    this.drawerIndex.set(-1);
    this.taskDraft.set({
      title: '',
      description: '',
      requirement: '',
      scoreWeight: 0,
      sortOrder: this.form.tasks.length + 1,
      dueTimeInput: '',
    });
    this.drawerVisible.set(true);
  }

  /** 打开编辑任务抽屉 */
  openEditTaskDrawer(i: number): void {
    const src = this.form.tasks[i];
    if (!src) return;
    this.drawerKind.set('task');
    this.drawerMode.set('edit');
    this.drawerIndex.set(i);
    this.taskDraft.set({
      title: src.title ?? '',
      description: src.description ?? '',
      requirement: src.requirement ?? '',
      scoreWeight: src.scoreWeight ?? 0,
      sortOrder: src.sortOrder ?? (i + 1),
      dueTimeInput: this.toDateTimeLocal(src.dueTime),
    });
    this.drawerVisible.set(true);
  }

  /** 打开新增资料抽屉 */
  openAddMaterialDrawer(): void {
    this.drawerKind.set('material');
    this.drawerMode.set('add');
    this.drawerIndex.set(-1);
    this.materialDraft.set({
      title: '',
      description: '',
      materialType: 0,
      resourceUrl: '',
      sortOrder: this.form.materials.length + 1,
    });
    this.drawerUploading.set(false);
    this.drawerVisible.set(true);
  }

  /** 打开编辑资料抽屉 */
  openEditMaterialDrawer(i: number): void {
    const src = this.form.materials[i];
    if (!src) return;
    this.drawerKind.set('material');
    this.drawerMode.set('edit');
    this.drawerIndex.set(i);
    this.materialDraft.set({
      title: src.title ?? '',
      description: src.description ?? '',
      materialType: src.materialType ?? 0,
      resourceUrl: src.resourceUrl ?? '',
      sortOrder: src.sortOrder ?? (i + 1),
    });
    this.drawerUploading.set(false);
    this.drawerVisible.set(true);
  }

  /** 关闭抽屉 */
  closeDrawer(): void {
    this.drawerVisible.set(false);
    // 延迟清空，让关闭动画播完
    setTimeout(() => {
      this.drawerKind.set(null);
      this.drawerIndex.set(-1);
      this.taskDraft.set(null);
      this.materialDraft.set(null);
      this.drawerUploading.set(false);
    }, 200);
  }

  /** 抽屉底部"保存"按钮：根据 kind 校验+写回 form.tasks / form.materials */
  saveDrawer(): void {
    const kind = this.drawerKind();
    if (kind === 'task') {
      this.saveTaskDraft();
    } else if (kind === 'material') {
      this.saveMaterialDraft();
    }
  }

  /** 抽屉底部"删除"按钮 */
  deleteFromDrawer(): void {
    const i = this.drawerIndex();
    const kind = this.drawerKind();
    if (i < 0) return;
    if (kind === 'task') this.removeTask(i);
    else if (kind === 'material') this.removeMaterial(i);
    this.closeDrawer();
  }

  // 兼容旧方法名（避免破坏 HTML 调用）
  addTask(): void { this.openAddTaskDrawer(); }
  addMaterial(): void { this.openAddMaterialDrawer(); }
  openAddTask(): void { this.openAddTaskDrawer(); }
  openAddMaterial(): void { this.openAddMaterialDrawer(); }
  openEditTask(i: number): void { this.openEditTaskDrawer(i); }
  openEditMaterial(i: number): void { this.openEditMaterialDrawer(i); }
  openTaskDrawer(i: number): void { this.openEditTaskDrawer(i); }
  openMaterialDrawer(i: number): void { this.openEditMaterialDrawer(i); }
  editFromDrawer(): void { /* no-op: 抽屉本身就是编辑态 */ }

  private saveTaskDraft(): void {
    const draft = this.taskDraft();
    if (!draft) return;

    const title = (draft.title || '').trim();
    if (!title) {
      this.message.warning('请填写任务名称');
      return;
    }

    const next = {
      title,
      description: (draft.description || '').trim(),
      requirement: (draft.requirement || '').trim(),
      scoreWeight: Number(draft.scoreWeight) || 0,
      sortOrder: draft.sortOrder ?? 1,
      dueTime: this.fromDateTimeLocal(draft.dueTimeInput),
    };
    if (this.drawerMode() === 'add') {
      this.form.tasks.push(next);
    } else {
      const i = this.drawerIndex();
      if (this.form.tasks[i]) this.form.tasks[i] = next;
    }
    this.form.tasks.forEach((t, idx) => t.sortOrder = idx + 1);
    this.cdr.markForCheck();
    this.closeDrawer();
    this.message.success(this.drawerMode() === 'add' ? '已新增任务' : '已更新任务');
  }

  private saveMaterialDraft(): void {
    const draft = this.materialDraft();
    if (!draft) return;

    const title = (draft.title || '').trim();
    if (!title) {
      this.message.warning('请填写资料名称');
      return;
    }
    const isUrlType = draft.materialType === 3 || draft.materialType === 4;
    const resourceUrl = (draft.resourceUrl || '').trim();
    if (isUrlType && !resourceUrl) {
      this.message.warning('请填写 URL');
      return;
    }
    if (!isUrlType && !resourceUrl) {
      this.message.warning('请上传资料文件');
      return;
    }

    const next = {
      title,
      description: (draft.description || '').trim(),
      materialType: draft.materialType,
      resourceUrl,
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
    this.closeDrawer();
    this.message.success(this.drawerMode() === 'add' ? '已新增资料' : '已更新资料');
  }

  /** ISO/字符串 → <input type="datetime-local"> 需要的 YYYY-MM-DDTHH:mm 格式 */
  private toDateTimeLocal(value: string | Date | undefined | null): string {
    if (!value) return '';
    const d = typeof value === 'string' ? new Date(value) : value;
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  /** datetime-local 字符串 → ISO（UTC）字符串，方便后端 DateTime 解析 */
  private fromDateTimeLocal(input: string | undefined | null): string | undefined {
    if (!input) return undefined;
    const d = new Date(input);
    if (isNaN(d.getTime())) return undefined;
    return d.toISOString();
  }

  removeTask(i: number): void {
    this.form.tasks.splice(i, 1);
    this.form.tasks.forEach((t, idx) => t.sortOrder = idx + 1);
    this.cdr.markForCheck();
  }

  removeMaterial(i: number): void {
    this.form.materials.splice(i, 1);
    this.form.materials.forEach((m, idx) => m.sortOrder = idx + 1);
    this.cdr.markForCheck();
  }

  /** 资料类型显示名称 */
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

  /** 资料类型 tag 颜色 */
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

  // --- Tab 4 ------------------------------------------------

  private loadEnrollmentsAndSubmissions(pid: string): void {
    this.practicumService.getEnrollmentList({ projectId: pid, skipCount: 0, maxResultCount: 200 })
      .subscribe(r => { this.enrollments.set(r.items || []); this.cdr.markForCheck(); });
    this.practicumService.getSubmissionList({ projectId: pid, skipCount: 0, maxResultCount: 200 })
      .subscribe(r => { this.submissions.set(r.items || []); this.cdr.markForCheck(); });
    this.pratChatService.getAgentConfig(pid).subscribe({
      next: c => { this.agentConfigForm = c; this.cdr.markForCheck(); },
      error: () => {},
    });
  }

  /**
   * 保存任务 / 资料后调用：拉最新 detail 刷新 form 内的 tasks / materials，
   * 同时 reload 列表（让表格的 taskCount / materialCount 同步）。
   * 不走 openEdit——openEdit 会切 activeTab、闪一下 modal、且不 reload。
   */
  private refreshDetail(): void {
    const pid = this.selectedProjectId;
    if (!pid) return;
    this.practicumService.getDetail(pid).subscribe(detail => {
      this.applyDetailToForm(detail);
      this.reload();
      this.cdr.markForCheck();
    });
  }

  openGuidance(item: PracticumSubmissionDto): void {
    this.guidanceTarget = item;
    this.guidanceForm = { enrollmentId: item.enrollmentId, taskId: item.taskId, content: '', isVisibleToStudent: true };
    this.guidanceVisible = true;
  }

  saveGuidance(): void {
    this.practicumService.addGuidance(this.guidanceForm).subscribe({
      next: () => { this.guidanceVisible = false; this.message.success('指导记录已保存'); },
      error: () => this.message.error('指导记录保存失败'),
    });
  }

  openScore(item: PracticumSubmissionDto): void {
    this.scoreTarget = item;
    this.scoreForm = { submissionId: item.id, score: item.score || 0, gradeLevel: '', comment: item.teacherFeedback || '', rubricJson: '' };
    this.scoreVisible = true;
  }

  saveScore(): void {
    if (!this.scoreTarget) { return; }
    this.practicumService.scoreEnrollment(this.scoreTarget.enrollmentId, this.scoreForm).subscribe({
      next: () => {
        this.scoreVisible = false;
        this.message.success('评分已保存');
        if (this.selectedProjectId) { this.loadEnrollmentsAndSubmissions(this.selectedProjectId); }
      },
      error: () => this.message.error('评分失败'),
    });
  }

  exportScores(): void {
    this.practicumService.exportAssessments(this.selectedProjectId || undefined).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a'); a.href = url;
        a.download = `实训成绩_${new Date().toISOString().slice(0, 10)}.xlsx`;
        a.click(); window.URL.revokeObjectURL(url);
      },
      error: () => this.message.error('导出失败'),
    });
  }

  statusLabel(s: PracticumProjectStatus): string {
    return ({ [PracticumProjectStatus.Draft]: '草稿', [PracticumProjectStatus.Published]: '已发布', [PracticumProjectStatus.Archived]: '已归档' } as Record<number, string>)[s] || '未知';
  }

  submissionLabel(s: PracticumSubmissionStatus): string {
    return ({ [PracticumSubmissionStatus.Submitted]: '已提交', [PracticumSubmissionStatus.Returned]: '已退回', [PracticumSubmissionStatus.Reviewed]: '已评阅' } as Record<number, string>)[s] || '未知';
  }

  // ===== OSS 上传 handlers =====

  /** 用 form.coverImageUrl 同步 nz-upload 卡片（已存在的封面直接显示）。 */
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

  /** 封面上传前的校验 + 触发 OSS 上传。返回 false 阻止 nz-upload 默认行为。 */
  beforeCoverUpload = (file: NzUploadFile): boolean => {
    const rawFile = file as any as File;
    const allowed = ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp'];
    if (!allowed.includes(rawFile.type)) {
      this.message.error('封面仅支持 JPG/PNG/GIF/WebP/BMP 格式');
      return false;
    }
    if (rawFile.size > 10 * 1024 * 1024) {
      this.message.error('封面大小不能超过 10MB');
      return false;
    }
    this.coverUploading = true;
    this.ossUploadService.uploadImage(rawFile).subscribe({
      next: (res) => {
        this.coverUploading = false;
        this.form.coverImageUrl = res.url;
        this.coverFileList = [{
          uid: res.objectKey,
          name: res.originalFileName,
          status: 'done',
          url: res.url,
        }];
        this.message.success('封面上传成功');
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.coverUploading = false;
        this.coverFileList = [];
        this.message.error('封面上传失败: ' + (err?.error?.error?.message || err?.message || '未知错误'));
        this.cdr.markForCheck();
      },
    });
    return false;
  };

  removeCover = (): boolean => {
    this.form.coverImageUrl = '';
    this.coverFileList = [];
    this.cdr.markForCheck();
    return true;
  };

  /**
   * 资料文件上传前的校验 + 触发 OSS 上传。
   * 上传成功后将 OSS 返回的 URL 写回对应 material.resourceUrl。
   */
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
          // 没填标题时，用文件名兜底
          if (!m.title) m.title = res.originalFileName;
        }
        this.materialUploading = { ...this.materialUploading, [index]: false };
        this.message.success(`资料上传成功：${res.originalFileName}`);
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.materialUploading = { ...this.materialUploading, [index]: false };
        this.message.error('资料上传失败: ' + (err?.error?.error?.message || err?.message || '未知错误'));
        this.cdr.markForCheck();
      },
    });
    return false;
  };

  /** 资料卡片清除已上传的文件（仅清空 resourceUrl，不删 OSS 对象）。 */
  removeMaterialFile = (index: number) => (): boolean => {
    const m = this.form.materials[index];
    if (m) m.resourceUrl = '';
    this.cdr.markForCheck();
    return true;
  };

  // ===== 抽屉内的资料上传（操作 materialDraft 而非 form.materials） =====

  /**
   * 抽屉内资料文件上传前的校验 + 触发 OSS 上传。
   * 上传成功后将 OSS 返回的 URL 写回 materialDraft.resourceUrl。
   */
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
          this.message.success(`资料上传成功：${res.originalFileName}`);
          this.cdr.markForCheck();
        },
        error: (err) => {
          this.drawerUploading.set(false);
          this.message.error('资料上传失败: ' + (err?.error?.error?.message || err?.message || '未知错误'));
          this.cdr.markForCheck();
        },
      });
      return false;
    };
  };

  /** 抽屉内清除已上传的文件（仅清空 draft.resourceUrl，不删 OSS 对象）。 */
  removeMaterialFileInDrawer = (): (() => boolean) => {
    return (): boolean => {
      const draft = this.materialDraft();
      if (draft) {
        this.materialDraft.set({ ...draft, resourceUrl: '' });
      }
      this.cdr.markForCheck();
      return true;
    };
  };

  // ─── Agent Config ───────────────────────────

  saveAgentConfig(): void {
    if (!this.selectedProjectId) return;
    this.pratChatService.updateAgentConfig(this.selectedProjectId, {
      agentName: this.agentConfigForm.agentName,
      agentPrompt: this.agentConfigForm.agentPrompt,
    }).subscribe({
      next: () => { this.message.success('智能体配置已保存'); },
      error: () => { this.message.error('保存智能体配置失败'); },
    });
  }
}
