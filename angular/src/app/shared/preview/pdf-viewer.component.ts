import {
  Component,
  OnInit,
  OnDestroy,
  input,
  output,
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
  /** Full PDF URL mode */
  previewUrl = input<string>('');
  /** Per-page PDF mode: resource ID for /preview-pdf-page/{pageNum} */
  resourceId = input<string>('');
  /** ArrayBuffer mode (legacy) */
  data = input<ArrayBuffer>(new ArrayBuffer(0));
  fileName = input('');
  /** 加载失败时触发（如 PPTX 转换失败，调用方可降级到幻灯片预览） */
  loadFailed = output<void>();

  currentPage = signal(1);
  totalPages = signal(0);
  scale = signal(1);
  isLoading = signal(true);
  error = signal('');
  renderedCount = signal(0);
  /** 已渲染的页码集合 */
  renderedPages = signal<Set<number>>(new Set());
  /** 当前是否处于"适应页面"缩放模式（整页在容器内完整可见） */
  fitActive = signal(false);

  private readonly containerRef = viewChild<ElementRef<HTMLDivElement>>('pdfContainer');
  private readonly pagesHostRef = viewChild<ElementRef<HTMLDivElement>>('pagesHost');

  private readonly zone = inject(NgZone);

  private pdfDoc: any = null;
  private pageItems: Map<number, { canvas: HTMLCanvasElement; wrapper: HTMLElement }> = new Map();
  private renderQueue: number[] = [];
  private loaded = false;
  private destroyed = false;
  /** Per-page PDF mode: resource ID for page URLs */
  private pageResourceId = '';
  /** Total pages known in per-page mode */
  private knownTotalPages = 0;

  constructor() {
    // 键盘快捷键：← → 翻页
    this.zone.runOutsideAngular(() => {
      document.addEventListener('keydown', this.handleKeyDown);
    });
  }

  ngOnInit() {}

  ngAfterViewInit() {
    const rid = this.resourceId();
    const url = this.previewUrl();
    const d = this.data();

    if (rid && !this.loaded) {
      // Per-page PDF mode: 每页独立 PDF，首页秒出
      setTimeout(() => {
        if (!this.destroyed && !this.loaded) {
          this.loadPdfPerPage(rid);
        }
      }, 0);
    } else if (url && !this.loaded) {
      // Full URL streaming mode
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

  /** 导航到指定页，如果未渲染则优先从服务器加载 */
  async goToPage(pageNum: number) {
    if (pageNum < 1 || pageNum > this.totalPages() || pageNum === this.currentPage()) return;
    this.currentPage.set(pageNum);

    // 适应页面模式下，切换到新页时按新页尺寸重新适配
    if (this.fitActive()) {
      await this.fitToPage(pageNum);
      return;
    }

    this.showPage(pageNum);

    // 如果该页未渲染，立即加载
    if (!this.isPageRendered(pageNum)) {
      if (this.pageResourceId) {
        // Per-page mode: 从服务器加载单页 PDF
        const url = `/api/resource-file/${this.pageResourceId}/preview-pdf-page/${pageNum}`;
        await this.renderPerPagePdf(pageNum, url);
      } else {
        // Full PDF mode: 从已加载的 doc 渲染
        await this.renderPageWithPriority(pageNum);
      }
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
      pdfjsLib.GlobalWorkerOptions.workerSrc = 'assets/pdfjs/pdf.worker.min.mjs';

      const loadingTask = pdfjsLib.getDocument({
        url,
        cMapUrl: 'assets/pdfjs/cmaps/',
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

      // 首次加载自动"适应页面"：整页在容器内完整可见
      const fitScale = await this.computeFitScale(1);
      if (fitScale != null && !this.destroyed) {
        this.scale.set(fitScale);
        this.fitActive.set(true);
      }

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
      this.loadFailed.emit();
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
      pdfjsLib.GlobalWorkerOptions.workerSrc = 'assets/pdfjs/pdf.worker.min.mjs';

      const copy = data.slice(0);
      const loadingTask = pdfjsLib.getDocument({
        data: new Uint8Array(copy),
        cMapUrl: 'assets/pdfjs/cmaps/',
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
      this.loadFailed.emit();
      if (!this.destroyed) {
        this.error.set(e.message || 'Failed to load PDF');
        this.isLoading.set(false);
        this.loaded = true;
      }
    }
  }

  /** 逐页加载模式：先获取总页数，然后按需加载每页的小 PDF */
  private async loadPdfPerPage(rid: string) {
    if (this.destroyed) return;

    try {
      this.isLoading.set(true);
      this.error.set('');
      this.renderedCount.set(0);
      this.renderedPages.set(new Set());
      this.renderQueue = [];
      this.loaded = false;
      this.pageResourceId = rid;

      // 轮询 /preview-pdf-info 等待后端拆分完成（PPTX 后端转换可能需要 20s+）
      // 一次请求 = 一个 ready 回答，避免 500 个 HEAD 探测
      let total = 0;
      let polls = 0;
      while (total === 0 && polls < 120 && !this.destroyed) {
        polls++;
        const resp = await fetch(`/api/resource-file/${rid}/preview-pdf-info`);
        if (resp.ok) {
          const info = await resp.json();
          if (info?.ready && info.count > 0) {
            total = info.count;
            break;
          }
        }
        if (total === 0) {
          // 还没转换完，等 1 秒再试
          await new Promise((resolve) => setTimeout(resolve, 1000));
        }
      }

      if (total === 0) {
        this.error.set('文档转换超时，请稍后重试');
        this.isLoading.set(false);
        this.loaded = true;
        return;
      }
      this.totalPages.set(total);
      this.knownTotalPages = total;

      // 更新 loading 文案提示用户等待转换
      if (polls > 1) {
        this.renderedCount.set(0); // triggers template re-eval for loading text update
      }

      // 创建所有页面的 canvas 占位
      this.zone.runOutsideAngular(() => {
        this.createAllCanvases(total);
      });

      await new Promise((resolve) => setTimeout(resolve, 50));
      if (this.destroyed) return;

      this.loaded = true;

      // 加载并渲染第 1 页，完成后立即显示
      const page1Url = `/api/resource-file/${rid}/preview-pdf-page/1`;
      await this.renderPerPagePdf(1, page1Url);
      this.showPage(1);
      this.currentPage.set(1);
      this.isLoading.set(false);

      // 后台预加载其余页面
      if (total > 1) {
        this.renderQueue = Array.from({ length: total - 1 }, (_, i) => i + 2);
        this.preloadPerPageInBackground(rid);
      }
    } catch (e: any) {
      console.error('PDF per-page load error:', e);
      this.loadFailed.emit();
      if (!this.destroyed) {
        this.error.set(e.message || 'Failed to load PDF');
        this.isLoading.set(false);
        this.loaded = true;
      }
    }
  }

  /** 后台逐个预加载单页 PDF */
  private preloadPerPageInBackground(rid: string) {
    if (this.destroyed) return;
    if (this.renderQueue.length === 0) return;

    const pageNum = this.renderQueue.shift()!;
    const url = `/api/resource-file/${rid}/preview-pdf-page/${pageNum}`;
    this.renderPerPagePdf(pageNum, url).finally(() => {
      if (!this.destroyed) {
        setTimeout(() => this.preloadPerPageInBackground(rid), 100);
      }
    });
  }

  /** 渲染单页 PDF（每页是独立的 ~200KB PDF 文件） */
  private async renderPerPagePdf(pageNum: number, url: string) {
    if (this.destroyed) return;
    if (this.isPageRendered(pageNum)) return;

    try {
      const pdfjsLib = await import('pdfjs-dist');
      pdfjsLib.GlobalWorkerOptions.workerSrc = 'assets/pdfjs/pdf.worker.min.mjs';

      const doc = await pdfjsLib.getDocument({
        url,
        cMapUrl: 'assets/pdfjs/cmaps/',
        cMapPacked: true,
      }).promise;

      const page = await doc.getPage(1); // 每页 PDF 只有 1 页
      const viewport = page.getViewport({ scale: this.scale() });
      const item = this.pageItems.get(pageNum);
      if (!item) { doc.destroy(); return; }

      item.canvas.height = viewport.height;
      item.canvas.width = viewport.width;

      await this.zone.runOutsideAngular(() =>
        page.render({ canvasContext: item.canvas.getContext('2d')!, viewport }).promise
      );

      doc.destroy();

      this.renderedPages.update((s) => {
        const next = new Set(s);
        next.add(pageNum);
        return next;
      });
      this.renderedCount.update((v) => v + 1);
    } catch (e) {
      console.warn(`Per-page render ${pageNum} error:`, e);
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
      const viewport = page.getViewport({ scale: this.scale() });
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
      const viewport = page.getViewport({ scale: this.scale() });
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
    const current = this.currentPage();

    // 保持当前页优先渲染并展示，其余页后台重渲
    await this.renderPageWithPriority(current);
    this.showPage(current);
    this.currentPage.set(current);
    this.isLoading.set(false);

    if (total > 1) {
      this.renderQueue = Array.from({ length: total }, (_, i) => i + 1).filter((p) => p !== current);
      this.backgroundRenderAll();
    }
  }

  zoomIn() {
    if (this.scale() >= 3) return;
    this.fitActive.set(false);
    this.scale.update((v) => Math.min(3, v + 0.25));
    this.reRenderAll();
  }

  zoomOut() {
    if (this.scale() <= 0.25) return;
    this.fitActive.set(false);
    this.scale.update((v) => Math.max(0.25, v - 0.25));
    this.reRenderAll();
  }

  resetZoom() {
    this.fitActive.set(false);
    this.scale.set(1);
    this.reRenderAll();
  }

  /**
   * 适应页面：将当前页缩放到容器内完整可见（同时满足宽度与高度）。
   * 作为模式保持，翻页时按新页尺寸重新适配。
   */
  async fitToPage(pageNum?: number) {
    if (this.isLoading() || this.destroyed) return;
    const target = pageNum ?? this.currentPage();
    const s = await this.computeFitScale(target);
    if (s == null) return;

    this.scale.set(s);
    this.fitActive.set(true);
    this.currentPage.set(target);

    // 以新缩放渲染当前页（其余页留待后台/翻页时重渲）
    await this.renderPageWithPriority(target);
    this.showPage(target);
  }

  /** 计算让第 pageNum 页在容器内完整可见的缩放比例（同时约束宽与高） */
  private async computeFitScale(pageNum: number): Promise<number | null> {
    const container = this.containerRef()?.nativeElement;
    if (!container || this.destroyed) return null;

    let width = 0;
    let height = 0;

    if (this.pdfDoc) {
      try {
        const page = await this.pdfDoc.getPage(pageNum);
        const vp = page.getViewport({ scale: 1 });
        width = vp.width;
        height = vp.height;
      } catch {
        return null;
      }
    } else if (this.pageResourceId) {
      try {
        const pdfjsLib = await import('pdfjs-dist');
        pdfjsLib.GlobalWorkerOptions.workerSrc = 'assets/pdfjs/pdf.worker.min.mjs';
        const doc = await pdfjsLib.getDocument({
          url: `/api/resource-file/${this.pageResourceId}/preview-pdf-page/${pageNum}`,
        }).promise;
        const page = await doc.getPage(1);
        const vp = page.getViewport({ scale: 1 });
        width = vp.width;
        height = vp.height;
        doc.destroy();
      } catch {
        return null;
      }
    } else {
      return null;
    }

    if (width <= 0 || height <= 0) return null;

    // 上下留白各 24px，左右留白各 24px
    const availH = container.clientHeight - 48;
    const availW = container.clientWidth - 48;
    if (availH <= 0 || availW <= 0) return null;

    const s = Math.min(availH / height, availW / width);
    return Math.max(0.25, Math.min(3, s));
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
