import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzResultModule } from 'ng-zorro-antd/result';
import { LocalizationPipe } from '@abp/ng.core';
import { PdfViewerComponent } from './pdf-viewer.component';
import { PptxViewerComponent } from './pptx-viewer.component';
import { WordViewerComponent } from './word-viewer.component';
import { ExcelViewerComponent } from './excel-viewer.component';
import { MediaViewerComponent } from './media-viewer.component';
import { TextViewerComponent } from './text-viewer.component';

type FileType = 'pdf' | 'word' | 'excel' | 'pptx' | 'ppt' | 'image' | 'video' | 'audio' | 'text' | 'unsupported';

@Component({
  selector: 'app-file-preview',
  standalone: true,
  imports: [
    CommonModule,
    NzModalModule,
    NzButtonModule,
    NzIconModule,
    NzSpinModule,
    NzResultModule,
    LocalizationPipe,
    PdfViewerComponent,
    PptxViewerComponent,
    WordViewerComponent,
    ExcelViewerComponent,
    MediaViewerComponent,
    TextViewerComponent,
  ],
  templateUrl: './file-preview.component.html',
  styleUrls: ['./file-preview.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FilePreviewComponent {
  /** Stable object reference to prevent unnecessary ngOnChanges on nz-modal */
  readonly modalStyle = { top: '20px' };

  visible = signal(false);
  resourceId = signal('');
  resourceName = signal('');
  fileExtension = signal('');
  fileSize = signal(0);

  fileData = signal<ArrayBuffer>(new ArrayBuffer(0));
  fileUrl = signal('');
  isLoading = signal(false);
  loadError = signal('');
  /** PPTX PDF 转换失败时降级到幻灯片片段提取预览（异常文件 soffice 无法转换） */
  slideViewerMode = signal(false);
  /** P0-1 轻量版：文件过大时不走在线预览（避免前端解析卡死 / 内存爆掉），降级为"提示 + 下载"页。 */
  tooLarge = signal(false);
  /** P0-1 轻量版：文件类型不受支持时也走降级页（不强行预览）。 */
  unsupported = signal(false);

  // 在线预览大小上限：超过此大小提示用户下载。
  // pptx 不设上限（服务端按需提取单张幻灯片）
  private static readonly PREVIEW_SIZE_LIMIT: Partial<Record<FileType, number>> = {
    text: 2 * 1024 * 1024,         // 2 MB
    pdf: 25 * 1024 * 1024,         // 25 MB
    word: 25 * 1024 * 1024,        // 25 MB
    excel: 20 * 1024 * 1024,       // 20 MB
    image: 50 * 1024 * 1024,       // 50 MB
  };

  // 使用原生 fetch() 而非 Angular HttpClient/RestService，
  // 因为 fetch() 自动携带同源 cookie（ABP OIDC 认证 cookie），
  // 而 HttpClient 裸请求不走 ABP RestService 的 Bearer token 拦截器。
  // 参考：kg-edu-vite-antd FilePreview.tsx 使用 fetch() 加载文件预览数据。

  private static readonly EXTENSION_MAP: Record<string, FileType> = {
    pdf: 'pdf',
    docx: 'word', dotx: 'word', doc: 'word',
    xls: 'excel', xlsx: 'excel', csv: 'excel',
    pptx: 'pptx', potx: 'pptx', ppt: 'ppt',  // .ppt = legacy PowerPoint, converted to PDF preview
    jpg: 'image', jpeg: 'image', png: 'image', gif: 'image', bmp: 'image', webp: 'image', svg: 'image',
    mp4: 'video', webm: 'video', avi: 'video', mov: 'video',
    mp3: 'audio', wav: 'audio', ogg: 'audio', flac: 'audio',
    txt: 'text', json: 'text', xml: 'text', md: 'text', log: 'text',
    js: 'text', ts: 'text', css: 'text', html: 'text', htm: 'text',
    yml: 'text', yaml: 'text', ini: 'text', cfg: 'text', conf: 'text',
    sh: 'text', bat: 'text', py: 'text', java: 'text', c: 'text',
    cpp: 'text', h: 'text', cs: 'text', go: 'text', rs: 'text',
    sql: 'text', tsv: 'text',
  };

  get fileType(): FileType {
    // 优先使用显式传入的 fileExtension
    let ext = this.fileExtension().toLowerCase().replace('.', '');
    if (ext && FilePreviewComponent.EXTENSION_MAP[ext]) {
      return FilePreviewComponent.EXTENSION_MAP[ext];
    }
    // 回退：从 resourceName（通常是 originalFileName）提取扩展名
    const nameExt = this.extractExtension(this.resourceName());
    if (nameExt) {
      return FilePreviewComponent.EXTENSION_MAP[nameExt] || 'unsupported';
    }
    // 当 fileExtension 为空时，尝试从 Content-Type 检测（兜底）
    return 'unsupported';
  }

  /** 从文件名中提取小写无点扩展名，如 "test.docx" => "docx" */
  private extractExtension(fileName: string): string {
    if (!fileName) return '';
    const dot = fileName.lastIndexOf('.');
    if (dot >= 0 && dot < fileName.length - 1) {
      return fileName.substring(dot + 1).toLowerCase();
    }
    return '';
  }

  get fileName(): string {
    const name = this.resourceName();
    if (!name) return 'file';
    // 从 resourceName 或 fileExtension 推导扩展名，确保文件名始终带扩展名
    let ext = this.fileExtension().toLowerCase().replace('.', '');
    if (!ext) {
      ext = this.extractExtension(name);
    }
    if (!ext) return name;
    // 如果文件名已以此扩展名结尾，不重复追加
    if (name.toLowerCase().endsWith('.' + ext)) return name;
    return name + '.' + ext;
  }

  open(resourceId: string, resourceName: string, fileExtension: string, fileSize: number) {
    console.log('[FilePreview] open() called, stack:', new Error().stack);
    this.resourceId.set(resourceId);
    this.resourceName.set(resourceName);
    this.fileExtension.set(fileExtension);
    this.fileSize.set(fileSize);
    this.loadError.set('');
    this.fileData.set(new ArrayBuffer(0));
    this.fileUrl.set('');
    this.slideViewerMode.set(false);
    // P0-1 轻量版：打开前先判断大小 / 类型。
    this.tooLarge.set(false);
    this.unsupported.set(false);
    this.visible.set(true);

    // 先看是否"类型不支持"——直接降级，避免无谓的请求。
    // 但如果 fileExtension 为空，尝试从 Content-Type 检测（处理数据库扩展名为空的旧资源）
    if (this.fileType === 'unsupported' && !this.fileExtension()) {
      this.detectContentTypeAndOpen(resourceId);
      return;
    }
    if (this.fileType === 'unsupported') {
      this.unsupported.set(true);
      this.isLoading.set(false);
      return;
    }

    // 再看是否"文件过大"——超过类型对应的阈值则降级。
    const limit = FilePreviewComponent.PREVIEW_SIZE_LIMIT[this.fileType];
    if (limit && fileSize > limit) {
      this.tooLarge.set(true);
      this.isLoading.set(false);
      return;
    }

    this.loadFile();
  }

  /**
   * 当 fileExtension 为空时，通过 HEAD 请求探测 Content-Type 来推断文件类型。
   * 这样即使数据库中 fileExtension 为 null，也能正确预览旧资源（如总复习.ppt）。
   */
  private detectContentTypeAndOpen(resourceId: string) {
    this.isLoading.set(true);
    fetch(`/api/resource-file/${resourceId}/preview`, { method: 'HEAD' })
      .then((response) => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const ct = response.headers.get('Content-Type') || '';
        const ext = this.contentTypeToExtension(ct);
        if (!ext) {
          this.unsupported.set(true);
          this.isLoading.set(false);
          return;
        }
        this.fileExtension.set(ext);
        this.unsupported.set(false);
        this.isLoading.set(false);
        if (this.fileType === 'unsupported') {
          this.unsupported.set(true);
          return;
        }
        const limit = FilePreviewComponent.PREVIEW_SIZE_LIMIT[this.fileType];
        if (limit && this.fileSize() > limit) {
          this.tooLarge.set(true);
          this.isLoading.set(false);
          return;
        }
        this.loadFile();
      })
      .catch(() => {
        this.unsupported.set(true);
        this.isLoading.set(false);
      });
  }

  private contentTypeToExtension(ct: string): string {
    const map: Record<string, string> = {
      'application/pdf': 'pdf',
      'application/msword': 'doc',
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document': 'docx',
      'application/vnd.ms-excel': 'xls',
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': 'xlsx',
      'application/vnd.ms-powerpoint': 'ppt',
      'application/vnd.openxmlformats-officedocument.presentationml.presentation': 'pptx',
      'video/mp4': 'mp4',
      'audio/mpeg': 'mp3',
      'image/jpeg': 'jpg',
      'image/png': 'png',
      'text/plain': 'txt',
    };
    const base = ct.split(';')[0].trim().toLowerCase();
    return map[base] || '';
  }

  close() {
    console.log('[FilePreview] close() called, stack:', new Error().stack);
    this.visible.set(false);
    this.fileData.set(new ArrayBuffer(0));
    this.fileUrl.set('');
  }

  /** 判断预览内容是否就绪 */
  previewReady(): boolean {
    if (this.isLoading() || this.loadError() || this.tooLarge() || this.unsupported()) return false;
    const type = this.fileType;
    // PDF/PPT/PPTX: previewUrl 模式（均通过后端 PDF 转换，完整加载）
    if (type === 'pdf' || type === 'ppt' || type === 'pptx') return !!this.fileUrl();
    // Video/Audio: streamUrl 模式（不下载 ArrayBuffer）
    if (type === 'video' || type === 'audio') return !!this.fileUrl();
    // Other: ArrayBuffer 模式
    return this.fileData().byteLength > 0;
  }

  /** PDF 预览加载失败（如 .ppt 转换失败）时，降级到服务端幻灯片提取预览 */
  onPdfPreviewFailed() {
    if (this.fileType === 'pptx') {
      this.slideViewerMode.set(true);
    }
  }

  /**
   * 下载原始文件。
   * 用 fetch 校验响应：后端对未审核/无权限资源返回 JSON 403，
   * 直接 <a href> 会跟随 302 到 AccessDenied 页面，把 HTML 保存成 "4KB 错误文件"。
   * 仅当响应确实是文件内容时才触发浏览器下载。
   */
  async download() {
    if (!this.resourceId()) return;
    const url = `/api/resource-file/${this.resourceId()}/download`;
    try {
      const resp = await fetch(url);
      if (!resp.ok) {
        let msg = `下载失败（${resp.status}）`;
        try {
          const body = await resp.json();
          if (body?.message) msg = body.message;
        } catch {
          // 非 JSON 响应（如重定向后的 HTML），保持默认提示
        }
        this.loadError.set(msg);
        return;
      }

      const blob = await resp.blob();
      const a = document.createElement('a');
      const objectUrl = URL.createObjectURL(blob);
      a.href = objectUrl;
      a.download = this.fileName;
      document.body.appendChild(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
    } catch {
      this.loadError.set('下载失败，请稍后重试');
    }
  }

  private loadFile() {
    if (!this.resourceId()) return;

    this.isLoading.set(true);
    this.loadError.set('');

    const type = this.fileType;

    // PDF: 使用完整 PDF URL（支持 Range 请求逐页加载）
    if (type === 'pdf') {
      const previewUrl = `/api/resource-file/${this.resourceId()}/preview`;
      this.fileUrl.set(previewUrl);
      this.isLoading.set(false);
      return;
    }

    // PPTX/PPT: 使用完整 PDF（pdfjs 原生逐页加载，支持 Range 请求），保持原始版式。
    // 转换结果服务端缓存，源文件未变化时直接复用缓存 PDF。
    // .ppt = 旧版 PowerPoint（Composite Document），LibreOffice 支持转换。
    if (type === 'pptx' || type === 'ppt') {
      const previewUrl = `/api/resource-file/${this.resourceId()}/preview-pdf`;
      this.fileUrl.set(previewUrl);
      this.isLoading.set(false);
      return;
    }

    // Video/audio: direct server stream
    if (type === 'video' || type === 'audio') {
      const previewUrl = `/api/resource-file/${this.resourceId()}/preview`;
      this.fileUrl.set(previewUrl);
      this.isLoading.set(false);
      return;
    }

    // Word/Excel/Image/Text: 仍然通过 fetch 全量下载 ArrayBuffer
    const previewUrl = `/api/resource-file/${this.resourceId()}/preview`;

    // 使用原生 fetch() 而非 Angular HttpClient：
    // - fetch() 自动携带同源 cookie（ABP OIDC 认证 cookie 通过代理转发）
    // - 参考 kg-edu-vite-antd FilePreview.tsx 的实现方式
    fetch(previewUrl)
      .then(async (response) => {
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        const arrayBuffer = await response.arrayBuffer();
        this.fileData.set(arrayBuffer);
        this.isLoading.set(false);
      })
      .catch((err) => {
        console.error('File load error:', err);
        const msg = err instanceof Error ? err.message : '';
        if (msg.includes('403') || msg.includes('401')) {
          this.loadError.set('资源暂未审核通过或您没有访问权限');
        } else if (msg.includes('404')) {
          this.loadError.set('文件不存在，可能已被删除');
        } else {
          this.loadError.set('预览加载失败，请稍后重试');
        }
        this.isLoading.set(false);
      });
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
}
