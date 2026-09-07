import { Component, ChangeDetectionStrategy, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';

export interface ContentVersionEntry {
  versionNumber: number;
  creationTime?: string;
  creatorName?: string;
  value: any;
}

/**
 * 结构化字段的历史版本查看/采用：
 * 父组件按字段 key 把各版本快照映射为 value 列表传入，本组件负责展开、格式化展示与回填。
 */
@Component({
  selector: 'app-content-version-field',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, NzButtonModule],
  styles: [`
    .cvf-toggle { font-size: 12px; color: #1677ff; cursor: pointer; user-select: none; }
    .cvf-toggle:hover { text-decoration: underline; }
    .cvf-panel { margin-top: 6px; border: 1px solid #f0f0f0; border-radius: 8px; background: #fafbfc; max-height: 260px; overflow-y: auto; }
    .cvf-item { padding: 8px 10px; border-bottom: 1px solid #f0f0f0; }
    .cvf-item:last-child { border-bottom: 0; }
    .cvf-meta { display: flex; align-items: center; gap: 8px; font-size: 12px; color: #888; margin-bottom: 4px; }
    .cvf-meta b { color: #1677ff; }
    .cvf-val { font-size: 13px; color: rgba(0,0,0,.85); white-space: pre-wrap; word-break: break-word; margin: 0 0 6px; }
    .cvf-val .li { margin: 0 0 2px; }
    .cvf-sec { margin: 0 0 6px; }
    .cvf-sec b { font-weight: 600; }
    .cvf-empty { font-size: 12px; color: #bbb; }
  `],
  template: `
    @if (versions().length > 0) {
      <a class="cvf-toggle" (click)="toggle()">{{ open() ? '收起历史版本' : '查看历史版本 (' + versions().length + ')' }}</a>
      @if (open()) {
        <div class="cvf-panel">
          @for (v of versions(); track v.versionNumber) {
            <div class="cvf-item">
              <div class="cvf-meta">
                <b>v{{ v.versionNumber }}</b>
                <span>{{ v.creationTime | date:'yyyy-MM-dd HH:mm' }}</span>
                @if (v.creatorName) { <span>{{ v.creatorName }}</span> }
                <button nz-button nzType="link" nzSize="small" (click)="adopt.emit(v.value)">采用此版</button>
              </div>
              @switch (kind()) {
                @case ('list') {
                  @for (li of asList(v.value); track $index) { <p class="cvf-val li">• {{ li }}</p> }
                  @empty { <span class="cvf-empty">（该版本无此字段）</span> }
                }
                @case ('sections') {
                  @for (s of asList(v.value); track $index) {
                    <div class="cvf-sec"><b>{{ s?.name }}（{{ s?.duration }}分钟）</b><p class="cvf-val">{{ s?.content }}</p></div>
                  }
                  @empty { <span class="cvf-empty">（该版本无此字段）</span> }
                }
                @case ('pairs') {
                  @for (p of asList(v.value); track $index) {
                    <p class="cvf-val li">{{ p?.text }} → {{ p?.braille }}</p>
                  }
                  @empty { <span class="cvf-empty">（该版本无此字段）</span> }
                }
                @default {
                  @if (isEmpty(v.value)) { <span class="cvf-empty">（该版本无此字段）</span> }
                  @else { <p class="cvf-val">{{ v.value }}</p> }
                }
              }
            </div>
          }
        </div>
      }
    }
  `,
})
export class ContentVersionFieldComponent {
  versions = input<ContentVersionEntry[]>([]);
  kind = input<'text' | 'list' | 'sections' | 'pairs'>('text');
  adopt = output<any>();
  open = signal(false);

  toggle(): void {
    this.open.update(v => !v);
  }

  asList(v: any): any[] {
    return Array.isArray(v) ? v : [];
  }

  isEmpty(v: any): boolean {
    return v === null || v === undefined || v === '' || (Array.isArray(v) && v.length === 0);
  }
}
