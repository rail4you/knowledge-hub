import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
  ElementRef,
  NgZone,
  AfterViewInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';

interface SlideText {
  text: string;
  fontSize: number;
  bold: boolean;
  color: string;
}

interface SlideShape {
  x: number;
  y: number;
  w: number;
  h: number;
  align?: string;
  anchor?: string;
  texts: SlideText[];
  image?: string;
}

interface SlideData {
  index: number;
  width: number;
  height: number;
  shapes: SlideShape[];
  loaded: boolean;
  error?: string;
}

@Component({
  selector: 'app-pptx-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule, NzSpinModule],
  templateUrl: './pptx-viewer.component.html',
  styleUrls: ['./pptx-viewer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PptxViewerComponent implements OnDestroy, AfterViewInit {
  /** Resource ID to load slides from server-side extraction */
  resourceId = input<string>('');
  fileName = input('');

  readonly slides = signal<SlideData[]>([]);
  readonly currentIndex = signal(0);
  readonly isLoading = signal(true);
  readonly error = signal('');
  readonly zoom = signal(1);
  /** 幻灯片整体缩放（自动适配容器宽度，不含用户 zoom） */
  readonly fitScale = signal(1);

  readonly currentSlide = computed(() => this.slides()[this.currentIndex()] ?? null);
  readonly slideCount = computed(() => this.slides().length);
  readonly slideLabel = computed(() => `${this.currentIndex() + 1} / ${this.slideCount() || 0}`);
  readonly zoomLabel = computed(() => `${Math.round(this.zoom() * 100)}%`);
  readonly canGoPrev = computed(() => this.currentIndex() > 0);
  readonly canGoNext = computed(() => this.currentIndex() < this.slideCount() - 1);

  /** EMU -> px 的总缩放（1 英寸 = 914400 EMU = 96px，即 1px = 9525 EMU） */
  private static readonly EMU_PER_PX = 9525;
  /** 1pt = 12700 EMU */
  private static readonly EMU_PER_PT = 12700;

  readonly pxPerEmu = computed(() => (this.fitScale() * this.zoom()) / PptxViewerComponent.EMU_PER_PX);

  private renderToken = 0;
  private resizeObserver: ResizeObserver | null = null;

  private readonly stageEl = viewChild<ElementRef<HTMLDivElement>>('stage');
  private readonly zone = inject(NgZone);

  constructor() {
    effect(() => {
      const id = this.resourceId();
      if (id) {
        void this.loadFromServer(id);
      }
    });
  }

  ngAfterViewInit() {
    // 容器尺寸变化时重新适配（窗口缩放等）
    this.zone.runOutsideAngular(() => {
      const el = this.stageEl()?.nativeElement;
      if (!el) return;
      this.resizeObserver = new ResizeObserver(() => {
        this.zone.run(() => this.computeFitScale());
      });
      this.resizeObserver.observe(el);
    });
  }

  ngOnDestroy() {
    this.renderToken++;
    this.resizeObserver?.disconnect();
    this.resizeObserver = null;
  }

  prevSlide() { if (this.canGoPrev()) this.currentIndex.update(v => v - 1); }
  nextSlide() { if (this.canGoNext()) this.currentIndex.update(v => v + 1); }
  zoomIn() { this.zoom.update(v => Math.min(3, v + 0.25)); }
  zoomOut() { this.zoom.update(v => Math.max(0.5, v - 0.25)); }
  resetZoom() { this.zoom.set(1); }

  /** 形状的绝对定位（px） */
  shapeStyle(shape: SlideShape): Record<string, string> {
    const s = this.pxPerEmu();
    return {
      left: `${shape.x * s}px`,
      top: `${shape.y * s}px`,
      width: `${shape.w * s}px`,
      height: `${shape.h * s}px`,
    };
  }

  /** 形状内文字样式（字号按幻灯片缩放换算） */
  textStyle(text: SlideText): Record<string, string> {
    const s = this.pxPerEmu();
    const px = Math.max(4, text.fontSize * PptxViewerComponent.EMU_PER_PT * s);
    return {
      fontSize: `${px}px`,
      fontWeight: text.bold ? 'bold' : 'normal',
      color: text.color,
    };
  }

  cardStyle(): Record<string, string> {
    const slide = this.currentSlide();
    const s = this.pxPerEmu();
    return {
      width: `${(slide?.width ?? 0) * s}px`,
      height: `${(slide?.height ?? 0) * s}px`,
    };
  }

  /** 文本水平对齐 */
  textAlign(align?: string): string {
    switch (align) {
      case 'ctr': return 'center';
      case 'r': return 'right';
      case 'just': return 'justify';
      default: return 'left';
    }
  }

  /** 文本垂直锚点（flex 对齐） */
  anchorAlign(anchor?: string): string {
    switch (anchor) {
      case 't': return 'flex-start';
      case 'b': return 'flex-end';
      default: return 'center';
    }
  }

  imageUrl(mediaPath: string): string {
    // 逐段编码，保留 / 分隔符，避免 %2F 导致后端 {*mediaPath} 无法匹配条目
    const encoded = mediaPath.split('/').map(encodeURIComponent).join('/');
    return `/api/resource-file/${this.resourceId()}/media/${encoded}`;
  }

  private async loadFromServer(resourceId: string) {
    const token = ++this.renderToken;
    this.isLoading.set(true);
    this.error.set('');

    try {
      // Step 1: Get slide count
      const countResp = await fetch(`/api/resource-file/${resourceId}/slides/count`);
      if (!countResp.ok) {
        let msg = '无法获取幻灯片数量';
        try {
          const body = await countResp.json();
          if (body?.message) msg = body.message;
        } catch {
          // ignore
        }
        throw new Error(msg);
      }
      const { count } = await countResp.json() as { count: number };

      if (token !== this.renderToken) return;

      // Initialize empty slide slots
      const slideArr: SlideData[] = Array.from({ length: count }, (_, i) => ({
        index: i + 1,
        width: 12192000,
        height: 6858000,
        shapes: [],
        loaded: false,
      }));
      this.slides.set(slideArr);
      this.isLoading.set(false);

      // Step 2: Load slide 1 immediately
      await this.loadSlide(resourceId, 1, token);

      // Step 3: Preload nearby slides in background
      this.preloadNearbySlides(0);
    } catch (err: unknown) {
      if (token === this.renderToken) {
        this.error.set(err instanceof Error ? err.message : '加载失败');
        this.isLoading.set(false);
      }
    }
  }

  private async loadSlide(resourceId: string, slideNumber: number, token: number) {
    const idx = slideNumber - 1;
    if (idx < 0 || idx >= this.slides().length) return;
    if (this.slides()[idx].loaded) return;

    try {
      const resp = await fetch(`/api/resource-file/${resourceId}/slides/${slideNumber}`);
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);

      const data = await resp.json() as {
        slideNumber: number;
        width: number;
        height: number;
        shapes: SlideShape[];
      };
      if (token !== this.renderToken) return;

      this.slides.update(arr => {
        const newArr = [...arr];
        if (newArr[idx]) {
          newArr[idx] = {
            ...newArr[idx],
            width: data.width || newArr[idx].width,
            height: data.height || newArr[idx].height,
            shapes: data.shapes || [],
            loaded: true,
          };
        }
        return newArr;
      });

      this.computeFitScale();
    } catch {
      // Silently fail for individual slides; user can retry by navigating
    }
  }

  private preloadNearbySlides(currentIdx: number) {
    const total = this.slideCount();
    const toLoad: number[] = [];

    for (let i = 1; i <= 3; i++) {
      const next = currentIdx + i;
      if (next < total) toLoad.push(next + 1); // slide numbers are 1-based
    }
    const prev = currentIdx - 1;
    if (prev >= 0) toLoad.push(prev + 1);

    const rid = this.resourceId();
    const token = this.renderToken;
    for (const sn of toLoad) {
      void this.loadSlide(rid, sn, token);
    }
  }

  /** 根据容器宽度计算整体缩放，使幻灯片完整可见 */
  private computeFitScale() {
    const slide = this.currentSlide();
    const stage = this.stageEl()?.nativeElement;
    if (!slide || !stage || slide.width <= 0) return;

    const availW = stage.clientWidth - 48; // 左右留白
    if (availW <= 0) return;
    const nativeW = slide.width / PptxViewerComponent.EMU_PER_PX;
    if (nativeW <= 0) return;

    this.fitScale.set(Math.max(0.2, Math.min(availW / nativeW, 2)));
  }
}
