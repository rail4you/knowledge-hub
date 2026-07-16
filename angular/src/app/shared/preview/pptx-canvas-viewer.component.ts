import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import type { PPTXViewer } from 'pptxviewjs';

/**
 * PPTX 预览（客户端 Canvas 渲染）
 *
 * 使用 pptxviewjs 在浏览器内解析 .pptx 并以 Canvas 形式还原每张幻灯片，
 * 相比旧实现（后端 ZIP 拆解 + 前端裸渲染）能保留图片位置、文本框坐标、
 * 形状布局，对图片型 PPTX 效果显著改善。
 *
 * pptxviewjs 通过动态 import 按需加载，不进首屏 bundle。
 */
@Component({
  selector: 'app-pptx-canvas-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule, NzSpinModule],
  templateUrl: './pptx-canvas-viewer.component.html',
  styleUrls: ['./pptx-canvas-viewer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PptxCanvasViewerComponent implements OnDestroy {
  /** 完整的 PPTX 文件字节（来自 /preview 端点的 ArrayBuffer） */
  data = input.required<ArrayBuffer>();
  fileName = input('');

  readonly isLoading = signal(true);
  readonly error = signal('');
  readonly slideCount = signal(0);
  readonly currentIndex = signal(0);
  readonly zoom = signal(1);

  readonly slideLabel = computed(
    () => `${this.currentIndex() + 1} / ${this.slideCount() || 0}`,
  );
  readonly zoomLabel = computed(() => `${Math.round(this.zoom() * 100)}%`);
  readonly canGoPrev = computed(() => this.currentIndex() > 0);
  readonly canGoNext = computed(() => this.currentIndex() < this.slideCount() - 1);

  readonly visiblePageIndexes = computed(() => {
    const total = this.slideCount();
    const current = this.currentIndex();
    const maxVisible = 10;
    if (total <= maxVisible) {
      return Array.from({ length: total }, (_, i) => i);
    }
    const half = Math.floor(maxVisible / 2);
    let start = Math.max(0, current - half);
    let end = Math.min(total - 1, start + maxVisible - 1);
    if (end - start < maxVisible - 1) {
      start = Math.max(0, end - maxVisible + 1);
    }
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  });
  readonly hasLeftEllipsis = computed(() => this.visiblePageIndexes()[0] > 0);
  readonly hasRightEllipsis = computed(() => {
    const pages = this.visiblePageIndexes();
    return pages[pages.length - 1] < this.slideCount() - 1;
  });

  private readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('canvas');

  private viewer: PPTXViewer | null = null;
  private renderToken = 0;

  constructor() {
    // 当 data 变化或 canvas DOM 就绪时，触发加载
    effect(() => {
      const data = this.data();
      const canvasEl = this.canvasRef()?.nativeElement;
      if (!data || data.byteLength === 0 || !canvasEl) return;
      void this.loadPptx(data, canvasEl);
    });
  }

  ngOnDestroy(): void {
    this.renderToken++;
    this.destroyViewer();
  }

  async prevSlide(): Promise<void> {
    if (!this.canGoPrev() || !this.viewer) return;
    const next = this.currentIndex() - 1;
    this.currentIndex.set(next);
    await this.viewer.goToSlide(next, this.canvasRef()?.nativeElement);
  }

  async nextSlide(): Promise<void> {
    if (!this.canGoNext() || !this.viewer) return;
    const next = this.currentIndex() + 1;
    this.currentIndex.set(next);
    await this.viewer.goToSlide(next, this.canvasRef()?.nativeElement);
  }

  async goToSlide(index: number): Promise<void> {
    if (!this.viewer || index < 0 || index >= this.slideCount()) return;
    if (index === this.currentIndex()) return;
    this.currentIndex.set(index);
    await this.viewer.goToSlide(index, this.canvasRef()?.nativeElement);
  }

  zoomIn(): void {
    this.zoom.update(v => Math.min(2, v + 0.25));
  }
  zoomOut(): void {
    this.zoom.update(v => Math.max(0.5, v - 0.25));
  }
  resetZoom(): void {
    this.zoom.set(1);
  }
  pageTrackBy = (index: number) => index;

  private async loadPptx(data: ArrayBuffer, canvas: HTMLCanvasElement): Promise<void> {
    const token = ++this.renderToken;
    this.isLoading.set(true);
    this.error.set('');

    // 释放旧 viewer
    this.destroyViewer();

    try {
      // 动态加载 pptxviewjs（按需进 bundle）
      const mod = await import('pptxviewjs');
      if (token !== this.renderToken) return;

      const ViewerCtor = mod.PPTXViewer;
      const viewer = new ViewerCtor({
        canvas,
        slideSizeMode: 'fit',
        backgroundColor: '#ffffff',
      });

      await viewer.loadFile(data);
      if (token !== this.renderToken) {
        viewer.destroy();
        return;
      }

      this.viewer = viewer;
      const total = viewer.getSlideCount();
      this.slideCount.set(total);
      this.currentIndex.set(0);
      this.isLoading.set(false);

      // 渲染第一张
      await viewer.render(canvas);
    } catch (err: unknown) {
      if (token !== this.renderToken) return;
      console.error('[PptxCanvasViewer] load failed:', err);
      this.error.set(err instanceof Error ? err.message : 'PPTX 解析失败');
      this.isLoading.set(false);
    }
  }

  private destroyViewer(): void {
    if (this.viewer) {
      try {
        this.viewer.destroy();
      } catch {
        // ignore
      }
      this.viewer = null;
    }
  }
}