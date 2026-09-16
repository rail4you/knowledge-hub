import { ChangeDetectionStrategy, Component, effect, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { loadStatSnapshot, saveStatSnapshot, StatSnapshotItem } from '../../../shared/cache/stat-snapshot';

export interface StudentHeroStat {
  label: string;
  value: number;
  suffix?: string;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-student-hero',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './student-hero.component.html',
  styleUrls: ['./student-hero.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentHeroComponent {
  /** 页面标题，与左侧导航/tab 标签一致（同时作为统计快照的缓存 key） */
  readonly title = input('');
  /** 一句话说明（可选） */
  readonly description = input('');
  /** 右侧数据总览 */
  readonly stats = input<StudentHeroStat[]>([]);

  /** 实际展示的统计：数据加载期间回显上次快照，避免「从 0 闪变到实际数值」 */
  readonly displayStats = signal<StudentHeroStat[]>([]);

  constructor() {
    effect(() => {
      const incoming = this.stats();
      const cached = loadStatSnapshot(this.title());
      // 数据已就绪（任意一项非 0）：直接展示真实值并持久化缓存
      if (incoming.some((s) => (s?.value ?? 0) !== 0)) {
        this.displayStats.set(incoming);
        saveStatSnapshot(this.title(), incoming as StatSnapshotItem[]);
        return;
      }
      // 数据仍为空/加载中：有缓存则回显缓存消除 0 闪变，无缓存则显示实际（0）
      this.displayStats.set((cached as StudentHeroStat[] | null) ?? incoming);
    });
  }
}
