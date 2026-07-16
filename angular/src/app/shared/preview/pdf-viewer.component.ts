import {
  Component,
  OnInit,
  OnDestroy,
  input,
  viewChild,
  ElementRef,
  signal,
  ChangeDetectionStrategy,
  AfterViewInit,
  NgZone,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';

@Component({
  selector: 'app-pdf-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule, NzSpinModule],
  templateUrl: './pdf-viewer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PdfViewerComponent implements OnInit, OnDestroy, AfterViewInit {
  data = input.required<ArrayBuffer>();
  fileName = input('');

  currentPage = signal(1);
  totalPages = signal(0);
  scale = signal(1);
  isLoading = signal(true);
  error = signal('');
  renderedCount = signal(0);

  private readonly containerRef = viewChild<ElementRef<HTMLDivElement>>('pdfContainer');
  private readonly pagesHostRef = viewChild<ElementRef<HTMLDivElement>>('pagesHost');

  private readonly zone = inject(NgZone);

  private pdfDoc: any = null;
  private canvases: HTMLCanvasElement[] = [];
  private isRendering = false;
  private renderQueue: number[] = [];
  private loaded = false;
  private destroyed = false;

  constructor() {}

  ngOnInit() {}

  ngAfterViewInit() {
    const d = this.data();
    if (d && d.byteLength > 0 && !this.loaded) {
      setTimeout(() => {
        if (!this.destroyed && !this.loaded) {
          this.loadPdf(d);
        }
      }, 0);
    }
  }

  ngOnDestroy() {
    this.destroyed = true;
    this.pdfDoc = null;
    this.isRendering = false;
    this.renderQueue = [];
    this.canvases = [];
  }

  private get containerEl(): HTMLElement | null {
    return this.containerRef()?.nativeElement ?? null;
  }

  private get pagesHost(): HTMLElement | null {
    return this.pagesHostRef()?.nativeElement ?? null;
  }

  /**
   * 命令式创建所有 canvas 元素，完全绕开 Angular 的 @for/*ngFor 控制流，
   * 避免 Angular 21 变更检测导致组件被销毁的 bug。
   */
  private createCanvasElements(totalPages: number) {
    const host = this.pagesHost;
    if (!host) return;

    // 清理旧元素
    host.innerHTML = '';
    this.canvases = [];

    for (let i = 1; i <= totalPages; i++) {
      const wrapper = document.createElement('div');
      wrapper.className = 'pdf-page-wrapper';

      const canvas = document.createElement('canvas');
      canvas.className = 'pdf-page-canvas';

      const label = document.createElement('div');
      label.className = 'pdf-page-label';
      label.textContent = `— ${i} —`;

      wrapper.appendChild(canvas);
      wrapper.appendChild(label);
      host.appendChild(wrapper);

      this.canvases.push(canvas);
    }
  }

  private async loadPdf(data: ArrayBuffer) {
    if (this.destroyed) return;

    try {
      this.isLoading.set(true);
      this.error.set('');
      this.renderedCount.set(0);
      this.renderQueue = [];
      this.isRendering = false;
      this.loaded = false;

      const pdfjsLib = await import('pdfjs-dist');
      pdfjsLib.GlobalWorkerOptions.workerSrc =
        'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/pdf.worker.min.mjs';

      // 复制 ArrayBuffer，防止 pdf.js Web Worker 内部 transfer 导致原 buffer detached
      const copy = data.slice(0);
      const loadingTask = pdfjsLib.getDocument({
        data: new Uint8Array(copy),
        cMapUrl: 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/cmaps/',
        cMapPacked: true,
      });

      this.pdfDoc = await loadingTask.promise;
      const total = this.pdfDoc.numPages;

      this.totalPages.set(total);

      // 在 Angular zone 外创建 canvas 元素，避免触发变更检测
      this.zone.runOutsideAngular(() => {
        this.createCanvasElements(total);
      });

      // 等待 DOM 更新
      await new Promise((resolve) => setTimeout(resolve, 50));
      if (this.destroyed) return;

      this.renderQueue = Array.from({ length: total }, (_, i) => i + 1);
      this.loaded = true;
      await this.processRenderQueue();
    } catch (e: any) {
      console.error('PDF load error:', e);
      if (!this.destroyed) {
        this.error.set(e.message || 'Failed to load PDF');
        this.isLoading.set(false);
        this.loaded = true;
      }
    }
  }

  private async processRenderQueue() {
    if (this.destroyed) return;

    if (this.isRendering || this.renderQueue.length === 0) {
      if (this.renderQueue.length === 0) {
        this.isLoading.set(false);
      }
      return;
    }

    this.isRendering = true;
    const batch = this.renderQueue.splice(0, 2);

    for (const pageNum of batch) {
      if (this.destroyed) return;
      await this.renderSinglePage(pageNum);
    }

    if (this.destroyed) return;

    this.isRendering = false;

    if (this.renderQueue.length > 0) {
      await new Promise((resolve) => setTimeout(resolve, 0));
      await this.processRenderQueue();
    } else {
      this.isLoading.set(false);
    }
  }

  private async renderSinglePage(pageNum: number) {
    if (!this.pdfDoc || this.destroyed) return;

    try {
      const page = await this.pdfDoc.getPage(pageNum);
      const viewport = page.getViewport({ scale: this.scale() * 1.5 });
      const canvas = this.canvases[pageNum - 1];
      if (!canvas) return;

      canvas.height = viewport.height;
      canvas.width = viewport.width;

      // 在 Angular zone 外执行 canvas 渲染，避免触发变更检测
      await this.zone.runOutsideAngular(() =>
        page.render({ canvasContext: canvas.getContext('2d')!, viewport }).promise
      );

      if (!this.destroyed) {
        this.renderedCount.update((v) => v + 1);
      }
    } catch (e) {
      console.warn(`Render page ${pageNum} error:`, e);
    }
  }

  private async reRenderAll() {
    if (!this.pdfDoc || this.destroyed) return;

    this.isLoading.set(true);
    this.renderedCount.set(0);
    this.renderQueue = [];
    this.isRendering = false;

    // 清空所有 canvas
    for (const c of this.canvases) {
      c.getContext('2d')?.clearRect(0, 0, c.width, c.height);
    }

    const total = this.totalPages();
    this.renderQueue = Array.from({ length: total }, (_, i) => i + 1);
    await new Promise((resolve) => setTimeout(resolve, 0));
    await this.processRenderQueue();
  }

  zoomIn() {
    if (this.scale() >= 3) return;
    this.scale.update((v) => Math.min(3, v + 0.25));
    this.reRenderAll();
  }

  zoomOut() {
    if (this.scale() <= 0.25) return;
    this.scale.update((v) => Math.max(0.25, v - 0.25));
    this.reRenderAll();
  }

  fitWidth() {
    this.scale.set(1);
    this.reRenderAll();
  }

  getScalePercent(): number {
    return Math.round(this.scale() * 100);
  }

  onShieldClick(event: MouseEvent) {
    event.stopPropagation();
  }

  onScroll() {
    const el = this.containerEl;
    if (!el) return;

    const wrappers = el.querySelectorAll('.pdf-page-wrapper');
    const containerRect = el.getBoundingClientRect();
    const midPoint = containerRect.top + containerRect.height / 3;

    let closestPage = 1;
    let closestDist = Infinity;

    wrappers.forEach((w) => {
      const rect = w.getBoundingClientRect();
      const dist = Math.abs(rect.top + rect.height / 2 - midPoint);
      if (dist < closestDist) {
        closestDist = dist;
        const label = w.querySelector('.pdf-page-label');
        if (label) {
          const match = label.textContent?.match(/(\d+)/);
          if (match) closestPage = parseInt(match[1], 10);
        }
      }
    });

    this.currentPage.set(closestPage);
  }
}
