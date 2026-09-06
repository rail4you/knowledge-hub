import { Component, ChangeDetectionStrategy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
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
import { HttpClient } from '@angular/common/http';
import { SPECIAL_EDU_CATEGORIES, SpecialEduService } from '../special-edu.service';

@Component({
  selector: 'app-iep',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NzCardModule, NzFormModule, NzInputModule, NzSelectModule, NzButtonModule, NzSpinModule, NzTagModule, NzDividerModule, NzTableModule, NzGridModule, NzTabsModule, NzModalModule, NzRadioModule],
  template: `
  <nz-card nzTitle="IEP 教学实施方案" [nzExtra]="extraTpl">
    <nz-tabset [(nzSelectedIndex)]="activeTab">
      <nz-tab nzTitle="录入/生成">
        <div nz-row [nzGutter]="16" style="margin-top:12px">
          <div nz-col [nzSpan]="9">
            <nz-form-item><nz-form-label>关联课程</nz-form-label>
              <nz-select [(ngModel)]="input.courseId" (ngModelChange)="onCourseChange()" nzAllowClear nzPlaceHolder="选择课程（自动加载）" style="width:100%">
                @for (c of courses(); track c.id) { <nz-option [nzValue]="c.id" [nzLabel]="c.title"></nz-option> }
              </nz-select></nz-form-item>
            <nz-form-item><nz-form-label>学生（自动加载本租户学生）</nz-form-label>
              <nz-select [(ngModel)]="input.studentUserId" (ngModelChange)="onStudentChange()" nzShowSearch nzPlaceHolder="选择学生" style="width:100%">
                @for (s of students(); track s.id) {
                  <nz-option [nzValue]="s.id" [nzLabel]="s.name + ' (' + s.userName + ')' + (s.enrolledInSelectedCourse ? ' · 已选课' : '')"></nz-option>
                }
              </nz-select></nz-form-item>
            <nz-form-item><nz-form-label>障碍类别</nz-form-label>
              <nz-select [(ngModel)]="input.category" style="width:100%">
                @for (c of categories; track c.value) { <nz-option [nzValue]="c.value" [nzLabel]="c.label"></nz-option> }
              </nz-select></nz-form-item>
            <nz-form-item><nz-form-label>评估数据（手动表单）</nz-form-label>
              <textarea nz-input rows="3" [(ngModel)]="input.assessmentData" placeholder="如：听力损失60dB，词汇量30，精细动作弱"></textarea></nz-form-item>
            <nz-form-item><nz-form-label>当前发展水平</nz-form-label>
              <textarea nz-input rows="2" [(ngModel)]="input.currentLevel" placeholder="如：能指认20个常见词，等待能力约30秒"></textarea></nz-form-item>
            <nz-form-item><nz-form-label>家庭需求 / 家校信息</nz-form-label>
              <textarea nz-input rows="2" [(ngModel)]="input.familyNeeds" placeholder="如：家长每晚可配合15分钟"></textarea></nz-form-item>
            <button nz-button nzType="primary" (click)="generate()" [nzLoading]="generating()">生成 IEP（自动存为草稿）</button>
          </div>
          <div nz-col [nzSpan]="15">
            <nz-spin [nzSpinning]="generating()">
              @if (result(); as r) {
                <h3>现状分析</h3><p>{{ r.profileSummary }}</p>
                <nz-divider nzText="长期目标"></nz-divider>
                @for (g of r.longTermGoals; track g) { <p>• {{ g }}</p> }
                <nz-divider nzText="短期目标"></nz-divider>
                @for (g of r.shortTermGoals; track g) { <p>• {{ g }}</p> }
                <nz-divider nzText="教学策略"></nz-divider>
                @for (g of r.strategies; track g) { <p>• {{ g }}</p> }
                <nz-divider nzText="评估方式"></nz-divider>
                @for (g of r.evaluation; track g) { <p>• {{ g }}</p> }
                <nz-divider nzText="家校协同"></nz-divider>
                @for (g of r.homeSchool; track g) { <p>• {{ g }}</p> }
                <p style="color:#888">法规依据：{{ r.legalBasis }}</p>
                <button nz-button (click)="exportDocx()">导出 Word</button>
              } @else {
                <p style="color:#999">填写评估数据后生成，生成后自动保存为草稿，可在“IEP 列表”中提交审核。</p>
              }
            </nz-spin>
          </div>
        </div>
      </nz-tab>
      <nz-tab nzTitle="IEP 列表">
        <nz-table [nzData]="list()" nzSize="small" style="margin-top:12px">
          <thead><tr><th>学生</th><th>类别</th><th>版本</th><th>状态</th><th>审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of list(); track h.id) {
              <tr><td>{{ h.studentName }}</td><td>{{ h.categoryName }}</td><td>v{{ h.versionNumber }}</td>
              <td>{{ statusName(h.status) }}</td><td>{{ h.reviewerName || '—' }}</td>
              <td>
                <a (click)="view(h)">查看</a>
                @if (h.status === 0 || h.status === 2 || h.status === 3) {
                  <a (click)="openSubmit(h)" style="margin-left:8px">提交审核</a>
                }
                <a (click)="revision(h)" style="margin-left:8px">修订版</a>
                <a (click)="remove(h)" style="margin-left:8px;color:#ff4d4f">删除</a>
              </td></tr>
            }
          </tbody>
        </nz-table>
      </nz-tab>
      <nz-tab [nzTitle]="'待审核 (' + pending().length + ')'">
        <nz-table [nzData]="pending()" nzSize="small" style="margin-top:12px">
          <thead><tr><th>学生</th><th>类别</th><th>版本</th><th>指派审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of pending(); track h.id) {
              <tr><td>{{ h.studentName }}</td><td>{{ h.categoryName }}</td><td>v{{ h.versionNumber }}</td>
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
  <ng-template #extraTpl><span style="color:#888">生成即存草稿 · 审核后发布 · 版本可迭代</span></ng-template>

  <nz-modal [(nzVisible)]="submitVisible" nzTitle="提交审核 — 指派审核教师" (nzOnCancel)="submitVisible = false" (nzOnOk)="confirmSubmit()">
    <p>学生：{{ submitTarget?.studentName }}</p>
    <p>审核教师（本租户）</p>
    <nz-select [(ngModel)]="submitReviewerId" nzAllowClear nzPlaceHolder="选择教师，可不选" style="width:100%">
      @for (t of teachers(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.name + ' (' + t.userName + ' · ' + t.roleName + ')'"></nz-option> }
    </nz-select>
  </nz-modal>

  <nz-modal [(nzVisible)]="reviewVisible" nzTitle="审核 IEP" (nzOnCancel)="reviewVisible = false" (nzOnOk)="confirmReview()">
    <p>学生：{{ reviewTarget?.studentName }}</p>
    <nz-radio-group [(ngModel)]="reviewApproved">
      <label nz-radio [nzValue]="true">通过（发布）</label>
      <label nz-radio [nzValue]="false">驳回（退回草稿）</label>
    </nz-radio-group>
    <p style="margin-top:8px">审核意见</p>
    <textarea nz-input rows="3" [(ngModel)]="reviewComment"></textarea>
  </nz-modal>
  `,
})
export class IepComponent {
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
  courses = signal<any[]>([]);
  students = signal<any[]>([]);
  teachers = signal<any[]>([]);
  savedId = signal<string | null>(null);
  activeTab = 0;
  input: any = { studentName: '', studentUserId: '', courseId: null, category: 0, assessmentData: '', currentLevel: '', familyNeeds: '', customPrompt: '' };
  submitVisible = false;
  submitTarget: any = null;
  submitReviewerId: string | null = null;
  reviewVisible = false;
  reviewTarget: any = null;
  reviewApproved = true;
  reviewComment = '';

  constructor() {
    this.loadAll();
    this.loadCourses();
    this.loadStudents();
    this.loadTeachers();
  }

  statusName(s: number): string {
    return ['草稿', '待审核', '已审核', '已发布', '已归档'][s] ?? String(s);
  }

  loadAll(): void {
    this.http.get<any>('/api/learning/special-edu/ieps', { params: { maxResultCount: '50' } as any })
      .subscribe({ next: (r: any) => this.list.set(r?.items ?? []), error: () => {} });
    this.http.get<any>('/api/learning/special-edu/ieps', { params: { maxResultCount: '50', status: '1' } as any })
      .subscribe({ next: (r: any) => this.pending.set(r?.items ?? []), error: () => {} });
  }

  loadCourses(): void {
    this.http.get<any[]>('/api/app/special-edu-option/course-options')
      .subscribe({ next: r => this.courses.set(r ?? []), error: () => {} });
  }

  loadStudents(): void {
    const params: any = {};
    if (this.input.courseId) params.courseId = this.input.courseId;
    this.http.get<any[]>('/api/app/special-edu-option/student-options', { params })
      .subscribe({ next: r => this.students.set(r ?? []), error: () => {} });
  }

  loadTeachers(): void {
    this.http.get<any[]>('/api/app/special-edu-option/teacher-options')
      .subscribe({ next: r => this.teachers.set(r ?? []), error: () => {} });
  }

  onCourseChange(): void {
    this.loadStudents();
  }

  onStudentChange(): void {
    const s = this.students().find(x => x.id === this.input.studentUserId);
    if (s) this.input.studentName = s.name;
  }

  async generate(): Promise<void> {
    if (!this.input.assessmentData?.trim()) {
      this.msg.warning('请填写评估数据');
      return;
    }
    if (!this.input.studentUserId) {
      this.msg.warning('请先选择学生');
      return;
    }
    this.generating.set(true);
    this.result.set(null);
    this.savedId.set(null);
    let buf = '';
    try {
      const body = { ...this.input };
      for await (const chunk of this.svc.generateIep(body)) {
        buf += chunk.content ?? '';
        const parsed = this.svc.tryParse<any>(buf);
        if (parsed?.profileSummary) this.result.set(parsed);
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
    if (!this.rawJson() || !this.input.studentUserId) return;
    this.http.post<any>('/api/learning/special-edu/ieps', {
      studentUserId: this.input.studentUserId, studentName: this.input.studentName,
      category: this.input.category, courseId: this.input.courseId || undefined,
      resultJson: this.rawJson(), sourceInputJson: JSON.stringify(this.input),
    }).subscribe({
      next: r => { this.savedId.set(r.id); this.msg.success('已生成并自动保存为草稿'); this.loadAll(); },
      error: () => this.msg.error('自动保存失败，请重试'),
    });
  }

  view(h: any): void {
    this.savedId.set(h.id);
    this.input.studentName = h.studentName;
    this.input.studentUserId = h.studentUserId;
    this.input.courseId = h.courseId ?? null;
    this.loadStudents();
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
    this.http.post<any>(`/api/learning/special-edu/ieps/${this.submitTarget.id}/submit`, { reviewerUserId: this.submitReviewerId })
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
    this.http.post<any>('/api/learning/special-edu/ieps/review',
      { id: this.reviewTarget.id, approved: this.reviewApproved, comment: this.reviewComment })
      .subscribe({
        next: () => { this.msg.success(this.reviewApproved ? '已审核发布' : '已驳回'); this.reviewVisible = false; this.loadAll(); },
        error: (e) => this.msg.error(e?.error?.message ?? '审核失败'),
      });
  }

  revision(h: any): void {
    this.http.post<any>(`/api/learning/special-edu/ieps/${h.id}/revision`, {})
      .subscribe({ next: r => { this.msg.success(`已创建 v${r.versionNumber} 修订版`); this.loadAll(); }, error: () => this.msg.error('修订失败') });
  }

  remove(h: any): void {
    this.modal.confirm({
      nzTitle: '确认删除该 IEP 吗？',
      nzOnOk: () => this.http.delete(`/api/learning/special-edu/ieps/${h.id}`)
        .subscribe({ next: () => { this.msg.success('已删除'); this.loadAll(); }, error: () => this.msg.error('删除失败') }),
    });
  }

  exportDocx(): void {
    if (!this.rawJson()) return;
    this.svc.downloadBlob('/api/learning/special-edu/export-iep-docx', { resultJson: this.rawJson() }, `IEP_${Date.now()}.docx`)
      .catch(() => this.msg.error('导出失败'));
  }
}
