import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type KpiTone = 'default' | 'primary' | 'weak' | 'fair' | 'ok';

/**
 * 指标卡（学生端成绩卡 / 教师端 KPI）。
 * 大数字 + 单位 + 标签 + 可选提示与图标。
 */
@Component({
  selector: 'app-analysis-kpi',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="kpi" [class]="'tone-' + tone()">
      <div class="kpi__head">
        @if (label()) {
          <span class="kpi__label">{{ label() }}</span>
        }
        @if (hint()) {
          <span class="kpi__hint">{{ hint() }}</span>
        }
      </div>
      <div class="kpi__value">
        <b>{{ value() }}</b>
        @if (suffix()) {
          <i>{{ suffix() }}</i>
        }
      </div>
    </div>
  `,
  styleUrls: ['./analysis-kpi.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnalysisKpiComponent {
  readonly label = input<string>('');
  readonly value = input.required<string | number>();
  readonly suffix = input<string>('');
  readonly hint = input<string>('');
  readonly tone = input<KpiTone>('default');
}
