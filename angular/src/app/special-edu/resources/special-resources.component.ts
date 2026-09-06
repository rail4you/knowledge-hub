import { Component, ChangeDetectionStrategy, signal, inject } from '@angular/core';
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
import { HttpClient } from '@angular/common/http';
import { SPECIAL_EDU_CATEGORIES, SPECIAL_RESOURCE_MODALITIES, SpecialEduService } from '../special-edu.service';

@Component({
  selector: 'app-special-resources',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NzCardModule, NzInputModule, NzSelectModule, NzButtonModule, NzSpinModule, NzDividerModule, NzTableModule, NzGridModule, NzTabsModule, NzModalModule, NzRadioModule],
  template: `
  <nz-card nzTitle="多模态课程资源">
    <nz-tabset [(nzSelectedIndex)]="activeTab">
      <nz-tab nzTitle="录入/生成">
        <div nz-row [nzGutter]="16" style="margin-top:12px">
          <div nz-col [nzSpan]="9">
            <p>类别</p>
            <nz-select [(ngModel)]="input.category" style="width:100%">
              @for (c of categories; track c.value) { <nz-option [nzValue]="c.value" [nzLabel]="c.label"></nz-option> }
            </nz-select>
            <p style="margin-top:8px">资源类型</p>
            <nz-select [(ngModel)]="input.modality" style="width:100%">
              @for (m of modalities; track m.value) { <nz-option [nzValue]="m.value" [nzLabel]="m.label"></nz-option> }
            </nz-select>
            <p style="margin-top:8px">主题</p>
            <input nz-input [(ngModel)]="input.topic" placeholder="如：轮流玩积木 / 洗手步骤" />
            <p style="margin-top:8px">学生特点</p>
            <textarea nz-input rows="2" [(ngModel)]="input.studentTraits"></textarea>
            <p style="margin-top:8px">定制要求</p>
            <textarea nz-input rows="2" [(ngModel)]="input.customPrompt"></textarea>
            <div style="margin-top:8px">
              <button nz-button nzType="primary" (click)="generate()" [nzLoading]="generating()">生成（自动存为草稿）</button>
            </div>
          </div>
          <div nz-col [nzSpan]="15">
            <nz-spin [nzSpinning]="generating()">
              @if (result(); as r) {
                <h3>{{ r.title }}</h3>
                @for (c of r.content; track c) { <p>• {{ c }}</p> }
                <button nz-button (click)="exportDocx()">导出 Word</button>
              } @else {
                <p style="color:#999">支持文本 / 图片描述 / 音频脚本 / 视频脚本 / 社交故事 / 视觉支持 / 行为干预方案。生成后自动保存为草稿。</p>
              }
            </nz-spin>
          </div>
        </div>
      </nz-tab>
      <nz-tab nzTitle="资源列表">
        <nz-table [nzData]="list()" nzSize="small" style="margin-top:12px">
          <thead><tr><th>标题</th><th>类型</th><th>状态</th><th>审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of list(); track h.id) {
              <tr><td>{{ h.title }}</td><td>{{ h.modalityName }}</td><td>{{ statusName(h.status) }}</td>
              <td>{{ h.reviewerName || '—' }}</td>
              <td>
                <a (click)="view(h)">查看</a>
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
          <thead><tr><th>标题</th><th>类型</th><th>指派审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of pending(); track h.id) {
              <tr><td>{{ h.title }}</td><td>{{ h.modalityName }}</td>
              <td>{{ h.reviewerName || '未指派' }}</td>
              <td>
                <a (click)="view(h)">查看</a>
                <a (click)="openReview(h, true)" style="margin-left:8px">通过</a>
                <a (click)="openReview(h, false)" style="margin-left:8px;color:#ff4d4f">驳回</a>
              </td></tr>
            }
          </tbody>
        </nz-table>
      </nz-tab>
    </nz-tabset>
  </nz-card>

  <nz-modal [(nzVisible)]="submitVisible" nzTitle="提交审核 — 指派审核教师" (nzOnCancel)="submitVisible = false" (nzOnOk)="confirmSubmit()">
    <p>资源：{{ submitTarget?.title }}</p>
    <p>审核教师（本租户）</p>
    <nz-select [(ngModel)]="submitReviewerId" nzAllowClear nzPlaceHolder="选择教师，可不选" style="width:100%">
      @for (t of teachers(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.name + ' (' + t.userName + ' · ' + t.roleName + ')'"></nz-option> }
    </nz-select>
  </nz-modal>

  <nz-modal [(nzVisible)]="reviewVisible" nzTitle="审核资源" (nzOnCancel)="reviewVisible = false" (nzOnOk)="confirmReview()">
    <p>资源：{{ reviewTarget?.title }}</p>
    <nz-radio-group [(ngModel)]="reviewApproved">
      <label nz-radio [nzValue]="true">通过（发布）</label>
      <label nz-radio [nzValue]="false">驳回（退回草稿）</label>
    </nz-radio-group>
    <p style="margin-top:8px">审核意见</p>
    <textarea nz-input rows="3" [(ngModel)]="reviewComment"></textarea>
  </nz-modal>
  `,
})
export class SpecialResourcesComponent {
  svc = inject(SpecialEduService);
  private http = inject(HttpClient);
  private msg = inject(NzMessageService);
  private modal = inject(NzModalService);
  categories = SPECIAL_EDU_CATEGORIES;
  modalities = SPECIAL_RESOURCE_MODALITIES;
  generating = signal(false);
  result = signal<any>(null);
  rawJson = signal('');
  list = signal<any[]>([]);
  pending = signal<any[]>([]);
  teachers = signal<any[]>([]);
  activeTab = 0;
  input: any = { category: 3, modality: 'SocialStory', topic: '', studentTraits: '', customPrompt: '' };
  submitVisible = false;
  submitTarget: any = null;
  submitReviewerId: string | null = null;
  reviewVisible = false;
  reviewTarget: any = null;
  reviewApproved = true;
  reviewComment = '';

  constructor() {
    this.loadAll();
    this.loadTeachers();
  }

  statusName(s: number): string {
    return ['草稿', '待审核', '已审核', '已发布', '已归档'][s] ?? String(s);
  }

  loadAll(): void {
    this.http.get<any>('/api/learning/special-edu/resources', { params: { maxResultCount: '50' } as any })
      .subscribe({ next: (r: any) => this.list.set(r?.items ?? []), error: () => {} });
    this.http.get<any>('/api/learning/special-edu/resources', { params: { maxResultCount: '50', status: '1' } as any })
      .subscribe({ next: (r: any) => this.pending.set(r?.items ?? []), error: () => {} });
  }

  loadTeachers(): void {
    this.http.get<any[]>('/api/app/special-edu-option/teacher-options')
      .subscribe({ next: r => this.teachers.set(r ?? []), error: () => {} });
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
      next: () => { this.msg.success('已生成并自动保存为草稿'); this.loadAll(); },
      error: () => this.msg.error('自动保存失败，请重试'),
    });
  }

  view(h: any): void {
    const parsed = this.svc.tryParse<any>(h.rawJson ?? '');
    if (parsed) {
      this.result.set(parsed);
      this.rawJson.set(h.rawJson);
      this.activeTab = 0;
    }
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
}
