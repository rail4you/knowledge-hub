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
