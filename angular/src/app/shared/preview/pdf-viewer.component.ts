import { Component, OnInit, OnDestroy, input, viewChild, ElementRef, signal, ChangeDetectionStrategy, AfterViewInit, NgZone, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';

@Component({
  selector: 'app-pdf-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule, NzSpinModule, NzDropDownModule],
  templateUrl: './pdf-viewer.component.html',
  styleUrls: ['./pdf-viewer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PdfViewerComponent implements OnInit, OnDestroy, AfterViewInit {
  /** 直接 PDF 文件 URL（pdf 类型，/preview 支持 Range 流式加载） */
  previewUrl = input<string>('');
  /** Office 文档：先轮询 /preview-pdf-info 等待后端转换，就绪后流式加载 /preview-pdf */
  resourceId = input<string>('');
  fileName = input('');

  currentPage = signal(1);
  totalPages = signal(0);
  scale = signal(1);
  isLoading = signal(true);
  error = signal('');
  renderedCount = signal(0);
  /** 后端是否仍在转换（Office 首次预览等待转换），用于区分「转换中」与「加载中」文案 */
  converting = signal(false);
  /** 已渲染的页码集合 */
  renderedPages = signal<Set<number>>(new Set());
  /** 当前是否处于"适应页面"缩放模式（整页在容器内完整可见） */
  fitActive = signal(false);
  /** 默认"占满宽度"模式下，页面高度放得下时垂直居中 */
  centerVertically = signal(false);

  private readonly containerRef = viewChild<ElementRef<HTMLDivElement>>('pdfContainer');
  private readonly pagesHostRef = viewChild<ElementRef<HTMLDivElement>>('pagesHost');

  private readonly zone = inject(NgZone);

  private pdfDoc: any = null;
  private pageItems: Map<number, { canvas: HTMLCanvasElement; wrapper: HTMLElement }> = new Map();
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
    const rid = this.resourceId();
    const url = this.previewUrl();

    if (rid && !this.loaded) {
      // Office 文档：轮询转换状态 → 流式加载整份 PDF
      setTimeout(() => {
        if (!this.destroyed && !this.loaded) {
          this.loadOfficePreview(rid);
        }
      }, 0);
    } else if (url && !this.loaded) {
      // 直接 PDF URL 流式加载
      setTimeout(() => {
        if (!this.destroyed && !this.loaded) {
          this.loadPdfFromUrl(url);
        }
      }, 0);
    }
  }

  ngOnDestroy() {
    document.removeEventListener('keydown', this.handleKeyDown);
    this.destroyed = true;
    this.pdfDoc = null;
    this.pageItems.clear();
  }

  private get pagesHost(): HTMLElement | null {
    return this.pagesHostRef()?.nativeElement ?? null;
  }

  isPageRendered(page: number): boolean {
    return this.renderedPages().has(page);
  }

  /** 导航到指定页，如果未渲染则立即渲染 */
  async goToPage(pageNum: number) {
    if (pageNum < 1 || pageNum > this.totalPages() || pageNum === this.currentPage()) return;
    this.currentPage.set(pageNum);

    // 适应页面模式下，切换到新页时按新页尺寸重新适配
    if (this.fitActive()) {
      await this.fitToPage(pageNum);
      return;
    }

    this.showPage(pageNum);

    // 如果该页未渲染，立即渲染
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

      // 默认占满舞台宽度（超高纵向滚动 / 放得下垂直居中），消除大面积留白
      await this.applyDefaultFit(1);

      // 优先渲染第 1 页，完成后立即展示
      await this.renderPageWithPriority(1);
      this.showPage(1);
      this.currentPage.set(1);
      this.isLoading.set(false);

      // 流式加载：只预渲染第 2 页，其余翻页时按需渲染（pdfjs Range 只取所需字节）。
      if (total > 1) {
        this.preloadNext(1);
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

  /**
   * Office 文档预览：轮询 /preview-pdf-info 等待媒体流水线转换就绪，再流式加载 /preview-pdf。
   * 转换由后端在资源上传后自动执行，前端只读取状态、不触发转换；
   * 未就绪时保持 loading（converting）状态，等后端完成即可。
   */
  private async loadOfficePreview(rid: string) {
    if (this.destroyed) return;

    const url = `/api/resource-file/${rid}/preview-pdf`;

    try {
      this.isLoading.set(true);
      this.converting.set(true);
      this.error.set('');
      this.renderedCount.set(0);
      this.renderedPages.set(new Set());
      this.loaded = false;

      let ready = false;
      let tooLarge = false;
      let polls = 0;
      while (!ready && !tooLarge && polls < 600 && !this.destroyed) {
        polls++;
        const resp = await fetch(`/api/resource-file/${rid}/preview-pdf-info`);
        if (resp.ok) {
          const info = await resp.json();
          if (info?.ready) {
            ready = true;
            break;
          }
          if (info?.tooLarge) {
            tooLarge = true;
            break;
          }
        }
        if (!ready && !tooLarge) {
          // 还没转换完，等 1 秒再试
          await new Promise((resolve) => setTimeout(resolve, 1000));
        }
      }

      if (tooLarge) {
        this.error.set('文件过大，暂不支持在线预览，请下载后查看');
        this.isLoading.set(false);
        this.loaded = true;
        return;
      }

      if (!ready) {
        this.error.set('文档转换超时，请稍后重试');
        this.isLoading.set(false);
        this.loaded = true;
        return;
      }

      this.converting.set(false);
      await this.loadPdfFromUrl(url);
    } catch (e: any) {
      console.error('PDF office preview error:', e);
      if (!this.destroyed) {
        this.error.set(e.message || 'Failed to load PDF');
        this.isLoading.set(false);
        this.loaded = true;
      }
    }
  }

  /** 流式预加载：只渲染当前页之后的 1 页；其余翻页时由 goToPage 按需渲染（Range 只取所需字节） */
  private preloadNext(current: number) {
    if (this.destroyed) return;
    const next = current + 1;
    if (next > this.totalPages()) return;
    if (this.isPageRendered(next)) return;
    void this.renderPageWithPriority(next);
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

  /** 后台逐页渲染已移除：流式预览改为只渲染当前页+下一页，其余按需渲染。 */
  private async reRenderAll() {
    if (this.destroyed) return;

    this.isLoading.set(true);
    this.renderedCount.set(0);
    this.renderedPages.set(new Set());

    const total = this.totalPages();
    const current = this.currentPage();

    // 保持当前页优先渲染并展示，下一页流式预加载，其余按需渲染
    await this.renderPageAtCurrentScale(current);
    this.showPage(current);
    this.currentPage.set(current);
    this.isLoading.set(false);

    if (total > 1) {
      this.preloadNext(current);
    }
  }

  /** 按当前缩放渲染指定页 */
  private async renderPageAtCurrentScale(pageNum: number) {
    await this.renderPageWithPriority(pageNum);
  }

  zoomIn() {
    if (this.scale() >= 3) return;
    this.fitActive.set(false);
    this.centerVertically.set(false);
    this.scale.update((v) => Math.min(3, v + 0.25));
    void this.reRenderAll();
  }

  zoomOut() {
    if (this.scale() <= 0.25) return;
    this.fitActive.set(false);
    this.centerVertically.set(false);
    this.scale.update((v) => Math.max(0.25, v - 0.25));
    void this.reRenderAll();
  }

  resetZoom() {
    this.fitActive.set(false);
    this.centerVertically.set(false);
    this.scale.set(1);
    void this.reRenderAll();
  }

  /** 缩放预设（下拉菜单）：'fit' = 适应页面，数字 = 固定比例 */
  applyScalePreset(v: number | 'fit') {
    if (v === 'fit') {
      void this.fitToPage();
    } else {
      this.fitActive.set(false);
      this.centerVertically.set(false);
      this.scale.set(v);
      void this.reRenderAll();
    }
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
    this.centerVertically.set(true);
    this.currentPage.set(target);

    // 清空已渲染标记：其余页翻到时会按新比例重渲
    this.renderedPages.set(new Set());
    this.renderedCount.set(0);

    // 以新缩放渲染当前页（其余页留待后台/翻页时重渲）
    await this.renderPageAtCurrentScale(target);
    this.showPage(target);
  }

  /** 取第 pageNum 页的原始尺寸（pt） */
  private async getPageDimensions(pageNum: number): Promise<{ width: number; height: number } | null> {
    if (this.pdfDoc) {
      try {
        const page = await this.pdfDoc.getPage(pageNum);
        const vp = page.getViewport({ scale: 1 });
        return { width: vp.width, height: vp.height };
      } catch {
        return null;
      }
    }
    return null;
  }

  /** 计算让第 pageNum 页在容器内完整可见的缩放比例（同时约束宽与高） */
  private async computeFitScale(pageNum: number): Promise<number | null> {
    const container = this.containerRef()?.nativeElement;
    if (!container || this.destroyed) return null;

    const dims = await this.getPageDimensions(pageNum);
    if (dims == null) return null;

    // 舞台内边距：水平 8px、垂直 12px
    const availH = container.clientHeight - 24;
    const availW = container.clientWidth - 16;
    if (availH <= 0 || availW <= 0) return null;

    const s = Math.min(availH / dims.height, availW / dims.width);
    return Math.max(0.25, Math.min(3, s));
  }

  /** 计算占满舞台宽度的缩放比例（超高时纵向滚动） */
  private async computeFitWidthScale(pageNum: number): Promise<number | null> {
    const container = this.containerRef()?.nativeElement;
    if (!container || this.destroyed) return null;

    const dims = await this.getPageDimensions(pageNum);
    if (dims == null || dims.width <= 0) return null;

    const availW = container.clientWidth - 16; // 水平留白 8px × 2
    if (availW <= 0) return null;

    return Math.max(0.25, Math.min(3, availW / dims.width));
  }

  /**
   * 默认适配：占满舞台宽度（左右仅小边距，无大片空白）。
   * 页面高度放得下时垂直居中，放不下则顶部对齐 + 纵向滚动。
   */
  private async applyDefaultFit(pageNum: number): Promise<boolean> {
    const container = this.containerRef()?.nativeElement;
    if (!container || this.destroyed) return false;

    const s = await this.computeFitWidthScale(pageNum);
    if (s == null) return false;

    const dims = await this.getPageDimensions(pageNum);

    this.scale.set(s);
    this.fitActive.set(false);

    const fittedHeight = dims ? dims.height * s : Infinity;
    this.centerVertically.set(fittedHeight <= container.clientHeight - 24);

    return true;
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
