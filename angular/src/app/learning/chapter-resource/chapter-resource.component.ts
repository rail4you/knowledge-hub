import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { LocalizationPipe } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { KnowledgeResourceService } from '../../proxy/courses/knowledge-resource.service';
import { CourseResourceService } from '../../proxy/courses/course-resource.service';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import type { CourseDto, ChapterDto } from '../../proxy/courses/dtos/models';
import type { CreateUpdateKnowledgeResourceDto, KnowledgeResourceDto, CourseResourceDto } from '../../proxy/courses/dtos/models';

@Component({
  selector: 'app-chapter-resource',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzTagModule,
    NzIconModule,
    NzSpinModule,
    NzSelectModule,
    NzModalModule,
    NzTableModule,
    NzCheckboxModule,
    NzPaginationModule,
    NzTooltipModule,
    NzSwitchModule,
  ],
  templateUrl: './chapter-resource.component.html',
  styleUrls: ['./chapter-resource.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChapterResourceComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);
  private readonly knowledgeResourceService = inject(KnowledgeResourceService);
  private readonly courseResourceService = inject(CourseResourceService);
  private readonly message = inject(NzMessageService);

  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);
  readonly chapters = signal<ChapterDto[]>([]);
  readonly expandedNodes = signal<Set<string>>(new Set());

  // ── 左侧章节树：搜索 + 仅显示有关联 ────────────────────────────────
  readonly chapterKeyword = signal('');
  readonly onlyWithLinked = signal(false);
  readonly isChapterFiltering = computed(
    () => this.chapterKeyword().trim() !== '' || this.onlyWithLinked()
  );

  /** 各章节直接挂载的资源数量（取自章节树自带的 knowledgeResources） */
  readonly chapterResourceCountMap = computed(() => {
    const map = new Map<string, number>();
    const walk = (nodes: ChapterDto[]) => {
      for (const n of nodes || []) {
        if (n.id) {
          map.set(n.id, (n.knowledgeResources || []).length);
        }
        if (n.children?.length) walk(n.children);
      }
    };
    walk(this.chapters());
    return map;
  });

  /** 至少挂了一个资源的章节数（用于筛选文案） */
  readonly contentChapterCount = computed(() => {
    let count = 0;
    for (const v of this.chapterResourceCountMap().values()) {
      if (v > 0) count++;
    }
    return count;
  });

  /** 过滤后的章节树：保留命中节点及其祖先链；筛选时模板自动全展开 */
  readonly visibleChapters = computed(() => {
    const kw = this.chapterKeyword().trim().toLowerCase();
    const onlyLinked = this.onlyWithLinked();
    if (!kw && !onlyLinked) return this.chapters();

    const filter = (nodes: ChapterDto[]): ChapterDto[] => {
      const out: ChapterDto[] = [];
      for (const n of nodes || []) {
        const children = filter(n.children || []);
        if (this.chapterSelfVisible(n, kw, onlyLinked) || children.length > 0) {
          out.push({ ...n, children });
        }
      }
      return out;
    };
    return filter(this.chapters());
  });

  // ── 右侧：当前选中章节 ───────────────────────────────────────────
  readonly selectedChapterId = signal<string | null>(null);
  readonly selectedChapterTitle = signal('');

  readonly chapterResources = signal<KnowledgeResourceDto[]>([]);
  /** 课程资源池：所有可被关联到章节的资源（来自 course-resource 列表） */
  readonly libraryResources = signal<CourseResourceDto[]>([]);
  readonly linkedLoading = signal(false);

  // 右侧表格分页
  readonly linkedPage = signal(1);
  readonly linkedPageSize = signal(10);
  readonly pagedChapterResources = computed(() => {
    const start = (this.linkedPage() - 1) * this.linkedPageSize();
    return this.chapterResources().slice(start, start + this.linkedPageSize());
  });

  // ── 关联资源弹窗 ─────────────────────────────────────────────────
  readonly linkModalVisible = signal(false);
  readonly linkModalKeyword = signal('');
  readonly linkModalPage = signal(1);
  readonly linkModalPageSize = signal(8);
  readonly linkModalSelectedIds = signal<Set<string>>(new Set());
  readonly linkModalSubmitting = signal(false);
  /** 弹窗内是否展示已关联到当前章节的资源（默认隐藏，避免干扰"待添加"列表） */
  readonly showLinkedInModal = signal(false);

  /** 已关联到当前章节的资源 ResourceId 集合（按 resourceId 匹配资源池条目） */
  readonly alreadyLinkedToCurrentChapter = computed(() => {
    const set = new Set<string>();
    for (const r of this.chapterResources()) {
      if (r.resourceId) set.add(r.resourceId);
    }
    return set;
  });

  /** 弹窗内的可选资源列表（搜索过滤；默认不展示已关联的，开启开关后可一并查看） */
  readonly linkCandidates = computed(() => {
    const kw = this.linkModalKeyword().trim().toLowerCase();
    const showLinked = this.showLinkedInModal();
    let list = this.libraryResources();
    if (!showLinked) {
      const linked = this.alreadyLinkedToCurrentChapter();
      list = list.filter(r => !r.resourceId || !linked.has(r.resourceId));
    }
    if (kw) {
      list = list.filter(r => {
        const name = (r.resourceName ?? r.originalFileName ?? '').toLowerCase();
        return name.includes(kw) || (r.description ?? '').toLowerCase().includes(kw);
      });
    }
    return list;
  });

  readonly pagedLinkCandidates = computed(() => {
    const start = (this.linkModalPage() - 1) * this.linkModalPageSize();
    return this.linkCandidates().slice(start, start + this.linkModalPageSize());
  });

  /** 当前分页内"可被选中"的资源 id（即未关联的） */
  readonly selectableOnPageIds = computed(() => {
    const linked = this.alreadyLinkedToCurrentChapter();
    return this.pagedLinkCandidates()
      .map(r => r.resourceId)
      .filter((id): id is string => !!id && !linked.has(id));
  });

  readonly linkModalAllChecked = computed(() => {
    const ids = this.selectableOnPageIds();
    if (ids.length === 0) return false;
    const sel = this.linkModalSelectedIds();
    return ids.every(id => sel.has(id));
  });

  readonly linkModalIndeterminate = computed(() => {
    const ids = this.selectableOnPageIds();
    if (ids.length === 0) return false;
    const sel = this.linkModalSelectedIds();
    const some = ids.some(id => sel.has(id));
    return some && !this.linkModalAllChecked();
  });

  /** 弹窗底部真正要新增关联的条目数（已关联的不计入） */
  readonly linkModalEffectiveCount = computed(() => {
    const linked = this.alreadyLinkedToCurrentChapter();
    let n = 0;
    for (const id of this.linkModalSelectedIds()) {
      if (!linked.has(id)) n++;
    }
    return n;
  });

  // 顶部统计条
  readonly selectedCourse = computed(() =>
    this.courses().find(c => c.id === this.selectedCourseId()) ?? null
  );

  readonly poolCount = computed(() => this.libraryResources().length);
  readonly linkedResourceCount = computed(() => {
    let n = 0;
    for (const v of this.chapterResourceCountMap().values()) n += v;
    return n;
  });
  readonly linkedChapterCount = computed(() => this.contentChapterCount());

  ngOnInit() {
    this.loadCourses();
  }

  loadCourses() {
    this.courseService.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe({
      next: result => {
        this.courses.set(result.items || []);
      },
    });
  }

  onCourseSelected(courseId: string) {
    if (!courseId) return;
    this.selectedCourseId.set(courseId);
    this.selectedChapterId.set(null);
    this.selectedChapterTitle.set('');
    this.chapterResources.set([]);
    this.linkedPage.set(1);
    this.loadChapterTree();
    this.loadLibraryResources();
  }

  loadChapterTree() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.chapterService.getChapterTree(courseId).subscribe({
      next: data => {
        const list = data || [];
        this.chapters.set(list);
        // 默认展开顶级章节
        const expanded = new Set<string>(list.map(n => n.id!).filter(Boolean));
        this.expandedNodes.set(expanded);
      },
    });
  }

  loadLibraryResources() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.courseResourceService.getByCourse(courseId).subscribe({
      next: data => {
        this.libraryResources.set(data || []);
      },
      error: () => {
        this.message.error('加载课程资源失败');
      },
    });
  }

  // ── 章节树交互 ──────────────────────────────────────────────────
  onChapterItemClick(node: ChapterDto): void {
    if (!node.id) return;
    if (node.children?.length && !this.expandedNodes().has(node.id)) {
      const set = new Set(this.expandedNodes());
      set.add(node.id);
      this.expandedNodes.set(set);
    }
    this.selectChapter(node);
  }

  toggleChapter(event: MouseEvent, id: string): void {
    event.stopPropagation();
    const set = new Set(this.expandedNodes());
    if (set.has(id)) set.delete(id);
    else set.add(id);
    this.expandedNodes.set(set);
  }

  isExpanded(id: string): boolean {
    return this.expandedNodes().has(id);
  }

  hasChildren(node: ChapterDto): boolean {
    return !!node.children && node.children.length > 0;
  }

  /** 单个章节自身是否满足当前筛选条件（祖先链由 visibleChapters 保留） */
  private chapterSelfVisible(node: ChapterDto, kw: string, onlyLinked: boolean): boolean {
    if (onlyLinked) {
      const cnt = node.id ? this.chapterResourceCountMap().get(node.id) || 0 : 0;
      if (cnt === 0) return false;
    }
    if (kw && !(node.title || '').toLowerCase().includes(kw)) return false;
    return true;
  }

  clearChapterFilter(): void {
    this.chapterKeyword.set('');
    this.onlyWithLinked.set(false);
  }

  selectChapter(chapter: ChapterDto) {
    this.selectedChapterId.set(chapter.id ?? null);
    this.selectedChapterTitle.set(chapter.title ?? '');
    this.linkedPage.set(1);
    this.loadChapterResources();
  }

  loadChapterResources() {
    const chapterId = this.selectedChapterId();
    if (!chapterId) return;

    this.linkedLoading.set(true);
    this.knowledgeResourceService.getByChapter(chapterId).subscribe({
      next: data => {
        this.chapterResources.set(data || []);
        this.linkedLoading.set(false);
      },
      error: () => {
        this.linkedLoading.set(false);
        this.message.error('加载章节资源失败');
      },
    });
  }

  onLinkedPageChange(page: number) {
    this.linkedPage.set(page);
  }

  // ── 关联资源弹窗 ───────────────────────────────────────────────
  openLinkModal() {
    if (!this.selectedChapterId()) {
      this.message.warning('请先选择章节');
      return;
    }
    if (this.libraryResources().length === 0) {
      this.message.warning('该课程还没有关联任何资源，请先在「课程资源」页为课程挑选资源');
      return;
    }
    this.linkModalKeyword.set('');
    this.linkModalPage.set(1);
    this.linkModalSelectedIds.set(new Set());
    this.showLinkedInModal.set(false);
    this.linkModalVisible.set(true);
  }

  closeLinkModal() {
    this.linkModalVisible.set(false);
    this.linkModalSelectedIds.set(new Set());
    this.linkModalKeyword.set('');
    this.linkModalPage.set(1);
    this.showLinkedInModal.set(false);
  }

  onLinkKeywordChange(value: string) {
    this.linkModalKeyword.set(value);
    this.linkModalPage.set(1);
  }

  onLinkPageChange(page: number) {
    this.linkModalPage.set(page);
  }

  /** 切换「显示已关联」开关：回到第一页，避免空选状态跨越分页 */
  onShowLinkedChange(value: boolean) {
    this.showLinkedInModal.set(value);
    this.linkModalPage.set(1);
  }

  isLinkSelected(resourceId: string | undefined): boolean {
    if (!resourceId) return false;
    return this.linkModalSelectedIds().has(resourceId);
  }

  isAlreadyLinked(resourceId: string | undefined): boolean {
    if (!resourceId) return false;
    return this.alreadyLinkedToCurrentChapter().has(resourceId);
  }

  toggleLinkOne(resourceId: string | undefined, checked: boolean) {
    if (!resourceId) return;
    const set = new Set(this.linkModalSelectedIds());
    if (checked) set.add(resourceId);
    else set.delete(resourceId);
    this.linkModalSelectedIds.set(set);
  }

  toggleLinkAll(checked: boolean) {
    const ids = this.selectableOnPageIds();
    if (ids.length === 0) return;
    const set = new Set(this.linkModalSelectedIds());
    for (const id of ids) {
      if (checked) set.add(id);
      else set.delete(id);
    }
    this.linkModalSelectedIds.set(set);
  }

  /** 批量关联：将弹窗内选中的"新"资源关联到当前章节 */
  confirmLinkResources() {
    const chapterId = this.selectedChapterId();
    const courseId = this.selectedCourseId();
    if (!chapterId || !courseId) return;

    const selectedIds = Array.from(this.linkModalSelectedIds());
    const linked = this.alreadyLinkedToCurrentChapter();
    const targetIds = selectedIds.filter(id => !linked.has(id));
    if (targetIds.length === 0) {
      this.message.warning('请先选择要关联的资源');
      return;
    }

    const byId = new Map(this.libraryResources().map(r => [r.resourceId, r]));
    const tasks = targetIds
      .map(rid => byId.get(rid))
      .filter((r): r is CourseResourceDto => !!r)
      .map(resource => {
        const dto: CreateUpdateKnowledgeResourceDto = {
          courseId: courseId,
          chapterId: chapterId,
          name: resource.resourceName ?? resource.originalFileName ?? '',
          description: resource.description ?? '',
          content: resource.filePath ?? '',
          importanceLevel: 'normal',
          difficulty: 1,
          sortOrder: 0,
          tags: resource.keywords ?? '',
          parentId: null,
          resourceId: resource.resourceId ?? '',
        };
        return this.knowledgeResourceService.create(dto);
      });

    if (tasks.length === 0) return;

    this.linkModalSubmitting.set(true);
    forkJoin(tasks).subscribe({
      next: () => {
        this.linkModalSubmitting.set(false);
        this.message.success(`已成功关联 ${tasks.length} 个资源到章节`);
        this.closeLinkModal();
        this.loadChapterResources();
        this.loadChapterTree(); // 刷新徽标
      },
      error: err => {
        this.linkModalSubmitting.set(false);
        const detail =
          err?.error?.error?.message ||
          err?.error?.message ||
          err?.message ||
          '';
        this.message.error('关联失败：' + (detail || '未知错误'));
        this.loadChapterResources();
      },
    });
  }

  // ── 取消关联（单条） ────────────────────────────────────────────
  unlinkResource(resource: KnowledgeResourceDto) {
    if (!resource.id) return;

    const dto: CreateUpdateKnowledgeResourceDto = {
      courseId: resource.courseId,
      chapterId: null,
      name: resource.name,
      description: resource.description,
      content: resource.content,
      importanceLevel: resource.importanceLevel,
      difficulty: resource.difficulty,
      sortOrder: resource.sortOrder,
      tags: resource.tags,
      parentId: resource.parentId,
    };

    this.knowledgeResourceService.update(resource.id, dto).subscribe({
      next: () => {
        this.message.success('已取消关联');
        this.loadChapterResources();
        this.loadChapterTree(); // 刷新徽标
      },
      error: err => {
        const detail =
          err?.error?.error?.message ||
          err?.error?.message ||
          err?.message ||
          '';
        this.message.error('取消关联失败：' + (detail || '未知错误'));
      },
    });
  }

  // ── 工具方法 ───────────────────────────────────────────────────
  /** KnowledgeResourceDto 没有 resourceType 字段，按扩展名推断类型用于徽标展示 */
  inferResourceType(extension: string | null | undefined): ResourceType {
    if (!extension) return ResourceType.Document;
    const ext = extension.toLowerCase().replace(/^\./, '');
    if (['mp4', 'mov', 'avi', 'mkv', 'webm', 'flv'].includes(ext)) return ResourceType.Video;
    if (['mp3', 'wav', 'ogg', 'm4a', 'aac', 'flac'].includes(ext)) return ResourceType.Audio;
    if (['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp', 'svg'].includes(ext)) return ResourceType.Image;
    if (['ppt', 'pptx'].includes(ext)) return ResourceType.PPT;
    return ResourceType.Document;
  }

  getResourceTypeLabel(extension: string | null | undefined): string {
    return this.getResourceTypeName(this.inferResourceType(extension));
  }

  getResourceTypeColor(extension: string | null | undefined): string {
    return this.getResourceTypeColorByEnum(this.inferResourceType(extension));
  }

  getResourceTypeName(type: ResourceType | undefined): string {
    if (type === undefined || type === null) return '';
    const map: Record<number, string> = {
      [ResourceType.Document]: '文档',
      [ResourceType.Video]: '视频',
      [ResourceType.Audio]: '音频',
      [ResourceType.Image]: '图片',
      [ResourceType.PPT]: '演示文稿',
    };
    return map[type] ?? '资料';
  }

  getResourceTypeColorByEnum(type: ResourceType | undefined): string {
    if (type === undefined || type === null) return 'default';
    const map: Record<number, string> = {
      [ResourceType.Document]: 'blue',
      [ResourceType.Video]: 'purple',
      [ResourceType.Audio]: 'cyan',
      [ResourceType.Image]: 'green',
      [ResourceType.PPT]: 'orange',
    };
    return map[type] ?? 'default';
  }

  formatFileSize(bytes?: number | null): string {
    if (!bytes || bytes <= 0) return '—';
    if (bytes < 1024) return `${bytes} B`;
    const kb = bytes / 1024;
    if (kb < 1024) return `${kb.toFixed(kb < 10 ? 1 : 0)} KB`;
    const mb = kb / 1024;
    if (mb < 1024) return `${mb.toFixed(mb < 10 ? 1 : 0)} MB`;
    return `${(mb / 1024).toFixed(2)} GB`;
  }

  trackChapter = (_: number, n: ChapterDto) => n.id;
  trackResource = (_: number, r: KnowledgeResourceDto) => r.id;
  trackCourseResource = (_: number, r: CourseResourceDto) => r.resourceId;
}