import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  computed,
  effect,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { PPTXViewer } from 'pptxviewjs';

@Component({
  selector: 'app-pptx-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule],
  templateUrl: './pptx-viewer.component.html',
  styleUrls: ['./pptx-viewer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PptxViewerComponent implements OnDestroy {
  data = input.required<ArrayBuffer>();
  fileName = input('');

  readonly canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('slideCanvas');

  readonly slideCount = signal(0);
  readonly currentSlide = signal(0);
  readonly isLoading = signal(true);
  readonly isRendering = signal(false);
  readonly error = signal('');
  readonly zoom = signal(1);

  readonly slideLabel = computed(() => `${this.currentSlide() + 1} / ${this.slideCount() || 0}`);
  readonly zoomLabel = computed(() => `${Math.round(this.zoom() * 100)}%`);
  readonly canGoPrev = computed(() => this.currentSlide() > 0);
  readonly canGoNext = computed(() => this.currentSlide() < this.slideCount() - 1);
  readonly visiblePageIndexes = computed(() => {
    const total = this.slideCount();
    const current = this.currentSlide();
    const maxVisible = 10;

    if (total <= maxVisible) {
      return Array.from({ length: total }, (_, index) => index);
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
  readonly hasRightEllipsis = computed(() => this.visiblePageIndexes()[this.visiblePageIndexes().length - 1] < this.slideCount() - 1);

  private viewer: PPTXViewer | null = null;
  private renderToken = 0;

  constructor() {
    effect(() => {
      const data = this.data();
      const canvas = this.canvasRef()?.nativeElement;

      if (!data?.byteLength || !canvas) {
        return;
      }

      void this.loadPresentation(data, canvas);
    });
  }

  async prevSlide() {
    if (!this.canGoPrev()) return;
    await this.renderSlide(this.currentSlide() - 1);
  }

  async nextSlide() {
    if (!this.canGoNext()) return;
    await this.renderSlide(this.currentSlide() + 1);
  }

  async goToSlide(index: number) {
    if (index < 0 || index >= this.slideCount()) return;
    await this.renderSlide(index);
  }

  async zoomIn() {
    if (this.zoom() >= 2) return;
    this.zoom.update(value => Math.min(2, value + 0.25));
    await this.renderSlide(this.currentSlide());
  }

  async zoomOut() {
    if (this.zoom() <= 0.5) return;
    this.zoom.update(value => Math.max(0.5, value - 0.25));
    await this.renderSlide(this.currentSlide());
  }

  async resetZoom() {
    if (this.zoom() === 1) return;
    this.zoom.set(1);
    await this.renderSlide(this.currentSlide());
  }

  pageTrackBy = (index: number) => index;

  ngOnDestroy() {
    this.renderToken++;
    this.disposeViewer();
  }

  private async loadPresentation(data: ArrayBuffer, canvas: HTMLCanvasElement) {
    const token = ++this.renderToken;
    this.disposeViewer();

    this.isLoading.set(true);
    this.isRendering.set(false);
    this.error.set('');
    this.slideCount.set(0);
    this.currentSlide.set(0);
    this.zoom.set(1);

    try {
      const viewer = new PPTXViewer({
        canvas,
        debug: false,
        slideSizeMode: 'fit',
        backgroundColor: '#ffffff',
        autoRenderFirstSlide: false,
      });

      await viewer.loadFile(data.slice(0));

      if (token !== this.renderToken) {
        viewer.destroy();
        return;
      }

      this.viewer = viewer;
      this.slideCount.set(viewer.getSlideCount());
      await this.renderSlide(0, token);
    } catch (error: unknown) {
      if (token !== this.renderToken) {
        return;
      }

      console.error('PPTX render error:', error);
      this.error.set(error instanceof Error ? error.message : 'PPTX 预览加载失败');
    } finally {
      if (token === this.renderToken) {
        this.isLoading.set(false);
      }
    }
  }

  private async renderSlide(index: number, token = this.renderToken) {
    if (!this.viewer) return;

    this.isRendering.set(true);

    try {
      const canvas = this.canvasRef()?.nativeElement;
      if (!canvas) {
        return;
      }

      await this.viewer.render(canvas, {
        slideIndex: index,
        scale: this.zoom(),
        quality: 'high',
      });

      if (token !== this.renderToken) {
        return;
      }

      this.currentSlide.set(this.viewer.getCurrentSlideIndex());
      this.error.set('');
    } catch (error: unknown) {
      if (token !== this.renderToken) {
        return;
      }

      console.error('PPTX slide render error:', error);
      this.error.set(error instanceof Error ? error.message : 'PPTX 页面渲染失败');
    } finally {
      if (token === this.renderToken) {
        this.isRendering.set(false);
      }
    }
  }

  private disposeViewer() {
    if (!this.viewer) return;

    this.viewer.destroy();
    this.viewer = null;
  }
}
