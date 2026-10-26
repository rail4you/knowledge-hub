import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzIconModule } from 'ng-zorro-antd/icon';

export interface LibraryPickerOption {
  id: string;
  name: string;
  /** 0 私有 / 1 公开 / 2 院校共建（决定图标） */
  visibility?: number | null;
  /** 库内课程数（实训场景用于提示） */
  courseCount?: number | null;
}

/**
 * 教学资源库多选器（表格 + checkbox）。
 *
 * <p>课程 / 实训表单共用：一个内容可同时关联多个库，可勾选也可取消。
 * 图标按库可见性区分：私有 lock / 公开 team / 共建 apartment。</p>
 */
@Component({
  selector: 'app-library-picker',
  standalone: true,
  imports: [CommonModule, FormsModule, NzCheckboxModule, NzIconModule],
  templateUrl: './library-picker.component.html',
  styleUrls: ['./library-picker.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LibraryPickerComponent {
  @Input() options: LibraryPickerOption[] = [];
  @Input() selected: string[] = [];
  @Input() hint = '可多选；不勾选任何资源库即视为「未关联」。';
  @Input() emptyText = '暂无可关联的资源库';
  @Input() showCourseCount = false;

  @Output() selectedChange = new EventEmitter<string[]>();

  isChecked(id: string): boolean {
    return (this.selected || []).includes(id);
  }

  toggle(id: string, checked: boolean): void {
    const current = this.selected || [];
    const next = checked
      ? Array.from(new Set([...current, id]))
      : current.filter((x) => x !== id);
    this.selectedChange.emit(next);
  }

  iconOf(option: LibraryPickerOption): string {
    switch (option.visibility) {
      case 1:
        return 'team';
      case 2:
        return 'apartment';
      default:
        return 'lock';
    }
  }

  kindLabel(option: LibraryPickerOption): string {
    switch (option.visibility) {
      case 1:
        return '公开资源库 · 本校全体教师可见';
      case 2:
        return '院校共建资源库 · 主建校与关联校共享';
      default:
        return '私有资源库 · 仅被分配的教师可见';
    }
  }
}
