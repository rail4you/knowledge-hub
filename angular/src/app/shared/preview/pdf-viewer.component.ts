import {
  Component,
  OnInit,
  OnDestroy,
  input,
  viewChild,
  ElementRef,
  signal,
  computed,
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
  data = input<ArrayBuffer>(new ArrayBuffer(0));
  previewUrl = input<string>('');
  fileName = input('');

  currentPage = signal(1);
  totalPages = signal(0);
  scale = signal(1);
  isLoading = signal(true);
  error = signal('');
  renderedCount = signal(0);
  /** 已渲染的页码集合（用于 page-strip 显示渲染状态） */
  renderedPages = signal<Set<number>>(new Set());

  /** page-strip 显示的页码列表 */
  readonly pageNumbers = computed(() => {
    const total = this.totalPages();
    if (total === 0) return [] as number[];
    return Array.from({ length: total }, (_, i) => i + 1);
  });

  private readonly containerRef = viewChild<ElementRef<HTMLDivElement>>('pdfContainer');
  private readonly pagesHostRef = viewChild<ElementRef<HTMLDivElement>>('pagesHost');

  private readonly zone = inject(NgZone);

  private pdfDoc: any = null;
  private pageItems: Map<number, { canvas: HTMLCanvasElement; wrapper: HTMLElement }> = new Map();
  private renderQueue: number[] = [];
  private loaded = false;
  private destroyed = false;

  constructor() {
    // 键盘快捷键：← → 翻页
    this.zone.runOutsideAngular(() => {
      document.addEventListener('keydown', this.handleKeyDown);
    });
  }

  ngOnInit() {}

  ngAfterViewInit() {
    const url = this.previewUrl();
    const d = this.data();

    if (url && !this.loaded) {
      setTimeout(() => {
        if (!this.destroyed && !this.loaded) {
          this.loadPdfFromUrl(url);
        }
      }, 0);
    } else if (d && d.byteLength > 0 && !this.loaded) {
      setTimeout(() => {
        if (!this.destroyed && !this.loaded) {
          this.loadPdfFromBuffer(d);
        }
      }, 0);
    }
  }

  ngOnDestroy() {
    document.removeEventListener('keydown', this.handleKeyDown);
    this.destroyed = true;
    this.pdfDoc = null;
    this.renderQueue = [];
    this.pageItems.clear();
  }

  private get pagesHost(): HTMLElement | null {
    return this.pagesHostRef()?.nativeElement ?? null;
  }

  isPageRendered(page: number): boolean {
    return this.renderedPages().has(page);
  }

  /** 导航到指定页，如果未渲染则优先渲染 */
  async goToPage(pageNum: number) {
    if (pageNum < 1 || pageNum > this.totalPages() || pageNum === this.currentPage()) return;
    this.currentPage.set(pageNum);
    this.showPage(pageNum);

    // 如果该页未渲染，优先渲染
    if (!this.isPageRendered(pageNum)) {
      await this.renderPageWithPriority(pageNum);
    }
  }

  goToPrev() {
    if (this.currentPage() > 1) {
      this.goToPage(this.currentPage() - 1);
    }
  }

  goToNext() {
    if (this.currentPage() < this.totalPages()) {
      this.goToPage(this.currentPage() + 1);
    }
  }

  /** 切换当前显示的页面（隐藏其他） */
  private showPage(pageNum: number) {
    const host = this.pagesHost;
    if (!host) return;

    // 隐藏所有页面
    for (const [p, item] of this.pageItems) {
      item.wrapper.style.display = p === pageNum ? '' : 'none';
    }
  }

  private async loadPdfFromUrl(url: string) {
    if (this.destroyed) return;

    try {
      this.isLoading.set(true);
      this.error.set('');
      this.renderedCount.set(0);
      this.renderedPages.set(new Set());
      this.renderQueue = [];
      this.loaded = false;

      const pdfjsLib = await import('pdfjs-dist');
      pdfjsLib.GlobalWorkerOptions.workerSrc =
        'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/pdf.worker.min.mjs';

      const loadingTask = pdfjsLib.getDocument({
        url,
        cMapUrl: 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/cmaps/',
        cMapPacked: true,
        enableXfa: true,
      });

      this.pdfDoc = await loadingTask.promise;
      const total = this.pdfDoc.numPages;
      this.totalPages.set(total);

      // 预创建所有页面的 canvas（命令式，绕开 Angular）
      this.zone.runOutsideAngular(() => {
        this.createAllCanvases(total);
      });

      await new Promise((resolve) => setTimeout(resolve, 50));
      if (this.destroyed) return;

      this.loaded = true;

      // 优先渲染第 1 页，完成后立即展示
      await this.renderPageWithPriority(1);
      this.showPage(1);
      this.currentPage.set(1);
      this.isLoading.set(false);

      // 后台加载第 2 页及以后
      if (total > 1) {
        this.renderQueue = Array.from({ length: total - 1 }, (_, i) => i + 2);
        this.backgroundRenderAll();
      }
    } catch (e: any) {
      console.error('PDF load error:', e);
      if (!this.destroyed) {
        this.error.set(e.message || 'Failed to load PDF');
        this.isLoading.set(false);
        this.loaded = true;
      }
    }
  }

  private async loadPdfFromBuffer(data: ArrayBuffer) {
    if (this.destroyed) return;

    try {
      this.isLoading.set(true);
      this.error.set('');
      this.renderedCount.set(0);
      this.renderedPages.set(new Set());
      this.renderQueue = [];
      this.loaded = false;

      const pdfjsLib = await import('pdfjs-dist');
      pdfjsLib.GlobalWorkerOptions.workerSrc =
        'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/pdf.worker.min.mjs';

      const copy = data.slice(0);
      const loadingTask = pdfjsLib.getDocument({
        data: new Uint8Array(copy),
        cMapUrl: 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.10.38/cmaps/',
        cMapPacked: true,
      });

      this.pdfDoc = await loadingTask.promise;
      const total = this.pdfDoc.numPages;
      this.totalPages.set(total);

      this.zone.runOutsideAngular(() => {
        this.createAllCanvases(total);
      });

      await new Promise((resolve) => setTimeout(resolve, 50));
      if (this.destroyed) return;

      this.loaded = true;

      await this.renderPageWithPriority(1);
      this.showPage(1);
      this.currentPage.set(1);
      this.isLoading.set(false);

      if (total > 1) {
        this.renderQueue = Array.from({ length: total - 1 }, (_, i) => i + 2);
        this.backgroundRenderAll();
      }
    } catch (e: any) {
      console.error('PDF load error:', e);
      if (!this.destroyed) {
        this.error.set(e.message || 'Failed to load PDF');
        this.isLoading.set(false);
        this.loaded = true;
      }
    }
  }

  /** 命令式创建所有页面的 canvas 元素，初始全部隐藏 */
  private createAllCanvases(total: number) {
    const host = this.pagesHost;
    if (!host) return;

    host.innerHTML = '';
    this.pageItems.clear();

    for (let i = 1; i <= total; i++) {
      const wrapper = document.createElement('div');
      wrapper.className = 'pdf-page-wrapper';
      wrapper.style.display = 'none'; // 默认隐藏

      const canvas = document.createElement('canvas');
      canvas.className = 'pdf-page-canvas';

      wrapper.appendChild(canvas);
      host.appendChild(wrapper);

      this.pageItems.set(i, { canvas, wrapper });
    }
  }

  /** 优先渲染指定页面（高优先级，用于用户导航到的页） */
  private async renderPageWithPriority(pageNum: number) {
    if (!this.pdfDoc || this.destroyed) return;

    try {
      const page = await this.pdfDoc.getPage(pageNum);
      const viewport = page.getViewport({ scale: this.scale() * 1.5 });
      const item = this.pageItems.get(pageNum);
      if (!item) return;

      item.canvas.height = viewport.height;
      item.canvas.width = viewport.width;

      await this.zone.runOutsideAngular(() =>
        page.render({ canvasContext: item.canvas.getContext('2d')!, viewport }).promise
      );

      // 标记已渲染
      this.renderedPages.update((s) => {
        const next = new Set(s);
        next.add(pageNum);
        return next;
      });
      this.renderedCount.update((v) => v + 1);
    } catch (e) {
      console.warn(`Render page ${pageNum} error:`, e);
    }
  }

  /** 后台逐页渲染（低优先级，使用 setTimeout 避免阻塞） */
  private backgroundRenderAll() {
    this.renderNextInBackground();
  }

  private renderNextInBackground() {
    if (this.destroyed) return;
    if (this.renderQueue.length === 0) return;

    const pageNum = this.renderQueue.shift()!;
    this.renderPageInBackground(pageNum).finally(() => {
      if (!this.destroyed) {
        // 每 16ms 渲染一页，保证 UI 流畅
        setTimeout(() => this.renderNextInBackground(), 16);
      }
    });
  }

  private async renderPageInBackground(pageNum: number) {
    if (!this.pdfDoc || this.destroyed) return;

    try {
      const page = await this.pdfDoc.getPage(pageNum);
      const viewport = page.getViewport({ scale: this.scale() * 1.5 });
      const item = this.pageItems.get(pageNum);
      if (!item) return;

      item.canvas.height = viewport.height;
      item.canvas.width = viewport.width;

      await this.zone.runOutsideAngular(() =>
        page.render({ canvasContext: item.canvas.getContext('2d')!, viewport }).promise
      );

      this.renderedPages.update((s) => {
        const next = new Set(s);
        next.add(pageNum);
        return next;
      });
      this.renderedCount.update((v) => v + 1);
    } catch (e) {
      console.warn(`Render page ${pageNum} error:`, e);
    }
  }

  private async reRenderAll() {
    if (!this.pdfDoc || this.destroyed) return;

    this.isLoading.set(true);
    this.renderedCount.set(0);
    this.renderedPages.set(new Set());

    const total = this.totalPages();

    await this.renderPageWithPriority(1);
    this.showPage(1);
    this.currentPage.set(1);
    this.isLoading.set(false);

    if (total > 1) {
      this.renderQueue = Array.from({ length: total - 1 }, (_, i) => i + 2);
      this.backgroundRenderAll();
    }
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

  private handleKeyDown = (event: KeyboardEvent) => {
    // 仅在 pdf viewer 可见且未加载时响应
    if (this.destroyed || this.isLoading()) return;
    if (event.key === 'ArrowLeft') {
      event.preventDefault();
      this.zone.run(() => this.goToPrev());
    } else if (event.key === 'ArrowRight') {
      event.preventDefault();
      this.zone.run(() => this.goToNext());
    }
  };
}
