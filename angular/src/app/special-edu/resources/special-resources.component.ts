import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { SPECIAL_EDU_CATEGORIES, SPECIAL_RESOURCE_MODALITIES, SpecialEduService } from '../special-edu.service';
import { BrailleViewerComponent } from '../braille-viewer/braille-viewer.component';
import { ContentVersionFieldComponent } from '../content-version-field.component';

@Component({
  selector: 'app-special-resources',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NzCardModule, NzInputModule, NzSelectModule, NzButtonModule, NzSpinModule, NzDividerModule, NzTableModule, NzGridModule, NzTabsModule, NzModalModule, NzRadioModule, NzTooltipModule, BrailleViewerComponent, ContentVersionFieldComponent],
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
  <nz-card nzTitle="多模态课程资源" [nzExtra]="extraTpl">
    <nz-tabs [(nzSelectedIndex)]="activeTab">
      <nz-tab nzTitle="资源列表">
        <nz-table [nzData]="list()" nzSize="small" style="margin-top:12px">
          <thead><tr><th>标题</th><th>类型</th><th>版本</th><th>状态</th><th>最后修改</th><th>审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of list(); track h.id) {
              <tr><td>{{ h.title }}</td><td>{{ h.modalityName }}</td><td>v{{ h.versionNumber ?? 1 }}</td><td>{{ statusName(h.status) }}</td>
              <td>{{ (h.lastModificationTime || h.creationTime) | date:'yyyy-MM-dd HH:mm' }}</td>
              <td>{{ h.reviewerName || '—' }}</td>
              <td>
                <a (click)="view(h)">查看</a>
                <a (click)="openEdit(h)" style="margin-left:8px">编辑</a>
                <a (click)="exportOne(h)" style="margin-left:8px">导出</a>
                @if (h.status === 0 || h.status === 2 || h.status === 3) {
                  <a (click)="openSubmit(h)" style="margin-left:8px">提交审核</a>
                }
                <a (click)="remove(h)" style="margin-left:8px;color:#ff4d4f">删除</a>
              </td></tr>
            }
          </tbody>
        </nz-table>
      </nz-tab>
      <nz-tab [nzTitle]="'待审核 (' + pending().length + ')'">
        <nz-table [nzData]="pending()" nzSize="small" style="margin-top:12px">
          <thead><tr><th>标题</th><th>类型</th><th>版本</th><th>指派审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of pending(); track h.id) {
              <tr><td>{{ h.title }}</td><td>{{ h.modalityName }}</td>
              <td>v{{ h.versionNumber ?? 1 }}</td>
              <td>{{ h.reviewerName || '未指派' }}</td>
              <td>
                <a (click)="view(h)">查看</a>
                <a (click)="openEdit(h)" style="margin-left:8px">编辑</a>
                <a (click)="openReview(h, true)" style="margin-left:8px">通过</a>
                <a (click)="openReview(h, false)" style="margin-left:8px;color:#ff4d4f">驳回</a>
              </td></tr>
            }
          </tbody>
        </nz-table>
      </nz-tab>
    </nz-tabs>
  </nz-card>
  <ng-template #extraTpl>
    <button nz-button nzType="primary" nzSize="small" (click)="openCreate()">新建资源</button>
  </ng-template>

  <!-- 新建：弹出表单 -->
  <nz-modal [(nzVisible)]="createVisible" nzTitle="新建多模态资源" [nzWidth]="640" (nzOnCancel)="createVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    <div class="dh-modal">
    <div class="dh-form">
    <div class="field">
    <span class="label">类别</span>
    <nz-select [(ngModel)]="input.category" style="width:100%">
      @for (c of categories; track c.value) { <nz-option [nzValue]="c.value" [nzLabel]="c.label"></nz-option> }
    </nz-select>
    </div>
    <div class="field">
    <span class="label">资源类型</span>
    <nz-select [(ngModel)]="input.modality" style="width:100%">
      @for (m of modalities; track m.value) { <nz-option [nzValue]="m.value" [nzLabel]="m.label"></nz-option> }
    </nz-select>
    </div>
    <label class="field full">
    <span class="label">主题</span>
    <input nz-input [(ngModel)]="input.topic" placeholder="如：轮流玩积木 / 洗手步骤" />
    </label>
    <label class="field full">
    <span class="label">学生特点</span>
    <textarea nz-input rows="2" [(ngModel)]="input.studentTraits"></textarea>
    </label>
    <label class="field full">
    <span class="label">定制要求</span>
    <textarea nz-input rows="2" [(ngModel)]="input.customPrompt"></textarea>
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
        <div>支持 8 种资源类型：文本 / 图片描述 / 音频脚本 / 视频脚本 / 社交故事 / 视觉支持材料 / 行为干预方案 / 盲文对照。</div>
        <div>点击生成后自动保存为草稿，可在列表中提交审核、导出 Word。</div>
        <div>中文盲文走现行盲文，AI 内容须经教师核对后用于教学。</div>
      </ng-template>
      <button nz-button (click)="createVisible = false">取消</button>
      <button nz-button nzType="primary" (click)="generate()" [nzLoading]="generating()">生成（自动存为草稿）</button>
    </div>
    </div>

    </ng-container>
  </nz-modal>

  <!-- 查看 -->
  <nz-modal [(nzVisible)]="viewVisible" [nzTitle]="viewTarget?.title || '查看资源'" nzWidth="900" (nzOnCancel)="viewVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    <p style="color:#888">{{ viewTarget?.modalityName }} · {{ viewTarget?.categoryName }} · {{ statusName(viewTarget?.status) }}</p>
    @if (viewPairs().length > 0) {
      <app-braille-viewer [pairs]="viewPairs()"></app-braille-viewer>
    } @else if (viewContent().length > 0) {
      @for (c of viewContent(); track c) { <p>• {{ c }}</p> }
    } @else {
      <p style="color:#999">暂无可展示内容（历史数据缺少正文，可删除后重新生成）。</p>
    }
    @if (viewTarget?.reviewComment) { <p style="color:#c00">审核意见：{{ viewTarget.reviewComment }}</p> }
    <div style="margin-top:12px">
      <button nz-button nzType="primary" (click)="openEdit(viewTarget)">编辑内容（当前 v{{ viewTarget?.versionNumber ?? 1 }}）</button>
    </div>

    </ng-container>
  </nz-modal>

  <!-- 结构化编辑：每字段可看历史版本并采用，保存自动 +1 -->
  <nz-modal [(nzVisible)]="editVisible" [nzTitle]="'编辑内容（当前 v' + (edit.versionNumber ?? 1) + '，保存后自动 +1）'" [nzWidth]="640" (nzOnCancel)="editVisible = false" [nzFooter]="null">
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
    <label class="field full">
      <span class="label">正文（每行一条）</span>
      <textarea nz-input rows="8" [(ngModel)]="edit.content"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('content')" (adopt)="adoptField('content', $event)"></app-content-version-field>
    </label>
    </div>
    <div class="modal-foot">
      <button nz-button (click)="editVisible = false">取消</button>
      <button nz-button nzType="primary" (click)="saveEdit()" [nzLoading]="editSaving()">保存（自动存为新版本草稿）</button>
    </div>
    </div>
    }
    </ng-container>
  </nz-modal>

  <nz-modal [(nzVisible)]="submitVisible" nzTitle="提交审核 — 指派审核教师" [nzWidth]="520" (nzOnCancel)="submitVisible = false" (nzOnOk)="confirmSubmit()">
    <ng-container *nzModalContent>
    <div class="dh-form">
    <label class="field full"><span class="label">资源</span><span>{{ submitTarget?.title }}</span></label>
    <div class="field full">
    <span class="label">审核教师</span>
    <nz-select [(ngModel)]="submitReviewerId" nzAllowClear nzPlaceHolder="选择教师，可不选" style="width:100%">
      @for (t of teachers(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.name + ' (' + t.userName + ' · ' + t.roleName + ')'"></nz-option> }
    </nz-select>
    </div>
    </div>
    </ng-container>
  </nz-modal>

  <nz-modal [(nzVisible)]="reviewVisible" nzTitle="审核资源" [nzWidth]="520" (nzOnCancel)="reviewVisible = false" (nzOnOk)="confirmReview()">
    <ng-container *nzModalContent>
    <div class="dh-form">
    <label class="field full"><span class="label">资源</span><span>{{ reviewTarget?.title }}</span></label>
    <div class="field full">
    <span class="label">审核结论</span>
    <nz-radio-group [(ngModel)]="reviewApproved">
      <label nz-radio [nzValue]="true">通过（发布）</label>
      <label nz-radio [nzValue]="false">驳回（退回草稿）</label>
    </nz-radio-group>
    </div>
    <label class="field full">
    <span class="label">审核意见</span>
    <textarea nz-input rows="3" [(ngModel)]="reviewComment"></textarea>
    </label>
    </div>
    </ng-container>
  </nz-modal>
  `,
})
export class SpecialResourcesComponent {
  svc = inject(SpecialEduService);
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private msg = inject(NzMessageService);
  private modal = inject(NzModalService);
  categories = SPECIAL_EDU_CATEGORIES;
  // 多模态页不混入盲文对照（盲文有独立页面），下拉与列表均剔除。
  modalities = SPECIAL_RESOURCE_MODALITIES.filter(m => m.value !== 'BrailleParallel');
  generating = signal(false);
  result = signal<any>(null);
  rawJson = signal('');
  list = signal<any[]>([]);
  pending = signal<any[]>([]);
  teachers = signal<any[]>([]);
  activeTab = 0;
  input: any = { category: 3, modality: 'SocialStory', topic: '', studentTraits: '', customPrompt: '' };
  createVisible = false;
  viewVisible = false;
  viewTarget: any = null;
  viewPairs = signal<any[]>([]);
  viewContent = signal<string[]>([]);
  submitVisible = false;
  submitTarget: any = null;
  submitReviewerId: string | null = null;
  reviewVisible = false;
  reviewTarget: any = null;
  reviewApproved = true;
  reviewComment = '';
  // 结构化编辑
  editVisible = false;
  editLoading = signal(false);
  editSaving = signal(false);
  versions = signal<any[]>([]);
  parsedVersions = computed(() => this.versions().map(v => ({ ...v, snap: this.svc.tryParse<any>(v.snapshotJson ?? '{}') ?? {} })));
  edit: any = this.emptyEdit();

  constructor() {
    this.loadAll();
    this.loadTeachers();
    // 从盲文对照页“新建盲文对照”跳转时，自动打开对应类型的新建弹窗。
    this.route.queryParamMap.subscribe(params => {
      const create = params.get('create');
      if (create && this.modalities.some(m => m.value === create)) {
        this.input.modality = create;
        this.openCreate();
      }
    });
  }

  statusName(s: number): string {
    return ['草稿', '待审核', '已审核', '已发布', '已归档'][s] ?? String(s);
  }

  loadAll(): void {
    const baseParams: any = { maxResultCount: '50', excludeModality: 'BrailleParallel' };
    this.http.get<any>('/api/learning/special-edu/resources', { params: baseParams })
      .subscribe({ next: (r: any) => this.list.set(r?.items ?? []), error: () => {} });
    this.http.get<any>('/api/learning/special-edu/resources', { params: { ...baseParams, status: '1' } })
      .subscribe({ next: (r: any) => this.pending.set(r?.items ?? []), error: () => {} });
  }

  loadTeachers(): void {
    this.http.get<any[]>('/api/app/special-edu-option/teacher-options')
      .subscribe({ next: r => this.teachers.set(r ?? []), error: () => {} });
  }

  openCreate(): void {
    this.result.set(null);
    this.rawJson.set('');
    this.createVisible = true;
  }

  async generate(): Promise<void> {
    if (!this.input.topic?.trim()) {
      this.msg.warning('请填写主题');
      return;
    }
    this.generating.set(true);
    this.result.set(null);
    let buf = '';
    try {
      for await (const chunk of this.svc.generateResource(this.input)) {
        buf += chunk.content ?? '';
        const parsed = this.svc.tryParse<any>(buf);
        if (parsed?.title) this.result.set(parsed);
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
      title: parsed?.title ?? this.input.topic, category: this.input.category, modality: this.input.modality,
      resultJson: this.rawJson(), sourceInputJson: JSON.stringify(this.input),
    }).subscribe({
      next: () => { this.msg.success('已生成并自动保存为草稿'); this.createVisible = false; this.loadAll(); },
      error: () => this.msg.error('自动保存失败，请重试'),
    });
  }

  view(h: any): void {
    this.viewTarget = h;
    try {
      const raw = JSON.parse(h.rawJson ?? '{}');
      this.viewPairs.set(Array.isArray(raw?.pairs) ? raw.pairs : []);
      const content = Array.isArray(raw?.content) ? raw.content : [];
      // 老数据 RawJson 为空时，用列表已有的正文兜底，保证查看有效果。
      this.viewContent.set(content.length > 0 ? content : String(h.contentText ?? '').split('\n').filter((x: string) => x.trim()));
    } catch {
      this.viewPairs.set([]);
      this.viewContent.set(String(h.contentText ?? '').split('\n').filter((x: string) => x.trim()));
    }
    this.viewVisible = true;
  }

  openSubmit(h: any): void {
    this.submitTarget = h;
    this.submitReviewerId = h.reviewerUserId ?? null;
    this.submitVisible = true;
  }

  confirmSubmit(): void {
    if (!this.submitTarget) return;
    this.http.post<any>(`/api/learning/special-edu/resources/${this.submitTarget.id}/submit`, { reviewerUserId: this.submitReviewerId })
      .subscribe({
        next: () => { this.msg.success('已提交审核'); this.submitVisible = false; this.loadAll(); },
        error: (e) => this.msg.error(e?.error?.message ?? '提交失败'),
      });
  }

  openReview(h: any, approved: boolean): void {
    this.reviewTarget = h;
    this.reviewApproved = approved;
    this.reviewComment = '';
    this.reviewVisible = true;
  }

  confirmReview(): void {
    if (!this.reviewTarget) return;
    this.http.post<any>('/api/learning/special-edu/resources/review',
      { id: this.reviewTarget.id, approved: this.reviewApproved, comment: this.reviewComment })
      .subscribe({
        next: () => { this.msg.success(this.reviewApproved ? '已审核发布' : '已驳回'); this.reviewVisible = false; this.loadAll(); },
        error: (e) => this.msg.error(e?.error?.message ?? '审核失败'),
      });
  }

  remove(h: any): void {
    this.modal.confirm({
      nzTitle: '确认删除该资源吗？',
      nzOnOk: () => this.http.delete(`/api/learning/special-edu/resources/${h.id}`)
        .subscribe({ next: () => { this.msg.success('已删除'); this.loadAll(); }, error: () => this.msg.error('删除失败') }),
    });
  }

  exportDocx(): void {
    if (!this.rawJson()) return;
    this.svc.downloadBlob('/api/learning/special-edu/export-resource-docx',
      { title: '', category: this.input.category, modality: this.input.modality, resultJson: this.rawJson() },
      `特教资源_${Date.now()}.docx`).catch(() => this.msg.error('导出失败'));
  }

  exportOne(h: any): void {
    this.svc.downloadBlob('/api/learning/special-edu/export-resource-docx',
      { title: h.title, category: 0, modality: h.modality, resultJson: h.rawJson },
      `${h.title}.docx`).catch(() => this.msg.error('导出失败'));
  }

  // ── 结构化编辑 + 版本 ──
  emptyEdit(): any {
    return { id: '', versionNumber: 1, title: '', content: '', pairs: [] };
  }

  joinLines(v: any): string {
    return Array.isArray(v) ? v.join('\n') : (v ?? '');
  }

  splitLines(s: any): string[] {
    return String(s ?? '').split('\n').map(t => t.trim()).filter(t => t);
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
    const contentArr: string[] = Array.isArray(d.content) ? d.content
      : String(d.contentText ?? '').split('\n').filter((x: string) => x.trim());
    this.edit = {
      id: d.id, versionNumber: d.versionNumber ?? 1, title: d.title ?? '',
      content: contentArr.join('\n'),
      pairs: Array.isArray(d.pairs) ? JSON.parse(JSON.stringify(d.pairs)) : [],
    };
  }

  fieldVersions(key: string): any[] {
    return this.parsedVersions()
      .filter(v => v.versionNumber < (this.edit.versionNumber ?? 999))
      .map(v => ({ versionNumber: v.versionNumber, creationTime: v.creationTime, creatorName: v.creatorName, value: v.snap?.[key] }));
  }

  adoptField(key: string, value: any): void {
    if (Array.isArray(value) && key === 'content') this.edit.content = value.join('\n');
    else if (key === 'content') this.edit.content = value ?? '';
    else this.edit[key] = value ?? '';
  }

  saveEdit(): void {
    if (!this.edit.id) return;
    const content = this.splitLines(this.edit.content);
    const pairs = this.edit.pairs ?? [];
    const resultJson = JSON.stringify({ title: this.edit.title, content, pairs });
    this.editSaving.set(true);
    this.http.post<any>(`/api/learning/special-edu/resources/${this.edit.id}/content`, {
      title: this.edit.title, content, pairs, resultJson,
    }).subscribe({
      next: r => {
        this.editSaving.set(false);
        this.editVisible = false;
        this.msg.success(`已保存为 v${r?.versionNumber ?? ''}（自动存为草稿）`);
        this.loadAll();
      },
      error: e => { this.editSaving.set(false); this.msg.error(e?.error?.message ?? '保存失败'); },
    });
  }
}
