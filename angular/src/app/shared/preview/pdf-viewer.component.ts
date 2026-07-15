import {
  Component,
  OnInit,
  OnDestroy,
  input,
  viewChild,
  viewChildren,
  ElementRef,
  signal,
  effect,
  ChangeDetectionStrategy,
  AfterViewInit,
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
  pages = signal<number[]>([]);

  private readonly containerRef = viewChild<ElementRef<HTMLDivElement>>('pdfContainer');
  readonly canvases = viewChildren<ElementRef<HTMLCanvasElement>>('pdfCanvas');

  private pdfDoc: any = null;
  private isRendering = false;
  private renderQueue: number[] = [];
  private loaded = false;

  constructor() {
    effect(() => {
      const d = this.data();
      if (d && d.byteLength > 0 && !this.loaded) {
        this.loadPdf(d);
      }
    });
  }

  ngOnInit() {}

  ngAfterViewInit() {}

  ngOnDestroy() {
    this.pdfDoc = null;
    this.isRendering = false;
  }

  private get containerEl(): HTMLElement | null {
    return this.containerRef()?.nativeElement ?? null;
  }

  private async loadPdf(data: ArrayBuffer) {
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

      const loadingTask = pdfjsLib.getDocument({
        data: new Uint8Array(data),
        cMapUrl: 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/cmaps/',
        cMapPacked: true,
      });

      this.pdfDoc = await loadingTask.promise;
      const total = this.pdfDoc.numPages;

      this.totalPages.set(total);
      this.pages.set(Array.from({ length: total }, (_, i) => i + 1));

      // Wait for Angular to render canvas elements
      await new Promise((resolve) => setTimeout(resolve, 50));

      this.renderQueue = Array.from({ length: total }, (_, i) => i + 1);
      this.loaded = true;
      await this.processRenderQueue();
    } catch (e: any) {
      console.error('PDF load error:', e);
      this.error.set(e.message || 'Failed to load PDF');
      this.isLoading.set(false);
      this.loaded = true;
    }
  }

  private async processRenderQueue() {
    if (this.isRendering || this.renderQueue.length === 0) {
      if (this.renderQueue.length === 0) {
        this.isLoading.set(false);
      }
      return;
    }

    this.isRendering = true;
    const batch = this.renderQueue.splice(0, 2);

    for (const pageNum of batch) {
      await this.renderSinglePage(pageNum);
    }

    this.isRendering = false;

    if (this.renderQueue.length > 0) {
      await new Promise((resolve) => setTimeout(resolve, 0));
      await this.processRenderQueue();
    } else {
      this.isLoading.set(false);
    }
  }

  private getCanvas(pageNum: number): HTMLCanvasElement | null {
    const idx = pageNum - 1;
    const refs = this.canvases();
    if (idx >= 0 && idx < refs.length) {
      return refs[idx].nativeElement;
    }
    return null;
  }

  private async renderSinglePage(pageNum: number) {
    if (!this.pdfDoc) return;

    try {
      const page = await this.pdfDoc.getPage(pageNum);
      const viewport = page.getViewport({ scale: this.scale() * 1.5 });
      const canvas = this.getCanvas(pageNum);
      if (!canvas) return;

      canvas.height = viewport.height;
      canvas.width = viewport.width;

      await page.render({ canvasContext: canvas.getContext('2d')!, viewport }).promise;

      this.renderedCount.update((v) => v + 1);
    } catch (e) {
      console.warn(`Render page ${pageNum} error:`, e);
    }
  }

  private async reRenderAll() {
    if (!this.pdfDoc) return;

    this.isLoading.set(true);
    this.renderedCount.set(0);
    this.renderQueue = [];
    this.isRendering = false;

    // 清空所有 canvas
    for (const ref of this.canvases()) {
      const c = ref.nativeElement;
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
