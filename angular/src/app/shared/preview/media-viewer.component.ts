import { Component, signal, effect, untracked, OnDestroy, input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzResultModule } from 'ng-zorro-antd/result';

/** 图片后缀 → MIME（仅图片走 blob 通道；音视频走服务器流式 URL，不在此列） */
const IMAGE_MIME: Record<string, string> = {
  jpg: 'image/jpeg', jpeg: 'image/jpeg', png: 'image/png', gif: 'image/gif',
  bmp: 'image/bmp', webp: 'image/webp', svg: 'image/svg+xml',
};

@Component({
  selector: 'app-media-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule, NzSpinModule, NzResultModule],
  templateUrl: './media-viewer.component.html',
  styleUrls: ['./media-viewer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MediaViewerComponent implements OnDestroy {
  /** 图片走全量字节（file-preview 已下载为 ArrayBuffer） */
  data = input<ArrayBuffer>();
  /** 音视频走服务器流式 URL：后端 PhysicalFile + enableRangeProcessing 支持 Range 边下边播 */
  streamUrl = input('');
  fileName = input('');
  /**
   * 视频封面图（缩略图）URL：视频未点击播放前只显示这张图 + 播放大按钮，
   * 完全不向服务端请求视频本体 —— 几百 MB 的视频在远程环境下若一打开就拉流，
   * 会长期停在“加载中”（尤其 moov 在文件尾、且反向代理未透传 Range 时）。
   */
  posterUrl = input('');
  fileType = input<'image' | 'video' | 'audio'>('image');

  /** 真正喂给 <img>/<video>/<audio> 的地址：音视频即服务端 URL，只有图片才是 blob: URL */
  mediaUrl = signal('');
  scale = signal(1);
  /** 用户已点击封面：此时才真正挂载 <video> 并开始流式加载 */
  videoStarted = signal(false);
  /** 播放失败（codec 不支持、源加载错误等）→ 切到下载引导 */
  playbackError = signal(false);
  /** 首帧元数据未就绪：期间盖一层 loading，避免大视频先闪黑框 */
  videoLoading = signal(false);

  constructor() {
    effect(() => {
      // 只跟踪这四个输入。
      const type = this.fileType();
      const stream = this.streamUrl();
      const data = this.data();
      const name = this.fileName();

      // setMediaUrl 内部读写 mediaUrl，必须 untracked：
      // 否则 effect 会依赖自己写入的信号，产生自触发循环
      // （blob 通道下表现为「建 blob → 读 blobUrl → revoke → 再建」的无限重建）。
      untracked(() => {
        this.playbackError.set(false);
        this.videoLoading.set(type === 'video');

        if (stream) {
          // 音视频：直接把服务端 URL 交给 <video>/<audio>，
          // 由浏览器用 Range 分片拉取，绝不整份下载成 ArrayBuffer。
          this.setMediaUrl(stream);
        } else if (data && data.byteLength > 0) {
          const ext = name.split('.').pop()?.toLowerCase() ?? '';
          const mime = IMAGE_MIME[ext];
          this.setMediaUrl(URL.createObjectURL(new Blob([data], mime ? { type: mime } : undefined)));
        } else {
          this.setMediaUrl('');
        }
      });
    });
  }

  ngOnDestroy() {
    this.revokeIfBlob(this.mediaUrl());
  }

  /** 切换媒体地址；只有 blob: URL 需要释放，服务端 URL 交给浏览器缓存 */
  private setMediaUrl(url: string) {
    this.revokeIfBlob(this.mediaUrl());
    this.mediaUrl.set(url);
  }

  private revokeIfBlob(url: string) {
    if (url.startsWith('blob:')) {
      URL.revokeObjectURL(url);
    }
  }

  /**
   * 元数据/首帧就绪 → 收起 loading。
   * 只在首帧前调用一次；之后的播放中缓冲由浏览器原生控制条提示，
   * 早期版本挂在 waiting/stalled 上，每次重缓冲都整屏盖黑，反而像卡死。
   */
  onMediaReady(): void {
    this.videoLoading.set(false);
  }

  onVideoError(): void {
    this.playbackError.set(true);
    this.videoLoading.set(false);
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
