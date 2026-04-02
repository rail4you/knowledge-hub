import { Component, signal, effect, computed, input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';

interface SlideContent {
  texts: { value: string; fontSize?: number; bold?: boolean; color?: string }[];
  images: { src: string; x: number; y: number; width: number; height: number }[];
}

@Component({
  selector: 'app-pptx-viewer',
  standalone: true,
  imports: [CommonModule, NzButtonModule, NzIconModule, NzSpinModule],
  templateUrl: './pptx-viewer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PptxViewerComponent {
  data = input.required<ArrayBuffer>();
  fileName = input('');

  slides = signal<SlideContent[]>([]);
  currentSlide = signal(0);
  isLoading = signal(true);
  error = signal('');
  showGrid = signal(false);
  scale = signal(1);

  slide = computed(() => this.slides()[this.currentSlide()] || null);

  constructor() {
    effect(() => {
      const d = this.data();
      if (d && d.byteLength > 0) {
        this.parsePptx(d);
      }
    });
  }

  private async parsePptx(data: ArrayBuffer) {
    try {
      this.isLoading.set(true);
      this.error.set('');
      const JSZip = (await import('jszip')).default;
      const zip = await JSZip.loadAsync(data);

      const slideFiles: string[] = [];
      zip.forEach((relativePath) => {
        const match = relativePath.match(/^ppt\/slides\/slide(\d+)\.xml$/);
        if (match) {
          slideFiles.push(relativePath);
        }
      });
      slideFiles.sort((a, b) => {
        const numA = parseInt(a.match(/slide(\d+)/)?.[1] || '0');
        const numB = parseInt(b.match(/slide(\d+)/)?.[1] || '0');
        return numA - numB;
      });

      const parsedSlides: SlideContent[] = [];

      for (const slidePath of slideFiles) {
        const slideXml = await zip.file(slidePath)?.async('text') || '';
        const slideNum = slidePath.match(/slide(\d+)/)?.[1];

        // Parse relationships to get image mappings
        const relsPath = `ppt/slides/_rels/slide${slideNum}.xml.rels`;
        const relsXml = await zip.file(relsPath)?.async('text') || '';
        const imageRels: Record<string, string> = {};
        const relRegex = /Target="([^"]*media[^"]*)"[^>]*Id="([^"]*)"/g;
        let relMatch;
        while ((relMatch = relRegex.exec(relsXml)) !== null) {
          imageRels[relMatch[2]] = relMatch[1].replace('../', 'ppt/');
        }

        // Extract texts
        const texts: SlideContent['texts'] = [];
        const textRegex = /<a:t>([^<]*)<\/a:t>/g;
        let textMatch;
        while ((textMatch = textRegex.exec(slideXml)) !== null) {
          const value = textMatch[1].trim();
          if (value) {
            const beforeText = slideXml.substring(0, textMatch.index);
            const rPrMatch = beforeText.match(/<a:rPr[^>]*sz="(\d+)"[^>]*(?:b="1")?[^>]*>/g);
            let fontSize: number | undefined;
            let bold = false;
            if (rPrMatch) {
              const last = rPrMatch[rPrMatch.length - 1];
              const szMatch = last.match(/sz="(\d+)"/);
              if (szMatch) fontSize = parseInt(szMatch[1]) / 100;
              if (last.includes('b="1"')) bold = true;
            }
            texts.push({ value, fontSize, bold });
          }
        }

        // Extract images
        const images: SlideContent['images'] = [];
        const imgRegex = /<a:blip[^>]*r:embed="([^"]*)"[^>]*\/>/g;
        let imgMatch;
        let imgIndex = 0;
        while ((imgMatch = imgRegex.exec(slideXml)) !== null) {
          const rId = imgMatch[1];
          const imagePath = imageRels[rId];
          if (imagePath) {
            const imageFile = zip.file(imagePath);
            if (imageFile) {
              const blob = await imageFile.async('blob');
              const url = URL.createObjectURL(blob);
              images.push({
                src: url,
                x: 50 + (imgIndex % 2) * 200,
                y: 100 + Math.floor(imgIndex / 2) * 200,
                width: 300,
                height: 200,
              });
              imgIndex++;
            }
          }
        }

        parsedSlides.push({ texts, images });
      }

      this.slides.set(parsedSlides);
      this.currentSlide.set(0);
    } catch (e: any) {
      console.error('PPTX parse error:', e);
      this.error.set(e.message || 'Failed to parse PPTX file');
    } finally {
      this.isLoading.set(false);
    }
  }

  prevSlide() {
    if (this.currentSlide() <= 0) return;
    this.currentSlide.update(v => v - 1);
  }

  nextSlide() {
    if (this.currentSlide() >= this.slides().length - 1) return;
    this.currentSlide.update(v => v + 1);
  }

  goToSlide(index: number) {
    this.currentSlide.set(index);
    this.showGrid.set(false);
  }

  toggleGrid() {
    this.showGrid.update(v => !v);
  }

  zoomIn() {
    this.scale.update(v => Math.min(2, v + 0.25));
  }

  zoomOut() {
    this.scale.update(v => Math.max(0.5, v - 0.25));
  }

  getScalePercent(): number {
    return Math.round(this.scale() * 100);
  }
}
