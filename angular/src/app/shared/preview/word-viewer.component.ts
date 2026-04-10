import { Component, signal, effect, input, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';

@Component({
  selector: 'app-word-viewer',
  standalone: true,
  imports: [CommonModule, NzSpinModule],
  templateUrl: './word-viewer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WordViewerComponent {
  data = input.required<ArrayBuffer>();
  fileName = input('');

  htmlContent = signal<SafeHtml>('');
  isLoading = signal(true);
  error = signal('');

  private readonly sanitizer = inject(DomSanitizer);

  constructor() {
    effect(() => {
      const d = this.data();
      if (d && d.byteLength > 0) {
        this.parseWord(d);
      }
    });
  }

  private async parseWord(data: ArrayBuffer) {
    try {
      this.isLoading.set(true);
      this.error.set('');

      const mammoth = await import('mammoth');
      const result = await mammoth.default.convertToHtml({ arrayBuffer: data });
      const safeHtml = this.sanitizer.bypassSecurityTrustHtml(result.value);
      this.htmlContent.set(safeHtml);

      if (result.messages.length > 0) {
        console.warn('Mammoth warnings:', result.messages);
      }
    } catch (e: any) {
      console.error('Word parse error:', e);
      this.error.set(e.message || 'Failed to parse Word document');
    } finally {
      this.isLoading.set(false);
    }
  }
}
