import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import { KnowledgeResourceService } from '../../proxy/courses/knowledge-resource.service';
import { ResourceService } from '../../proxy/resources/resource.service';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import type { CourseDto, ChapterDto } from '../../proxy/courses/dtos/models';
import type { CreateUpdateKnowledgeResourceDto, KnowledgeResourceDto } from '../../proxy/courses/dtos/models';
import type { ResourceDto } from '../../proxy/resources/models';

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
    NzSelectModule,
  ],
  templateUrl: './chapter-resource.component.html',
  styleUrls: ['./chapter-resource.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChapterResourceComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);
  private readonly knowledgeResourceService = inject(KnowledgeResourceService);
  private readonly resourceService = inject(ResourceService);
  private readonly message = inject(NzMessageService);

  courses = signal<CourseDto[]>([]);
  selectedCourseId = signal<string | null>(null);
  chapters = signal<ChapterDto[]>([]);
  expandedNodes = signal<Set<string>>(new Set());
  selectedChapterId = signal<string | null>(null);
  selectedChapterTitle = signal('');

  chapterResources = signal<KnowledgeResourceDto[]>([]);
  // 资源库中「联盟审核通过」的资源
  libraryResources = signal<ResourceDto[]>([]);
  loading = signal(false);
  libraryLoading = signal(false);
  searchText = signal('');

  // 过滤掉已在本章节关联过的资源（按名称去重，因为同一份资源在库只出现一次）
  availableResources = computed(() => {
    const linkedNames = new Set(
      this.chapterResources()
        .map(r => (r.name ?? '').trim().toLowerCase())
        .filter(n => !!n)
    );
    const search = this.searchText().toLowerCase();
    let available = this.libraryResources().filter(
      r => !linkedNames.has((r.name ?? '').trim().toLowerCase())
    );
    if (search) {
      available = available.filter(r =>
        r.name?.toLowerCase().includes(search) ||
        r.description?.toLowerCase().includes(search)
      );
    }
    return available;
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
    this.selectedCourseId.set(courseId);
    this.selectedChapterId.set(null);
    this.selectedChapterTitle.set('');
    this.chapterResources.set([]);
    this.searchText.set('');
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
    this.libraryLoading.set(true);
    this.resourceService.getLeagueApproved({ skipCount: 0, maxResultCount: 1000 }).subscribe({
      next: (result) => {
        this.libraryResources.set(result.items || []);
        this.libraryLoading.set(false);
      },
      error: () => {
        this.libraryLoading.set(false);
        this.message.error('加载资源库失败');
      },
    });
  }

  private collectNodeIds(nodes: ChapterDto[], set: Set<string>) {
    for (const node of nodes) {
      if (node.id) set.add(node.id);
      if (node.children?.length) this.collectNodeIds(node.children, set);
    }
  }

  selectChapter(chapter: ChapterDto) {
    this.selectedChapterId.set(chapter.id ?? null);
    this.selectedChapterTitle.set(chapter.title ?? '');
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

  linkResource(resource: ResourceDto) {
    const chapterId = this.selectedChapterId();
    const courseId = this.selectedCourseId();
    if (!chapterId || !courseId) return;

    // 以资源库条目为模板创建 KnowledgeResource，绑定到当前章节
    const dto: CreateUpdateKnowledgeResourceDto = {
      courseId: courseId,
      chapterId: chapterId,
      name: resource.name ?? '',
      description: resource.description ?? '',
      content: resource.filePath ?? '',
      importanceLevel: 'normal',
      difficulty: 1,
      sortOrder: 0,
      tags: resource.keywords ?? '',
      parentId: null,
    };

    this.knowledgeResourceService.create(dto).subscribe({
      next: () => {
        this.message.success('资源已关联到章节');
        this.loadChapterResources();
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

  getImportanceLabel(level: string | undefined): string {
    const map: Record<string, string> = {
      core: '核心',
      important: '重要',
      normal: '一般',
      extended: '拓展',
    };
    return map[level ?? 'normal'] ?? '一般';
  }

  getImportanceColor(level: string | undefined): string {
    const map: Record<string, string> = {
      core: 'red',
      important: 'orange',
      normal: 'blue',
      extended: 'default',
    };
    return map[level ?? 'normal'] ?? 'default';
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
