import { buildProxiedImageUrl } from './certificate-canvas.util';

/**
 * 下载证书图片（课程证书 / 微专业证书通用）。
 *
 * <para>
 * 证书图存在 OSS（跨域），`a[download]` 对跨域资源无效（浏览器会直接打开图片），
 * 因此统一走「fetch → blob → objectURL → a[download]」；
 * 又因 OSS 未配置 CORS，先尝试直连，失败再经站内图片代理 `/api/image-proxy` 转发。
 * </para>
 *
 * @param imageUrl 证书图片地址
 * @param fileName 下载文件名（含扩展名）
 * @returns 是否已触发下载（false 表示调用方需要回退处理）
 */
export async function downloadCertificateImage(imageUrl: string, fileName: string): Promise<boolean> {
  if (!imageUrl) {
    return false;
  }

  const candidates = [imageUrl];
  const proxied = buildProxiedImageUrl(imageUrl);
  if (proxied !== imageUrl) {
    candidates.push(proxied);
  }

  for (const url of candidates) {
    try {
      const res = await fetch(url, { credentials: 'omit' });
      if (!res.ok) {
        continue;
      }
      const blob = await res.blob();
      if (!blob.size) {
        continue;
      }
      triggerBlobDownload(blob, fileName);
      return true;
    } catch {
      // 直连失败（跨域 / 网络）→ 试下一个候选地址
    }
  }

  return false;
}

/** 用 blob 触发浏览器下载 */
export function triggerBlobDownload(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 5000);
}

/**
 * 证书文件名：`<课程或微专业名称>-证书-<证书编号>.png`
 * 非法字符（/ \ : * ? " < > |）统一替换为下划线。
 */
export function buildCertificateFileName(title: string, certificateNo?: string | null, extension = 'png'): string {
  const safeTitle = (title || '证书').replace(/[\\/:*?"<>|]/g, '_').trim();
  const safeNo = (certificateNo || '').replace(/[\\/:*?"<>|]/g, '_').trim();
  const base = safeNo ? `${safeTitle}-${safeNo}` : safeTitle;
  return `${base}.${extension}`;
}
