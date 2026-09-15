import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { QuickNavService, QuickNavItem } from './quick-nav.service';

/**
 * 全局命令面板（Command Palette）UI。
 * 在根组件挂载一次，所有页面均可通过 `Ctrl/Cmd + K` 打开。
 *
 * 关键设计：
 * - `Ctrl/Cmd + K` 与 `Esc` 必须在 INPUT 过滤之前处理，避免焦点在表单控件中失效；
 * - 输入框 focus 通过 `setTimeout(0)` 在下一帧拿到 view child；
 * - 列表用 group 标题分组渲染，鼠标 hover 同步高亮索引。
 */
@Component({
  selector: 'app-quick-nav',
  standalone: true,
  imports: [CommonModule, FormsModule, NzIconModule],
  templateUrl: './quick-nav.component.html',
  styleUrls: ['./quick-nav.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuickNavComponent {
  readonly nav = inject(QuickNavService);
  readonly inputRef = viewChild<ElementRef<HTMLInputElement>>('quickNavInput');

  /** 按 group 分组渲染当前过滤结果（每条 entry 自带 flatIdx 用于 hover / 高亮同步）。 */
  readonly groupedResults = computed(() => {
    const flat = this.nav.filtered();
    const groups = new Map<string, { item: QuickNavItem; flatIdx: number }[]>();
    let flatIdx = 0;
    for (const entry of flat) {
      const list = groups.get(entry.item.group) ?? [];
      list.push({ item: entry.item, flatIdx });
      groups.set(entry.item.group, list);
      flatIdx++;
    }
    return Array.from(groups.entries()).map(([group, entries]) => ({
      group,
      groupIcon: entries[0].item.groupIcon,
      entries,
    }));
  });

  constructor() {
    // 面板打开时聚焦输入框。
    effect(() => {
      if (this.nav.open()) {
        setTimeout(() => {
          const input = this.inputRef()?.nativeElement;
          input?.focus();
          input?.select();
        }, 0);
      }
    });
  }

  /**
   * 全局快捷键监听：
   * - `Ctrl+K` / `Cmd+K`：打开 / 关闭面板（任意上下文，包括焦点在表单控件中）；
   * - `Esc`：关闭已打开的面板（同上）；
   * - `?`（或 Shift+`/`）：切换面板（焦点不在表单时）。
   *
   * 必须在 INPUT 过滤之前判断 Ctrl+K / Esc，否则面板打开后按 Esc 关不掉。
   */
  @HostListener('window:keydown', ['$event'])
  onGlobalKeydown(event: KeyboardEvent): void {
    const key = event.key;
    const lower = key.toLowerCase();

    if ((event.metaKey || event.ctrlKey) && lower === 'k') {
      event.preventDefault();
      this.nav.toggle();
      return;
    }
    if (event.metaKey || event.ctrlKey || event.altKey) {
      return;
    }
    if (key === 'Escape') {
      if (this.nav.open()) {
        event.preventDefault();
        this.nav.close();
      }
      return;
    }

    // 输入控件获焦时（除 Esc/Ctrl+K 之外）不让位表单。
    const target = event.target as HTMLElement | null;
    if (target) {
      const tag = target.tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || target.isContentEditable) {
        return;
      }
    }

    if (key === '?' || (event.shiftKey && lower === '/')) {
      event.preventDefault();
      this.nav.toggle();
    }
  }

  /** 输入框键盘：↑/↓/Enter/Home/End。 */
  onInputKeydown(event: KeyboardEvent): void {
    const list = this.nav.filtered();
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Enter' || event.key === 'Home' || event.key === 'End') {
      this.nav.handleKeydown(event, list);
      // 选中后滚动进可视区。
      if (event.key !== 'Enter') {
        queueMicrotask(() => this.scrollSelectedIntoView());
      }
    }
  }

  /** 鼠标 hover 同步索引（用渲染顺序中的扁平 idx）。 */
  onRowHover(flatIdx: number): void {
    this.nav.index.set(flatIdx);
  }

  /** 鼠标点击直接跳转。 */
  onRowClick(item: ReturnType<QuickNavService['filtered']>[number]['item']): void {
    this.nav.goTo(item);
  }

  /** 阻止 backdrop 关闭冒泡到面板内容。 */
  swallowEvent(event: Event): void {
    event.stopPropagation();
  }

  private scrollSelectedIntoView(): void {
    const el = document.querySelector('.quick-nav-row--active') as HTMLElement | null;
    el?.scrollIntoView({ block: 'nearest' });
  }
}