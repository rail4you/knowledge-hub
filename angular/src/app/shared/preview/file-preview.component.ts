import { Component, signal, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzResultModule } from 'ng-zorro-antd/result';
import { LocalizationPipe, EnvironmentService } from '@abp/ng.core';
import { ResourceService } from '../../proxy/resources';
import { PdfViewerComponent } from './pdf-viewer.component';
import { WordViewerComponent } from './word-viewer.component';
import { ExcelViewerComponent } from './excel-viewer.component';
import { PptxViewerComponent } from './pptx-viewer.component';
import { MediaViewerComponent } from './media-viewer.component';
import { TextViewerComponent } from './text-viewer.component';

type FileType = 'pdf' | 'word' | 'excel' | 'pptx' | 'image' | 'video' | 'audio' | 'text' | 'unsupported';

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
    WordViewerComponent,
    ExcelViewerComponent,
    PptxViewerComponent,
    MediaViewerComponent,
    TextViewerComponent,
  ],
  templateUrl: './file-preview.component.html',
  styleUrls: ['./file-preview.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FilePreviewComponent {
  visible = signal(false);
  resourceId = signal('');
  resourceName = signal('');
  fileExtension = signal('');
  fileSize = signal(0);

  fileData = signal<ArrayBuffer>(new ArrayBuffer(0));
  fileUrl = signal('');
  isLoading = signal(false);
  loadError = signal('');
  /** P0-1 轻量版：文件过大时不走在线预览（避免前端解析卡死 / 内存爆掉），降级为"提示 + 下载"页。 */
  tooLarge = signal(false);
  /** P0-1 轻量版：文件类型不受支持时也走降级页（不强行预览）。 */
  unsupported = signal(false);

  // 在线预览大小上限：超过此大小提示用户下载。
  // docx/pdf/pptx 按 25MB 统一限制，图片/视频/音频流式播放不设严格上限。
  private static readonly PREVIEW_SIZE_LIMIT: Partial<Record<FileType, number>> = {
    text: 2 * 1024 * 1024,         // 2 MB
    pdf: 25 * 1024 * 1024,         // 25 MB
    word: 25 * 1024 * 1024,        // 25 MB
    excel: 20 * 1024 * 1024,       // 20 MB
    pptx: 25 * 1024 * 1024,        // 25 MB
    image: 50 * 1024 * 1024,       // 50 MB
  };

  private readonly http = inject(HttpClient);
  private readonly resourceService = inject(ResourceService);
  private readonly environmentService = inject(EnvironmentService);

  private static readonly EXTENSION_MAP: Record<string, FileType> = {
    pdf: 'pdf',
    docx: 'word', dotx: 'word',
    xls: 'excel', xlsx: 'excel', csv: 'excel',
    pptx: 'pptx', potx: 'pptx',
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
    const ext = this.fileExtension().toLowerCase().replace('.', '');
    return FilePreviewComponent.EXTENSION_MAP[ext] || 'unsupported';
  }

  get fileName(): string {
    const ext = this.fileExtension().toLowerCase().replace('.', '');
    return this.resourceName() || `file.${ext}`;
  }

  open(resourceId: string, resourceName: string, fileExtension: string, fileSize: number) {
    this.resourceId.set(resourceId);
    this.resourceName.set(resourceName);
    this.fileExtension.set(fileExtension);
    this.fileSize.set(fileSize);
    this.loadError.set('');
    this.fileData.set(new ArrayBuffer(0));
    this.fileUrl.set('');
    // P0-1 轻量版：打开前先判断大小 / 类型。
    this.tooLarge.set(false);
    this.unsupported.set(false);
    this.visible.set(true);

    // 先看是否"类型不支持"——直接降级，避免无谓的请求。
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

  close() {
    this.visible.set(false);
    this.fileData.set(new ArrayBuffer(0));
    this.fileUrl.set('');
  }

  download() {
    if (!this.resourceId()) return;
    const url = `/api/resource-file/${this.resourceId()}/download`;
    const a = document.createElement('a');
    a.href = url;
    a.download = this.fileName;
    a.click();
  }

  private loadFile() {
    if (!this.resourceId()) return;

    this.isLoading.set(true);
    this.loadError.set('');

    const type = this.fileType;

    // Video and audio: use streaming URL for native browser playback
    if (type === 'video' || type === 'audio') {
      this.resourceService.getFileUrl(this.resourceId()).subscribe({
        next: (url: string) => {
          const env = this.environmentService.getEnvironment();
          const baseUrl = env?.apis?.default?.url || '';
          const fullUrl = url.startsWith('http') ? url : baseUrl + url;
          this.fileUrl.set(fullUrl);
          this.isLoading.set(false);
        },
        error: (err) => {
          console.error('Failed to get file URL:', err);
          this.loadError.set('预览加载失败，请稍后重试');
          this.isLoading.set(false);
        },
      });
      return;
    }

    // 所有要解析的文件（pdf / word / excel / pptx / image / text）都走 fetch 拿到 ArrayBuffer
    const previewUrl = `/api/resource-file/${this.resourceId()}/preview`;
    this.http.get(previewUrl, { responseType: 'arraybuffer' }).subscribe({
      next: (arrayBuffer: ArrayBuffer) => {
        this.fileData.set(arrayBuffer);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('File load error:', err);
        this.loadError.set('预览加载失败，请稍后重试');
        this.isLoading.set(false);
      },
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
