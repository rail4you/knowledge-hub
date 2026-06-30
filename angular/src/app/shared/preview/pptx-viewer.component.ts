import {
  ChangeDetectionStrategy,
  Component,
  OnDestroy,
  computed,
  effect,
  input,
  signal,
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

interface SlideData {
  index: number;
  texts: SlideText[];
  images: string[];
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
export class PptxViewerComponent implements OnDestroy {
  /** Resource ID to load slides from server-side extraction */
  resourceId = input<string>('');
  fileName = input('');

  readonly slides = signal<SlideData[]>([]);
  readonly currentIndex = signal(0);
  readonly isLoading = signal(true);
  readonly error = signal('');
  readonly zoom = signal(1);

  readonly currentSlide = computed(() => this.slides()[this.currentIndex()] ?? null);
  readonly slideCount = computed(() => this.slides().length);
  readonly slideLabel = computed(() => `${this.currentIndex() + 1} / ${this.slideCount() || 0}`);
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

  private renderToken = 0;

  constructor() {
    effect(() => {
      const id = this.resourceId();
      if (id) {
        void this.loadFromServer(id);
      }
    });
  }

  ngOnDestroy() {
    this.renderToken++;
  }

  prevSlide() { if (this.canGoPrev()) this.currentIndex.update(v => v - 1); }
  nextSlide() { if (this.canGoNext()) this.currentIndex.update(v => v + 1); }
  goToSlide(index: number) {
    if (index >= 0 && index < this.slideCount()) {
      this.currentIndex.set(index);
      // 立即加载目标幻灯片
      void this.loadSlide(this.resourceId(), index + 1, this.renderToken);
      // 预加载邻近幻灯片
      this.preloadNearbySlides(index);
    }
  }
  zoomIn() { this.zoom.update(v => Math.min(2, v + 0.25)); }
  zoomOut() { this.zoom.update(v => Math.max(0.5, v - 0.25)); }
  resetZoom() { this.zoom.set(1); }
  pageTrackBy = (index: number) => index;

  private async loadFromServer(resourceId: string) {
    const token = ++this.renderToken;
    this.isLoading.set(true);
    this.error.set('');

    try {
      // Step 1: Get slide count
      const countResp = await fetch(`/api/resource-file/${resourceId}/slides/count`);
      if (!countResp.ok) throw new Error('无法获取幻灯片数量');
      const { count } = await countResp.json() as { count: number };

      if (token !== this.renderToken) return;

      // Initialize empty slide slots
      const slideArr: SlideData[] = Array.from({ length: count }, (_, i) => ({
        index: i + 1,
        texts: [],
        images: [],
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

      const data = await resp.json() as { slideNumber: number; texts: SlideText[]; images: string[] };
      if (token !== this.renderToken) return;

      // 将 images 路径转为完整的 media API URL
      const imageUrls = (data.images || []).map(
        img => `/api/resource-file/${resourceId}/media/${encodeURIComponent(img)}`
      );

      this.slides.update(arr => {
        const newArr = [...arr];
        if (newArr[idx]) {
          newArr[idx] = { ...newArr[idx], texts: data.texts || [], images: imageUrls, loaded: true };
        }
        return newArr;
      });
    } catch {
      // Silently fail for individual slides; user can retry by navigating
    }
  }

  private preloadNearbySlides(currentIdx: number) {
    const total = this.slideCount();
    const toLoad: number[] = [];

    // Load up to 3 slides ahead and 1 behind
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
}
