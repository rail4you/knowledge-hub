import { Component, signal, inject, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DragDropModule, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
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
    DragDropModule,
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
    // 关键修复：直接用 chapter 自身的 courseId，而不是 selectedCourseId() ?? ''。
    // 旧实现在 selectedCourseId 为 null 时会发空字符串到后端，触发 Guid 解析失败，
    // 前端仅显示笼统的"更新失败"，用户（包括开发者）无法定位原因。
    const courseId = (chapter as any).courseId || this.selectedCourseId() || '';
    this.formData = {
      courseId,
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
          error: (err) => this.showApiError(err, '删除失败'),
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
    // 兜底：courseId 为空时拒绝发送，避免后端 Guid 解析失败
    if (!this.formData.courseId) {
      this.message.error('课程 ID 缺失，请重新打开编辑窗口');
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
      error: (err) => {
        this.saving = false;
        this.showApiError(err, this.isEdit ? '更新失败' : '创建失败');
      },
    });
  }

  /**
   * 统一提取并展示后端真实错误。
   * 旧实现 error: () => {...} 丢弃了 err，前端消息只显示"更新失败"，
   * 导致像 "PN结与MOSFET" 这种带特殊字符的章节保存失败时，
   * 用户和开发者都无法定位真正原因（可能是 Guid 解析失败、长度超限、外键冲突等）。
   * 现在把后端 message 透传出来，必要时再附 console 详细堆栈。
   */
  private showApiError(err: any, fallback: string): void {
    const detail =
      err?.error?.error?.message ||
      err?.error?.message ||
      err?.message ||
      '未知错误';
    console.error('[ChapterManagement]', fallback, err);
    this.message.error(`${fallback}：${detail}`);
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

  // P1-21：教师端章节详情面板展示知识点列表时使用的辅助方法（标签 + 配色）。
  // 与学生端 student-course-learn 保持一致，方便用户视觉上对应起来。
  importanceLabel(level?: string): string {
    const map: Record<string, string> = {
      core: '核心',
      important: '重要',
      normal: '一般',
      extended: '拓展',
    };
    return map[level || 'normal'] || '一般';
  }

  importanceColor(level?: string): string {
    const map: Record<string, string> = {
      core: '#ef4444',
      important: '#f59e0b',
      normal: '#1e6ce8',
      extended: '#10b981',
    };
    return map[level || 'normal'] || '#1e6ce8';
  }

  // Drag and drop for chapter reordering
  onChapterDrop(event: CdkDragDrop<ChapterDto[]>, parentId: string | null = null) {
    // Don't do anything if dropped in the same position
    if (event.previousIndex === event.currentIndex && event.previousContainer === event.container) {
      return;
    }

    const chaptersCopy = this.deepCloneChapters(this.chapters());
    
    // Find the source and target containers in the tree
    const sourceNodes = this.findNodeList(chaptersCopy, event.previousContainer.id);
    const targetNodes = event.container === event.previousContainer 
      ? sourceNodes 
      : this.findNodeList(chaptersCopy, event.container.id);

    if (!sourceNodes || !targetNodes) {
      this.loadChapterTree(); // Reload if we can't find the nodes
      return;
    }

    // Move the item
    const movedNode = sourceNodes.splice(event.previousIndex, 1)[0];
    
    if (event.previousContainer === event.container) {
      // Same container - just reorder
      targetNodes.splice(event.currentIndex, 0, movedNode);
    } else {
      // Different container - move to new parent
      targetNodes.splice(event.currentIndex, 0, movedNode);
      
      // Update parent ID
      const oldParentId = event.previousContainer.id === 'root' ? null : event.previousContainer.id;
      const newParentId = event.container.id === 'root' ? null : event.container.id;
      
      // Find the moved node and update its parent
      this.updateNodeParent(chaptersCopy, movedNode.id!, newParentId);
    }

    // Update local state
    this.chapters.set(chaptersCopy);
    this.cdr.markForCheck();

    // Sync order to server
    this.syncChapterOrderAfterDrop(targetNodes, event.container.id === 'root' ? null : event.container.id);
  }

  private deepCloneChapters(nodes: ChapterDto[]): ChapterDto[] {
    return nodes.map(node => ({
      ...node,
      children: node.children ? this.deepCloneChapters(node.children) : []
    }));
  }

  private findNodeList(nodes: ChapterDto[], containerId: string): ChapterDto[] | null {
    if (containerId === 'root' || containerId === '') {
      return nodes;
    }

    for (const node of nodes) {
      if (node.id === containerId) {
        return node.children || [];
      }
      if (node.children?.length) {
        const found = this.findNodeList(node.children, containerId);
        if (found) return found;
      }
    }
    return null;
  }

  private updateNodeParent(nodes: ChapterDto[], nodeId: string, newParentId: string | null) {
    for (const node of nodes) {
      if (node.id === nodeId) {
        node.parentId = newParentId;
        return;
      }
      if (node.children?.length) {
        this.updateNodeParent(node.children, nodeId, newParentId);
      }
    }
  }

  private syncChapterOrderAfterDrop(nodes: ChapterDto[], parentId: string | null) {
    const orders = nodes.map((node, index) => ({
      chapterId: node.id!,
      sortOrder: index * 10
    }));

    this.chapterService.reorderChapters(orders).subscribe({
      next: () => {
        // Order saved successfully
        this.message.success('章节顺序已更新');
      },
      error: () => {
        this.message.error('保存顺序失败');
        this.loadChapterTree();
      }
    });
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
