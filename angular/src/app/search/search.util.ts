/**
 * 搜索结果展示相关的纯函数工具。
 */

/** 匹配 UUID 形式（如 3a230d5a-ecb9-9b4a-cbf6-17d3b3505353） */
const UUID_REGEX = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;

/** 匹配连续 24 位及以上的 hex 字符串（防止其他形式的 ID 残留） */
const LONG_HEX_REGEX = /\b[0-9a-f]{24,}\b/gi;

/**
 * 清理预览文本里的 UUID 与长 hex。
 * 保留 Meili 返回的高亮 `<em>` 标签结构。
 */
export function stripUuids(html: string): string {
  if (!html) return '';
  return html.replace(UUID_REGEX, '').replace(LONG_HEX_REGEX, '');
}

export function getFileIcon(ext: string | null | undefined): string {
  const map: Record<string, string> = {
    '.pdf': 'file-pdf',
    '.doc': 'file-word',
    '.docx': 'file-word',
    '.xls': 'file-excel',
    '.xlsx': 'file-excel',
    '.ppt': 'file-ppt',
    '.pptx': 'file-ppt',
    '.txt': 'file-text',
    '.md': 'file-text',
    '.jpg': 'file-image',
    '.jpeg': 'file-image',
    '.png': 'file-image'
  };
  return map[(ext || '').toLowerCase()] || 'file';
}

export function getScoreColor(score: number): string {
  if (score >= 0.8) return 'green';
  if (score >= 0.5) return 'blue';
  if (score >= 0.3) return 'orange';
  return 'red';
}
