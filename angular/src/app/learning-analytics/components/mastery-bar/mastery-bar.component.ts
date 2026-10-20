import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MasteryLevel } from '../../models/learning-analytics.models';

/**
 * 知识点掌握度横条（带教学目标标尺）。
 * 学生端/教师端共用，颜色阈值：<60% 薄弱 / <75% 待巩固 / ≥75% 达标。
 */
@Component({
  selector: 'app-mastery-bar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './mastery-bar.component.html',
  styleUrls: ['./mastery-bar.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MasteryBarComponent {
  readonly name = input.required<string>();
  /** 掌握度 0~1 */
  readonly value = input.required<number>();
  /** 教学目标线 0~1（0 表示不显示标尺） */
  readonly target = input<number>(0.75);
  /** 有效作答量，用于「数据不足」提示 */
  readonly evidence = input<number>(0);
  /** 等级覆盖（后端已判定时优先使用） */
  readonly level = input<MasteryLevel | null>(null);
  /** 是否显示「薄弱」文字标记 */
  readonly showWeakLabel = input<boolean>(true);
  /** 是否为预测值 */
  readonly predicted = input<boolean>(false);

  readonly percent = computed(() => Math.round((this.value() ?? 0) * 100));

  readonly resolvedLevel = computed<MasteryLevel>(() => {
    if (this.level() !== null && this.level() !== undefined) return this.level() as MasteryLevel;
    const v = this.value() ?? 0;
    return v < 0.6 ? MasteryLevel.Weak : v < 0.75 ? MasteryLevel.Fair : MasteryLevel.Ok;
  });

  readonly levelClass = computed(() => {
    switch (this.resolvedLevel()) {
      case MasteryLevel.Weak:
        return 'weak';
      case MasteryLevel.Fair:
        return 'fair';
      default:
        return 'ok';
    }
  });

  readonly targetPercent = computed(() => (this.target() > 0 ? Math.round(this.target() * 100) : 0));

  /** 数据不足（作答量 < 2 时提示，避免误读） */
  readonly insufficient = computed(() => this.evidence() > 0 && this.evidence() < 2);
}
