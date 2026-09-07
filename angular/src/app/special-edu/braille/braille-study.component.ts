import { Component, ChangeDetectionStrategy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { HttpClient } from '@angular/common/http';
import { SpecialEduService } from '../special-edu.service';
import { BrailleViewerComponent } from '../braille-viewer/braille-viewer.component';

/**
 * 盲文对照学习卡（教师端独立特殊资源，不混入多模态脚本）：
 * 列表查看对照 + 本页新建（主题/学生特点/定制要求），生成后自动存草稿。
 */
@Component({
  selector: 'app-braille-study',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NzCardModule, NzButtonModule, NzInputModule, NzSelectModule, NzSpinModule, NzDividerModule, NzTableModule, NzModalModule, NzTooltipModule, BrailleViewerComponent],
  template: `
  <nz-card nzTitle="盲文对照学习卡" [nzExtra]="extraTpl">
    <p style="color:#888">点位显示 + 翻译对照。中文走现行盲文（原则不标调、分词连写），英文/数字走一级盲文；对照内容须经教师核对后用于教学。</p>
    <nz-table [nzData]="list()" nzSize="small">
      <thead><tr><th>标题</th><th>类别</th><th>状态</th><th>操作</th></tr></thead>
      <tbody>
        @for (h of list(); track h.id) {
          <tr><td>{{ h.title }}</td><td>{{ h.categoryName }}</td><td>{{ statusName(h.status) }}</td>
          <td>
            <a (click)="view(h)">查看对照</a>
            <a (click)="exportOne(h)" style="margin-left:8px">导出</a>
          </td></tr>
        }
      </tbody>
    </nz-table>
  </nz-card>
  <ng-template #extraTpl>
    <button nz-button nzType="primary" nzSize="small" (click)="openCreate()">新建盲文对照</button>
  </ng-template>

  <nz-modal [(nzVisible)]="createVisible" nzTitle="新建盲文对照" nzWidth="800" (nzOnCancel)="createVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    <p>对照文本</p>
    <textarea nz-input rows="3" [(ngModel)]="input.text" placeholder="如：你好中国 / 静夜思 床前明月光"></textarea>
    <p style="margin-top:8px">盲文方案</p>
    <nz-select [(ngModel)]="input.scheme" style="width:100%">
      <nz-option nzValue="xianxing" nzLabel="现行盲文（中文）"></nz-option>
      <nz-option nzValue="grade1" nzLabel="英语一级盲文（英文/数字）"></nz-option>
    </nz-select>
    <p style="margin-top:8px">声调</p>
    <nz-select [(ngModel)]="input.tone" style="width:100%">
      <nz-option nzValue="none" nzLabel="不标调（默认，按现行盲文规则）"></nz-option>
      <nz-option nzValue="marked" nzLabel="标调（教学演示用）"></nz-option>
    </nz-select>
    <p style="margin-top:8px">备注（可选）</p>
    <textarea nz-input rows="2" [(ngModel)]="input.note" placeholder="如：只出词语对照；用于三年级课堂"></textarea>
    <div style="margin-top:12px">
      <button nz-button nzType="primary" (click)="generate()" [nzLoading]="generating()">生成对照卡（自动存为草稿）</button>
      <button nz-button nzShape="circle" nz-tooltip [nzTooltipTitle]="helpTpl" nzTooltipPlacement="right" style="margin-left:8px" aria-label="填写说明">?</button>
      <ng-template #helpTpl>
        <div>输入对照文本，选择盲文方案与声调后生成明文—盲文对照卡。</div>
        <div>中文按现行盲文（原则不标调、分词连写），英文/数字按一级盲文。</div>
        <div>盲文点位须经教师核对后用于教学。</div>
      </ng-template>
    </div>
    <nz-spin [nzSpinning]="generating()" style="margin-top:12px">
      @if (result(); as r) {
        <nz-divider></nz-divider>
        <h3>{{ r.title }}</h3>
        @if (r.pairs?.length) {
          <app-braille-viewer [pairs]="r.pairs"></app-braille-viewer>
        } @else {
          @for (c of r.content; track c) { <p>• {{ c }}</p> }
        }
        <button nz-button (click)="exportDocx()">导出 Word</button>
      }
    </nz-spin>

    </ng-container>
  </nz-modal>

  <nz-modal [(nzVisible)]="viewVisible" [nzTitle]="viewTitle" [nzFooter]="null" nzWidth="900" (nzOnCancel)="viewVisible = false">
    <ng-container *nzModalContent>
    @if (viewPairs().length > 0) {
      <app-braille-viewer [pairs]="viewPairs()"></app-braille-viewer>
    } @else {
      <p style="color:#999">该资源暂无对照数据。</p>
    }

    </ng-container>
  </nz-modal>
  `,
})
export class BrailleStudyComponent {
  svc = inject(SpecialEduService);
  private http = inject(HttpClient);
  private msg = inject(NzMessageService);
  list = signal<any[]>([]);
  generating = signal(false);
  result = signal<any>(null);
  rawJson = signal('');
  input: any = { scheme: 'xianxing', tone: 'none', text: '', note: '' };
  createVisible = false;
  viewVisible = false;
  viewTitle = '';
  viewPairs = signal<any[]>([]);

  constructor() {
    this.load();
  }

  statusName(s: number): string {
    return ['草稿', '待审核', '已审核', '已发布', '已归档'][s] ?? String(s);
  }

  load(): void {
    this.http.get<any>('/api/learning/special-edu/resources', { params: { maxResultCount: '50', modality: 'BrailleParallel' } as any })
      .subscribe({ next: (r: any) => this.list.set(r?.items ?? []), error: () => {} });
  }

  openCreate(): void {
    this.result.set(null);
    this.rawJson.set('');
    this.createVisible = true;
  }

  async generate(): Promise<void> {
    if (!this.input.text?.trim()) {
      this.msg.warning('请填写对照文本');
      return;
    }
    this.generating.set(true);
    this.result.set(null);
    let buf = '';
    try {
      // 类别固定视障（2）；转写规则拼入定制要求，后端按盲文方案组织提示词。
      const schemeLabel = this.input.scheme === 'grade1' ? '英语一级盲文' : '现行盲文';
      const toneLabel = this.input.tone === 'marked' ? '标调' : '不标调';
      const body = {
        category: 2,
        modality: 'BrailleParallel',
        topic: this.input.text,
        studentTraits: '',
        customPrompt: `盲文方案：${schemeLabel}；声调：${toneLabel}；备注：${this.input.note || '无'}`,
      };
      for await (const chunk of this.svc.generateResource(body)) {
        buf += chunk.content ?? '';
        const parsed = this.svc.tryParse<any>(buf);
        if (parsed?.title || parsed?.pairs) this.result.set(parsed);
        if (chunk.isComplete) break;
      }
      this.rawJson.set(buf);
      const parsed = this.svc.tryParse<any>(buf);
      if (parsed) {
        this.result.set(parsed);
        this.autoSave();
      } else if (buf) {
        this.msg.warning('生成内容非标准 JSON，未自动保存，可重试');
      }
    } catch (e: any) {
      this.msg.error(e?.message ?? '生成失败');
    } finally {
      this.generating.set(false);
    }
  }

  autoSave(): void {
    if (!this.rawJson()) return;
    const parsed = this.svc.tryParse<any>(this.rawJson());
    this.http.post<any>('/api/learning/special-edu/resources', {
      title: parsed?.title ?? this.input.text.slice(0, 30), category: 2, modality: 'BrailleParallel',
      resultJson: this.rawJson(), sourceInputJson: JSON.stringify(this.input),
    }).subscribe({
      next: () => { this.msg.success('已生成并自动保存为草稿'); this.createVisible = false; this.load(); },
      error: () => this.msg.error('自动保存失败，请重试'),
    });
  }

  view(h: any): void {
    this.viewTitle = h.title;
    try {
      const raw = JSON.parse(h.rawJson ?? '{}');
      this.viewPairs.set(Array.isArray(raw?.pairs) ? raw.pairs : []);
    } catch {
      this.viewPairs.set([]);
    }
    this.viewVisible = true;
  }

  exportDocx(): void {
    if (!this.rawJson()) return;
    this.svc.downloadBlob('/api/learning/special-edu/export-resource-docx',
      { title: '', category: 2, modality: 'BrailleParallel', resultJson: this.rawJson() },
      `盲文对照_${Date.now()}.docx`).catch(() => this.msg.error('导出失败'));
  }

  exportOne(h: any): void {
    this.svc.downloadBlob('/api/learning/special-edu/export-resource-docx',
      { title: h.title, category: 0, modality: h.modality, resultJson: h.rawJson },
      `${h.title}.docx`).catch(() => this.msg.error('导出失败'));
  }
}
