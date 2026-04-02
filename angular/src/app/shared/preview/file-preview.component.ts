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
  isLoading = signal(false);
  loadError = signal('');

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
    this.visible.set(true);

    this.loadFile();
  }

  close() {
    this.visible.set(false);
    this.fileData.set(new ArrayBuffer(0));
  }

  download() {
    if (!this.resourceId()) return;
    this.resourceService.download(this.resourceId()).subscribe({
      next: (data: any) => {
        const arrayBuffer = this.toArrayBuffer(data);
        const ext = this.fileExtension().toLowerCase().replace('.', '');
        const mimeMap: Record<string, string> = {
          pdf: 'application/pdf', docx: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
          xlsx: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
          pptx: 'application/vnd.openxmlformats-officedocument.presentationml.presentation',
        };
        const blob = new Blob([arrayBuffer], { type: mimeMap[ext] || 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = this.fileName;
        a.click();
        URL.revokeObjectURL(url);
      },
    });
  }

  private toArrayBuffer(data: any): ArrayBuffer {
    if (typeof data === 'string') {
      const binary = atob(data);
      const buffer = new ArrayBuffer(binary.length);
      const view = new Uint8Array(buffer);
      for (let i = 0; i < binary.length; i++) {
        view[i] = binary.charCodeAt(i);
      }
      return buffer;
    }
    // If it's already an ArrayBuffer, return directly
    if (data instanceof ArrayBuffer) return data;
    // Otherwise convert from typed array
    return new Uint8Array(data).buffer.slice(0) as ArrayBuffer;
  }

  private loadFile() {
    if (!this.resourceId()) return;

    this.isLoading.set(true);
    this.loadError.set('');

    this.resourceService.download(this.resourceId()).subscribe({
      next: (data: any) => {
        const arrayBuffer = this.toArrayBuffer(data);
        this.fileData.set(arrayBuffer);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('File load error:', err);
        this.loadError.set('Failed to load file for preview');
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
