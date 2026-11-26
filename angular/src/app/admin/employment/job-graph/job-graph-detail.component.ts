import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { JobCompetencyGraphService } from '../../../proxy/employment/job-competency-graph.service';
import { CompetencyService } from '../../../proxy/competencies/competency.service';
import type {
  JobCompetencyGraphDetailDto,
  JobCoreTaskDto,
  JobGraphCoverageDto,
  SaveJobCoreTaskInput,
} from '../../../proxy/employment/dtos/models';
import type { CompetencyDto } from '../../../proxy/competencies/dtos/models';
import type { CourseDto } from '../../../proxy/courses/dtos/models';
import { MyCoursesService } from '../../../learning/shared/my-courses.service';
import { JobGraphCanvasComponent } from './shared/job-graph-canvas.component';

/**
 * 岗位图谱详情。
 *
 * 布局（自上而下）：岗位信息 → 统计卡（整行铺满）→ 图谱（颜色分层）→ 战役/能力/课程三张表。
 * 表格口径：
 * - **核心任务表**：任务名 / 权重 / 要求能力数 / 操作（挂能力、编辑、删除）
 * - **能力表**：按能力去重聚合（同一能力被多个任务要求时只出现一行，列出被哪些任务用到）
 * - **课程表**：每门课覆盖了本岗位多少能力
 */
@Component({
  selector: 'app-job-graph-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzTableModule,
    NzTagModule,
    NzProgressModule,
    NzModalModule,
    NzFormModule,
    NzInputNumberModule,
    JobGraphCanvasComponent,
  ],
  templateUrl: './job-graph-detail.component.html',
  styleUrls: ['./job-graph-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JobGraphDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(JobCompetencyGraphService);
  private readonly competencyService = inject(CompetencyService);
  private readonly myCourses = inject(MyCoursesService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly graphId = signal<string>('');
  readonly detail = signal<JobCompetencyGraphDetailDto | null>(null);
  readonly coverage = signal<JobGraphCoverageDto | null>(null);
  readonly loading = signal(false);

  /** 达成度用的课程上下文（可空 = 只看结构） */
  readonly courseId = signal<string | null>(null);
  readonly courses = signal<CourseDto[]>([]);
  readonly competencyOptions = signal<{ id: string; label: string }[]>([]);

  readonly isTaskModalOpen = signal(false);
  readonly editingTaskId = signal<string | null>(null);
  readonly taskForm = signal<SaveJobCoreTaskInput>(this.emptyTaskForm());

  readonly linkingTask = signal<JobCoreTaskDto | null>(null);
  readonly linkCompetencyId = signal<string | null>(null);
  readonly linkWeight = signal<number>(1);
  readonly linkLevel = signal<number>(1);

  readonly levelLabel: Record<number, string> = { 0: '了解', 1: '熟练', 2: '精通' };

  readonly taskCount = computed(() => this.detail()?.tasks?.length ?? 0);

  /** 能力表数据：按能力去重聚合（同一能力可能被多个任务要求） */
  readonly competencyRows = computed(() => {
    const rows = new Map<
      string,
      {
        competencyId: string;
        code?: string | null;
        name: string;
        requiredLevel: number;
        knowledgeCount: number;
        achievement?: number | null;
        courses: string[];
        tasks: string[];
        linkIds: string[];
      }
    >();

    for (const task of this.detail()?.tasks ?? []) {
      for (const comp of task.competencies ?? []) {
        let row = rows.get(comp.competencyId!);
        if (!row) {
          row = {
            competencyId: comp.competencyId!,
            code: comp.code,
            name: comp.name ?? '',
            requiredLevel: comp.requiredLevel ?? 1,
            knowledgeCount: comp.knowledgeCount ?? 0,
            achievement: comp.achievement,
            courses: (comp.courses ?? []).map(c => c.courseTitle ?? ''),
            tasks: [],
            linkIds: [],
          };
          rows.set(comp.competencyId!, row);
        }

        row.requiredLevel = Math.max(row.requiredLevel, comp.requiredLevel ?? 1);
        if (comp.knowledgeCount) row.knowledgeCount = comp.knowledgeCount;
        if (comp.achievement != null) row.achievement = comp.achievement;
        if ((comp.courses ?? []).length) row.courses = (comp.courses ?? []).map(c => c.courseTitle ?? '');
        if (!row.tasks.includes(task.title ?? '')) row.tasks.push(task.title ?? '');
        if (comp.id) row.linkIds.push(comp.id);
      }
    }

    return [...rows.values()].sort((a, b) => b.requiredLevel - a.requiredLevel || a.name.localeCompare(b.name));
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    this.graphId.set(id);
    this.load();
    this.loadCompetencyOptions();
    this.loadCourses();
  }

  load(): void {
    const id = this.graphId();
    if (!id) return;

    this.loading.set(true);
    this.service.getDetail(id, this.courseId() ?? undefined).subscribe({
      next: data => {
        this.detail.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.detail.set(null);
        this.loading.set(false);
      },
    });

    this.service.getCoverage(id, this.courseId() ?? undefined).subscribe({
      next: data => this.coverage.set(data),
      error: () => this.coverage.set(null),
    });
  }

  loadCourses(): void {
    this.myCourses.getCourses(300).subscribe({
      next: list => this.courses.set(list as any),
      error: () => this.courses.set([]),
    });
  }

  loadCompetencyOptions(): void {
    this.competencyService.getTree().subscribe({
      next: rows => {
        const flat: { id: string; label: string }[] = [];
        const walk = (nodes: CompetencyDto[], depth: number) => {
          for (const n of nodes) {
            if (!n.id) continue;
            const code = n.code ? `${n.code} ` : '';
            flat.push({ id: n.id, label: '　'.repeat(depth) + code + (n.name ?? '') });
            if (n.children?.length) walk(n.children, depth + 1);
          }
        };
        walk(rows ?? [], 0);
        this.competencyOptions.set(flat);
      },
      error: () => this.competencyOptions.set([]),
    });
  }

  onCourseChange(courseId: string | null): void {
    this.courseId.set(courseId);
    this.load();
  }

  // ── 核心任务 ────────────────────────────────────────────────

  openCreateTask(): void {
    this.editingTaskId.set(null);
    this.taskForm.set({ ...this.emptyTaskForm(), graphId: this.graphId() });
    this.isTaskModalOpen.set(true);
  }

  openEditTask(task: JobCoreTaskDto): void {
    this.editingTaskId.set(task.id!);
    this.taskForm.set({
      graphId: this.graphId(),
      title: task.title ?? '',
      description: task.description ?? '',
      weight: task.weight ?? 1,
      sortOrder: task.sortOrder ?? 0,
    });
    this.isTaskModalOpen.set(true);
  }

  patchTaskForm(patch: Partial<SaveJobCoreTaskInput>): void {
    this.taskForm.set({ ...this.taskForm(), ...patch });
  }

  saveTask(): void {
    const model = this.taskForm();
    if (!model.title?.trim()) {
      this.message.warning('请填写任务名称');
      return;
    }

    const id = this.editingTaskId();
    const req = id ? this.service.updateTask(id, model) : this.service.createTask(model);
    req.subscribe({
      next: () => {
        this.isTaskModalOpen.set(false);
        this.message.success(id ? '已保存' : '已添加任务');
        this.load();
      },
      error: err => this.message.error(err?.error?.error?.message || '保存失败'),
    });
  }

  removeTask(task: JobCoreTaskDto): void {
    this.modal.confirm({
      nzTitle: '删除核心任务',
      nzContent: `任务下的能力关联会一起删除，确定删除「${task.title}」吗？`,
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.service.deleteTask(task.id!).subscribe({
            next: () => {
              this.message.success('已删除');
              this.load();
              resolve();
            },
            error: err => {
              this.message.error(err?.error?.error?.message || '删除失败');
              reject(err);
            },
          });
        }),
    });
  }

  // ── 任务 → 能力 ─────────────────────────────────────────────

  openLink(task: JobCoreTaskDto): void {
    this.linkingTask.set(task);
    this.linkCompetencyId.set(null);
    this.linkWeight.set(1);
    this.linkLevel.set(1);
  }

  submitLink(): void {
    const task = this.linkingTask();
    const competencyId = this.linkCompetencyId();
    if (!task?.id || !competencyId) {
      this.message.warning('请选择要挂到该任务的能力');
      return;
    }

    this.service
      .linkCompetency({ coreTaskId: task.id, competencyId, weight: this.linkWeight(), requiredLevel: this.linkLevel() })
      .subscribe({
        next: () => {
          this.linkingTask.set(null);
          this.message.success('已关联能力');
          this.load();
        },
        error: err => this.message.error(err?.error?.error?.message || '关联失败'),
      });
  }

  unlink(linkId?: string | null): void {
    if (!linkId) return;
    this.service.unlinkCompetency(linkId).subscribe({
      next: () => {
        this.message.success('已解除');
        this.load();
      },
      error: err => this.message.error(err?.error?.error?.message || '解除失败'),
    });
  }

  /** 能力表里"解除"：该能力可能被多个任务要求，逐个解除 */
  unlinkCompetencyRow(row: { name: string; linkIds: string[] }): void {
    if (row.linkIds.length === 0) return;
    this.modal.confirm({
      nzTitle: '解除能力关联',
      nzContent: `「${row.name}」被 ${row.linkIds.length} 个任务要求，会全部解除（能力项本身不受影响）。`,
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          let remain = row.linkIds.length;
          row.linkIds.forEach(id =>
            this.service.unlinkCompetency(id).subscribe({
              next: () => {
                if (--remain === 0) {
                  this.message.success('已解除');
                  this.load();
                  resolve();
                }
              },
              error: err => {
                this.message.error(err?.error?.error?.message || '解除失败');
                reject(err);
              },
            }),
          );
        }),
    });
  }

  // ── 视图辅助 ────────────────────────────────────────────────

  percent(value?: number | null): number {
    return Math.round((value ?? 0) * 100);
  }

  achievementColor(value?: number | null): string {
    if (value === null || value === undefined) return 'default';
    if (value < 0.6) return 'error';
    if (value < 0.75) return 'warning';
    return 'success';
  }

  back(): void {
    this.router.navigate(['/admin/employment/job-graphs']);
  }

  private emptyTaskForm(): SaveJobCoreTaskInput {
    return { graphId: '', title: '', description: '', weight: 1, sortOrder: 0 };
  }
}
