import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import type { ChapterDto } from '../../proxy/courses/dtos/models';

/**
 * 章节多选（表格版）。
 *
 * <p>交互：左侧复选框逐级联动（勾父节点自动带上全部子章节，勾子节点父节点显示半选），
 * 章节以「嵌套表格」逐级缩进展示（与章节管理页一致），
 * 顶部可搜索章节标题 / 描述，并控制展开层级。</p>
 *
 * <p>用法：父组件持有选中集合，通过 [selectedIds] 传入，
 * 选中变化时通过 (selectedChange) 事件回写；组件内部不持久化选中状态。</p>
 */
@Component({
  selector: 'app-chapter-picker',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCheckboxModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTooltipModule,
  ],
  templateUrl: './chapter-picker.component.html',
  styleUrls: ['./chapter-picker.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChapterPickerComponent {
  /** 章节树（根节点数组，含 children） */
  readonly chapters = input<ChapterDto[]>([]);
  /** 当前已选章节 id（父组件持有） */
  readonly selectedIds = input<string[]>([]);
  /** 加载中 */
  readonly loading = input(false);
  /** 无数据时的提示文案 */
  readonly emptyText = input('该课程暂无章节');
  /** 表格区最大高度（px），弹窗内可调小 */
  readonly maxHeight = input(380);
  /** 是否显示工具条（搜索 + 展开层级） */
  readonly showToolbar = input(true);

  /** 选中变化：回传新的 id 数组 */
  readonly selectedChange = output<string[]>();

  readonly keyword = signal('');
  readonly expandedIds = signal<Set<string>>(new Set());

  constructor() {
    // 章节树加载完成后默认「展开全部」（与章节管理页一致）；
    // 展开状态由组件自己维护，父组件只负责选中集合。
    effect(() => {
      this.chapters();
      untracked(() => {
        if (this.expandLevel() === -1) this.expandAll();
      });
    });
  }

  /**
   * 展开层级（下拉列表控制）：
   * 0 = 仅一级，1 = 展开到二级，2 = 展开到三级，-1 = 展开全部
   */
  readonly expandLevel = signal<0 | 1 | 2 | -1>(-1);

  readonly isFiltering = computed(() => this.keyword().trim() !== '');

  /** 搜索过滤后的章节树（保留命中节点的祖先链） */
  readonly visibleChapters = computed(() => {
    const kw = this.keyword().trim().toLowerCase();
    const all = this.chapters();
    if (!kw) return all;
    const prune = (nodes: ChapterDto[]): ChapterDto[] => {
      const out: ChapterDto[] = [];
      for (const n of nodes || []) {
        const kids = n.children?.length ? prune(n.children) : [];
        const hitTitle = (n.title ?? '').toLowerCase().includes(kw);
        const hitDesc = (n.description ?? '').toLowerCase().includes(kw);
        if (hitTitle || hitDesc || kids.length > 0) {
          out.push({ ...n, children: kids });
        }
      }
      return out;
    };
    return prune(all);
  });

  /** 全部章节总数（含子章节） */
  readonly totalCount = computed(() => this.countNodes(this.chapters()));

  readonly selectedCount = computed(() => this.selectedIds().length);

  private countNodes(nodes: ChapterDto[] | undefined): number {
    let count = 0;
    for (const n of nodes || []) {
      count++;
      count += this.countNodes(n.children);
    }
    return count;
  }

  private collectIds(nodes: ChapterDto[] | undefined, out: string[]): void {
    for (const n of nodes || []) {
      if (n.id) out.push(n.id);
      this.collectIds(n.children, out);
    }
  }

  /** 当前可见章节 id（含子孙） */
  readonly visibleChapterIds = computed(() => {
    const ids: string[] = [];
    this.collectIds(this.visibleChapters(), ids);
    return ids;
  });

  readonly isAllVisibleChecked = computed(() => {
    const ids = this.visibleChapterIds();
    if (ids.length === 0) return false;
    const sel = new Set(this.selectedIds());
    return ids.every(id => sel.has(id));
  });

  readonly isVisibleIndeterminate = computed(() => {
    const ids = this.visibleChapterIds();
    if (ids.length === 0) return false;
    const sel = new Set(this.selectedIds());
    const some = ids.some(id => sel.has(id));
    return some && !ids.every(id => sel.has(id));
  });

  hasChildren(node: ChapterDto): boolean {
    return !!node.children && node.children.length > 0;
  }

  isExpanded(id: string | undefined): boolean {
    return !!id && this.expandedIds().has(id);
  }

  toggleExpand(id: string | undefined): void {
    if (!id) return;
    const set = new Set(this.expandedIds());
    if (set.has(id)) set.delete(id);
    else set.add(id);
    this.expandedIds.set(set);
  }

  onExpandLevelChange(level: 0 | 1 | 2 | -1): void {
    this.expandLevel.set(level);
    if (level === -1) this.expandAll();
    else if (level === 0) this.expandedIds.set(new Set());
    else this.expandToDepth(level);
  }

  expandAll(): void {
    const ids: string[] = [];
    const walk = (nodes: ChapterDto[] | undefined) => {
      for (const n of nodes || []) {
        if (this.hasChildren(n) && n.id) ids.push(n.id);
        walk(n.children);
      }
    };
    walk(this.chapters());
    this.expandedIds.set(new Set(ids));
  }

  /** 按层级展开：展开深度 < depth 的所有父节点 */
  expandToDepth(depth: number): void {
    const ids = new Set<string>();
    const walk = (nodes: ChapterDto[] | undefined, d: number) => {
      for (const n of nodes || []) {
        if (d < depth && this.hasChildren(n) && n.id) ids.add(n.id);
        walk(n.children, d + 1);
      }
    };
    walk(this.chapters(), 0);
    this.expandedIds.set(ids);
  }

  /** 某节点（含全部子孙）是否全选 */
  isChapterChecked(node: ChapterDto): boolean {
    const ids: string[] = [];
    this.collectIds([node], ids);
    if (ids.length === 0) return false;
    const sel = new Set(this.selectedIds());
    return ids.every(id => sel.has(id));
  }

  /** 某节点是否部分选中 */
  isChapterIndeterminate(node: ChapterDto): boolean {
    const ids: string[] = [];
    this.collectIds([node], ids);
    if (ids.length === 0) return false;
    const sel = new Set(this.selectedIds());
    const some = ids.some(id => sel.has(id));
    return some && !ids.every(id => sel.has(id));
  }

  toggleChapter(node: ChapterDto, checked: boolean): void {
    const ids: string[] = [];
    this.collectIds([node], ids);
    const set = new Set(this.selectedIds());
    if (checked) ids.forEach(id => set.add(id));
    else ids.forEach(id => set.delete(id));
    this.selectedChange.emit([...set]);
  }

  /** 表头勾选框：全选 / 取消全选当前可见章节 */
  toggleAllVisible(checked: boolean): void {
    const set = new Set(this.selectedIds());
    if (checked) this.visibleChapterIds().forEach(id => set.add(id));
    else this.visibleChapterIds().forEach(id => set.delete(id));
    this.selectedChange.emit([...set]);
  }

  /** 清空已选 */
  clearAll(): void {
    this.selectedChange.emit([]);
  }

  trackChapter = (_: number, n: ChapterDto): string => n.id ?? '';
}
