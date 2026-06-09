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

interface SlideText {
  text: string;
  fontSize: number;
  bold: boolean;
  color: string;
}

interface Slide {
  index: number;
  texts: SlideText[];
  error?: string;
}

@Component({
  selector: 'app-pptx-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule],
  templateUrl: './pptx-viewer.component.html',
  styleUrls: ['./pptx-viewer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PptxViewerComponent implements OnDestroy {
  data = input<ArrayBuffer>();
  streamUrl = input('');
  fileName = input('');

  readonly slides = signal<Slide[]>([]);
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
      // Stream URL mode (preferred for large files): fetch from URL directly
      const url = this.streamUrl();
      if (url) {
        void this.loadFromUrl(url);
        return;
      }
      // ArrayBuffer mode (fallback for smaller files)
      const d = this.data();
      if (d?.byteLength > 0) {
        void this.parseZip(d);
      }
    });
  }

  ngOnDestroy() {
    this.renderToken++;
  }

  prevSlide() {
    if (this.canGoPrev()) {
      this.currentIndex.update(v => v - 1);
    }
  }

  nextSlide() {
    if (this.canGoNext()) {
      this.currentIndex.update(v => v + 1);
    }
  }

  goToSlide(index: number) {
    if (index >= 0 && index < this.slideCount()) {
      this.currentIndex.set(index);
    }
  }

  zoomIn() {
    this.zoom.update(v => Math.min(2, v + 0.25));
  }

  zoomOut() {
    this.zoom.update(v => Math.max(0.5, v - 0.25));
  }

  resetZoom() {
    this.zoom.set(1);
  }

  pageTrackBy = (index: number) => index;

  private async loadFromUrl(url: string) {
    const token = ++this.renderToken;
    this.isLoading.set(true);
    this.error.set('');
    this.slides.set([]);
    this.currentIndex.set(0);

    try {
      // Download in chunks to work around Angular proxy ~8MB response truncation.
      const arrayBuffer = await this.downloadChunked(url, token);
      if (token === this.renderToken) {
        console.log(`PPTX downloaded: ${arrayBuffer.byteLength} bytes`);
        await this.parseZip(arrayBuffer, token);
      }
    } catch (err: unknown) {
      if (token === this.renderToken) {
        console.error('PPTX fetch error:', err);
        this.error.set(err instanceof Error ? err.message : 'PPTX 加载失败');
        this.isLoading.set(false);
      }
    }
  }

  /** Download a file in 2MB chunks using HTTP Range requests, then reassemble.
   *  Avoids potential proxy/delivery truncation for large files.
   *  Uses Content-Length from HEAD; falls back to fileSize from input if HEAD not supported. */
  private async downloadChunked(url: string, token: number): Promise<ArrayBuffer> {
    // Try HEAD first, fall back to known size or single request
    let totalSize = 0;
    try {
      const headResp = await fetch(url, { method: 'HEAD' });
      if (headResp.ok) {
        const cl = headResp.headers.get('Content-Length');
        if (cl) totalSize = parseInt(cl, 10);
      }
    } catch {
      // HEAD may not be supported
    }

    if (totalSize === 0) {
      // No Content-Length — try single request (works for small files)
      const resp = await fetch(url);
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const data = await resp.arrayBuffer();
      console.log(`PPTX single-request: ${data.byteLength} bytes`);
      return data;
    }

    if (totalSize <= 4 * 1024 * 1024) {
      // Small file (< 4MB) — single request is fine
      const resp = await fetch(url);
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      return resp.arrayBuffer();
    }

    // Phase 2: download in 2MB chunks and reassemble
    const CHUNK = 2 * 1024 * 1024; // 2 MB per chunk
    const chunks: ArrayBuffer[] = [];
    let offset = 0;

    while (offset < totalSize) {
      if (token !== this.renderToken) throw new Error('Cancelled');
      const end = Math.min(offset + CHUNK - 1, totalSize - 1);
      const chunkResp = await fetch(url, {
        headers: { Range: `bytes=${offset}-${end}` },
        cache: 'no-cache',
      });
      if (!chunkResp.ok) throw new Error(`HTTP ${chunkResp.status}`);
      const chunk = await chunkResp.arrayBuffer();
      chunks.push(chunk);
      console.log(`PPTX chunk ${offset}-${end} -> ${chunk.byteLength} bytes`);
      offset = end + 1;
    }

    // Reassemble into single ArrayBuffer
    const total = chunks.reduce((s, c) => s + c.byteLength, 0);
    const result = new Uint8Array(total);
    let pos = 0;
    for (const c of chunks) {
      result.set(new Uint8Array(c), pos);
      pos += c.byteLength;
    }
    return result.buffer;
  }

  private async parseZip(data: ArrayBuffer, token?: number) {
    const t = token ?? ++this.renderToken;
    if (!token) {
      this.isLoading.set(true);
      this.error.set('');
      this.slides.set([]);
      this.currentIndex.set(0);
    }

    try {
      const JSZip = (await import('jszip')).default;
      const zip = await JSZip.loadAsync(data);

      // Find slide files sorted by number
      const slideFiles = Object.keys(zip.files)
        .filter(name => /^ppt\/slides\/slide\d+\.xml$/.test(name))
        .sort((a, b) => {
          const numA = parseInt(a.match(/\d+/)?.[0] || '0', 10);
          const numB = parseInt(b.match(/\d+/)?.[0] || '0', 10);
          return numA - numB;
        });

      if (slideFiles.length === 0) {
        throw new Error('PPTX 文件中未找到幻灯片');
      }

      const parsedSlides: Slide[] = [];
      for (const filePath of slideFiles) {
        if (t !== this.renderToken) return;
        try {
          const xml = await zip.file(filePath)?.async('text');
          if (xml) {
            const slideNum = parseInt(filePath.match(/\d+/)?.[0] || '0', 10);
            parsedSlides.push(this.parseSlideXml(xml, slideNum));
          }
        } catch {
          // Skip broken slides
        }
      }

      if (t === this.renderToken) {
        this.slides.set(parsedSlides);
        this.isLoading.set(false);
      }
    } catch (err: unknown) {
      if (t === this.renderToken) {
        console.error('PPTX parse error:', err);
        this.error.set(err instanceof Error ? err.message : 'PPTX 解析失败');
        this.isLoading.set(false);
      }
    }
  }

  private parseSlideXml(xml: string, index: number): Slide {
    const parser = new DOMParser();
    const doc = parser.parseFromString(xml, 'text/xml');

    // Check for parse errors
    const parseError = doc.querySelector('parsererror');
    if (parseError) {
      return { index, texts: [], error: '幻灯片 XML 解析错误' };
    }

    const texts: SlideText[] = [];
    const textNodes = doc.querySelectorAll('a\\:t, t');

    textNodes.forEach(t => {
      const text = t.textContent || '';
      if (!text.trim()) return;

      // Walk up to find formatting
      let rPr: Element | null = null;
      let parent = t.parentElement;
      while (parent && !rPr) {
        rPr = parent.querySelector('a\\:rPr, rPr');
        parent = parent.parentElement;
      }

      let fontSize = 18;
      let bold = false;
      let color = '#333333';

      if (rPr) {
        const sz = rPr.getAttribute('sz');
        if (sz) fontSize = parseInt(sz, 10) / 100;

        bold = rPr.querySelector('a\\:b, b') !== null;

        const solidFill = rPr.querySelector('a\\:solidFill, solidFill');
        if (solidFill) {
          const srgbClr = solidFill.querySelector('a\\:srgbClr, srgbClr');
          if (srgbClr) {
            const val = srgbClr.getAttribute('val');
            if (val) color = '#' + val;
          }
        }
      }

      texts.push({ text, fontSize, bold, color });
    });

    return { index, texts };
  }
}
