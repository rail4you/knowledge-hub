import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
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
import { ContentVersionFieldComponent } from '../content-version-field.component';

/**
 * 盲文对照学习卡（教师端独立特殊资源，不混入多模态脚本）：
 * 列表查看对照 + 本页新建（主题/学生特点/定制要求），生成后自动存草稿。
 */
@Component({
  selector: 'app-braille-study',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NzCardModule, NzButtonModule, NzInputModule, NzSelectModule, NzSpinModule, NzDividerModule, NzTableModule, NzModalModule, NzTooltipModule, BrailleViewerComponent, ContentVersionFieldComponent],
  styles: [`
    .dh-modal { display: flex; flex-direction: column; gap: 18px; max-height: 74vh; overflow-y: auto; padding: 2px 2px 0; }
    .dh-modal::-webkit-scrollbar { width: 5px; }
    .dh-modal::-webkit-scrollbar-thumb { background: #d4dde8; border-radius: 3px; }
    .dh-form { display: grid; grid-template-columns: 1fr 1fr; gap: 12px 16px; }
    .dh-form .field { display: flex; flex-direction: column; gap: 4px; }
    .dh-form .field.full { grid-column: 1 / -1; }
    .dh-form .label { font-size: 13px; color: rgba(0,0,0,.65); line-height: 20px; }
    textarea { resize: vertical; }
    .modal-foot { display: flex; justify-content: flex-end; gap: 12px; padding: 14px 0 2px; border-top: 1px solid #f0f0f0; margin-top: 2px; position: sticky; bottom: 0; background: #fff; }
    @media (max-width: 560px) { .dh-form { grid-template-columns: 1fr; } }
  `],
  template: `
  <nz-card nzTitle="盲文对照学习卡" [nzExtra]="extraTpl">
    <p style="color:#888">点位显示 + 翻译对照。中文走现行盲文（原则不标调、分词连写），英文/数字走一级盲文；对照内容须经教师核对后用于教学。</p>
    <nz-table [nzData]="list()" nzSize="small">
      <thead><tr><th>标题</th><th>类别</th><th>版本</th><th>状态</th><th>最后修改</th><th>操作</th></tr></thead>
      <tbody>
        @for (h of list(); track h.id) {
          <tr><td>{{ h.title }}</td><td>{{ h.categoryName }}</td><td>v{{ h.versionNumber ?? 1 }}</td><td>{{ statusName(h.status) }}</td>
          <td>{{ (h.lastModificationTime || h.creationTime) | date:'yyyy-MM-dd HH:mm' }}</td>
          <td>
            <a (click)="view(h)">查看对照</a>
            <a (click)="openEdit(h)" style="margin-left:8px">编辑</a>
            <a (click)="exportOne(h)" style="margin-left:8px">导出</a>
          </td></tr>
        }
      </tbody>
    </nz-table>
  </nz-card>
  <ng-template #extraTpl>
    <button nz-button nzType="primary" nzSize="small" (click)="openCreate()">新建盲文对照</button>
  </ng-template>

  <nz-modal [(nzVisible)]="createVisible" nzTitle="新建盲文对照" [nzWidth]="640" (nzOnCancel)="createVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    <div class="dh-modal">
    <div class="dh-form">
    <label class="field full">
    <span class="label">对照文本</span>
    <textarea nz-input rows="3" [(ngModel)]="input.text" placeholder="如：你好中国 / 静夜思 床前明月光"></textarea>
    </label>
    <div class="field">
    <span class="label">盲文方案</span>
    <nz-select [(ngModel)]="input.scheme" style="width:100%">
      <nz-option nzValue="xianxing" nzLabel="现行盲文（中文）"></nz-option>
      <nz-option nzValue="grade1" nzLabel="英语一级盲文（英文/数字）"></nz-option>
    </nz-select>
    </div>
    <div class="field">
    <span class="label">声调</span>
    <nz-select [(ngModel)]="input.tone" style="width:100%">
      <nz-option nzValue="none" nzLabel="不标调（默认，按现行盲文规则）"></nz-option>
      <nz-option nzValue="marked" nzLabel="标调（教学演示用）"></nz-option>
    </nz-select>
    </div>
    <label class="field full">
    <span class="label">备注</span>
    <textarea nz-input rows="2" [(ngModel)]="input.note" placeholder="如：只出词语对照；用于三年级课堂"></textarea>
    </label>
    </div>
    <nz-spin [nzSpinning]="generating()">
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
    <div class="modal-foot">
      <button nz-button nzShape="circle" nz-tooltip [nzTooltipTitle]="helpTpl" nzTooltipPlacement="top" aria-label="填写说明">?</button>
      <ng-template #helpTpl>
        <div>输入对照文本，选择盲文方案与声调后生成明文—盲文对照卡。</div>
        <div>中文按现行盲文（原则不标调、分词连写），英文/数字按一级盲文。</div>
        <div>盲文点位须经教师核对后用于教学。</div>
      </ng-template>
      <button nz-button (click)="createVisible = false">取消</button>
      <button nz-button nzType="primary" (click)="generate()" [nzLoading]="generating()">生成对照卡（自动存为草稿）</button>
    </div>
    </div>

    </ng-container>
  </nz-modal>

  <nz-modal [(nzVisible)]="viewVisible" [nzTitle]="viewTitle" [nzFooter]="null" nzWidth="900" (nzOnCancel)="viewVisible = false">
    <ng-container *nzModalContent>
    @if (viewPairs().length > 0) {
      <app-braille-viewer [pairs]="viewPairs()"></app-braille-viewer>
    } @else {
      <p style="color:#999">该资源暂无对照数据。</p>
    }
    @if (viewTarget) {
      <div style="margin-top:12px">
        <button nz-button nzType="primary" (click)="openEdit(viewTarget)">编辑内容（当前 v{{ viewTarget?.versionNumber ?? 1 }}）</button>
      </div>
    }
    </ng-container>
  </nz-modal>

  <!-- 结构化编辑：盲文对照 pairs 可逐条改，每条可看历史并采用，保存自动 +1 -->
  <nz-modal [(nzVisible)]="editVisible" [nzTitle]="'编辑盲文对照（当前 v' + (edit.versionNumber ?? 1) + '，保存后自动 +1）'" [nzWidth]="720" (nzOnCancel)="editVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    @if (editLoading()) {
      <div style="text-align:center;padding:48px;"><nz-spin nzSimple></nz-spin></div>
    } @else {
    <div class="dh-modal">
    <div class="dh-form">
    <label class="field full">
      <span class="label">标题</span>
      <input nz-input [(ngModel)]="edit.title" />
      <app-content-version-field [versions]="fieldVersions('title')" (adopt)="adoptField('title', $event)"></app-content-version-field>
    </label>
    <div class="field full">
      <span class="label">对照条目（明文 / 盲文点位）</span>
      @for (p of edit.pairs; track $index; let i = $index) {
        <div style="display:flex;gap:8px;margin-bottom:6px;align-items:center;">
          <input nz-input [(ngModel)]="p.text" placeholder="明文" style="flex:1" />
          <input nz-input [(ngModel)]="p.braille" placeholder="盲文" style="flex:1" />
          <button nz-button nzType="default" nzDanger nzSize="small" (click)="removePair(i)">删除</button>
        </div>
      }
      <div><button nz-button nzType="dashed" nzSize="small" (click)="addPair()">+ 添加条目</button></div>
      <app-content-version-field kind="pairs" [versions]="fieldVersions('pairs')" (adopt)="adoptField('pairs', $event)"></app-content-version-field>
    </div>
    </div>
    <div class="modal-foot">
      <button nz-button (click)="editVisible = false">取消</button>
      <button nz-button nzType="primary" (click)="saveEdit()" [nzLoading]="editSaving()">保存（自动存为新版本草稿）</button>
    </div>
    </div>
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
  viewTarget: any = null;
  viewTitle = '';
  viewPairs = signal<any[]>([]);
  // 结构化编辑
  editVisible = false;
  editLoading = signal(false);
  editSaving = signal(false);
  versions = signal<any[]>([]);
  parsedVersions = computed(() => this.versions().map(v => ({ ...v, snap: this.svc.tryParse<any>(v.snapshotJson ?? '{}') ?? {} })));
  edit: any = this.emptyEdit();

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
    this.viewTarget = h;
    this.viewTitle = h.title;
    this.viewVisible = true;
    this.viewPairs.set([]);
    this.http.get<any>(`/api/learning/special-edu/resources/${h.id}`).subscribe({
      next: d => {
        this.viewTarget = d;
        this.viewTitle = d.title;
        this.viewPairs.set(Array.isArray(d.pairs) ? d.pairs : this.parsePairs(d.rawJson));
      },
      error: () => this.viewPairs.set(this.parsePairs(h.rawJson)),
    });
  }

  parsePairs(rawJson: any): any[] {
    const parsed = this.svc.tryParse<any>(rawJson ?? '{}');
    return Array.isArray(parsed?.pairs) ? parsed.pairs : [];
  }

  // ── 结构化编辑 + 版本 ──
  emptyEdit(): any {
    return { id: '', versionNumber: 1, title: '', pairs: [] };
  }

  openEdit(h: any): void {
    this.viewVisible = false;
    this.edit = this.emptyEdit();
    this.versions.set([]);
    this.editLoading.set(true);
    this.editVisible = true;
    this.http.get<any>(`/api/learning/special-edu/resources/${h.id}`).subscribe({
      next: d => { this.fillEdit(d); this.editLoading.set(false); },
      error: () => { this.fillEdit(h); this.editLoading.set(false); },
    });
    this.http.get<any[]>(`/api/learning/special-edu/resources/${h.id}/versions`).subscribe({
      next: v => this.versions.set(v ?? []),
      error: () => this.versions.set([]),
    });
  }

  fillEdit(d: any): void {
    let pairs: any[] = Array.isArray(d.pairs) ? d.pairs : this.parsePairs(d.rawJson);
    this.edit = {
      id: d.id, versionNumber: d.versionNumber ?? 1, title: d.title ?? '',
      pairs: JSON.parse(JSON.stringify(pairs)),
    };
  }

  fieldVersions(key: string): any[] {
    return this.parsedVersions()
      .filter(v => v.versionNumber < (this.edit.versionNumber ?? 999))
      .map(v => ({ versionNumber: v.versionNumber, creationTime: v.creationTime, creatorName: v.creatorName, value: v.snap?.[key] }));
  }

  adoptField(key: string, value: any): void {
    this.edit[key] = value === undefined || value === null ? (key === 'pairs' ? [] : '') : JSON.parse(JSON.stringify(value));
  }

  addPair(): void {
    this.edit.pairs = [...(this.edit.pairs || []), { text: '', pinyin: '', braille: '', note: '' }];
  }

  removePair(i: number): void {
    this.edit.pairs = (this.edit.pairs || []).filter((_: any, idx: number) => idx !== i);
  }

  saveEdit(): void {
    if (!this.edit.id) return;
    const pairs = (this.edit.pairs || []).map((p: any) => ({
      text: p.text ?? '', pinyin: p.pinyin ?? '', braille: p.braille ?? '', note: p.note ?? '',
    }));
    const resultJson = JSON.stringify({ title: this.edit.title, content: [], pairs });
    this.editSaving.set(true);
    this.http.post<any>(`/api/learning/special-edu/resources/${this.edit.id}/content`, {
      title: this.edit.title, content: [], pairs, resultJson,
    }).subscribe({
      next: r => {
        this.editSaving.set(false);
        this.editVisible = false;
        this.msg.success(`已保存为 v${r?.versionNumber ?? ''}（自动存为草稿）`);
        this.load();
      },
      error: e => { this.editSaving.set(false); this.msg.error(e?.error?.message ?? '保存失败'); },
    });
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
