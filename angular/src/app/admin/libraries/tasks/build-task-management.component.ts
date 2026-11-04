import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PermissionService } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { BuildTaskService } from '../../../proxy/resource-libraries/build-task.service';
import { ResourceLibraryService } from '../../../proxy/resource-libraries/resource-library.service';
import type {
  BuildTaskDto,
  BuildTaskTemplateDto,
  CreateBuildTaskDto,
  LibraryTemplateDto,
  ResourceLibraryDto,
  ResourceLibraryPartnerTenantDto,
} from '../../../proxy/resource-libraries/dtos/models';
import { BuildTaskStatus } from '../../../proxy/resource-libraries/build-task-status.enum';
import { LibraryContentType } from '../../../proxy/resource-libraries/library-content-type.enum';

type TabKey = 'tasks' | 'templates';

/**
 * 共建任务管理（V2.0 P3）。
 *
 * 关键语义提示（页面始终可见）：**任务验收 ≠ 资源发布**。
 * 交付物验收通过只表示交付项达成，资源进库仍需库准入审核。
 */
@Component({
  selector: 'app-build-task-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzProgressModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './build-task-management.component.html',
  styleUrls: ['./build-task-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BuildTaskManagementComponent implements OnInit {
  private readonly taskService = inject(BuildTaskService);
  private readonly libraryService = inject(ResourceLibraryService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly permission = inject(PermissionService);

  readonly canManage = this.permission.getGrantedPolicy('KnowledgeHub.ResourceLibraries.ManageTask');
  readonly canManageTemplate = this.permission.getGrantedPolicy('KnowledgeHub.ResourceLibraries.ManageTemplate');

  readonly tab = signal<TabKey>('tasks');
  readonly loading = signal(false);
  readonly tasks = signal<BuildTaskDto[]>([]);
  readonly total = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  readonly filter = signal('');
  readonly statusFilter = signal<BuildTaskStatus | null>(null);
  readonly libraryFilter = signal<string | null>(null);

  readonly libraries = signal<ResourceLibraryDto[]>([]);
  readonly partners = signal<ResourceLibraryPartnerTenantDto[]>([]);
  readonly taskTemplates = signal<BuildTaskTemplateDto[]>([]);
  readonly libraryTemplates = signal<LibraryTemplateDto[]>([]);

  // 建任务表单
  readonly createOpen = signal(false);
  readonly creating = signal(false);
  readonly form = signal<CreateBuildTaskDto>({
    libraryId: '',
    title: '',
    collaboratorSchoolIds: [],
    items: [],
  } as CreateBuildTaskDto);

  // 详情
  readonly detail = signal<BuildTaskDto | null>(null);
  readonly detailOpen = signal(false);
  readonly reviewing = signal(false);
  readonly reviewComment = signal('');

  readonly taskCount = computed(() => this.tasks().length);

  /** 模板选择（payload 里是可选字段，模板中不能直接取 any 属性）。 */
  readonly formTemplateId = computed(() => (this.form() as any).templateId as string | undefined);
  readonly formDueAt = computed(() => (this.form() as any).dueAt as string | null | undefined);

  ngOnInit(): void {
    this.loadLibraries();
    this.reload();
  }

  // ── 列表 ──

  setTab(tab: TabKey): void {
    this.tab.set(tab);
    if (tab === 'templates') {
      this.loadTaskTemplates();
      this.loadLibraryTemplates();
    }
  }

  reload(): void {
    this.loading.set(true);
    this.taskService
      .getTasks({
        skipCount: (this.pageIndex() - 1) * this.pageSize(),
        maxResultCount: this.pageSize(),
        libraryId: this.libraryFilter() ?? undefined,
        status: this.statusFilter() ?? undefined,
        filter: this.filter() || undefined,
        sorting: 'creationTime desc',
      } as any)
      .subscribe({
        next: res => {
          this.tasks.set(res.items ?? []);
          this.total.set(res.totalCount ?? 0);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('加载共建任务失败');
        },
      });
  }

  onFilterChange(value: string): void {
    this.filter.set(value ?? '');
    this.pageIndex.set(1);
    this.reload();
  }

  onStatusChange(value: BuildTaskStatus | null): void {
    this.statusFilter.set(value);
    this.pageIndex.set(1);
    this.reload();
  }

  onLibraryChange(value: string | null): void {
    this.libraryFilter.set(value);
    this.pageIndex.set(1);
    this.reload();
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
    this.reload();
  }

  // ── 基础数据 ──

  loadLibraries(): void {
    this.libraryService.getList({} as any).subscribe({
      next: list => this.libraries.set(list ?? []),
    });
    this.libraryService.getCandidatePartnerTenants().subscribe({
      next: list => this.partners.set(list ?? []),
    });
  }

  loadTaskTemplates(): void {
    // 未选库时传空 Guid（后端视为不限库），否则路由参数为空会 400
    this.taskService
      .getTaskTemplates(this.libraryFilter() ?? '00000000-0000-0000-0000-000000000000')
      .subscribe({
      next: list => this.taskTemplates.set(list ?? []),
      error: () => this.taskTemplates.set([]),
    });
  }

  loadLibraryTemplates(): void {
    this.taskService.getTemplates().subscribe({
      next: list => this.libraryTemplates.set(list ?? []),
      error: () => this.libraryTemplates.set([]),
    });
  }

  // ── 建任务 ──

  openCreate(): void {
    this.form.set({
      libraryId: this.libraryFilter() ?? '',
      title: '',
      dueAt: null,
      requirements: '',
      collaboratorSchoolIds: [],
      items: [],
    } as any);
    this.loadTaskTemplates();
    this.createOpen.set(true);
  }

  patchForm(patch: Record<string, unknown>): void {
    this.form.set({ ...this.form(), ...patch } as CreateBuildTaskDto);
  }

  /** 选了任务模板 → 自动带出交付项预览（后端会按模板实例化）。 */
  onTemplateChange(templateId: string | null): void {
    this.patchForm({ templateId: templateId ?? undefined });
  }

  onDueAtChange(value: string | null): void {
    this.patchForm({ dueAt: value || null } as any);
  }

  readonly selectedTemplateItems = computed(() => {
    const id = (this.form() as any).templateId as string | undefined;
    if (!id) return [];
    return this.taskTemplates().find(t => t.id === id)?.items ?? [];
  });

  submitCreate(): void {
    const form = this.form();
    if (!form.libraryId) {
      this.message.warning('请选择资源库');
      return;
    }
    if (!form.title?.trim()) {
      this.message.warning('请填写任务标题');
      return;
    }

    this.creating.set(true);
    this.taskService.createTask(form).subscribe({
      next: dto => {
        this.creating.set(false);
        this.createOpen.set(false);
        this.message.success(`任务已创建（草稿）。交付项 ${dto.items?.length ?? 0} 个，发布后可协作提交。`);
        this.reload();
      },
      error: err => {
        this.creating.set(false);
        this.message.error(err?.error?.error?.message ?? '创建失败');
      },
    });
  }

  publishTask(task: BuildTaskDto): void {
    this.modal.confirm({
      nzTitle: '发布任务',
      nzContent: `发布后「${task.title}」对协作学校与教师可见，可提交交付物。`,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.taskService.publishTask(task.id!).subscribe({
            next: () => {
              this.message.success('已发布');
              this.reload();
              resolve();
            },
            error: err => {
              this.message.error(err?.error?.error?.message ?? '发布失败');
              reject(err);
            },
          });
        }),
    });
  }

  closeTask(task: BuildTaskDto): void {
    this.modal.confirm({
      nzTitle: '关闭任务',
      nzContent: `关闭后「${task.title}」不再接收交付物（历史记录保留）。`,
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.taskService.closeTask(task.id!).subscribe({
            next: () => {
              this.message.success('已关闭');
              this.reload();
              resolve();
            },
            error: err => {
              this.message.error(err?.error?.error?.message ?? '操作失败');
              reject(err);
            },
          });
        }),
    });
  }

  openDetail(task: BuildTaskDto): void {
    this.taskService.getTask(task.id!).subscribe({
      next: dto => {
        this.detail.set(dto);
        this.reviewComment.set('');
        this.detailOpen.set(true);
      },
      error: err => this.message.error(err?.error?.error?.message ?? '加载详情失败'),
    });
  }

  closeDetail(): void {
    this.detailOpen.set(false);
    this.detail.set(null);
  }

  reviewDelivery(deliveryId: string, accept: boolean): void {
    this.reviewing.set(true);
    this.taskService
      .reviewDelivery({ deliveryId, accept, comment: this.reviewComment() || undefined } as any)
      .subscribe({
        next: res => {
          this.reviewing.set(false);
          this.message.success(accept ? '已验收' : '已退回');
          const current = this.detail();
          if (current?.id) {
            this.openDetail(current);
          }
          this.reload();
        },
        error: err => {
          this.reviewing.set(false);
          this.message.error(err?.error?.error?.message ?? '操作失败');
        },
      });
  }

  // ── 展示辅助 ──

  statusColor(status?: BuildTaskStatus): string {
    switch (status) {
      case BuildTaskStatus.Accepted:
        return 'success';
      case BuildTaskStatus.Returned:
        return 'error';
      case BuildTaskStatus.Closed:
        return 'default';
      case BuildTaskStatus.Draft:
        return 'warning';
      default:
        return 'processing';
    }
  }

  contentTypeName(type?: LibraryContentType): string {
    switch (type) {
      case LibraryContentType.Course:
        return '课程';
      case LibraryContentType.Practicum:
        return '实训';
      default:
        return '文件资源';
    }
  }

  trackById = (_: number, item: { id?: string }) => item.id;

  /** 模板配置里的启用栏目（modules），用于表格展示。 */
  templateModules(tpl: LibraryTemplateDto): string {
    const labels: Record<string, string> = {
      course: '课程',
      practice: '实训',
      resource: '资源',
      exam: '试卷',
    };
    try {
      const parsed = JSON.parse(tpl.configJson || '{}');
      const modules = Array.isArray(parsed?.modules) ? parsed.modules : [];
      if (!modules.length) return '—';
      return modules.map((m: string) => labels[m] ?? m).join('、');
    } catch {
      return '—';
    }
  }

  readonly statusOptions = [
    { value: BuildTaskStatus.Draft, label: '草稿' },
    { value: BuildTaskStatus.Published, label: '已发布' },
    { value: BuildTaskStatus.InProgress, label: '进行中' },
    { value: BuildTaskStatus.Accepted, label: '已验收' },
    { value: BuildTaskStatus.Returned, label: '已退回' },
    { value: BuildTaskStatus.Closed, label: '已关闭' },
  ];
}
