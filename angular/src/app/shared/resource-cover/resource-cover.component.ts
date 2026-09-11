import {
  Component,
  ChangeDetectionStrategy,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';

type CoverMode = 'video' | 'image' | 'pdf' | 'icon';

/**
 * 资源封面：视频用首帧缩略图，图片直接显示，PDF 渲染第一页，
 * 其他类型（Office 文档/音频等）显示调用方传入的 fallback 内容（ng-content）。
 *
 * - 进入视口才加载（IntersectionObserver），列表页不会一次刷出十几个请求；
 * - 缩略图请求带 countView=false，不计入浏览量；
 * - 加载失败自动回退到 fallback。
 */
@Component({
  selector: 'app-resource-cover',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './resource-cover.component.html',
  styleUrls: ['./resource-cover.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceCoverComponent {
  readonly resourceId = input('');
  readonly resourceType = input<number>(ResourceType.Document);
  /** 用于判断扩展名（originalFileName 优先） */
  readonly fileName = input('');
  readonly alt = input('');

  private readonly destroyRef = inject(DestroyRef);
  private readonly hostRef = viewChild<ElementRef<HTMLElement>>('host');
  private readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('pdfCanvas');

  private observer: IntersectionObserver | null = null;
  private pdfDoc: { destroy: () => void } | null = null;
  private destroyed = false;

  /** 小写无点扩展名 */
  private readonly ext = computed(() => {
    const name = this.fileName() || '';
    const dot = name.lastIndexOf('.');
    return dot >= 0 ? name.substring(dot + 1).toLowerCase() : '';
  });

  readonly mode = computed<CoverMode>(() => {
    if (!this.resourceId()) return 'icon';
    if (this.resourceType() === ResourceType.Video) return 'video';
    if (this.resourceType() === ResourceType.Image) return 'image';
    // 只有原生 PDF 才做第一页渲染：Office 文档的第一页需要服务端转换，
    // 列表页每张卡片都触发转换会压垮 Gotenberg，这里直接回退图标。
    if (this.ext() === 'pdf') return 'pdf';
    return 'icon';
  });

  /** 进入视口后才加载真实媒体 */
  readonly loadMedia = signal(false);
  readonly failed = signal(false);
  /** 缩略图加载失败后回退原图/视频 */
  readonly thumbFailed = signal(false);
  readonly pdfReady = signal(false);

  readonly previewUrl = computed(() =>
    this.resourceId() ? `/api/resource-file/${this.resourceId()}/preview?countView=false` : ''
  );

  /** 服务端生成的缩略图（图片缩放 / 视频抽帧），列表封面优先使用，避免下载原文件 */
  readonly thumbnailUrl = computed(() =>
    this.resourceId() ? `/api/resource-file/${this.resourceId()}/thumbnail?w=400` : ''
  );

  constructor() {
    // 输入变化时重置状态并重新监听（组件被复用时）
    effect(() => {
      const el = this.hostRef()?.nativeElement;
      // 显式订阅输入
      const rid = this.resourceId();
      this.resourceType();
      this.fileName();
      this.failed.set(false);
      this.thumbFailed.set(false);
      this.pdfReady.set(false);
      this.loadMedia.set(false);
      this.destroyPdf();
      if (el && rid) {
        this.observe(el, rid);
      }
    });
    this.destroyRef.onDestroy(() => {
      this.destroyed = true;
      this.disconnect();
      this.destroyPdf();
    });
  }

  onMediaError() {
    this.failed.set(true);
  }

  /** 缩略图加载失败：图片回退原图，视频回退 <video> */
  onThumbError() {
    if (this.thumbFailed()) {
      this.failed.set(true);
      return;
    }
    this.thumbFailed.set(true);
  }

  private observe(el: HTMLElement, rid: string) {
    this.disconnect();
    // 不支持 IntersectionObserver 的环境直接加载
    if (typeof IntersectionObserver === 'undefined') {
      this.handleVisible(rid);
      return;
    }
    this.observer = new IntersectionObserver(
      entries => {
        if (entries.some(e => e.isIntersecting)) {
          this.handleVisible(rid);
          this.disconnect();
        }
      },
      { rootMargin: '200px' }
    );
    this.observer.observe(el);
  }

  private handleVisible(rid: string) {
    if (this.destroyed || rid !== this.resourceId()) return;
    this.loadMedia.set(true);
    if (this.mode() === 'pdf') {
      // 等 canvas 挂载后再渲染
      setTimeout(() => this.renderPdfFirstPage(rid), 0);
    }
  }

  private async renderPdfFirstPage(rid: string) {
    try {
      const canvas = this.canvasRef()?.nativeElement;
      const host = this.hostRef()?.nativeElement;
      if (!canvas || !host) return;
      const pdfjsLib = await import('pdfjs-dist');
      pdfjsLib.GlobalWorkerOptions.workerSrc = 'assets/pdfjs/pdf.worker.min.mjs';
      if (this.destroyed || rid !== this.resourceId()) return;
      const doc = await pdfjsLib.getDocument({ url: this.previewUrl(), withCredentials: true }).promise;
      if (this.destroyed || rid !== this.resourceId()) {
        doc.destroy();
        return;
      }
      this.destroyPdf();
      this.pdfDoc = doc;
      const page = await doc.getPage(1);
      if (this.destroyed || rid !== this.resourceId()) return;
      const hostWidth = host.clientWidth || 320;
      const base = page.getViewport({ scale: 1 });
      const scale = Math.min(1.5, Math.max(0.5, hostWidth / base.width));
      const viewport = page.getViewport({ scale });
      canvas.width = Math.floor(viewport.width);
      canvas.height = Math.floor(viewport.height);
      await page.render({ canvasContext: canvas.getContext('2d')!, viewport }).promise;
      if (!this.destroyed && rid === this.resourceId()) {
        this.pdfReady.set(true);
      }
    } catch {
      if (!this.destroyed && rid === this.resourceId()) {
        this.failed.set(true);
      }
    }
  }

  private destroyPdf() {
    try {
      this.pdfDoc?.destroy();
    } catch {
      // 忽略销毁异常
    }
    this.pdfDoc = null;
  }

  private disconnect() {
    this.observer?.disconnect();
    this.observer = null;
  }
}
