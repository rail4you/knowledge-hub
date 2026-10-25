/**
 * ECharts 主题桥接。
 *
 * ECharts 画在 canvas 上，拿不到 CSS 变量，因此这里从 `:root` 读取
 * 设计令牌（`src/styles/_design-tokens.scss`）后传给图表配置。
 * 这样组件 SCSS / TS 里都不需要再出现硬编码色值。
 */
export interface ChartPalette {
  accent: string;
  accentDeep: string;
  accentLight: string;
  accentSoft: string;
  gold: string;
  ok: string;
  line: string;
  panel: string;
  ink: string;
  muted: string;
}

function readVar(styles: CSSStyleDeclaration, name: string): string {
  return styles.getPropertyValue(name).trim();
}

/** 读取当前生效的调色板；令牌缺失时返回空串，调用方据此跳过绘图 */
export function readChartPalette(): ChartPalette {
  const styles = getComputedStyle(document.documentElement);
  return {
    accent: readVar(styles, '--kh-accent'),
    accentDeep: readVar(styles, '--kh-accent-deep'),
    accentLight: readVar(styles, '--kh-accent-light'),
    accentSoft: readVar(styles, '--kh-accent-soft'),
    gold: readVar(styles, '--kh-gold'),
    ok: readVar(styles, '--kh-ok'),
    line: readVar(styles, '--kh-line'),
    panel: readVar(styles, '--kh-panel'),
    ink: readVar(styles, '--kh-ink'),
    muted: readVar(styles, '--kh-muted'),
  };
}
