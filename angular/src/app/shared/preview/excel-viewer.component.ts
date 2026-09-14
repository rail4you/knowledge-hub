import {
  Component,
  signal,
  effect,
  computed,
  input,
  viewChild,
  ElementRef,
  ChangeDetectionStrategy,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';

interface SheetData {
  name: string;
  rows: any[][];
  /** 全表实际列数（用于表头/表体列对齐，包含被截断的行） */
  cols: number;
  /** 全表实际行数（rows 可能被截断） */
  totalRows: number;
}

const MIN_SCALE = 0.5;
const MAX_SCALE = 2;
const SCALE_STEP = 0.1;
/** 单元格预览的行/列上限，防止超大表格卡死浏览器 */
const MAX_ROWS = 500;
const MAX_COLS = 100;

@Component({
  selector: 'app-excel-viewer',
  standalone: true,
  imports: [CommonModule, NzTabsModule, NzButtonModule, NzIconModule, NzSpinModule, NzTooltipModule],
  templateUrl: './excel-viewer.component.html',
  styleUrls: ['./excel-viewer.component.scss'],
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

  readonly minScale = MIN_SCALE;
  readonly maxScale = MAX_SCALE;
  readonly maxRows = MAX_ROWS;

  private readonly viewportRef = viewChild<ElementRef<HTMLDivElement>>('viewport');

  currentSheet = computed(() => this.sheets()[this.activeSheet()] || null);

  /** 列索引：以全表最大列数为准，保证表头与每一行 td 数量一致（列不错位） */
  colIndices = computed(() => {
    const sheet = this.currentSheet();
    if (!sheet) return [];
    const cols = Math.min(sheet.cols, MAX_COLS);
    return Array.from({ length: cols }, (_, i) => i);
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
      // cellDates: 把 Excel 日期序列号转成 Date；raw:false 让单元格按格式化文本显示
      // （如 1,234.00、2026/01/15），更贴近 Excel 里的实际观感。
      const workbook = XLSX.read(data, { type: 'array', cellDates: true });

      const sheetData: SheetData[] = workbook.SheetNames.map(name => {
        const sheet = workbook.Sheets[name];
        const allRows = XLSX.utils.sheet_to_json(sheet, {
          header: 1,
          defval: '',
          blankrows: false,
          raw: false,
        }) as any[][];

        const totalRows = allRows.length;
        const cols = allRows.reduce((max, row) => Math.max(max, row.length), 0);
        const rows = totalRows > MAX_ROWS ? allRows.slice(0, MAX_ROWS) : allRows;

        return { name, rows, cols, totalRows };
      });

      this.sheets.set(sheetData);
      this.activeSheet.set(0);
      this.scale.set(1);
    } catch (e: any) {
      console.error('Excel parse error:', e);
      this.error.set(e?.message || 'Failed to parse Excel file');
    } finally {
      this.isLoading.set(false);
    }
  }

  onSheetChange(index: number) {
    this.activeSheet.set(index);
    // 切换工作表后回到左上角，避免沿用上一张表的滚动位置
    const el = this.viewportRef()?.nativeElement;
    if (el) el.scrollTo({ top: 0, left: 0 });
  }

  isEmptyCell(value: unknown): boolean {
    return value === null || value === undefined || value === '';
  }

  zoomIn() {
    this.scale.update(v => Math.min(MAX_SCALE, +(v + SCALE_STEP).toFixed(2)));
  }

  zoomOut() {
    this.scale.update(v => Math.max(MIN_SCALE, +(v - SCALE_STEP).toFixed(2)));
  }

  resetZoom() {
    this.scale.set(1);
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
