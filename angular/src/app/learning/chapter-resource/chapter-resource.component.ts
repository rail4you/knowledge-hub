import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed, ChangeDetectorRef } from '@angular/core';
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
import type { CourseDto, ChapterDto } from '../../proxy/courses/dtos/models';
import type { CreateUpdateKnowledgeResourceDto, KnowledgeResourceDto } from '../../proxy/courses/dtos/models';

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
  private readonly message = inject(NzMessageService);
  private readonly cdr = inject(ChangeDetectorRef);

  courses = signal<CourseDto[]>([]);
  selectedCourseId = signal<string | null>(null);
  chapters = signal<ChapterDto[]>([]);
  expandedNodes = signal<Set<string>>(new Set());
  selectedChapterId = signal<string | null>(null);
  selectedChapterTitle = signal('');

  chapterResources = signal<KnowledgeResourceDto[]>([]);
  courseResources = signal<KnowledgeResourceDto[]>([]);
  loading = signal(false);
  searchText = signal('');

  // Resources available to add: course resources not linked to any chapter
  availableResources = computed(() => {
    const linked = new Set(this.chapterResources().map(r => r.id));
    const search = this.searchText().toLowerCase();
    let available = this.courseResources().filter(r => !linked.has(r.id));
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
    this.loadCourseResources();
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

  loadCourseResources() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.knowledgeResourceService.getByCourse(courseId).subscribe({
      next: (data) => {
        this.courseResources.set(data || []);
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

  linkResource(resource: KnowledgeResourceDto) {
    const chapterId = this.selectedChapterId();
    if (!chapterId || !resource.id) return;

    const dto: CreateUpdateKnowledgeResourceDto = {
      courseId: resource.courseId,
      chapterId: chapterId,
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
        this.message.success('资源已关联到章节');
        this.loadChapterResources();
      },
      error: () => {
        this.message.error('关联失败');
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
      error: () => {
        this.message.error('取消关联失败');
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

  getDifficultyLabel(difficulty: number | undefined): string {
    if (difficulty === undefined || difficulty === null) return '';
    const map: Record<number, string> = {
      1: '入门',
      2: '基础',
      3: '进阶',
      4: '高级',
      5: '专家',
    };
    return map[difficulty] ?? '';
  }
}
