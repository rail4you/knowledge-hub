/** AI 生成的题目选项可能自带编号前缀（如 "A. xxx"、"B．xxx"、"C) xxx"），
 * 而各端点渲染时又会统一加上 key（如 "A."），导致出现 "A. A. xxx" 之类的重复前缀。
 * 这里把 content 开头多余的点号/编号标记清理掉。 */
export function cleanOptionContent(content: string): string {
  let c = (content || '').trim();
  if (!c) return c;
  // 反复剥掉开头的选项标记："A." "A．" "A、" "A)" "A）" "A:" "A：" "A〕"
  // 只剥最多 3 层，避免误删真实内容
  for (let i = 0; i < 3; i++) {
    const next = c.replace(/^[A-Za-z]\s*[\.、．。）)〕：:]/, '');
    if (next === c) break;
    c = next.trim();
  }
  return c;
}