import { CertificateLayer } from '../../micro-majors/micro-major.service';

/** 画布参考宽度：图层 FontSize 以此为基础，按实际图片宽度等比缩放 */
export const REFERENCE_WIDTH = 1000;

/** 发证时填充到占位图层的真实数据 */
export interface CertificateRenderData {
  studentName?: string;
  studentNo?: string;
  advisor?: string;
  issueDate?: string;
  certificateNo?: string;
  validUntil?: string;
  microMajorTitle?: string;
  custom?: Record<string, string>;
}

/**
 * 通过后端图片代理加载图片，避免跨域 OSS 图片污染画布。
 * 返回的 HTMLImageElement 可安全地 drawImage 到 canvas。
 */
export function loadCertificateImage(imageUrl: string): Promise<HTMLImageElement> {
  const proxyUrl = buildProxiedImageUrl(imageUrl);
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('证书图片加载失败'));
    img.src = proxyUrl;
  });
}

/** 构造可跨域读取的代理图片 URL（解决 OSS 未配置 CORS 的画布污染问题） */
export function buildProxiedImageUrl(imageUrl: string): string {
  if (!imageUrl) return '';
  if (imageUrl.startsWith('/api/image-proxy')) return imageUrl;
  return `/api/image-proxy?url=${encodeURIComponent(imageUrl)}`;
}

/** 将日期解析为本地时间（避免 yyyy-MM-dd 字符串被当作 UTC 导致时区偏移一天） */
function parseLocalDate(date: Date | string | null | undefined): Date | null {
  if (!date) return null;
  if (typeof date === 'string') {
    const m = date.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (m) {
      const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
      return isNaN(d.getTime()) ? null : d;
    }
    const d = new Date(date);
    return isNaN(d.getTime()) ? null : d;
  }
  const d = new Date(date);
  return isNaN(d.getTime()) ? null : d;
}

/** 中文日期：2026年8月30日 */
export function formatChineseDate(date: Date | string | null | undefined): string {
  const d = parseLocalDate(date);
  if (!d) return '';
  return `${d.getFullYear()}年${d.getMonth() + 1}月${d.getDate()}日`;
}

/** 格式化日期输入框 value（yyyy-MM-dd） */
export function toDateInputValue(date: Date | string | null | undefined): string {
  const d = parseLocalDate(date);
  if (!d) return '';
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/** 取某个图层在发证时的真实填充值（空则不画） */
export function layerValue(layer: CertificateLayer, data: CertificateRenderData): string {
  switch (layer.fieldType) {
    case 'studentName': return data.studentName?.trim() ?? '';
    case 'studentNo': return data.studentNo?.trim() ?? '';
    case 'advisor': return data.advisor?.trim() ?? '';
    case 'issueDate': return data.issueDate || formatChineseDate(new Date());
    case 'certificateNo': return data.certificateNo?.trim() ?? '';
    case 'validUntil': return data.validUntil?.trim() ?? '';
    case 'microMajorTitle': return data.microMajorTitle?.trim() ?? '';
    case 'custom': return data.custom?.[layer.customFieldName || layer.label]?.trim() ?? '';
    default: return '';
  }
}

/** 模板编辑器里用来预览的占位示例文本（按字段类型给出接近真实长度的示例） */
export function sampleText(layer: CertificateLayer): string {
  switch (layer.fieldType) {
    case 'studentName': return '张三';
    case 'studentNo': return '20260001';
    case 'advisor': return '王教授';
    case 'issueDate': return '2026年8月30日';
    case 'certificateNo': return 'KG-MM-202608-0001';
    case 'validUntil': return '2029年8月30日';
    case 'microMajorTitle': return '人工智能微专业';
    case 'custom': return layer.customFieldName || layer.label || '自定义';
    default: return layer.label;
  }
}

/** 该字段类型的默认显示名 */
export function fieldLabel(fieldType: string): string {
  switch (fieldType) {
    case 'studentName': return '姓名';
    case 'studentNo': return '学号';
    case 'advisor': return '导师';
    case 'issueDate': return '发证时间';
    case 'certificateNo': return '证书编号';
    case 'validUntil': return '有效期';
    case 'microMajorTitle': return '微专业名称';
    case 'custom': return '自定义';
    default: return '字段';
  }
}

/** 所有可插入的占位符字段类型 */
export const CERTIFICATE_FIELD_TYPES: { value: string; label: string }[] = [
  { value: 'studentName', label: '姓名' },
  { value: 'studentNo', label: '学号' },
  { value: 'advisor', label: '导师' },
  { value: 'issueDate', label: '发证时间' },
  { value: 'certificateNo', label: '证书编号' },
  { value: 'validUntil', label: '有效期' },
  { value: 'microMajorTitle', label: '微专业名称' },
];

/** 在 canvas 上绘制证书：底层为模板图片，上层叠加各占位图层文本 */
export function drawCertificate(
  canvas: HTMLCanvasElement,
  image: HTMLImageElement,
  layers: CertificateLayer[],
  data: CertificateRenderData,
  placeholder: boolean = false,
): void {
  const w = image.naturalWidth;
  const h = image.naturalHeight;
  canvas.width = w;
  canvas.height = h;

  const ctx = canvas.getContext('2d');
  if (!ctx) return;

  ctx.clearRect(0, 0, w, h);
  ctx.drawImage(image, 0, 0, w, h);

  const scale = w / REFERENCE_WIDTH;
  for (const layer of layers) {
    let text = layerValue(layer, data);
    if (!text && placeholder) {
      text = sampleText(layer);
    }
    if (!text) continue;

    const fontSize = Math.max(1, layer.fontSize * scale);
    ctx.font = `${layer.fontWeight || 400} ${fontSize}px ${layer.fontFamily || 'sans-serif'}`;
    ctx.fillStyle = layer.color;
    ctx.textBaseline = 'middle';
    ctx.textAlign = layer.center ? 'center' : 'left';
    const x = (layer.x / 100) * w;
    const y = (layer.y / 100) * h;
    ctx.fillText(text, x, y);
  }
}

/** 将 canvas 导出为 PNG Blob */
export function canvasToBlob(canvas: HTMLCanvasElement, type: string = 'image/png'): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(blob => {
      if (blob) resolve(blob);
      else reject(new Error('导出证书图片失败'));
    }, type, 1);
  });
}
