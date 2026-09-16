import {
  Component,
  OnInit,
  OnDestroy,
  AfterViewInit,
  input,
  signal,
  computed,
  inject,
  viewChild,
  ElementRef,
  ChangeDetectionStrategy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { AuthFetchService } from '../utils/auth-fetch.service';

/**
 * 逐页图片流式预览（低带宽场景）。
 * 把 Office 转换后的整份 PDF 拆成"每页一张 JPEG"按需加载：第 1 页仅几十 KB 秒出，
 * 翻页/滚动时才请求对应页（浏览器原生 lazy + HTTP 磁盘缓存），不再传输整份 PDF。
 * 后端端点：/api/resource-file/{id}/pdf-page-info、/api/resource-file/{id}/pdf-page/{n}。
 */
@Component({
  selector: 'app-pdf-page-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule, NzSpinModule, NzDropDownModule],
  template: `
    <div class="page-viewer">
      <div class="viewer-toolbar">
        <div class="toolbar-section toolbar-nav">
          <button nz-button nzSize="small" nzType="text" class="toolbar-btn nav-btn" (click)="goToPage(currentPage() - 1)" [disabled]="currentPage() <= 1" title="上一页">
            <span nz-icon nzType="left"></span>
          </button>
          <span class="page-info">{{ currentPage() }}<span class="page-total"> / {{ totalPages() }}</span></span>
          <button nz-button nzSize="small" nzType="text" class="toolbar-btn nav-btn" (click)="goToPage(currentPage() + 1)" [disabled]="currentPage() >= totalPages()" title="下一页">
            <span nz-icon nzType="right"></span>
          </button>
        </div>

        <div class="toolbar-spacer"></div>

        <div class="toolbar-section toolbar-zoom">
          <button nz-button nzSize="small" nzType="text" class="toolbar-btn zoom-btn" (click)="zoomOut()" [disabled]="scale() <= 0.25" title="缩小">
            <span nz-icon nzType="zoom-out"></span>
          </button>
          <button nz-button nzSize="small" nzType="text" class="toolbar-btn zoom-btn" [class.active]="scale() === 1" (click)="fitWidth()" title="适应宽度">
            <span nz-icon nzType="fullscreen"></span>
          </button>
          <button nz-button nzSize="small" nzType="text" nz-dropdown class="toolbar-btn scale-btn" [nzDropdownMenu]="scaleMenu" nzTrigger="click" title="选择缩放比例">
            {{ getScalePercent() }}%
            <span nz-icon nzType="down" class="scale-caret"></span>
          </button>
          <button nz-button nzSize="small" nzType="text" class="toolbar-btn zoom-btn" (click)="zoomIn()" [disabled]="scale() >= 3" title="放大">
            <span nz-icon nzType="zoom-in"></span>
          </button>
        </div>
      </div>

      <nz-dropdown-menu #scaleMenu="nzDropdownMenu">
        <ul nz-menu class="scale-menu">
          <li nz-menu-item [class.scale-active]="scale() === 1" (click)="fitWidth()">适应宽度</li>
          <li nz-menu-divider></li>
          <li nz-menu-item [class.scale-active]="scale() === 0.5" (click)="setScale(0.5)">50%</li>
          <li nz-menu-item [class.scale-active]="scale() === 0.75" (click)="setScale(0.75)">75%</li>
          <li nz-menu-item [class.scale-active]="scale() === 1" (click)="setScale(1)">100%</li>
          <li nz-menu-item [class.scale-active]="scale() === 1.25" (click)="setScale(1.25)">125%</li>
          <li nz-menu-item [class.scale-active]="scale() === 1.5" (click)="setScale(1.5)">150%</li>
          <li nz-menu-item [class.scale-active]="scale() === 2" (click)="setScale(2)">200%</li>
        </ul>
      </nz-dropdown-menu>

      <div #scrollHost class="page-scroll">
        @for (p of pages(); track p) {
          <div class="page-item" [class.current]="p === currentPage()">
            <img
              class="page-img"
              [src]="pageUrl(p)"
              [loading]="p === 1 ? 'eager' : 'lazy'"
              decoding="async"
              [style.aspect-ratio]="aspectRatio()"
              [style.width]="scale() * 100 + '%'"
              alt="第 {{ p }} 页"
            />
          </div>
        }
      </div>

      @if (loading()) {
        <div class="page-loading-overlay">
          <div class="page-loading-inner">
            <nz-spin nzSimple nzSize="large"></nz-spin>
            <p class="page-loading-text">{{ converting() ? '正在转换文档…' : '正在加载…' }}</p>
          </div>
        </div>
      }

      @if (error()) {
        <div class="viewer-error">{{ error() }}</div>
      }
    </div>
  `,
  styles: [
    `
      .viewer-toolbar {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 8px 14px;
        background: #fff;
        border-bottom: 1px solid #ececec;
        flex-shrink: 0;
        flex-wrap: nowrap;
        user-select: none;
        -webkit-user-select: none;
      }
      .toolbar-section { display: flex; align-items: center; gap: 2px; }
      .toolbar-nav::after {
        content: '';
        width: 1px;
        height: 18px;
        background: #e8e8e8;
        margin-left: 10px;
      }
      .toolbar-spacer { flex: 1; }
      .toolbar-btn {
        width: 30px;
        height: 30px;
        min-width: 30px;
        padding: 0;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        border-radius: 6px;
        color: #595959;
        font-size: 14px;
        transition: all 0.15s;
      }
      .toolbar-btn:hover:not([disabled]) { color: #1890ff; background: #f0f7ff; }
      .toolbar-btn[disabled] { color: #d9d9d9; }
      .toolbar-btn.active { color: #1890ff; background: #e6f7ff; border: 1px solid #91caff; }
      .scale-btn {
        width: auto;
        min-width: 64px;
        padding: 0 8px;
        margin: 0 2px;
        font-size: 13px;
        font-weight: 500;
        font-variant-numeric: tabular-nums;
        border-radius: 6px;
      }
      .scale-btn .scale-caret { font-size: 10px; margin-left: 4px; color: #bfbfbf; }
      .scale-btn:hover:not([disabled]) .scale-caret { color: #1890ff; }
      .page-info {
        font-size: 13px;
        font-weight: 500;
        color: #262626;
        min-width: 52px;
        text-align: center;
        line-height: 30px;
        font-variant-numeric: tabular-nums;
      }
      .page-info .page-total { color: #bfbfbf; font-weight: 400; }
      .scale-menu { min-width: 140px; }
      .scale-menu .scale-active { font-weight: 600; color: #1890ff; }
      .viewer-error { padding: 20px; text-align: center; color: #ff4d4f; }

      .page-viewer {
        display: flex;
        flex-direction: column;
        height: calc(90vh - 200px);
        min-height: 420px;
        position: relative;
        overflow: hidden;
      }
      .page-scroll {
        flex: 1;
        overflow: auto;
        padding: 16px 12px;
        background: #505359;
        scrollbar-width: thin;
        scrollbar-color: #6c6f75 transparent;
      }
      .page-scroll::-webkit-scrollbar { width: 10px; height: 10px; }
      .page-scroll::-webkit-scrollbar-track { background: transparent; }
      .page-scroll::-webkit-scrollbar-thumb { background: #6c6f75; border-radius: 5px; }
      .page-scroll::-webkit-scrollbar-thumb:hover { background: #83868c; }
      .page-item {
        margin: 0 auto 16px;
        text-align: center;
        transition: opacity 0.2s;
      }
      .page-item.current { opacity: 1; }
      .page-img {
        display: block;
        margin: 0 auto;
        background: #fff;
        box-shadow: 0 2px 6px rgba(0, 0, 0, 0.28), 0 12px 40px rgba(0, 0, 0, 0.32);
        border-radius: 4px;
        user-select: none;
        -webkit-user-select: none;
      }
      .page-loading-overlay {
        position: absolute;
        inset: 0;
        background: rgba(80, 83, 89, 0.88);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 10;
      }
      .page-loading-inner { text-align: center; }
      .page-loading-text {
        margin-top: 12px;
        font-size: 13px;
        color: #e8e8e8;
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PdfPageViewerComponent implements OnInit, OnDestroy, AfterViewInit {
  readonly resourceId = input<string>('');

  private readonly authFetch = inject(AuthFetchService);
  private readonly scrollHostRef = viewChild<ElementRef<HTMLDivElement>>('scrollHost');

  readonly currentPage = signal(1);
  readonly totalPages = signal(0);
  readonly pageWidth = signal(0);
  readonly pageHeight = signal(0);
  readonly scale = signal(1);
  readonly loading = signal(true);
  readonly converting = signal(false);
  readonly error = signal('');

  readonly pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));
  readonly aspectRatio = computed(() => {
    const w = this.pageWidth();
    const h = this.pageHeight();
    return w > 0 && h > 0 ? `${w} / ${h}` : '16 / 9';
  });

  private destroy = false;
  private observer: IntersectionObserver | null = null;
  private pageEls: HTMLElement[] = [];

  ngOnInit() {
    this.loadInfo();
  }

  ngAfterViewInit() {}

  ngOnDestroy() {
    this.destroy = true;
    this.observer?.disconnect();
  }

  private pageUrl(page: number): string {
    return this.authFetch.withAccessToken(`/api/resource-file/${this.resourceId()}/pdf-page/${page}?w=2000`);
  }

  private loadInfo(): void {
    const rid = this.resourceId();
    if (!rid) return;

    this.loading.set(true);
    this.converting.set(true);

    const poll = () => {
      if (this.destroy) return;
      this.authFetch.fetch(`/api/resource-file/${rid}/pdf-page-info`)
        .then((r) => (r.ok ? r.json() : { ready: false, pageCount: 0 }))
        .then((info) => {
          if (this.destroy) return;
          if (info?.tooLarge) {
            this.error.set('文件过大，暂不支持在线预览，请下载后查看');
            this.loading.set(false);
            return;
          }
          if (info?.ready && info.pageCount > 0) {
            this.totalPages.set(info.pageCount);
            this.pageWidth.set(info.pageWidth || 0);
            this.pageHeight.set(info.pageHeight || 0);
            this.loading.set(false);
            this.converting.set(false);
            setTimeout(() => this.observePages(), 0);
            return;
          }
          // 未就绪：继续轮询
          setTimeout(poll, 1000);
        })
        .catch(() => {
          if (this.destroy) return;
          this.error.set('预览加载失败，请稍后重试');
          this.loading.set(false);
        });
    };

    poll();
  }

  private observePages(): void {
    const host = this.scrollHostRef()?.nativeElement;
    if (!host) return;
    this.observer?.disconnect();

    this.pageEls = Array.from(host.querySelectorAll<HTMLElement>('.page-item'));
    if (this.pageEls.length === 0) return;

    this.observer = new IntersectionObserver(
      (entries) => {
        for (const e of entries) {
          if (e.isIntersecting) {
            const idx = this.pageEls.indexOf(e.target as HTMLElement);
            if (idx >= 0 && idx + 1 !== this.currentPage()) {
              this.currentPage.set(idx + 1);
            }
          }
        }
      },
      { root: host, threshold: 0.5 },
    );
    this.pageEls.forEach((el) => this.observer!.observe(el));
  }

  goToPage(page: number): void {
    const el = this.pageEls[page - 1];
    if (el) el.scrollIntoView({ block: 'start', behavior: 'smooth' });
  }

  zoomIn(): void {
    this.setScale(Math.min(3, this.scale() * 1.25));
  }

  zoomOut(): void {
    this.setScale(Math.max(0.25, this.scale() / 1.25));
  }

  fitWidth(): void {
    this.setScale(1);
  }

  setScale(scale: number): void {
    this.scale.set(Math.round(scale * 100) / 100);
  }

  getScalePercent(): number {
    return Math.round(this.scale() * 100);
  }
}