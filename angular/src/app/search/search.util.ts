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

/**
 * 匹配类型
 * - content: 关键词在正文 / 标题中有高亮命中（最佳匹配）
 * - name:    正文没命中，但资源名命中了关键词
 * - fuzzy:   仅分数接近，没有明显命中
 */
export type MatchType = 'content' | 'name' | 'fuzzy';

export interface MatchInfo {
  type: MatchType;
  /** 资源名里命中关键词后的 HTML（高亮 `<em>` 包裹） */
  highlightedName: string;
}

/**
 * 计算一条搜索结果的匹配信息：
 * - 检测 highlightedContent / eventDescription 中是否有 <mark>
 * - 如果没有，再检测资源名是否包含搜索词（区分大小写忽略）
 * - 都没有则视为模糊匹配
 */
export function getMatchInfo(
  resourceName: string,
  highlightedContent: string | null | undefined,
  eventDescription: string | null | undefined,
  query: string
): MatchInfo {
  const q = (query || '').trim();
  const hc = highlightedContent || '';
  const ed = eventDescription || '';

  const hasContentHighlight = /<mark[\s>]/i.test(hc) || /<mark[\s>]/i.test(ed);

  if (hasContentHighlight) {
    return { type: 'content', highlightedName: escapeHtml(resourceName) };
  }

  const nameHighlighted = highlightInText(resourceName, q);
  if (nameHighlighted && nameHighlighted !== escapeHtml(resourceName)) {
    return { type: 'name', highlightedName: nameHighlighted };
  }

  return { type: 'fuzzy', highlightedName: escapeHtml(resourceName) };
}

function escapeHtml(text: string): string {
  return (text || '')
    .replace(/&/g, '&')
    .replace(/</g, '<')
    .replace(/>/g, '>');
}

/**
 * 在文本里查找关键词并用 <em> 包裹。
 * - 关键词按空白拆分后逐个匹配
 * - 不区分大小写，保留原文大小写
 */
export function highlightInText(text: string, query: string): string {
  const escaped = escapeHtml(text || '');
  if (!query || !query.trim()) return escaped;

  const tokens = query
    .trim()
    .split(/\s+/)
    .filter(t => t.length > 0)
    .sort((a, b) => b.length - a.length); // 长 token 优先，避免被短的覆盖

  let result = escaped;
  for (const token of tokens) {
    if (!token) continue;
    const safe = token.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const re = new RegExp(safe, 'gi');
    result = result.replace(re, m => `<em>${m}</em>`);
  }
  return result;
}

/**
 * 按资源名折叠结果：
 * - 第一个出现的 resourceName 保留
 * - 之后出现的同名（按 resourceId 同组）保留 pageNumber 最小的那条
 * - 其余同名的折叠掉
 *
 * 返回去重后的 results 与被折叠数量。
 */
export function foldByResourceName<
  T extends { resourceId: string; pageNumber: number; resourceName?: string }
>(results: T[]): { folded: T[]; hiddenCount: number } {
  const seen = new Map<string, T>();
  let hiddenCount = 0;
  for (const item of results) {
    const key = item.resourceId || item.resourceName || '';
    if (!key) continue;
    const existing = seen.get(key);
    if (!existing) {
      seen.set(key, item);
    } else if (item.pageNumber < existing.pageNumber) {
      seen.set(key, item);
      hiddenCount++;
    } else {
      hiddenCount++;
    }
  }
  return { folded: Array.from(seen.values()), hiddenCount };
}
