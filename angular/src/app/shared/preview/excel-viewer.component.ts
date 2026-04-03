import { Component, signal, effect, computed, input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';

interface SheetData {
  name: string;
  rows: any[][];
  cols: number;
}

@Component({
  selector: 'app-excel-viewer',
  standalone: true,
  imports: [CommonModule, NzTabsModule, NzButtonModule, NzIconModule, NzSpinModule],
  templateUrl: './excel-viewer.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExcelViewerComponent {
  data = input.required<ArrayBuffer>();
  fileName = input('');

  sheets = signal<SheetData[]>([]);
  activeSheet = signal(0);
  isLoading = signal(true);
  error = signal('');
  scale = signal(1);

  currentSheet = computed(() => this.sheets()[this.activeSheet()] || null);
  colIndices = computed(() => {
    const sheet = this.currentSheet();
    if (!sheet) return [];
    return Array.from({ length: sheet.cols }, (_, i) => i);
  });

  constructor() {
    effect(() => {
      const d = this.data();
      if (d && d.byteLength > 0) {
        this.parseExcel(d);
      }
    });
  }

  private async parseExcel(data: ArrayBuffer) {
    try {
      this.isLoading.set(true);
      this.error.set('');

      const XLSX = await import('xlsx');
      const workbook = XLSX.read(data, { type: 'array' });
      const sheetData = workbook.SheetNames.map(name => {
        const sheet = workbook.Sheets[name];
        const rows: any[][] = XLSX.utils.sheet_to_json(sheet, { header: 1, defval: '' });
        const maxCols = rows.reduce((max, row) => Math.max(max, row.length), 0);
        return { name, rows, cols: maxCols };
      });

      this.sheets.set(sheetData);
      this.activeSheet.set(0);
    } catch (e: any) {
      console.error('Excel parse error:', e);
      this.error.set(e.message || 'Failed to parse Excel file');
    } finally {
      this.isLoading.set(false);
    }
  }

  onSheetChange(index: number) {
    this.activeSheet.set(index);
  }

  zoomIn() {
    this.scale.update(v => Math.min(1.5, v + 0.1));
  }

  zoomOut() {
    this.scale.update(v => Math.max(0.5, v - 0.1));
  }

  getScalePercent(): number {
    return Math.round(this.scale() * 100);
  }

  getColName(index: number): string {
    let name = '';
    let i = index;
    while (i >= 0) {
      name = String.fromCharCode(65 + (i % 26)) + name;
      i = Math.floor(i / 26) - 1;
    }
    return name;
  }
}
