/**
 * 根据字符串稳定映射到调色板颜色：同一 key 始终返回同一颜色。
 * 用于课程/资讯/实训等封面渐变占位（无封面图时）。
 */
export function hashGradient(key: string, palettes: readonly string[]): string {
  if (!palettes.length) return '';
  let hash = 0;
  const k = key || 'x';
  for (let i = 0; i < k.length; i++) {
    hash = (hash * 31 + k.charCodeAt(i)) | 0;
  }
  return palettes[Math.abs(hash) % palettes.length];
}

/**
 * 课程封面柔和配色 —— 与课程列表页 `.cover.c0~c3` 完全一致的四种底色/文字色对。
 * 按课程标题哈希稳定取色：同一门课在列表、详情、学习页颜色恒定。
 * 无真实封面图时用它替代深色大色块。
 */
export const COURSE_COVER_PALETTES = [
  { bg: 'var(--kh-cover-1-bg)', ink: 'var(--kh-cover-1-ink)' },
  { bg: 'var(--kh-cover-2-bg)', ink: 'var(--kh-cover-2-ink)' },
  { bg: 'var(--kh-cover-3-bg)', ink: 'var(--kh-cover-3-ink)' },
  { bg: 'var(--kh-cover-4-bg)', ink: 'var(--kh-cover-4-ink)' },
] as const;

/** 按 key（课程标题优先）取一套课程封面配色 */
export function courseCoverPalette(key: string | null | undefined): { bg: string; ink: string } {
  const list = COURSE_COVER_PALETTES;
  const k = key || 'x';
  let sum = 0;
  for (let i = 0; i < k.length; i++) sum = (sum + k.charCodeAt(i)) % list.length;
  return list[sum];
}
