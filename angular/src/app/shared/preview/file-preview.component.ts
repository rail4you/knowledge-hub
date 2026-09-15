import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzResultModule } from 'ng-zorro-antd/result';
import { LocalizationPipe } from '@abp/ng.core';
import { PdfViewerComponent } from './pdf-viewer.component';
import { ExcelViewerComponent } from './excel-viewer.component';
import { MediaViewerComponent } from './media-viewer.component';
import { TextViewerComponent } from './text-viewer.component';
import { createBytesCache } from '../cache/bytes-cache';

/** 图片/Excel/文本预览字节的持久化缓存（跨刷新/新标签复用，避免重复下载） */
const fileBytesCache = createBytesCache('kh-file-preview-v1');

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
  isDownloadable = signal(true);

  fileData = signal<ArrayBuffer>(new ArrayBuffer(0));
  fileUrl = signal('');
  isLoading = signal(false);
  loadError = signal('');
  /** P0-1 轻量版：文件过大时不走在线预览（避免前端解析卡死 / 内存爆掉），降级为"提示 + 下载"页。 */
  tooLarge = signal(false);
  /** P0-1 轻量版：文件类型不受支持时也走降级页（不强行预览）。 */
  unsupported = signal(false);

  // 在线预览大小上限：超过此大小提示用户下载。
  // PPT/DOC 统一由后端媒体流水线转 PDF 预览（后端另有 100MB 转换上限，超出返回 tooLarge）。
  private static readonly PREVIEW_SIZE_LIMIT: Partial<Record<FileType, number>> = {
    text: 2 * 1024 * 1024,         // 2 MB
    pdf: 25 * 1024 * 1024,         // 25 MB
    word: 500 * 1024 * 1024,       // 500 MB（后端转换，资源可控）
    excel: 20 * 1024 * 1024,       // 20 MB（SheetJS 客户端解析，超大文件卡死浏览器）
    image: 50 * 1024 * 1024,       // 50 MB
    pptx: 500 * 1024 * 1024,       // 500 MB
    ppt: 500 * 1024 * 1024,        // 500 MB
  };

  // 使用原生 fetch() 而非 Angular HttpClient/RestService，
  // 因为 fetch() 自动携带同源 cookie（ABP OIDC 认证 cookie），
  // 而 HttpClient 裸请求不走 ABP RestService 的 Bearer token 拦截器。
  // 参考：kg-edu-vite-antd FilePreview.tsx 使用 fetch() 加载文件预览数据。

  private static readonly EXTENSION_MAP: Record<string, FileType> = {
    pdf: 'pdf',
    docx: 'word', dotx: 'word', doc: 'word',
    xls: 'excel', xlsx: 'excel',
    pptx: 'pptx', potx: 'pptx', ppt: 'ppt',  // Office 文档统一由后端转 PDF 预览
    jpg: 'image', jpeg: 'image', png: 'image', gif: 'image', bmp: 'image', webp: 'image', svg: 'image',
    mp4: 'video', webm: 'video', avi: 'video', mov: 'video',
    mp3: 'audio', wav: 'audio', ogg: 'audio', flac: 'audio',
    txt: 'text', json: 'text', xml: 'text', md: 'text', log: 'text', csv: 'text', tsv: 'text',
    js: 'text', ts: 'text', css: 'text', html: 'text', htm: 'text',
    yml: 'text', yaml: 'text', ini: 'text', cfg: 'text', conf: 'text',
    sh: 'text', bat: 'text', py: 'text', java: 'text', c: 'text',
    cpp: 'text', h: 'text', cs: 'text', go: 'text', rs: 'text',
    sql: 'text',
  };

  /** Office 文档（PPT/PPTX/DOC/DOCX）：统一走后端转换的 PDF 预览（Excel 走 SheetJS 客户端渲染） */
  get isOfficeType(): boolean {
    const t = this.fileType;
    return t === 'pptx' || t === 'ppt' || t === 'word';
  }

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

  open(resourceId: string, resourceName: string, fileExtension: string, fileSize: number, isDownloadable = true) {
    this.resourceId.set(resourceId);
    this.resourceName.set(resourceName);
    this.fileExtension.set(fileExtension);
    this.fileSize.set(fileSize);
    this.isDownloadable.set(isDownloadable);
    this.loadError.set('');
    this.fileData.set(new ArrayBuffer(0));
    this.fileUrl.set('');
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
   * 当 fileExtension 为空时，先通过资源详情 API 回填文件元数据（旧资源/版本管理场景下
   * 扩展名与大小存在 VERSIONS 表中，详情接口会自动回填），再按类型走正式预览。
   * 原实现探测 HEAD /preview 的 Content-Type —— 但 ABP 全站不接受 HEAD（405），
   * 探测永远失败，导致无扩展名资源一律无法预览（如课程学习页的旧视频资源）。
   */
  private detectContentTypeAndOpen(resourceId: string) {
    this.isLoading.set(true);
    fetch(`/api/app/resource/${resourceId}`)
      .then(async (response) => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const dto = await response.json();
        let ext = (dto?.fileExtension || '').replace('.', '').toLowerCase();
        const dtoName = dto?.originalFileName || dto?.name || '';
        if (!ext) ext = this.extractExtension(dtoName);
        if (!ext) {
          // 详情接口也拿不到扩展名：按不支持处理，避免无谓请求
          this.unsupported.set(true);
          this.isLoading.set(false);
          return;
        }
        this.fileExtension.set(ext);
        if (dtoName) this.resourceName.set(dtoName);
        if (dto?.fileSize) this.fileSize.set(dto.fileSize);
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

  close() {
    this.visible.set(false);
    this.fileData.set(new ArrayBuffer(0));
    this.fileUrl.set('');
  }

  /** 判断预览内容是否就绪 */
  previewReady(): boolean {
    if (this.isLoading() || this.loadError() || this.tooLarge() || this.unsupported()) return false;
    const type = this.fileType;
    // PDF: 直接流式加载源 PDF 文件
    if (type === 'pdf') return !!this.fileUrl();
    // Office: pdf-viewer 自行轮询转换状态并流式加载，加载态由 viewer 内部展示
    if (this.isOfficeType) return true;
    // Video/Audio: streamUrl 模式（不下载 ArrayBuffer）
    if (type === 'video' || type === 'audio') return !!this.fileUrl();
    // 图片/文本: ArrayBuffer 模式
    return this.fileData().byteLength > 0;
  }

  /**
   * 下载原始文件。
   * 与资源界面 / 学习页列表保持一致：直接用 <a href> 触发浏览器原生下载，
   * 由浏览器接管下载进度条 / 取消 / 断点续传（后端 PhysicalFile 已支持 Range）。
   * 之前用 fetch 取 blob 再 createObjectURL 的做法：
   * 1) 整个文件先进入 JS 内存，大文件卡死；
   * 2) 下载完成前浏览器无任何提示，也无法取消。
   */
  download() {
    if (!this.resourceId()) return;
    if (!this.isDownloadable()) {
      this.loadError.set('该资源不允许下载，仅支持在线预览');
      return;
    }
    const url = `/api/resource-file/${this.resourceId()}/download`;
    const a = document.createElement('a');
    a.href = url;
    // fileName getter 已保证带扩展名；若为空则交给服务器 Content-Disposition
    const name = this.fileName;
    if (name) a.download = name;
    document.body.appendChild(a);
    a.click();
    a.remove();
  }

  private loadFile() {
    if (!this.resourceId()) return;

    this.isLoading.set(true);
    this.loadError.set('');

    const type = this.fileType;

    // PDF: 直接流式加载源 PDF（/preview 支持 Range）
    if (type === 'pdf') {
      const previewUrl = `/api/resource-file/${this.resourceId()}/preview`;
      this.fileUrl.set(previewUrl);
      this.isLoading.set(false);
      return;
    }

    // Office（PPT/PPTX/DOC/DOCX）: 统一走后端媒体流水线转换的 PDF。
    // pdf-viewer 轮询 /preview-pdf-info 等待转换就绪（loading 状态），
    // 就绪后流式加载 /preview-pdf。未转换完成时不触发额外请求，等后端处理即可。
    if (this.isOfficeType) {
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

    // 图片/Excel/文本: 通过 fetch 全量下载 ArrayBuffer（Excel 交给 SheetJS 客户端渲染）
    const previewUrl = `/api/resource-file/${this.resourceId()}/preview`;

    // 使用原生 fetch() 而非 Angular HttpClient：
    // - fetch() 自动携带同源 cookie（ABP OIDC 认证 cookie 通过代理转发）
    // - 参考 kg-edu-vite-antd FilePreview.tsx 的实现方式
    // 持久化缓存：重复预览同一文件直接命中本地缓存，避免重复下载（图片/文档可达数十 MB）
    this.loadPreviewArrayBuffer(previewUrl)
      .then((arrayBuffer) => {
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

  /** 带持久化缓存的预览字节加载；命中缓存时用 1 字节 Range 请求保留后端浏览量统计 */
  private async loadPreviewArrayBuffer(url: string): Promise<ArrayBuffer> {
    const cached = await fileBytesCache.read(url);
    if (cached && cached.byteLength > 0) {
      void fetch(url, { headers: { Range: 'bytes=0-0' } }).catch(() => { /* 计数失败不影响预览 */ });
      return cached;
    }
    const response = await fetch(url);
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    const arrayBuffer = await response.arrayBuffer();
    void fileBytesCache.store(url, new Uint8Array(arrayBuffer.slice(0)));
    return arrayBuffer;
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
}
