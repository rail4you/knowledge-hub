/**
 * 下载文件名工具：确保下载文件名始终带扩展名。
 *
 * 背景：部分旧资源的 originalFileName 为空、resource.name 是"医疗"这类
 * 没有扩展名的展示名，前端直接拿它们当 <a download> 属性值时，保存下来的
 * 文件没有扩展名，双击打不开——"下载之后打不开了，格式可能有问题"。
 *
 * 现在后端 Download 接口已保证 Content-Disposition 文件名带扩展名，
 * 这里再兜底一层：只要前端能拿到任何文件名信息，就拼出一个带扩展名的名字。
 */

/**
 * 构造一个带扩展名的下载文件名。
 * - 优先用 originalFileName，其次 name；
 * - 候选名若缺少扩展名，用 fileExtension 补齐（fileExtension 可能带点也可能不带点）。
 * - 返回空字符串表示没有任何文件名信息可用（此时应交给服务器 Content-Disposition）。
 */
export function buildDownloadFileName(
  originalFileName?: string | null,
  name?: string | null,
  fileExtension?: string | null
): string {
  let base = (originalFileName || name || '').trim().replace(/[\\/]/g, '');
  if (!base) return '';

  const ext = (fileExtension || '').trim().replace(/^\./, '').toLowerCase();
  if (ext && !base.toLowerCase().endsWith('.' + ext)) {
    base += '.' + ext;
  }
  return base;
}