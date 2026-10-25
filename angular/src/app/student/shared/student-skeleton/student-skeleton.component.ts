import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * 学生端骨架屏。
 *
 * 原型（new_design）完全没有加载态，本组件为补齐：
 *   - loading：数据请求中
 *   - empty  ：无数据 / 筛选无结果 / 无权限（见 StudentEmptyComponent）
 *   - error  ：服务失败（见 StudentErrorComponent）
 *
 * 用法：<app-student-skeleton [variant]="'list'" [rows]="4" />
 */
export type StudentSkeletonVariant = 'text' | 'cards' | 'list' | 'stats' | 'detail';

@Component({
  selector: 'app-student-skeleton',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './student-skeleton.component.html',
  styleUrls: ['./student-skeleton.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentSkeletonComponent {
  /** 骨架形态 */
  readonly variant = input<StudentSkeletonVariant>('text');
  /** list / text 形态下的行数 */
  readonly rows = input(3);
  /** cards 形态下的卡片数 */
  readonly cards = input(3);
}
