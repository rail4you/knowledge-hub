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
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
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
    NzEmptyModule,
    NzCheckboxModule,
    NzTableModule,
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

  courses = signal<CourseDto[]>([]);
  selectedCourseId = signal<string | null>(null);
  chapters = signal<ChapterDto[]>([]);
  expandedNodes = signal<Set<string>>(new Set());
  selectedChapterId = signal<string | null>(null);
  selectedChapterTitle = signal('');

  chapterResources = signal<KnowledgeResourceDto[]>([]);
  // 当前课程已关联的资源（课程资源池）
  libraryResources = signal<CourseResourceDto[]>([]);
  // 左侧课程列表搜索关键字
  courseSearchText = signal('');

  readonly filteredCourses = computed(() => {
    const kw = this.courseSearchText().trim().toLowerCase();
    const all = this.courses();
    if (!kw) return all;
    return all.filter(c =>
      (c.title ?? '').toLowerCase().includes(kw) ||
      (c.majorName ?? '').toLowerCase().includes(kw));
  });

  readonly selectedCourse = computed(() =>
    this.courses().find(c => c.id === this.selectedCourseId()) ?? null);

  // 选中课程的统计：章节总数 / 已关联章节 / 已关联资源 / 资源池资源
  readonly courseStats = computed(() => {
    let chapterCount = 0;
    let linkedChapterCount = 0;
    let linkedResourceCount = 0;
    const walk = (nodes: ChapterDto[]) => {
      for (const n of nodes) {
        chapterCount++;
        const c = n.knowledgeResources?.length ?? 0;
        if (c > 0) {
          linkedChapterCount++;
          linkedResourceCount += c;
        }
        if (n.children?.length) walk(n.children);
      }
    };
    walk(this.chapters());
    return {
      chapterCount,
      linkedChapterCount,
      linkedResourceCount,
      poolCount: this.libraryResources().length,
    };
  });

  loading = signal(false);
  libraryLoading = signal(false);
  searchText = signal('');

  // 只显示有关联资源的章节
  onlyWithResources = signal(false);
  // 左侧章节树搜索关键字
  chapterSearchText = signal('');

  // 勾选待添加到章节的资源（键为课程资源池条目 id）
  selectedResourceIds = signal<Set<string>>(new Set());

  // 根据开关与搜索关键字过滤章节树，保留树状结构（含匹配节点的祖先链）
  visibleChapters = computed(() => {
    const base = this.onlyWithResources()
      ? this.filterTreeWithResources(this.chapters())
      : this.chapters();
    const kw = this.chapterSearchText().trim().toLowerCase();
    if (!kw) return base;
    return this.filterTreeByKeyword(base, kw);
  });

  onOnlyWithResourcesChange(checked: boolean) {
    this.onlyWithResources.set(checked);
    // 开启过滤时重新拉取章节树，确保按最新的关联数据过滤
    if (checked) this.loadChapterTree();
  }

  // 过滤掉已在本章节关联过的资源（按资源库 ResourceId 去重）
  availableResources = computed(() => {
    const linkedIds = new Set(
      this.chapterResources()
        .map(r => r.resourceId)
        .filter((id): id is string => !!id)
    );
    const search = this.searchText().toLowerCase();
    let available = this.libraryResources().filter(
      r => !linkedIds.has(r.resourceId ?? '')
    );
    if (search) {
      available = available.filter(r =>
        r.resourceName?.toLowerCase().includes(search) ||
        r.description?.toLowerCase().includes(search)
      );
    }
    return available;
  });

  selectedCount = computed(() => this.selectedResourceIds().size);

  isAllChecked = computed(() => {
    const avail = this.availableResources();
    return avail.length > 0 && avail.every(r => this.selectedResourceIds().has(r.id));
  });

  isIndeterminate = computed(() => {
    const avail = this.availableResources();
    if (avail.length === 0) return false;
    const checked = avail.filter(r => this.selectedResourceIds().has(r.id)).length;
    return checked > 0 && checked < avail.length;
  });

  ngOnInit() {
    this.loadCourses();
  }

  loadCourses() {
    this.courseService.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe({
      next: (result) => {
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
    this.searchText.set('');
    this.chapterSearchText.set('');
    this.clearSelection();
    this.loadChapterTree();
    this.loadLibraryResources();
  }

  loadChapterTree() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.chapterService.getChapterTree(courseId).subscribe({
      next: (data) => {
        this.chapters.set(data || []);
        const expanded = new Set<string>();
        this.collectNodeIds(data || [], expanded);
        this.expandedNodes.set(expanded);
      },
    });
  }

  loadLibraryResources() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.libraryLoading.set(true);
    this.courseResourceService.getByCourse(courseId).subscribe({
      next: (data) => {
        this.libraryResources.set(data || []);
        this.libraryLoading.set(false);
      },
      error: () => {
        this.libraryLoading.set(false);
        this.message.error('加载课程资源失败');
      },
    });
  }

  private collectNodeIds(nodes: ChapterDto[], set: Set<string>) {
    for (const node of nodes) {
      if (node.id) set.add(node.id);
      if (node.children?.length) this.collectNodeIds(node.children, set);
    }
  }

  private filterTreeByKeyword(nodes: ChapterDto[], keyword: string): ChapterDto[] {
    const result: ChapterDto[] = [];
    for (const node of nodes) {
      const children = this.filterTreeByKeyword(node.children ?? [], keyword);
      if ((node.title ?? '').toLowerCase().includes(keyword) || children.length > 0) {
        result.push({ ...node, children });
      }
    }
    return result;
  }

  private filterTreeWithResources(nodes: ChapterDto[]): ChapterDto[] {
    const result: ChapterDto[] = [];
    for (const node of nodes) {
      const children = this.filterTreeWithResources(node.children ?? []);
      const hasOwn = (node.knowledgeResources?.length ?? 0) > 0;
      if (hasOwn || children.length > 0) {
        result.push({ ...node, children });
      }
    }
    return result;
  }

  selectChapter(chapter: ChapterDto) {
    this.selectedChapterId.set(chapter.id ?? null);
    this.selectedChapterTitle.set(chapter.title ?? '');
    this.clearSelection();
    this.loadChapterResources();
  }

  loadChapterResources() {
    const chapterId = this.selectedChapterId();
    if (!chapterId) return;

    this.loading.set(true);
    this.knowledgeResourceService.getByChapter(chapterId).subscribe({
      next: (data) => {
        this.chapterResources.set(data || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载章节资源失败');
      },
    });
  }

  toggleNode(nodeId: string) {
    const current = new Set(this.expandedNodes());
    if (current.has(nodeId)) {
      current.delete(nodeId);
    } else {
      current.add(nodeId);
    }
    this.expandedNodes.set(current);
  }

  isExpanded(nodeId: string): boolean {
    return this.expandedNodes().has(nodeId);
  }

  hasChildren(node: ChapterDto): boolean {
    return !!node.children && node.children.length > 0;
  }

  linkResource(resource: CourseResourceDto) {
    const chapterId = this.selectedChapterId();
    const courseId = this.selectedCourseId();
    if (!chapterId || !courseId || !resource.resourceId) return;

    // 以课程资源池条目为模板创建 KnowledgeResource，绑定到当前章节
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
      resourceId: resource.resourceId,
    };

    this.knowledgeResourceService.create(dto).subscribe({
      next: () => {
        this.message.success('资源已关联到章节');
        this.clearSelection();
        this.loadChapterResources();
        this.loadChapterTree();
      },
      error: (err) => {
        const detail =
          err?.error?.error?.message ||
          err?.error?.message ||
          err?.message ||
          '';
        this.message.error('关联失败：' + (detail || '未知错误'));
      },
    });
  }

  isSelected(resourceId: string): boolean {
    return this.selectedResourceIds().has(resourceId);
  }

  onItemChecked(resourceId: string, checked: boolean) {
    const set = new Set(this.selectedResourceIds());
    if (checked) {
      set.add(resourceId);
    } else {
      set.delete(resourceId);
    }
    this.selectedResourceIds.set(set);
  }

  onAllChecked(checked: boolean) {
    const set = new Set(this.selectedResourceIds());
    for (const resource of this.availableResources()) {
      if (checked) {
        set.add(resource.id);
      } else {
        set.delete(resource.id);
      }
    }
    this.selectedResourceIds.set(set);
  }

  clearSelection() {
    this.selectedResourceIds.set(new Set());
  }

  linkSelected() {
    const chapterId = this.selectedChapterId();
    const courseId = this.selectedCourseId();
    if (!chapterId || !courseId) return;
    const ids = [...this.selectedResourceIds()];
    if (ids.length === 0) return;

    const byId = new Map(this.libraryResources().map(r => [r.id, r]));
    const tasks = ids
      .map(id => byId.get(id))
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

    forkJoin(tasks).subscribe({
      next: () => {
        this.message.success(`已关联 ${tasks.length} 个资源到章节`);
        this.clearSelection();
        this.loadChapterResources();
        this.loadChapterTree();
      },
      error: (err) => {
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
        this.loadChapterTree();
      },
      error: (err) => {
        const detail =
          err?.error?.error?.message ||
          err?.error?.message ||
          err?.message ||
          '';
        this.message.error('取消关联失败：' + (detail || '未知错误'));
      },
    });
  }

  getResourceTypeLabel(type: ResourceType | undefined): string {
    if (type === undefined || type === null) return '';
    const map: Record<number, string> = {
      [ResourceType.Document]: '文档',
      [ResourceType.Video]: '视频',
      [ResourceType.Audio]: '音频',
      [ResourceType.Image]: '图片',
      [ResourceType.PPT]: 'PPT',
    };
    return map[type] ?? '其他';
  }

  getResourceTypeColor(type: ResourceType | undefined): string {
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
}
