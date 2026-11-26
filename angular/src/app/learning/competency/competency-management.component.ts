import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { take } from 'rxjs';
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
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CompetencyService } from '../../proxy/competencies/competency.service';
import type {
  CompetencyDto,
  CourseCompetencyDto,
  CourseAchievementDto,
  CompetencyKnowledgeDto,
  SaveCompetencyInput,
} from '../../proxy/competencies/dtos/models';
import type { CourseDto, KnowledgeResourceDto } from '../../proxy/courses/dtos/models';
import { KnowledgeResourceService } from '../../proxy/courses/knowledge-resource.service';
import { MyCoursesService } from '../shared/my-courses.service';
import { CourseContextService } from '../course-context.service';

/**
 * 能力体系（毕业要求指标点 / 岗位能力）与**达成度**。
 *
 * 达成度不是手填的：能力 → 课程知识点（CompetencyKnowledgeLink）→ 学生掌握度（B1 统一算法）
 * 一路倒推出来。所以「映射了知识点 + 学生做过题」才会有数，否则显示"数据不足"。
 *
 * 页面结构（课程视角，老师最常用）：
 *   左：本课支撑的能力（权重、达成度、已关联知识点数）
 *   右：选中能力的知识点关联（可增删、调权重）
 *   抽屉：能力项维护（按专业的树，增删改）
 */
@Component({
  selector: 'app-competency-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
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
    NzDrawerModule,
    NzTooltipModule,
  ],
  templateUrl: './competency-management.component.html',
  styleUrls: ['./competency-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CompetencyManagementComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(CompetencyService);
  private readonly knowledgeService = inject(KnowledgeResourceService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);

  readonly achievement = signal<CourseAchievementDto | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);

  /** 能力项树（维护抽屉用） */
  readonly tree = signal<CompetencyDto[]>([]);
  readonly flatCompetencies = computed(() => {
    const out: { id: string; label: string }[] = [];
    const walk = (nodes: CompetencyDto[], depth: number) => {
      for (const n of nodes) {
        if (n.id) {
          const code = n.code ? `${n.code} ` : '';
          out.push({ id: n.id, label: '　'.repeat(depth) + code + (n.name ?? '') });
        }
        if (n.children?.length) walk(n.children, depth + 1);
      }
    };
    walk(this.tree(), 0);
    return out;
  });

  readonly isTreeDrawerOpen = signal(false);
  readonly competencyForm = signal<SaveCompetencyInput>(this.emptyCompetencyForm());
  readonly editingCompetencyId = signal<string | null>(null);

  /** 选中的能力（右侧知识点关联区） */
  readonly activeCompetency = signal<CourseCompetencyDto | null>(null);
  readonly knowledgeLinks = signal<CompetencyKnowledgeDto[]>([]);
  readonly courseKnowledgeOptions = signal<{ id: string; label: string }[]>([]);
  readonly linkTarget = signal<string | null>(null);
  readonly linkWeight = signal<number>(1);

  readonly overallPct = computed(() => Math.round((this.achievement()?.overallAchievement ?? 0) * 100));

  ngOnInit(): void {
    this.loadCourses();
    this.loadTree();
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      const courseId = params.get('courseId') || this.courseContext.currentCourseId();
      if (courseId) this.onCourseSelected(courseId);
    });
  }

  loadCourses(): void {
    this.myCourses.getCourses(300).subscribe({
      next: list => this.courses.set(list as any),
      error: () => this.courses.set([]),
    });
  }

  loadTree(): void {
    this.service.getTree().subscribe({
      next: rows => this.tree.set(rows ?? []),
      error: () => this.tree.set([]),
    });
  }

  onCourseSelected(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    this.activeCompetency.set(null);
    this.knowledgeLinks.set([]);
    if (!courseId) {
      this.achievement.set(null);
      return;
    }
    this.courseContext.setCourse(courseId);
    this.loadAchievement();
    this.loadCourseKnowledge(courseId);
  }

  loadAchievement(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    this.loading.set(true);
    this.service.getAchievement(courseId).subscribe({
      next: data => {
        this.achievement.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.achievement.set(null);
        this.loading.set(false);
      },
    });
  }

  /** 本课可关联的知识点（叶子优先，单元也允许） */
  loadCourseKnowledge(courseId: string): void {
    this.knowledgeService.getTree(courseId).subscribe({
      next: rows => {
        const flat: { id: string; label: string }[] = [];
        const walk = (nodes: KnowledgeResourceDto[], depth: number) => {
          for (const n of nodes) {
            if (!n.id) continue;
            flat.push({ id: n.id, label: '　'.repeat(depth) + (n.name ?? '') });
            if (n.children?.length) walk(n.children, depth + 1);
          }
        };
        walk(rows ?? [], 0);
        this.courseKnowledgeOptions.set(flat);
      },
      error: () => this.courseKnowledgeOptions.set([]),
    });
  }

  // ── 课程支撑的能力 ───────────────────────────────────────────

  selectCompetency(item: CourseCompetencyDto): void {
    this.activeCompetency.set(item);
    this.service.getKnowledgeLinks(item.competencyId!).subscribe({
      next: rows => this.knowledgeLinks.set(rows ?? []),
      error: () => this.knowledgeLinks.set([]),
    });
  }

  /** 把某个能力加入本课支撑（默认权重 1）。 */
  addSupport(competencyId: string | null): void {
    const courseId = this.selectedCourseId();
    if (!courseId || !competencyId) return;

    const current = this.achievement()?.items ?? [];
    if (current.some(x => x.competencyId === competencyId)) {
      this.message.warning('该能力已在本课支撑清单里');
      return;
    }

    const items = [
      ...current.map(x => ({ competencyId: x.competencyId!, weight: x.weight ?? 1, note: x.note ?? null })),
      { competencyId, weight: 1, note: null },
    ];
    this.saveSupports(items);
  }

  removeSupport(item: CourseCompetencyDto): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    const items = (this.achievement()?.items ?? [])
      .filter(x => x.competencyId !== item.competencyId)
      .map(x => ({ competencyId: x.competencyId!, weight: x.weight ?? 1, note: x.note ?? null }));
    this.saveSupports(items);
  }

  updateWeight(item: CourseCompetencyDto, weight: number): void {
    const items = (this.achievement()?.items ?? []).map(x => ({
      competencyId: x.competencyId!,
      weight: x.competencyId === item.competencyId ? weight : (x.weight ?? 1),
      note: x.note ?? null,
    }));
    this.saveSupports(items);
  }

  private saveSupports(items: { competencyId: string; weight: number; note: string | null }[]): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    this.saving.set(true);
    this.service.setCourseSupport({ courseId, items }).subscribe({
      next: rows => {
        this.saving.set(false);
        this.message.success('已更新课程能力支撑');
        this.loadAchievement();
        const active = this.activeCompetency();
        if (active) {
          const refreshed = (rows ?? []).find(x => x.competencyId === active.competencyId);
          if (refreshed) this.selectCompetency(refreshed);
          else {
            this.activeCompetency.set(null);
            this.knowledgeLinks.set([]);
          }
        }
      },
      error: err => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '保存失败');
      },
    });
  }

  // ── 能力 ↔ 知识点 ───────────────────────────────────────────

  linkKnowledge(): void {
    const competency = this.activeCompetency();
    const resourceId = this.linkTarget();
    if (!competency?.competencyId || !resourceId) {
      this.message.warning('请选择要关联的知识点');
      return;
    }

    this.service.linkKnowledge({
      competencyId: competency.competencyId,
      knowledgeResourceId: resourceId,
      weight: this.linkWeight(),
    }).subscribe({
      next: () => {
        this.linkTarget.set(null);
        this.message.success('已关联');
        this.selectCompetency(competency);
        this.loadAchievement();
      },
      error: err => this.message.error(err?.error?.error?.message || '关联失败'),
    });
  }

  unlinkKnowledge(link: CompetencyKnowledgeDto): void {
    const competency = this.activeCompetency();
    if (!link.id) return;
    this.service.unlinkKnowledge(link.id).subscribe({
      next: () => {
        this.message.success('已解除关联');
        if (competency) this.selectCompetency(competency);
        this.loadAchievement();
      },
      error: err => this.message.error(err?.error?.error?.message || '解除失败'),
    });
  }

  // ── 能力项维护（抽屉） ───────────────────────────────────────

  openTreeDrawer(): void {
    this.isTreeDrawerOpen.set(true);
    this.loadTree();
  }

  startCreate(parent?: CompetencyDto): void {
    this.editingCompetencyId.set(null);
    this.competencyForm.set({
      ...this.emptyCompetencyForm(),
      parentId: parent?.id ?? null,
      majorId: parent?.majorId ?? null,
      level: parent ? Math.min(5, (parent.level ?? 0) + 1) : 0,
    });
  }

  startEdit(node: CompetencyDto): void {
    this.editingCompetencyId.set(node.id!);
    this.competencyForm.set({
      majorId: node.majorId ?? null,
      parentId: node.parentId ?? null,
      level: node.level ?? 0,
      code: node.code ?? '',
      name: node.name ?? '',
      description: node.description ?? '',
      sortOrder: node.sortOrder ?? 0,
      isEnabled: node.isEnabled ?? true,
    });
  }

  patchForm(patch: Partial<SaveCompetencyInput>): void {
    this.competencyForm.set({ ...this.competencyForm(), ...patch });
  }

  saveCompetency(): void {
    const model = this.competencyForm();
    if (!model.name?.trim()) {
      this.message.warning('请填写能力名称');
      return;
    }

    const editingId = this.editingCompetencyId();
    const req = editingId
      ? this.service.update(editingId, model)
      : this.service.create(model);

    req.subscribe({
      next: () => {
        this.message.success(editingId ? '已保存' : '已创建');
        this.editingCompetencyId.set(null);
        this.loadTree();
        if (this.selectedCourseId()) this.loadAchievement();
      },
      error: err => this.message.error(err?.error?.error?.message || '保存失败'),
    });
  }

  removeCompetency(node: CompetencyDto): void {
    this.modal.confirm({
      nzTitle: '删除能力项',
      nzContent: `确定删除「${node.name}」吗？被课程支撑或有关联知识点时会被拒绝（避免达成度口径变成孤儿）。`,
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.service.delete(node.id!).subscribe({
            next: () => {
              this.message.success('已删除');
              this.loadTree();
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

  percent(value?: number | null): number {
    return Math.round((value ?? 0) * 100);
  }

  achievementState(item: CourseCompetencyDto): 'none' | 'weak' | 'fair' | 'ok' {
    if (!item.knowledgeCount) return 'none';
    if (!item.studentCount) return 'none';
    const v = item.achievement ?? 0;
    if (v < 0.6) return 'weak';
    if (v < 0.75) return 'fair';
    return 'ok';
  }

  achievementLabel(item: CourseCompetencyDto): string {
    switch (this.achievementState(item)) {
      case 'none':
        return '数据不足';
      case 'weak':
        return '未达成';
      case 'fair':
        return '待巩固';
      default:
        return '已达成';
    }
  }

  private emptyCompetencyForm(): SaveCompetencyInput {
    return {
      majorId: null,
      parentId: null,
      level: 0,
      code: '',
      name: '',
      description: '',
      sortOrder: 0,
      isEnabled: true,
    };
  }
}
