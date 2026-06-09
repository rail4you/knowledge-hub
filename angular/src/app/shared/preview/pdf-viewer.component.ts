import { Component, ElementRef, OnInit, OnDestroy, input, viewChild, signal, effect, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSliderModule } from 'ng-zorro-antd/slider';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSelectModule } from 'ng-zorro-antd/select';

@Component({
  selector: 'app-pdf-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule, NzSliderModule, NzSpaceModule, NzSpinModule, NzSelectModule],
  templateUrl: './pdf-viewer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PdfViewerComponent implements OnInit, OnDestroy {
  data = input.required<ArrayBuffer>();
  fileName = input('');

  currentPage = signal(1);
  totalPages = signal(0);
  scale = signal(1);
  isLoading = signal(true);
  error = signal('');

  private canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('pdfCanvas');
  private pdfDoc: any = null;
  private rendering = false;

  constructor() {
    effect(() => {
      const d = this.data();
      if (d && d.byteLength > 0) {
        this.loadPdf(d);
      }
    });
  }

  async ngOnInit() {
    // pdfjs will be loaded dynamically
  }

  ngOnDestroy() {
    this.pdfDoc = null;
  }

  private async loadPdf(data: ArrayBuffer) {
    try {
      this.isLoading.set(true);
      this.error.set('');
      const pdfjsLib = await import('pdfjs-dist');
      pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/pdf.worker.min.mjs';

      const loadingTask = pdfjsLib.getDocument({
        data: new Uint8Array(data),
        cMapUrl: 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/cmaps/',
        cMapPacked: true,
      });

      this.pdfDoc = await loadingTask.promise;
      this.totalPages.set(this.pdfDoc.numPages);
      this.currentPage.set(1);
      // Wait for Angular to render the canvas before rendering the PDF page
      await new Promise(resolve => setTimeout(resolve, 0));
      await this.renderPage(1);
    } catch (e: any) {
      console.error('PDF load error:', e);
      this.error.set(e.message || 'Failed to load PDF');
    } finally {
      this.isLoading.set(false);
    }
  }

  async renderPage(pageNum: number) {
    if (!this.pdfDoc || this.rendering) return;
    const canvasEl = this.canvasRef()?.nativeElement;
    if (!canvasEl) return;
    this.rendering = true;
    try {
      const page = await this.pdfDoc.getPage(pageNum);
      const viewport = page.getViewport({ scale: this.scale() * 1.5 });
      const context = canvasEl.getContext('2d')!;
      canvasEl.height = viewport.height;
      canvasEl.width = viewport.width;

      await page.render({ canvasContext: context, viewport }).promise;
    } catch (e) {
      console.error('Render page error:', e);
    } finally {
      this.rendering = false;
    }
  }

  prevPage() {
    if (this.currentPage() <= 1) return;
    this.currentPage.update(v => v - 1);
    this.renderPage(this.currentPage());
  }

  nextPage() {
    if (this.currentPage() >= this.totalPages()) return;
    this.currentPage.update(v => v + 1);
    this.renderPage(this.currentPage());
  }

  zoomIn() {
    if (this.scale() >= 3) return;
    this.scale.update(v => Math.min(3, v + 0.25));
    this.renderPage(this.currentPage());
  }

  zoomOut() {
    if (this.scale() <= 0.5) return;
    this.scale.update(v => Math.max(0.5, v - 0.25));
    this.renderPage(this.currentPage());
  }

  fitWidth() {
    this.scale.set(1);
    this.renderPage(this.currentPage());
  }

  getScalePercent(): number {
    return Math.round(this.scale() * 100);
  }
}
