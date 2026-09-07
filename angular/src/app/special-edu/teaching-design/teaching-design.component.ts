import { Component, ChangeDetectionStrategy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { HttpClient } from '@angular/common/http';
import { SPECIAL_EDU_CATEGORIES, SpecialEduService } from '../special-edu.service';

@Component({
  selector: 'app-teaching-design',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NzCardModule, NzFormModule, NzInputModule, NzInputNumberModule, NzSelectModule, NzButtonModule, NzSpinModule, NzTagModule, NzDividerModule, NzTableModule, NzGridModule, NzTabsModule, NzModalModule, NzRadioModule, NzTooltipModule],
  template: `
  <nz-card nzTitle="智能教学设计方案（特教）" [nzExtra]="extraTpl">
    <nz-tabs [(nzSelectedIndex)]="activeTab">
      <nz-tab nzTitle="方案列表">
        <nz-table [nzData]="list()" nzSize="small" style="margin-top:12px">
          <thead><tr><th>标题</th><th>类别</th><th>状态</th><th>审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of list(); track h.id) {
              <tr><td>{{ h.title }}</td><td>{{ h.categoryName }}</td><td>{{ statusName(h.status) }}</td>
              <td>{{ h.reviewerName || '—' }}</td>
              <td>
                <a (click)="view(h)">查看</a>
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
          <thead><tr><th>标题</th><th>类别</th><th>指派审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of pending(); track h.id) {
              <tr><td>{{ h.title }}</td><td>{{ h.categoryName }}</td>
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
    </nz-tabs>
  </nz-card>
  <ng-template #extraTpl>
    <button nz-button nzType="primary" nzSize="small" (click)="openCreate()">新建方案</button>
  </ng-template>

  <!-- 新建：弹出表单，生成后自动存为草稿 -->
  <nz-modal [(nzVisible)]="createVisible" nzTitle="新建教学设计方案" nzWidth="800" (nzOnCancel)="createVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    <nz-form-item><nz-form-label>特殊教育类别</nz-form-label>
      <nz-select [(ngModel)]="input.category" style="width:100%">
        @for (c of categories; track c.value) { <nz-option [nzValue]="c.value" [nzLabel]="c.label"></nz-option> }
      </nz-select></nz-form-item>
    <nz-form-item><nz-form-label>课程主题 / 课题</nz-form-label>
      <input nz-input [(ngModel)]="input.topic" placeholder="如：《认识水果》" /></nz-form-item>
    <nz-form-item><nz-form-label>教学目标（关键词）</nz-form-label>
      <textarea nz-input rows="2" [(ngModel)]="input.objectives" placeholder="如：指认三种水果、颜色配对、卫生习惯"></textarea></nz-form-item>
    <nz-form-item><nz-form-label>学生特点</nz-form-label>
      <textarea nz-input rows="2" [(ngModel)]="input.studentTraits" placeholder="如：培智三年级，注意力15分钟，需视觉提示"></textarea></nz-form-item>
    <nz-form-item><nz-form-label>教学条件</nz-form-label>
      <textarea nz-input rows="2" [(ngModel)]="input.conditions" placeholder="如：实物水果、图片卡、小组4人"></textarea></nz-form-item>
    <nz-form-item><nz-form-label>学科</nz-form-label><input nz-input [(ngModel)]="input.subject" placeholder="生活语文" /></nz-form-item>
    <nz-form-item><nz-form-label>学段</nz-form-label><input nz-input [(ngModel)]="input.grade" placeholder="培智三年级" /></nz-form-item>
    <nz-form-item><nz-form-label>课时（分钟）</nz-form-label>
      <nz-input-number [(ngModel)]="input.duration" [nzMin]="20" [nzMax]="120"></nz-input-number></nz-form-item>
    <nz-form-item><nz-form-label>附加要求</nz-form-label>
      <textarea nz-input rows="2" [(ngModel)]="input.customPrompt"></textarea></nz-form-item>
    <div>
      <button nz-button nzType="primary" (click)="generate()" [nzLoading]="generating()">生成方案（自动存为草稿）</button>
      <button nz-button nzShape="circle" nz-tooltip [nzTooltipTitle]="helpTpl" nzTooltipPlacement="right" style="margin-left:8px" aria-label="填写说明">?</button>
      <ng-template #helpTpl>
        <div>按教学目标、学生特点、教学条件生成完整特教教案。</div>
        <div>包含教学目标、重难点、教学过程、教学评价、板书设计及配套课件大纲、活动设计、评估工具。</div>
        <div>生成后自动保存为草稿，可在列表中提交审核、导出 Word。</div>
      </ng-template>
    </div>
    <nz-spin [nzSpinning]="generating()" style="margin-top:12px">
      @if (result(); as r) {
        <nz-divider></nz-divider>
        <h3>{{ r.title }}</h3>
        <p><nz-tag>{{ svc.categoryName(input.category) }}</nz-tag> {{ r.subject }} · {{ r.grade }} · {{ r.duration }}分钟</p>
        <nz-divider nzText="教学目标"></nz-divider>
        @for (o of r.objectives; track o) { <p>• {{ o }}</p> }
        <nz-divider nzText="教学重难点"></nz-divider>
        <p>重点：{{ (r.keyPoints || []).join('；') }}</p>
        <p>难点：{{ (r.difficulties || []).join('；') }}</p>
        <nz-divider nzText="教学过程"></nz-divider>
        @for (s of r.sections; track s.name) {
          <p><b>{{ s.name }}（{{ s.duration }}分钟）</b></p>
          <p>{{ s.content }}</p>
        }
        <nz-divider nzText="教学评价"></nz-divider>
        @for (a of r.assessment; track a) { <p>• {{ a }}</p> }
        <nz-divider nzText="板书设计"></nz-divider>
        @for (b of r.boardDesign; track b) { <p>• {{ b }}</p> }
        <nz-divider nzText="配套资源"></nz-divider>
        <p>课件大纲：{{ (r.slidesOutline || []).join(' / ') }}</p>
        <p>评估工具：{{ (r.assessmentTools || []).join(' / ') }}</p>
        <p style="color:#888">标准依据：{{ r.standardBasis }}</p>
        <button nz-button (click)="exportDocx()">导出 Word</button>
      }
    </nz-spin>

    </ng-container>
  </nz-modal>

  <!-- 查看 -->
  <nz-modal [(nzVisible)]="viewVisible" [nzTitle]="viewTarget?.title || '查看方案'" nzWidth="900" (nzOnCancel)="viewVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    @if (viewResult(); as r) {
      <p><nz-tag>{{ viewTarget?.categoryName }}</nz-tag> {{ r.subject }} · {{ r.grade }} · {{ r.duration }}分钟 · {{ statusName(viewTarget?.status) }}</p>
      <nz-divider nzText="教学目标"></nz-divider>
      @for (o of r.objectives; track o) { <p>• {{ o }}</p> }
      <nz-divider nzText="教学重难点"></nz-divider>
      <p>重点：{{ (r.keyPoints || []).join('；') }}</p>
      <p>难点：{{ (r.difficulties || []).join('；') }}</p>
      <nz-divider nzText="教学过程"></nz-divider>
      @for (s of r.sections; track s.name) {
        <p><b>{{ s.name }}（{{ s.duration }}分钟）</b></p>
        <p>{{ s.content }}</p>
      }
      <nz-divider nzText="教学评价"></nz-divider>
      @for (a of r.assessment; track a) { <p>• {{ a }}</p> }
      <nz-divider nzText="板书设计"></nz-divider>
      @for (b of r.boardDesign; track b) { <p>• {{ b }}</p> }
      <p style="color:#888">标准依据：{{ r.standardBasis }}</p>
      @if (viewTarget?.reviewComment) { <p style="color:#c00">审核意见：{{ viewTarget.reviewComment }}</p> }
    }
  
    </ng-container>
  </nz-modal>

  <!-- 提交审核：指派教师 -->
  <nz-modal [(nzVisible)]="submitVisible" nzTitle="提交审核 — 指派审核教师" (nzOnCancel)="submitVisible = false" (nzOnOk)="confirmSubmit()">
    <ng-container *nzModalContent>
    <p>方案：{{ submitTarget?.title }}</p>
    <p>审核教师（本租户）</p>
    <nz-select [(ngModel)]="submitReviewerId" nzAllowClear nzPlaceHolder="选择教师，可不选" style="width:100%">
      @for (t of teachers(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.name + ' (' + t.userName + ' · ' + t.roleName + ')'"></nz-option> }
    </nz-select>
  
    </ng-container>
  </nz-modal>

  <!-- 审核 -->
  <nz-modal [(nzVisible)]="reviewVisible" nzTitle="审核方案" (nzOnCancel)="reviewVisible = false" (nzOnOk)="confirmReview()">
    <ng-container *nzModalContent>
    <p>方案：{{ reviewTarget?.title }}</p>
    <nz-radio-group [(ngModel)]="reviewApproved">
      <label nz-radio [nzValue]="true">通过（发布）</label>
      <label nz-radio [nzValue]="false">驳回（退回草稿）</label>
    </nz-radio-group>
    <p style="margin-top:8px">审核意见</p>
    <textarea nz-input rows="3" [(ngModel)]="reviewComment"></textarea>
  
    </ng-container>
  </nz-modal>
  `,
})
export class TeachingDesignComponent {
  svc = inject(SpecialEduService);
  private http = inject(HttpClient);
  private msg = inject(NzMessageService);
  private modal = inject(NzModalService);
  categories = SPECIAL_EDU_CATEGORIES;
  generating = signal(false);
  result = signal<any>(null);
  rawJson = signal('');
  list = signal<any[]>([]);
  pending = signal<any[]>([]);
  teachers = signal<any[]>([]);
  activeTab = 0;
  input: any = { category: 0, topic: '', objectives: '', studentTraits: '', conditions: '', subject: '生活语文', grade: '', duration: 40, customPrompt: '' };
  createVisible = false;
  viewVisible = false;
  viewTarget: any = null;
  viewResult = signal<any>(null);
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
    this.http.get<any>('/api/learning/special-edu/teaching-designs', { params: { maxResultCount: '50' } as any })
      .subscribe({ next: (r: any) => this.list.set(r?.items ?? []), error: () => {} });
    this.http.get<any>('/api/learning/special-edu/teaching-designs', { params: { maxResultCount: '50', status: '1' } as any })
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
      this.msg.warning('请填写课程主题');
      return;
    }
    this.generating.set(true);
    this.result.set(null);
    let buf = '';
    try {
      for await (const chunk of this.svc.generateTeachingDesign(this.input)) {
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
    this.http.post<any>('/api/learning/special-edu/teaching-designs', {
      category: this.input.category, resultJson: this.rawJson(),
      sourceInputJson: JSON.stringify(this.input),
    }).subscribe({
      next: () => { this.msg.success('已生成并自动保存为草稿'); this.createVisible = false; this.loadAll(); },
      error: () => this.msg.error('自动保存失败，请重试'),
    });
  }

  view(h: any): void {
    this.viewTarget = h;
    this.viewResult.set(this.svc.tryParse<any>(h.rawJson ?? ''));
    this.viewVisible = true;
  }

  openSubmit(h: any): void {
    this.submitTarget = h;
    this.submitReviewerId = h.reviewerUserId ?? null;
    this.submitVisible = true;
  }

  confirmSubmit(): void {
    if (!this.submitTarget) return;
    this.http.post<any>(`/api/learning/special-edu/teaching-designs/${this.submitTarget.id}/submit`, { reviewerUserId: this.submitReviewerId })
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
    this.http.post<any>('/api/learning/special-edu/teaching-designs/review',
      { id: this.reviewTarget.id, approved: this.reviewApproved, comment: this.reviewComment })
      .subscribe({
        next: () => { this.msg.success(this.reviewApproved ? '已审核发布' : '已驳回'); this.reviewVisible = false; this.loadAll(); },
        error: (e) => this.msg.error(e?.error?.message ?? '审核失败'),
      });
  }

  remove(h: any): void {
    this.modal.confirm({
      nzTitle: '确认删除该方案吗？',
      nzOnOk: () => this.http.delete(`/api/learning/special-edu/teaching-designs/${h.id}`)
        .subscribe({ next: () => { this.msg.success('已删除'); this.loadAll(); }, error: () => this.msg.error('删除失败') }),
    });
  }

  exportDocx(): void {
    if (!this.rawJson()) return;
    this.svc.downloadBlob('/api/learning/special-edu/export-teaching-design-docx', { resultJson: this.rawJson() }, `特教教案_${Date.now()}.docx`)
      .catch(() => this.msg.error('导出失败'));
  }
}
