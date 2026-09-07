import { Component, ChangeDetectionStrategy, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';

export interface BraillePair {
  text: string;
  pinyin?: string;
  braille: string;
  note?: string;
}

const BIT_BY_DOT = [0, 1, 2, 4, 8, 16, 32, 64, 128];

/** Unicode 盲文字符 → 点位（1-8）。非盲文字符返回空数组。 */
export function brailleCharToDots(ch: string): number[] {
  const code = ch.codePointAt(0) ?? 0;
  if (code < 0x2800 || code > 0x28ff) return [];
  const offset = code - 0x2800;
  const dots: number[] = [];
  for (let d = 1; d <= 8; d++) {
    if (offset & BIT_BY_DOT[d]) dots.push(d);
  }
  return dots;
}

/** 是否盲文空方（空格）。 */
export function isBrailleBlank(ch: string): boolean {
  return ch === '⠀';
}

/**
 * 盲文点位显示 + 翻译对照组件。
 * 左侧明文（原文/拼音），右侧盲文 Unicode 方 + 逐格 2×3 点位图 + 点位编号。
 */
@Component({
  selector: 'app-braille-viewer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, NzCardModule],
  template: `
  <div class="braille-pair" *ngFor="let p of pairs">
    <div class="braille-text">
      <div class="braille-original">{{ p.text }}</div>
      @if (p.pinyin) { <div class="braille-pinyin">{{ p.pinyin }}</div> }
      @if (p.note) { <div class="braille-note">{{ p.note }}</div> }
    </div>
    <div class="braille-cells">
      @for (cell of cellsOf(p.braille); track $index) {
        @if (cell.blank) {
          <div class="braille-gap" title="空方（词间分隔）"></div>
        } @else if (cell.dots.length === 0) {
          <div class="braille-char-fallback" [title]="cell.ch">{{ cell.ch }}</div>
        } @else {
          <div class="braille-cell" [title]="'点位 ' + cell.dots.join('、')">
            <div class="braille-glyph">{{ cell.ch }}</div>
            <div class="braille-dots">
              @for (d of [1, 2, 3, 4, 5, 6]; track d) {
                <span class="braille-dot" [class.raised]="cell.dots.includes(d)" [title]="'点' + d"></span>
              }
            </div>
            <div class="braille-dots-label">{{ cell.dots.join('') }}</div>
          </div>
        }
      }
    </div>
  </div>
  `,
  styles: [`
    .braille-pair { display: flex; gap: 16px; padding: 12px 0; border-bottom: 1px dashed #e8e8e8; flex-wrap: wrap; }
    .braille-text { min-width: 180px; max-width: 300px; }
    .braille-original { font-size: 18px; font-weight: 600; }
    .braille-pinyin { color: #888; font-size: 12px; margin-top: 2px; }
    .braille-note { color: #666; font-size: 12px; margin-top: 6px; }
    .braille-cells { display: flex; flex-wrap: wrap; gap: 8px; align-items: flex-start; flex: 1; }
    .braille-cell { border: 1px solid #e8e8e8; border-radius: 6px; padding: 6px 8px; text-align: center; background: #fafafa; }
    .braille-glyph { font-size: 26px; line-height: 1.2; }
    .braille-dots { display: grid; grid-template-rows: repeat(3, 14px); grid-auto-flow: column; grid-auto-columns: 14px; gap: 3px; justify-content: center; margin-top: 4px; }
    .braille-dot { width: 14px; height: 14px; border-radius: 50%; border: 1px solid #bbb; background: #fff; display: inline-block; }
    .braille-dot.raised { background: #111; border-color: #111; }
    .braille-dots-label { font-size: 11px; color: #888; margin-top: 2px; }
    .braille-gap { width: 18px; }
    .braille-char-fallback { font-size: 20px; padding: 6px 4px; color: #999; }
  `],
})
export class BrailleViewerComponent {
  @Input() pairs: BraillePair[] = [];

  cellsOf(braille: string): { ch: string; dots: number[]; blank: boolean }[] {
    return Array.from(braille ?? '').map(ch => ({
      ch,
      dots: brailleCharToDots(ch),
      blank: isBrailleBlank(ch),
    }));
  }
}
