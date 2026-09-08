/**
 * CJK 友好 Markdown 预处理。
 *
 * 背景：marked（ngx-markdown 底层）严格遵循 CommonMark 的左右毗邻
 * （left/right-flanking）规则。当 `**加粗**` 的内外紧贴 CJK 字符，
 * 且加粗内容内部含有标点（如 `**文本?**`），或内容首尾带空格
 * （如 `**文本 **`）时，marked 拒绝解析为 <strong>，原样输出 `**`。
 * 而 Typora / GitHub 等常用编辑器对此更宽松，用户会认为是渲染异常。
 *
 * 复现：这是**文本?**测试 → marked 原样输出，期望 <strong>文本?</strong>
 *
 * 策略：在送给 marked 解析之前，把纯文本型的 `**...**` / `***...***`
 * 直接转成内联 HTML（<strong>），marked 会原样透传内联 HTML。
 * 为避免回归，跳过以下情况（留给 marked 原生处理）：
 * - 围栏代码块 ```...``` / ~~~...~~~ 与行内代码 `...` 内部
 * - 加粗内容内部含有完整 Markdown 结构（链接/图片/代码等）
 */
export function fixCjkMarkdown(src: string | null | undefined): string {
  if (!src || !src.includes('**')) {
    return src ?? '';
  }

  // 按代码片段切分：奇数段是代码（保护），偶数段是正文（处理）。
  const parts = src.split(/(```[\s\S]*?```|~~~[\s\S]*?~~~|`[^`\n]+?`)/g);

  for (let i = 0; i < parts.length; i += 2) {
    let text = parts[i];
    if (!text || !text.includes('**')) {
      continue;
    }

    // ***粗斜体*** 先处理，避免被 ** 规则切开。
    text = text.replace(/\*\*\*([^*\n]+?)\*\*\*/g, (m, inner: string) => {
      const value = inner.trim();
      if (!value || /[[!\]()<>~#]/.test(inner)) {
        return m;
      }
      return `<strong><em>${value}</em></strong>`;
    });

    // **加粗**：只处理纯文本内容（含 CJK / 标点 / 空格），首尾空格做 trim
    // （兼容 `**文本 **` 这类常用编辑器可渲染但 CommonMark 非法的写法）。
    text = text.replace(/\*\*([^*\n]+?)\*\*/g, (m, inner: string) => {
      const value = inner.trim();
      if (!value || /[[!\]()<>~#_]/.test(inner) || inner.includes('`')) {
        return m;
      }
      return `<strong>${value}</strong>`;
    });

    parts[i] = text;
  }

  return parts.join('');
}
