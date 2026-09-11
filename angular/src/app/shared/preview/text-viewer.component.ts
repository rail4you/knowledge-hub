import { Component, signal, computed, effect, input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzSpinModule } from 'ng-zorro-antd/spin';

@Component({
  selector: 'app-text-viewer',
  standalone: true,
  imports: [CommonModule, NzSpinModule],
  templateUrl: './text-viewer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TextViewerComponent {
  data = input.required<ArrayBuffer>();
  fileName = input('');

  content = signal('');
  isLoading = signal(true);
  error = signal('');

  /** 仅在文本变化时拆分一次，避免每轮变更检测都重新 split 整个文件 */
  readonly lines = computed(() => this.content().split('\n'));

  constructor() {
    effect(() => {
      const d = this.data();
      if (d && d.byteLength > 0) {
        this.decodeText(d);
      }
    });
  }

  private decodeText(data: ArrayBuffer) {
    try {
      this.isLoading.set(true);
      this.error.set('');

      const uint8 = new Uint8Array(data);

      // Try UTF-8 first, fallback to GBK if it contains replacement characters
      const utf8Decoder = new TextDecoder('utf-8');
      let text = utf8Decoder.decode(uint8);

      if (text.includes('\uFFFD')) {
        const gbkDecoder = new TextDecoder('gbk');
        text = gbkDecoder.decode(uint8);
      }

      this.content.set(text);
    } catch (e: any) {
      this.error.set(e.message || 'Failed to decode text file');
    } finally {
      this.isLoading.set(false);
    }
  }
}
