import { ChangeDetectionStrategy, Component, Input, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface RadarAxis {
  name: string;
  value: number;
  max?: number;
}

@Component({
  selector: 'app-mastery-radar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="radar">
      <svg
        class="radar__svg"
        viewBox="0 0 300 300"
        preserveAspectRatio="xMidYMid meet"
        aria-label="知识掌握度雷达图"
      >
        <defs>
          <linearGradient [attr.id]="gradId" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stop-color="#1e6ce8" stop-opacity="0.45" />
            <stop offset="100%" stop-color="#00b7ff" stop-opacity="0.18" />
          </linearGradient>
        </defs>

        @for (ring of rings(); track $index) {
          <polygon
            [attr.points]="ring"
            fill="none"
            stroke="#dde4ee"
            stroke-width="1"
            stroke-dasharray="2 3"
          />
        }

        @for (axis of axisLines(); track $index) {
          <line
            [attr.x1]="cx"
            [attr.y1]="cy"
            [attr.x2]="axis.x"
            [attr.y2]="axis.y"
            stroke="#dde4ee"
            stroke-width="1"
          />
        }

        @if (dataPolygon()) {
          <polygon
            [attr.points]="dataPolygon()!"
            [attr.fill]="'url(#' + gradId + ')'"
            stroke="#1e6ce8"
            stroke-width="2"
            stroke-linejoin="round"
          />
        }

        @for (pt of dataPoints(); track $index) {
          <circle
            [attr.cx]="pt.x"
            [attr.cy]="pt.y"
            r="4"
            fill="#fff"
            stroke="#1e6ce8"
            stroke-width="2"
          />
        }

        @for (lbl of labelPositions(); track $index) {
          <text
            [attr.x]="lbl.x"
            [attr.y]="lbl.y"
            [attr.text-anchor]="lbl.anchor"
            dy="0.3em"
            class="radar__label"
          >{{ lbl.text }}</text>
        }

        @for (val of valueLabels(); track $index) {
          @if (val.show) {
            <text
              [attr.x]="val.x"
              [attr.y]="val.y"
              [attr.text-anchor]="val.anchor"
              class="radar__value"
            >{{ val.text }}</text>
          }
        }
      </svg>

      @if (dataList().length === 0) {
        <div class="radar__empty">
          <span>暂无掌握度数据</span>
        </div>
      }
    </div>
  `,
  styleUrls: ['./mastery-radar.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MasteryRadarComponent {
  @Input() set data(v: RadarAxis[]) {
    this._data.set(v || []);
  }
  get data(): RadarAxis[] {
    return this._data();
  }
  protected _data = signal<RadarAxis[]>([]);

  @Input() showValues = true;

  readonly cx = 150;
  readonly cy = 150;
  readonly radius = 110;

  protected static instance = 0;
  protected readonly gradId = `radarGrad-${++MasteryRadarComponent.instance}`;

  protected readonly dataList = this._data;

  rings = computed<string[]>(() => {
    const layers = 5;
    const points: string[] = [];
    for (let layer = 1; layer <= layers; layer++) {
      const r = (this.radius * layer) / layers;
      const arr: string[] = [];
      const count = this._data().length || 6;
      for (let i = 0; i < count; i++) {
        const angle = this.getAngle(i, count);
        const x = this.cx + r * Math.cos(angle);
        const y = this.cy + r * Math.sin(angle);
        arr.push(`${x.toFixed(1)},${y.toFixed(1)}`);
      }
      points.push(arr.join(' '));
    }
    return points;
  });

  axisLines = computed<{ x: number; y: number }[]>(() => {
    const count = this._data().length || 6;
    return Array.from({ length: count }, (_, i) => {
      const angle = this.getAngle(i, count);
      return {
        x: this.cx + this.radius * Math.cos(angle),
        y: this.cy + this.radius * Math.sin(angle),
      };
    });
  });

  dataPolygon = computed<string | null>(() => {
    const data = this._data();
    if (data.length === 0) return null;
    const points: string[] = [];
    data.forEach((d, i) => {
      const angle = this.getAngle(i, data.length);
      const max = d.max || 100;
      const r = (this.radius * Math.max(0, Math.min(d.value, max))) / max;
      const x = this.cx + r * Math.cos(angle);
      const y = this.cy + r * Math.sin(angle);
      points.push(`${x.toFixed(1)},${y.toFixed(1)}`);
    });
    return points.join(' ');
  });

  dataPoints = computed<{ x: number; y: number }[]>(() => {
    const data = this._data();
    if (data.length === 0) return [];
    return data.map((d, i) => {
      const angle = this.getAngle(i, data.length);
      const max = d.max || 100;
      const r = (this.radius * Math.max(0, Math.min(d.value, max))) / max;
      return {
        x: this.cx + r * Math.cos(angle),
        y: this.cy + r * Math.sin(angle),
      };
    });
  });

  labelPositions = computed<{ x: number; y: number; text: string; anchor: string }[]>(() => {
    const data = this._data();
    if (data.length === 0) {
      return Array.from({ length: 6 }, (_, i) => {
        const angle = this.getAngle(i, 6);
        const r = this.radius + 18;
        return {
          x: this.cx + r * Math.cos(angle),
          y: this.cy + r * Math.sin(angle),
          text: '维度' + (i + 1),
          anchor: this.getAnchor(angle),
        };
      });
    }
    return data.map((d, i) => {
      const angle = this.getAngle(i, data.length);
      const r = this.radius + 18;
      return {
        x: this.cx + r * Math.cos(angle),
        y: this.cy + r * Math.sin(angle),
        text: d.name.length > 6 ? d.name.slice(0, 6) + '..' : d.name,
        anchor: this.getAnchor(angle),
      };
    });
  });

  valueLabels = computed<{ x: number; y: number; text: string; anchor: string; show: boolean }[]>(() => {
    if (!this.showValues) {
      return this.dataPoints().map(() => ({ x: 0, y: 0, text: '', anchor: 'middle', show: false }));
    }
    const data = this._data();
    return data.map((d, i) => {
      const angle = this.getAngle(i, data.length);
      const max = d.max || 100;
      const r = (this.radius * Math.max(0, Math.min(d.value, max))) / max + 12;
      return {
        x: this.cx + r * Math.cos(angle),
        y: this.cy + r * Math.sin(angle),
        text: Math.round(d.value) + '',
        anchor: this.getAnchor(angle),
        show: true,
      };
    });
  });

  private getAngle(i: number, count: number): number {
    return -Math.PI / 2 + (2 * Math.PI * i) / count;
  }

  private getAnchor(angle: number): 'start' | 'middle' | 'end' {
    const cos = Math.cos(angle);
    if (cos > 0.3) return 'start';
    if (cos < -0.3) return 'end';
    return 'middle';
  }
}
