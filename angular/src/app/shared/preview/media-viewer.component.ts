import { Component, signal, effect, OnDestroy, input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzResultModule } from 'ng-zorro-antd/result';
import { LocalizationPipe } from '@abp/ng.core';

@Component({
  selector: 'app-media-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule, NzResultModule, LocalizationPipe],
  templateUrl: './media-viewer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MediaViewerComponent implements OnDestroy {
  data = input<ArrayBuffer>();
  streamUrl = input('');
  fileName = input('');
  fileType = input<'image' | 'video' | 'audio'>('image');

  blobUrl = signal('');
  scale = signal(1);
  /** 视频播放失败（codec 不支持、源加载错误、MIME 不匹配等） */
  playbackError = signal(false);

  constructor() {
    effect(() => {
      const url = this.streamUrl();
      if (url) {
        // Streaming URL mode: use URL directly (video/audio)
        if (this.blobUrl()) {
          URL.revokeObjectURL(this.blobUrl());
        }
        this.blobUrl.set(url);
        // 切到新视频时重置错误状态
        this.playbackError.set(false);
        return;
      }

      const d = this.data();
      const name = this.fileName();
      if (d && d.byteLength > 0) {
        this.createBlobUrl(d, name);
        this.playbackError.set(false);
      }
    });
  }

  ngOnDestroy() {
    if (this.blobUrl() && !this.streamUrl()) {
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

  /**
   * 根据文件名后缀推断视频 MIME，用于 <source type="...">。
   * 已知扩展名映射为标准 MIME，未知扩展名退到 video/mp4（最常见的浏览器兼容类型）。
   * 这能让浏览器在加载资源前先做一次格式判断，比单靠 [src] 更可靠。
   */
  videoMimeType(): string {
    const name = this.fileName() || '';
    const ext = name.split('.').pop()?.toLowerCase() || '';
    const map: Record<string, string> = {
      mp4: 'video/mp4',
      m4v: 'video/mp4',
      webm: 'video/webm',
      mov: 'video/quicktime',
      avi: 'video/x-msvideo',
      mkv: 'video/x-matroska',
      ogv: 'video/ogg',
    };
    return map[ext] || 'video/mp4';
  }

  /**
   * <video> 元素的 (error) 事件回调。
   * 触发条件包括：codec 不支持、源 URL 404、MIME 与文件实际编码不一致等。
   * 触发后切换到 fallback 视图，提示用户下载到本地用播放器打开。
   */
  onVideoError(_event: Event): void {
    this.playbackError.set(true);
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
