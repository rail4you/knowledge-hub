import { ChangeDetectionStrategy, ChangeDetectorRef, Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzUploadFile, NzUploadModule } from 'ng-zorro-antd/upload';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { OssUploadService } from '../../shared/oss-upload.service';
import {
  CreateUpdatePracticumMaterialDto,
  CreateUpdatePracticumProjectDto,
  CreateUpdatePracticumTaskDto,
  PracticumProjectDto,
  PracticumProjectStatus,
  PracticumService,
} from '../../practicum/practicum.service';

type MaterialDraft = {
  title: string;
  description: string;
  materialType: number;
  resourceUrl: string;
  sortOrder: number;
  /** 资料所属项目（在新建 / 编辑表单里选择）。 */
  projectId: string;
};

/** 资料 Tab 平铺行：资料 + 其所属项目。 */
type MaterialRow = {
  projectId: string;
  projectTitle: string;
  material: CreateUpdatePracticumMaterialDto;
};

@Component({
  selector: 'app-practicum-management',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    NzButtonModule, NzCardModule, NzEmptyModule, NzInputModule, NzModalModule, NzSelectModule,
    NzSpinModule, NzSwitchModule, NzTableModule, NzTabsModule, NzTagModule, NzTooltipModule, NzIconModule, NzUploadModule,
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
  private readonly modal = inject(NzModalService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly projects = signal<PracticumProjectDto[]>([]);
  readonly courses = signal<CourseDto[]>([]);
  readonly statuses = PracticumProjectStatus;

  /** 0 = 实训管理，1 = 实训资料 */
  activeTab = 0;

  // ===== 实训管理：搜索 + 分页 =====
  filter = '';
  statusFilter: PracticumProjectStatus | null = null;
  pageIndex = 1;
  pageSize = 10;
  total = 0;
  listLoading = false;

  selectedProjectId: string | null = null;

  // ===== 项目表单（弹窗，新建 / 编辑，仅基础信息） =====
  formVisible = false;
  formSaving = false;
  editingId: string | null = null;
  /** 编辑表单显示用:后端 detail 返回的关联课程名称(不是 ID)。 */
  selectedCourseTitle = '';
  /** 编辑时暂存原始任务 / 资料，保存时原样回传，避免被清空。 */
  private originalTasks: CreateUpdatePracticumTaskDto[] = [];
  private originalMaterials: CreateUpdatePracticumMaterialDto[] = [];

  // ===== OSS 上传状态 =====
  coverUploading = false;
  coverFileList: NzUploadFile[] = [];
  @ViewChild('coverFileInput') coverFileInputRef?: ElementRef<HTMLInputElement>;

  // ===== 实训资料 Tab：全部资料平铺，归属项目在新建 / 编辑表单里选择 =====
  /** 供表单选择的已有项目（id + title）。 */
  materialProjects: { id: string; title: string }[] = [];
  /** 各项目的 detail 缓存：保存时用原始字段回填，避免时间漂移。 */
  private readonly projectBaseMap = new Map<string, { title: string; raw: any; materials: CreateUpdatePracticumMaterialDto[] }>();
  readonly allMaterialRows = signal<MaterialRow[]>([]);
  materialFilter = '';
  materialLoading = false;
  materialsLoaded = false;

  // ===== 资料抽屉(统一承载"查看 / 新增 / 编辑"资料) =====
  readonly drawerMode = signal<'add' | 'edit'>('add');
  readonly drawerIndex = signal(-1);
  readonly drawerVisible = signal(false);
  readonly drawerSaving = signal(false);
  readonly drawerUploading = signal(false);

  readonly materialDraft = signal<MaterialDraft | null>(null);

  /** 基本信息（资料与任务不在此表单维护）。 */
  form: CreateUpdatePracticumProjectDto = this.freshForm();

  ngOnInit(): void {
    this.loadCourses();
    this.reload(true);
  }

  private freshForm(): CreateUpdatePracticumProjectDto {
    this.selectedCourseTitle = '';
    this.coverFileList = [];
    this.originalTasks = [];
    this.originalMaterials = [];
    return { title: '', summary: '', description: '', coverImageUrl: '', courseId: undefined,
      major: '', className: '', status: PracticumProjectStatus.Draft,
      startTime: undefined, endTime: undefined, maxScore: 100, allowResubmission: true,
      tasks: [], materials: [] };
  }

  /** 把后端 detail 填到 form + 缓存原始任务/资料以便保存时回传。 */
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
      tasks: [],
      materials: [],
    };
    this.originalTasks = (detail.tasks || []).map((t: any) => ({
      title: t.title,
      description: t.description || '',
      requirement: t.requirement || '',
      dueTime: t.dueTime ? this.toDateTimeLocal(t.dueTime) : undefined,
      scoreWeight: t.scoreWeight,
      sortOrder: t.sortOrder,
    }));
    this.originalMaterials = (detail.materials || []).map((m: any) => ({
      taskId: m.taskId, title: m.title, description: m.description || '',
      materialType: m.materialType, resourceUrl: m.resourceUrl, sortOrder: m.sortOrder,
    }));
    this.selectedCourseTitle = detail.courseTitle || '';
    this.syncCoverFileList();
    this.cdr.markForCheck();
  }

  private loadCourses(): void {
    this.courseService.getList({ skipCount: 0, maxResultCount: 200 } as any)
      .subscribe(r => { this.courses.set(r.items || []); this.cdr.markForCheck(); });
  }

  // ─── 列表：搜索 + 分页 ───────────────────

  reload(resetPage = false): void {
    if (resetPage) this.pageIndex = 1;
    this.listLoading = true;
    this.cdr.markForCheck();
    const keyword = (this.filter || '').trim();
    this.practicumService.getList({
      filter: keyword || undefined,
      status: this.statusFilter ?? undefined,
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize,
    } as any).subscribe({
      next: r => {
        this.projects.set(r.items || []);
        this.total = r.totalCount ?? (r.items || []).length;
        this.listLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.listLoading = false;
        this.message.error('加载实训项目失败');
        this.cdr.markForCheck();
      },
    });
  }

  onSearch(): void {
    this.reload(true);
  }

  onReset(): void {
    this.filter = '';
    this.statusFilter = null;
    this.reload(true);
  }

  onPageIndexChange(index: number): void {
    this.pageIndex = index;
    this.reload();
  }

  onPageSizeChange(size: number): void {
    this.pageSize = size;
    this.reload(true);
  }

  onTabChange(index: number): void {
    this.activeTab = index;
    if (index === 1 && !this.materialsLoaded) this.loadAllMaterials();
    this.cdr.markForCheck();
  }

  /** 从资料 Tab 跳回管理 Tab 并定位到指定项目。 */
  gotoManageTab(projectId?: string): void {
    if (projectId) this.selectedProjectId = projectId;
    this.onTabChange(0);
  }

  // ─── 新建 / 编辑（弹窗表单，仅基础信息） ───────────────────

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

    this.practicumService.getDetail(p.id).subscribe({
      next: detail => this.applyDetailToForm(detail),
      error: () => this.message.error('加载项目详情失败'),
    });
  }

  closeForm(): void {
    this.formVisible = false;
    this.formSaving = false;
    this.cdr.markForCheck();
  }

  saveForm(): void {
    if (!(this.form.title || '').trim()) {
      this.message.warning('请填写项目名称');
      return;
    }
    if (this.form.startTime && this.form.endTime && this.form.startTime > this.form.endTime) {
      this.message.error('开始时间不能晚于结束时间');
      return;
    }
    this.formSaving = true;
    this.cdr.markForCheck();
    // 编辑时把原始任务 / 资料原样回传，避免弹窗保存清空它们；
    // 新建时任务与资料为空，后续去「实训资料」Tab 关联。
    const tasks = (this.originalTasks || []).map(t => ({
      ...t,
      dueTime: t.dueTime ? this.fromDateTimeLocal(t.dueTime) : undefined,
    }));
    const materials = [...(this.originalMaterials || [])];
    const body = {
      ...this.prepareFormPayload(),
      title: (this.form.title || '').trim(),
      tasks,
      materials,
    };
    const obs = this.editingId
      ? this.practicumService.update(this.editingId, body)
      : this.practicumService.create(body);

    obs.subscribe({
      next: r => {
        this.formSaving = false;
        this.message.success('实训项目已保存');
        this.selectedProjectId = r.id;
        this.editingId = r.id;
        this.formVisible = false;
        this.reload();
        // 资料 Tab 已加载过则同步刷新（项目改名 / 增删会影响资料表）
        if (this.materialsLoaded) this.loadAllMaterials();
        this.cdr.markForCheck();
      },
      error: () => {
        this.formSaving = false;
        this.message.error('保存失败');
        this.cdr.markForCheck();
      },
    });
  }

  confirmDeleteProject(id: string): void {
    this.modal.confirm({
      nzTitle: '确认删除该实训项目？',
      nzContent: '删除后不可恢复，已有学生参与的项目不允许删除。',
      nzOkText: '删除',
      nzOkDanger: true,
      nzCancelText: '取消',
      nzOnOk: () => this.deleteProject(id),
    });
  }

  deleteProject(id: string): void {
    this.practicumService.delete(id).subscribe({
      next: () => {
        this.message.success('实训项目已删除');
        if (this.selectedProjectId === id) {
          this.selectedProjectId = null;
          this.editingId = null;
        }
        if (this.materialsLoaded) this.loadAllMaterials();
        this.reload();
        this.cdr.markForCheck();
      },
      error: () => this.message.error('删除失败（可能已有学生参与）'),
    });
  }

  // ─── 实训资料 Tab：全部资料平铺 + 即时持久化 ───────────────────

  loadAllMaterials(): void {
    this.materialLoading = true;
    this.cdr.markForCheck();
    this.practicumService.getList({ skipCount: 0, maxResultCount: 200 } as any).subscribe({
      next: list => {
        const items = list.items || [];
        this.materialProjects = items.map(p => ({ id: p.id, title: p.title }));
        if (items.length === 0) {
          this.projectBaseMap.clear();
          this.rebuildMaterialRows();
          this.materialLoading = false;
          this.materialsLoaded = true;
          this.cdr.markForCheck();
          return;
        }
        forkJoin(items.map(p => this.practicumService.getDetail(p.id).pipe(catchError(() => of(null))))).subscribe({
          next: details => {
            this.projectBaseMap.clear();
            details.forEach((d: any, idx: number) => {
              if (!d) return;
              const mats: CreateUpdatePracticumMaterialDto[] = (d.materials || []).map((m: any) => ({
                taskId: m.taskId, title: m.title, description: m.description || '',
                materialType: m.materialType, resourceUrl: m.resourceUrl, sortOrder: m.sortOrder,
              }));
              mats.forEach((m, i) => m.sortOrder = i + 1);
              this.projectBaseMap.set(items[idx].id, { title: d.title, raw: d, materials: mats });
            });
            this.rebuildMaterialRows();
            this.materialLoading = false;
            this.materialsLoaded = true;
            this.cdr.markForCheck();
          },
          error: () => {
            this.materialLoading = false;
            this.message.error('加载项目资料失败');
            this.cdr.markForCheck();
          },
        });
      },
      error: () => {
        this.materialLoading = false;
        this.message.error('加载实训项目失败');
        this.cdr.markForCheck();
      },
    });
  }

  private rebuildMaterialRows(): void {
    const rows: MaterialRow[] = [];
    this.projectBaseMap.forEach((v, k) => {
      v.materials.forEach(m => rows.push({ projectId: k, projectTitle: v.title, material: m }));
    });
    this.allMaterialRows.set(rows);
  }

  filteredAllMaterials(): MaterialRow[] {
    const keyword = (this.materialFilter || '').trim().toLowerCase();
    const rows = this.allMaterialRows();
    if (!keyword) return rows;
    return rows.filter(r =>
      (r.material.title || '').toLowerCase().includes(keyword) ||
      (r.material.description || '').toLowerCase().includes(keyword) ||
      (r.projectTitle || '').toLowerCase().includes(keyword));
  }

  /** 把某项目的当前资料数组持久化（其余字段用缓存 detail 回填）。 */
  private persistMaterials(projectId: string) {
    const entry = this.projectBaseMap.get(projectId)!;
    const base = entry.raw;
    entry.materials.forEach((m, idx) => m.sortOrder = idx + 1);
    const body: CreateUpdatePracticumProjectDto = {
      title: base.title,
      summary: base.summary || '',
      description: base.description || '',
      coverImageUrl: base.coverImageUrl || '',
      courseId: base.courseId,
      major: base.major || '',
      className: base.className || '',
      status: base.status,
      startTime: base.startTime,
      endTime: base.endTime,
      maxScore: base.maxScore,
      allowResubmission: base.allowResubmission,
      tasks: (base.tasks || []).map((t: any) => ({
        title: t.title,
        description: t.description || '',
        requirement: t.requirement || '',
        dueTime: t.dueTime,
        scoreWeight: t.scoreWeight,
        sortOrder: t.sortOrder,
      })),
      materials: entry.materials.map((m, idx) => ({ ...m, sortOrder: idx + 1 })),
    };
    return this.practicumService.update(projectId, body);
  }

  private afterMaterialsPersisted(projectIds: string[], detailByProject: Map<string, any>): void {
    projectIds.forEach(pid => {
      const entry = this.projectBaseMap.get(pid);
      const detail = detailByProject.get(pid);
      if (entry && detail) {
        entry.raw = detail;
        entry.title = detail.title;
      }
    });
    this.rebuildMaterialRows();
    this.reload();
    this.cdr.markForCheck();
  }

  deleteMaterialRow(row: MaterialRow): void {
    const entry = this.projectBaseMap.get(row.projectId);
    if (!entry) return;
    const idx = entry.materials.indexOf(row.material);
    if (idx < 0) return;
    const [removed] = entry.materials.splice(idx, 1);
    entry.materials.forEach((m, i) => m.sortOrder = i + 1);
    this.rebuildMaterialRows();
    this.persistMaterials(row.projectId).subscribe({
      next: detail => {
        this.message.success('资料已删除');
        this.afterMaterialsPersisted([row.projectId], new Map([[row.projectId, detail]]));
      },
      error: () => {
        // 回滚
        entry.materials.splice(idx, 0, removed);
        entry.materials.forEach((m, i) => m.sortOrder = i + 1);
        this.rebuildMaterialRows();
        this.message.error('删除失败');
        this.cdr.markForCheck();
      },
    });
  }

  // ─── 资料抽屉（服务于「实训资料」Tab，保存时即时持久化） ─────────────────────────────────────

  /** 编辑态正在编辑的行（新增时为 null）。 */
  private editingRow: MaterialRow | null = null;

  openAddMaterialDrawer(): void {
    if (this.materialProjects.length === 0) {
      this.message.warning('暂无实训项目，请先在「实训管理」中新建项目');
      return;
    }
    this.drawerMode.set('add');
    this.drawerIndex.set(-1);
    this.editingRow = null;
    this.materialDraft.set({
      title: '', description: '', materialType: 0, resourceUrl: '',
      sortOrder: 1,
      projectId: this.materialProjects[0].id,
    });
    this.drawerUploading.set(false);
    this.drawerVisible.set(true);
  }

  openEditMaterialDrawer(row: MaterialRow): void {
    const entry = this.projectBaseMap.get(row.projectId);
    if (!entry) return;
    this.drawerMode.set('edit');
    this.drawerIndex.set(entry.materials.indexOf(row.material));
    this.editingRow = row;
    const src = row.material;
    this.materialDraft.set({
      title: src.title ?? '', description: src.description ?? '',
      materialType: src.materialType ?? 0, resourceUrl: src.resourceUrl ?? '',
      sortOrder: src.sortOrder ?? 1,
      projectId: row.projectId,
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

  /**
   * 用新对象替换当前 draft 的指定字段。
   * 直接对 draft.materialType = X 之类的就地修改不会触发 signal-aware 的
   * 变更检测，导致 *ngIf="draft.materialType === 3" 这类条件不重渲染、
   * “资料类型”点完之后下一项该换不换。这里统一走 set + 新对象。
   */
  patchDraft(patch: Partial<MaterialDraft>): void {
    const cur = this.materialDraft();
    if (!cur) return;
    this.materialDraft.set({ ...cur, ...patch });
  }

  saveDrawer(): void {
    if (!this.validateMaterialDraft()) return;
    const draft = this.materialDraft()!;
    const targetEntry = this.projectBaseMap.get(draft.projectId);
    if (!targetEntry) {
      this.message.warning('请选择所属项目');
      return;
    }
    const next: CreateUpdatePracticumMaterialDto = {
      title: (draft.title || '').trim(),
      description: (draft.description || '').trim(),
      materialType: draft.materialType,
      resourceUrl: (draft.resourceUrl || '').trim(),
      sortOrder: 1,
    };
    this.drawerSaving.set(true);
    this.cdr.markForCheck();

    if (this.drawerMode() === 'add') {
      targetEntry.materials.push(next);
      this.persistMaterials(draft.projectId).subscribe({
        next: detail => {
          this.drawerSaving.set(false);
          this.message.success('资料已添加');
          this.afterMaterialsPersisted([draft.projectId], new Map([[draft.projectId, detail]]));
          this.closeDrawer();
        },
        error: () => {
          this.drawerSaving.set(false);
          this.message.error('添加失败，已恢复');
          this.loadAllMaterials();
        },
      });
      return;
    }

    // 编辑：所属项目不变则原位替换，变更则从旧项目移到新项目
    const orig = this.editingRow;
    if (!orig) {
      this.drawerSaving.set(false);
      return;
    }
    const oldEntry = this.projectBaseMap.get(orig.projectId);
    if (!oldEntry) {
      this.drawerSaving.set(false);
      return;
    }
    const oldIdx = oldEntry.materials.indexOf(orig.material);
    if (oldIdx < 0) {
      this.drawerSaving.set(false);
      return;
    }
    if (orig.projectId === draft.projectId) {
      oldEntry.materials[oldIdx] = next;
      this.persistMaterials(draft.projectId).subscribe({
        next: detail => {
          this.drawerSaving.set(false);
          this.message.success('资料已更新');
          this.afterMaterialsPersisted([draft.projectId], new Map([[draft.projectId, detail]]));
          this.closeDrawer();
        },
        error: () => {
          this.drawerSaving.set(false);
          this.message.error('更新失败，已恢复');
          this.loadAllMaterials();
        },
      });
    } else {
      oldEntry.materials.splice(oldIdx, 1);
      targetEntry.materials.push(next);
      forkJoin([
        this.persistMaterials(orig.projectId).pipe(catchError(() => of(null))),
        this.persistMaterials(draft.projectId).pipe(catchError(() => of(null))),
      ]).subscribe({
        next: ([oldDetail, newDetail]) => {
          this.drawerSaving.set(false);
          if (!oldDetail || !newDetail) {
            this.message.error('移动失败，已恢复');
            this.loadAllMaterials();
            return;
          }
          this.message.success('资料已更新');
          this.afterMaterialsPersisted(
            [orig.projectId, draft.projectId],
            new Map([[orig.projectId, oldDetail], [draft.projectId, newDetail]]));
          this.closeDrawer();
        },
        error: () => {
          this.drawerSaving.set(false);
          this.message.error('更新失败，已恢复');
          this.loadAllMaterials();
        },
      });
    }
  }

  deleteFromDrawer(): void {
    const orig = this.editingRow;
    if (!orig) return;
    const entry = this.projectBaseMap.get(orig.projectId);
    if (!entry) return;
    const idx = entry.materials.indexOf(orig.material);
    if (idx < 0) return;
    entry.materials.splice(idx, 1);
    this.drawerSaving.set(true);
    this.persistMaterials(orig.projectId).subscribe({
      next: detail => {
        this.drawerSaving.set(false);
        this.message.success('资料已删除');
        this.afterMaterialsPersisted([orig.projectId], new Map([[orig.projectId, detail]]));
        this.closeDrawer();
      },
      error: () => {
        this.drawerSaving.set(false);
        this.message.error('删除失败，已恢复');
        this.loadAllMaterials();
        this.closeDrawer();
      },
    });
  }

  addMaterial(): void { this.openAddMaterialDrawer(); }
  openAddMaterial(): void { this.openAddMaterialDrawer(); }
  openEditMaterial(row: MaterialRow): void { this.openEditMaterialDrawer(row); }
  openMaterialDrawer(row: MaterialRow): void { this.openEditMaterialDrawer(row); }
  editFromDrawer(): void { /* no-op: 抽屉本身就是编辑态 */ }

  private validateMaterialDraft(): boolean {
    const draft = this.materialDraft();
    if (!draft) return false;
    if (!draft.projectId || !this.projectBaseMap.has(draft.projectId)) {
      this.message.warning('请选择所属项目');
      return false;
    }
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
    // datetime-local 输入 → 后端 ISO(UTC)
    payload.startTime = this.fromDateTimeLocal(this.form.startTime);
    payload.endTime = this.fromDateTimeLocal(this.form.endTime);
    return payload;
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
