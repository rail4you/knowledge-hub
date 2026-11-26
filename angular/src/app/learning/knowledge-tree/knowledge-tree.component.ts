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
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzUploadModule, type NzUploadFile } from 'ng-zorro-antd/upload';
import { AuthFetchService } from '../../shared/utils/auth-fetch.service';
import { KnowledgeImportService } from './knowledge-import.service';
import type { KnowledgeResourceImportResultDto } from '../../proxy/courses/dtos/models';
import { KnowledgeResourceService } from '../../proxy/courses/knowledge-resource.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import type { CourseDto, KnowledgeResourceDto, CreateUpdateKnowledgeResourceDto, ChapterDto } from '../../proxy/courses/dtos/models';
import { KnowledgeNodeType } from '../../proxy/courses/enums/knowledge-node-type.enum';
import { MyCoursesService } from '../shared/my-courses.service';
import { CourseContextService } from '../course-context.service';

/**
 * 知识点树管理（课程内容主干）。
 *
 * 业务定位（对齐 kg-edu 的 subject → unit → cell）：
 * - 单元（Unit）是目录节点，可再包含子单元或知识项；
 * - 知识项（Item）是叶子，掌握度与练习的最小粒度，习题/视频/资源都挂在它上面。
 *
 * 排序口径：同级顺序由后端 `SortOrder` 维护，物化路径 `SortPath` 由后端写路径自动重算，
 * 所以这里只需要「上移 / 下移」两个动作，无需前端自己算路径。
 */
@Component({
  selector: 'app-knowledge-tree',
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
    NzTooltipModule,
    NzModalModule,
    NzFormModule,
    NzInputNumberModule,
    NzRadioModule,
    NzUploadModule,
  ],
  templateUrl: './knowledge-tree.component.html',
  styleUrls: ['./knowledge-tree.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KnowledgeTreeComponent implements OnInit {
  private readonly myCourses = inject(MyCoursesService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly knowledgeService = inject(KnowledgeResourceService);
  private readonly chapterService = inject(ChapterService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly importer = inject(KnowledgeImportService);
  private readonly authFetch = inject(AuthFetchService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly tree = signal<KnowledgeResourceDto[]>([]);
  readonly chapters = signal<ChapterDto[]>([]);
  readonly loading = signal(false);
  readonly keyword = signal('');
  readonly expanded = signal<Set<string>>(new Set());

  /** Excel 导入 */
  readonly importOpen = signal(false);
  readonly importing = signal(false);
  readonly importFile = signal<File | null>(null);
  readonly importResult = signal<KnowledgeResourceImportResultDto | null>(null);

  readonly isModalOpen = signal(false);
  readonly editing = signal<KnowledgeResourceDto | null>(null);
  readonly saving = signal(false);
  readonly form = signal<CreateUpdateKnowledgeResourceDto>(this.emptyForm());

  /** 搜索过滤（保留命中节点的祖先链，避免搜到子节点却看不到路径）。 */
  readonly visibleTree = computed(() => {
    const kw = this.keyword().trim().toLowerCase();
    const all = this.tree();
    if (!kw) return all;

    const prune = (nodes: KnowledgeResourceDto[]): KnowledgeResourceDto[] => {
      const out: KnowledgeResourceDto[] = [];
      for (const n of nodes) {
        const kids = n.children?.length ? prune(n.children) : [];
        const hit =
          (n.name ?? '').toLowerCase().includes(kw) ||
          (n.description ?? '').toLowerCase().includes(kw) ||
          (n.tags ?? '').toLowerCase().includes(kw);
        if (hit || kids.length) {
          out.push({ ...n, children: kids });
        }
      }
      return out;
    };
    return prune(all);
  });

  /** 节点统计（单元数 / 知识项数），用于页头概览。 */
  readonly stats = computed(() => {
    let units = 0;
    let items = 0;
    const walk = (nodes: KnowledgeResourceDto[]) => {
      for (const n of nodes) {
        if (n.nodeType === KnowledgeNodeType.Unit) units++;
        else items++;
        if (n.children?.length) walk(n.children);
      }
    };
    walk(this.tree());
    return { units, items, total: units + items };
  });

  ngOnInit(): void {
    this.loadCourses();
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      // 优先级：URL 上的 courseId > 全局「当前课程」
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

  onCourseSelected(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    if (!courseId) {
      this.tree.set([]);
      return;
    }
    this.courseContext.setCourse(courseId);
    this.keyword.set('');
    this.loadTree();
    this.loadChapters();
  }

  loadTree(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    this.loading.set(true);
    this.knowledgeService.getTree(courseId).subscribe({
      next: rows => {
        this.tree.set(rows ?? []);
        this.expanded.set(new Set(this.collectIds(rows ?? [])));
        this.loading.set(false);
      },
      error: () => {
        this.tree.set([]);
        this.loading.set(false);
        this.message.error('知识点加载失败');
      },
    });
  }

  /** 章节下拉（知识点可绑定章节，便于按章聚合掌握度）。 */
  loadChapters(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    this.chapterService.getChapterTree(courseId).subscribe({
      next: rows => this.chapters.set(rows ?? []),
      error: () => this.chapters.set([]),
    });
  }

  hasChildren(node: KnowledgeResourceDto): boolean {
    return !!node.children?.length;
  }

  isExpanded(id?: string): boolean {
    return !!id && this.expanded().has(id);
  }

  toggleNode(id?: string): void {
    if (!id) return;
    const next = new Set(this.expanded());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.expanded.set(next);
  }

  expandAll(): void {
    this.expanded.set(new Set(this.collectIds(this.tree())));
  }

  collapseAll(): void {
    this.expanded.set(new Set());
  }

  // ── 新增 / 编辑 ───────────────────────────────────────────────

  openCreate(parent?: KnowledgeResourceDto): void {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    this.editing.set(null);
    this.form.set({
      ...this.emptyForm(),
      courseId,
      parentId: parent?.id ?? null,
      // 在单元下新建默认是知识项；顶层新建默认是单元
      nodeType: parent ? KnowledgeNodeType.Item : KnowledgeNodeType.Unit,
      chapterId: parent?.chapterId ?? null,
    });
    this.isModalOpen.set(true);
  }

  openEdit(node: KnowledgeResourceDto): void {
    this.editing.set(node);
    this.form.set({
      courseId: node.courseId!,
      chapterId: node.chapterId ?? null,
      name: node.name ?? '',
      description: node.description ?? '',
      content: node.content ?? '',
      importanceLevel: node.importanceLevel ?? 'normal',
      difficulty: node.difficulty ?? 1,
      sortOrder: node.sortOrder ?? 0,
      tags: node.tags ?? '',
      parentId: node.parentId ?? null,
      resourceId: node.resourceId ?? null,
      nodeType: node.nodeType ?? KnowledgeNodeType.Item,
    });
    this.isModalOpen.set(true);
  }

  /** 模板里不能写对象展开（Angular 模板不支持 spread），统一走这里打补丁。 */
  patchForm(patch: Partial<CreateUpdateKnowledgeResourceDto>): void {
    this.form.set({ ...this.form(), ...patch });
  }

  save(): void {
    const model = this.form();
    if (!model.name?.trim()) {
      this.message.warning('请填写知识点名称');
      return;
    }

    this.saving.set(true);
    const current = this.editing();
    const request = current
      ? this.knowledgeService.update(current.id!, model)
      : this.knowledgeService.create(model);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.isModalOpen.set(false);
        this.message.success(current ? '已保存' : '已创建');
        this.loadTree();
      },
      error: err => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '保存失败');
      },
    });
  }

  remove(node: KnowledgeResourceDto): void {
    this.modal.confirm({
      nzTitle: '删除知识点',
      nzContent: `确定删除「${node.name}」吗？${
        this.hasChildren(node) ? '它的子节点会上移一级，不会被一起删除。' : ''
      }`,
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.knowledgeService.delete(node.id!).subscribe({
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

  // ── Excel 导入（只支持 xlsx） ────────────────────────────────

  openImport(): void {
    if (!this.selectedCourseId()) {
      this.message.warning('请先选择课程');
      return;
    }
    this.importFile.set(null);
    this.importResult.set(null);
    this.importOpen.set(true);
  }

  beforeUpload = (file: NzUploadFile): boolean => {
    const raw = file as unknown as File;
    if (!/\.xlsx$/i.test(raw.name ?? '')) {
      this.message.error('只支持 .xlsx 文件（不支持 XLS / XMind / OPML）');
      return false;
    }
    this.importFile.set(raw);
    return false; // 阻止自动上传，交给 submitImport
  };

  submitImport(): void {
    const courseId = this.selectedCourseId();
    const file = this.importFile();
    if (!courseId || !file) {
      this.message.warning('请先选择 .xlsx 文件');
      return;
    }

    this.importing.set(true);
    this.importer
      .upload(courseId, file)
      .then(result => {
        this.importing.set(false);
        this.importResult.set(result);
        this.message.success(
          `导入完成：新增 ${result.createdTotal ?? 0} 个节点，复用 ${result.reusedCount ?? 0} 个`,
        );
        this.loadTree();
      })
      .catch(err => {
        this.importing.set(false);
        this.message.error(err?.error?.error?.message || '导入失败');
      });
  }

  downloadTemplate(): void {
    // IRemoteStreamContent：必须用原生 fetch（不带 Accept: application/json）+ blob
    this.authFetch
      .fetch(this.importer.templateUrl(), { method: 'GET' })
      .then(async response => {
        if (!response.ok) {
          this.message.error('模板下载失败');
          return;
        }
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = '知识点导入模板.xlsx';
        anchor.click();
        URL.revokeObjectURL(url);
      })
      .catch(() => this.message.error('模板下载失败'));
  }

  // ── 排序（同级上移 / 下移，后端重算物化路径） ────────────────

  canMoveUp(node: KnowledgeResourceDto): boolean {
    return this.siblingIndex(node) > 0;
  }

  canMoveDown(node: KnowledgeResourceDto): boolean {
    const siblings = this.siblingsOf(node);
    const idx = siblings.findIndex(x => x.id === node.id);
    return idx >= 0 && idx < siblings.length - 1;
  }

  moveUp(node: KnowledgeResourceDto): void {
    const siblings = this.siblingsOf(node);
    const idx = siblings.findIndex(x => x.id === node.id);
    if (idx <= 0) return;
    this.moveTo(node, idx - 1);
  }

  moveDown(node: KnowledgeResourceDto): void {
    const siblings = this.siblingsOf(node);
    const idx = siblings.findIndex(x => x.id === node.id);
    if (idx < 0 || idx >= siblings.length - 1) return;
    this.moveTo(node, idx + 1);
  }

  private moveTo(node: KnowledgeResourceDto, index: number): void {
    this.knowledgeService.move(node.id!, { parentId: node.parentId ?? null, index }).subscribe({
      next: () => this.loadTree(),
      error: err => this.message.error(err?.error?.error?.message || '排序失败'),
    });
  }

  /** 同级兄弟（从当前树里取，避免再发请求）。 */
  private siblingsOf(node: KnowledgeResourceDto): KnowledgeResourceDto[] {
    const parentId = node.parentId ?? null;
    if (!parentId) return this.tree();
    const stack = [...this.tree()];
    while (stack.length) {
      const cur = stack.pop()!;
      if (cur.id === parentId) return cur.children ?? [];
      if (cur.children?.length) stack.push(...cur.children);
    }
    return [];
  }

  private siblingIndex(node: KnowledgeResourceDto): number {
    return this.siblingsOf(node).findIndex(x => x.id === node.id);
  }

  // ── 辅助 ─────────────────────────────────────────────────────

  nodeTypeLabel(type?: KnowledgeNodeType): string {
    switch (type) {
      case KnowledgeNodeType.Unit:
        return '单元';
      case KnowledgeNodeType.Element:
        return '知识元';
      default:
        return '知识项';
    }
  }

  nodeTypeClass(type?: KnowledgeNodeType): string {
    switch (type) {
      case KnowledgeNodeType.Unit:
        return 'tag-unit';
      case KnowledgeNodeType.Element:
        return 'tag-element';
      default:
        return 'tag-item';
    }
  }

  chapterName(id?: string | null): string {
    if (!id) return '';
    const stack = [...this.chapters()];
    while (stack.length) {
      const cur = stack.pop()!;
      if (cur.id === id) return cur.title ?? '';
      if (cur.children?.length) stack.push(...cur.children);
    }
    return '';
  }

  private emptyForm(): CreateUpdateKnowledgeResourceDto {
    return {
      courseId: '',
      chapterId: null,
      name: '',
      description: '',
      content: '',
      importanceLevel: 'normal',
      difficulty: 1,
      sortOrder: 0,
      tags: '',
      parentId: null,
      resourceId: null,
      nodeType: KnowledgeNodeType.Unit,
    };
  }

  private collectIds(nodes: KnowledgeResourceDto[]): string[] {
    const out: string[] = [];
    const walk = (list: KnowledgeResourceDto[]) => {
      for (const n of list) {
        if (n.id) out.push(n.id);
        if (n.children?.length) walk(n.children);
      }
    };
    walk(nodes);
    return out;
  }
}
