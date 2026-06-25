import { provideAppInitializer } from '@angular/core';

/**
 * 在全局样式中注入侧边栏分割线 CSS。
 * 通过 route.provider.ts 中的 order 值为每组之间添加分隔线。
 */
export const SIDEBAR_DIVIDER_STYLES = [
  provideAppInitializer(() => {
    const style = document.createElement('style');
    style.textContent = `
      /* ── 侧边栏栏目分割线 ── */
      .lpx-navbar-item {
        position: relative;
      }
      /* order=1: 资源管理 → order=2: AI 管理之间的分割线 */
      .lpx-navbar-item[data-order="2"]::before,
      .lpx-navbar-item[data-order="3"]::before,
      .lpx-navbar-item[data-order="4"]::before,
      .lpx-navbar-item[data-order="5"]::before {
        content: '';
        display: block;
        height: 1px;
        margin: 8px 16px;
        background: var(--lpx-border-color, #e2e8f0);
      }

      /* ── 栏目名称样式 ── */
      .lpx-navbar-item > .lpx-menu-item-link > .lpx-menu-item-text {
        font-size: 11px;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.5px;
        opacity: 0.7;
      }
    `;
    document.head.appendChild(style);
  }),
];
