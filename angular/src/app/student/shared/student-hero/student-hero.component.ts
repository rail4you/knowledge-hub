import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

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
  /** 页面标题，与左侧导航/tab 标签一致 */
  readonly title = input('');
  /** 一句话说明（可选） */
  readonly description = input('');
  /** 右侧数据总览 */
  readonly stats = input<StudentHeroStat[]>([]);
}