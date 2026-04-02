import { Component, signal, effect, OnDestroy, input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';

@Component({
  selector: 'app-media-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule],
  templateUrl: './media-viewer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MediaViewerComponent implements OnDestroy {
  data = input.required<ArrayBuffer>();
  fileName = input('');
  fileType = input<'image' | 'video' | 'audio'>('image');

  blobUrl = signal('');
  scale = signal(1);

  constructor() {
    effect(() => {
      const d = this.data();
      const name = this.fileName();
      if (d && d.byteLength > 0) {
        this.createBlobUrl(d, name);
      }
    });
  }

  ngOnDestroy() {
    if (this.blobUrl()) {
      URL.revokeObjectURL(this.blobUrl());
    }
  }

  private createBlobUrl(data: ArrayBuffer, fileName: string) {
    if (this.blobUrl()) {
      URL.revokeObjectURL(this.blobUrl());
    }

    const ext = fileName.split('.').pop()?.toLowerCase() || '';
    const mimeMap: Record<string, string> = {
      jpg: 'image/jpeg', jpeg: 'image/jpeg', png: 'image/png', gif: 'image/gif',
      bmp: 'image/bmp', webp: 'image/webp', svg: 'image/svg+xml',
      mp4: 'video/mp4', webm: 'video/webm', avi: 'video/x-msvideo', mov: 'video/quicktime',
      mp3: 'audio/mpeg', wav: 'audio/wav', ogg: 'audio/ogg', flac: 'audio/flac',
    };

    const mimeType = mimeMap[ext] || 'application/octet-stream';
    const blob = new Blob([data], { type: mimeType });
    this.blobUrl.set(URL.createObjectURL(blob));
  }

  zoomIn() {
    this.scale.update(v => Math.min(5, v + 0.5));
  }

  zoomOut() {
    this.scale.update(v => Math.max(0.25, v - 0.5));
  }

  fitOriginal() {
    this.scale.set(1);
  }

  getScalePercent(): number {
    return Math.round(this.scale() * 100);
  }
}
