import { Component, signal, inject, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterService } from '../../proxy/courses/chapter.service';
import type { CourseDto, ChapterDto, CreateUpdateChapterDto, ChapterImportResultDto } from '../../proxy/courses/dtos/models';
import { ChapterMindMapComponent } from './chapter-mind-map/chapter-mind-map.component';

@Component({
  selector: 'app-chapter-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzModalModule,
    NzFormModule,
    NzInputNumberModule,
    ChapterMindMapComponent,
  ],
  templateUrl: './chapter-management.component.html',
  styleUrls: ['./chapter-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChapterManagementComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly chapterService = inject(ChapterService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly cdr = inject(ChangeDetectorRef);

  courses = signal<CourseDto[]>([]);
  selectedCourseId = signal<string | null>(null);
  chapters = signal<ChapterDto[]>([]);
  selectedChapter = signal<ChapterDto | null>(null);
  loading = signal(false);
  expandedNodes = signal<Set<string>>(new Set());
  isMindMapVisible = signal(false);

  // Modal state
  isModalVisible = false;
  isEdit = false;
  editId: string | null = null;
  saving = false;
  formData: CreateUpdateChapterDto = this.emptyForm();

  ngOnInit() {
    this.loadCourses();
  }

  private emptyForm(): CreateUpdateChapterDto {
    return {
      courseId: '',
      parentId: null,
      title: '',
      description: '',
      sortOrder: 0,
    };
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
    this.selectedChapter.set(null);
    this.loadChapterTree();
  }

  loadChapterTree() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.chapterService.getChapterTree(courseId).subscribe({
      next: (data) => {
        this.chapters.set(data || []);
        // Auto-expand all nodes
        const expanded = new Set<string>();
        this.collectNodeIds(data || [], expanded);
        this.expandedNodes.set(expanded);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
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
    this.selectedChapter.set(chapter);
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

  // Flatten chapters for parent selection dropdown
  getFlatChapters(): { id: string; title: string; depth: number }[] {
    const result: { id: string; title: string; depth: number }[] = [];
    const walk = (nodes: ChapterDto[], depth: number) => {
      for (const node of nodes) {
        if (node.id && node.title) {
          result.push({ id: node.id, title: node.title, depth });
        }
        if (node.children?.length) walk(node.children, depth + 1);
      }
    };
    walk(this.chapters(), 0);
    return result;
  }

  openCreateRootModal() {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    this.isEdit = false;
    this.editId = null;
    this.formData = {
      courseId,
      parentId: null,
      title: '',
      description: '',
      sortOrder: 0,
    };
    this.isModalVisible = true;
  }

  openCreateChildModal(parentChapter: ChapterDto) {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.isEdit = false;
    this.editId = null;
    this.formData = {
      courseId,
      parentId: parentChapter.id ?? null,
      title: '',
      description: '',
      sortOrder: 0,
    };
    this.isModalVisible = true;
  }

  openEditModal(chapter: ChapterDto) {
    this.isEdit = true;
    this.editId = chapter.id ?? null;
    this.formData = {
      courseId: this.selectedCourseId() ?? '',
      parentId: chapter.parentId ?? null,
      title: chapter.title ?? '',
      description: chapter.description ?? '',
      sortOrder: chapter.sortOrder ?? 0,
    };
    this.isModalVisible = true;
  }

  deleteChapter(chapter: ChapterDto) {
    this.modal.confirm({
      nzTitle: '确认删除',
      nzContent: `确定要删除章节「${chapter.title}」吗？子章节也会一并删除。`,
      nzOkText: '删除',
      nzOkDanger: true,
      nzCancelText: '取消',
      nzOnOk: () => {
        if (!chapter.id) return;
        this.chapterService.delete(chapter.id).subscribe({
          next: () => {
            this.message.success('章节已删除');
            if (this.selectedChapter()?.id === chapter.id) {
              this.selectedChapter.set(null);
            }
            this.loadChapterTree();
          },
          error: () => {
            this.message.error('删除失败');
          },
        });
      },
    });
  }

  handleModalCancel() {
    this.isModalVisible = false;
  }

  handleModalOk() {
    if (!this.formData.title?.trim()) {
      this.message.warning('请输入章节名称');
      return;
    }

    this.saving = true;
    const request = this.isEdit && this.editId
      ? this.chapterService.update(this.editId, this.formData)
      : this.chapterService.create(this.formData);

    request.subscribe({
      next: () => {
        this.saving = false;
        this.isModalVisible = false;
        this.message.success(this.isEdit ? '章节更新成功' : '章节创建成功');
        this.loadChapterTree();
      },
      error: () => {
        this.saving = false;
        this.message.error(this.isEdit ? '更新失败' : '创建失败');
      },
    });
  }

  openMindMap() {
    if (this.chapters().length === 0) {
      this.message.warning('当前课程暂无章节数据');
      return;
    }
    this.isMindMapVisible.set(true);
  }

  closeMindMap() {
    this.isMindMapVisible.set(false);
  }

  getSelectedCourseTitle(): string {
    const courseId = this.selectedCourseId();
    const course = this.courses().find(c => c.id === courseId);
    return course?.title ?? '';
  }

  // Import modal state
  isImportModalVisible = false;
  importing = false;
  selectedImportFile: File | null = null;

  openImportModal() {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    this.isImportModalVisible = true;
    this.selectedImportFile = null;
  }

  closeImportModal() {
    this.isImportModalVisible = false;
    this.selectedImportFile = null;
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];
      const isExcel = file.name.endsWith('.xlsx') || file.name.endsWith('.xls');
      if (!isExcel) {
        this.message.error('只能上传 Excel 文件 (.xlsx, .xls)');
        return;
      }
      const isLt10M = file.size / 1024 / 1024 < 10;
      if (!isLt10M) {
        this.message.error('文件大小不能超过 10MB');
        return;
      }

      // 保存文件引用
      this.selectedImportFile = file;
      this.cdr.markForCheck();
    }
  }

  handleImport() {
    if (!this.selectedImportFile) {
      this.message.warning('请先选择文件');
      return;
    }

    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.importing = true;
    const file = this.selectedImportFile;

    const formData = new FormData();
    formData.append('file', file, file.name);

    this.chapterService.importFromExcel(courseId, formData as any).subscribe({
      next: (result: ChapterImportResultDto) => {
        this.importing = false;
        if (result.failCount === 0) {
          this.message.success(`导入成功！共导入 ${result.successCount} 个章节`);
          this.closeImportModal();
          this.loadChapterTree();
        } else {
          const errorMsg = result.errors?.slice(0, 5).join('\n') || '';
          this.message.warning(`导入完成：成功 ${result.successCount}，失败 ${result.failCount}\n${errorMsg}`);
        }
      },
      error: (err) => {
        this.importing = false;
        this.message.error('导入失败: ' + (err.message || '未知错误'));
      },
    });
  }
}
