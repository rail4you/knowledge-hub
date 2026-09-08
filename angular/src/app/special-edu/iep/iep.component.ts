import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
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
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { HttpClient } from '@angular/common/http';
import { SPECIAL_EDU_CATEGORIES, SpecialEduService } from '../special-edu.service';
import { ContentVersionFieldComponent } from '../content-version-field.component';

@Component({
  selector: 'app-iep',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NzCardModule, NzFormModule, NzInputModule, NzSelectModule, NzButtonModule, NzSpinModule, NzTagModule, NzDividerModule, NzTableModule, NzGridModule, NzTabsModule, NzModalModule, NzRadioModule, NzTooltipModule, NzEmptyModule, ContentVersionFieldComponent],
  styles: [`
    .spedu-container { padding: 24px; max-width: 1400px; margin: 0 auto; }
    .spedu-header { background: var(--kh-panel); border: 1px solid var(--kh-line); border-radius: var(--kh-r-card); box-shadow: var(--kh-shadow-card); padding: 16px 20px; margin-bottom: 16px; }
    .spedu-header h1 { margin: 0; font-size: 20px; font-weight: 700; }
    .spedu-header p { margin: 4px 0 0; color: #888; font-size: 13px; }
    .spedu-card { min-height: calc(100vh - 320px); display: flex; flex-direction: column; }
    .spedu-card ::ng-deep .ant-card { flex: 1; display: flex; flex-direction: column; }
    .spedu-card ::ng-deep .ant-card-body { flex: 1; display: flex; flex-direction: column; }
    .row-actions { white-space: nowrap; }
    .table-toolbar { display: flex; justify-content: flex-end; gap: 12px; margin-bottom: 12px; flex-shrink: 0; }
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
  <div class="spedu-container">
  <div class="spedu-header">
    <h1>IEP 个别化教育计划</h1>
    <p>为特殊学生制定个别化教育计划：现状分析、长期/短期目标、教学策略与家校协同，草稿可提交审核发布，支持版本追溯与 Word 导出。</p>
  </div>
  <nz-card class="spedu-card">
    <nz-tabs [(nzSelectedIndex)]="activeTab">
      <nz-tab nzTitle="IEP 列表">
        <div class="table-toolbar">
          <button nz-button nzType="primary" (click)="openCreate()">新建 IEP</button>
        </div>
        <nz-table [nzData]="pagedList()" [nzFrontPagination]="false" [nzTotal]="list().length"
          [nzPageIndex]="listPageIndex()" [nzPageSize]="listPageSize()"
          (nzPageIndexChange)="onListPageIndexChange($event)" (nzPageSizeChange)="onListPageSizeChange($event)"
          [nzShowSizeChanger]="true" [nzShowQuickJumper]="true" [nzShowTotal]="totalTplList" [nzNoResult]="emptyTplList"
          nzSize="small">
          <thead><tr><th>学生</th><th>类别</th><th>版本</th><th>状态</th><th>最后修改</th><th>审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of pagedList(); track h.id) {
              <tr><td>{{ h.studentName }}</td><td>{{ h.categoryName }}</td><td>v{{ h.versionNumber }}</td>
              <td>{{ statusName(h.status) }}</td>
              <td>{{ (h.lastModificationTime || h.creationTime) | date:'yyyy-MM-dd HH:mm' }}</td>
              <td>{{ h.reviewerName || '—' }}</td>
              <td class="row-actions">
                <button nz-button nzType="link" nzSize="small" (click)="view(h)">查看</button>
                <button nz-button nzType="link" nzSize="small" (click)="openEdit(h)">编辑</button>
                @if (h.status === 0 || h.status === 2 || h.status === 3) {
                  <button nz-button nzType="link" nzSize="small" (click)="openSubmit(h)">提交审核</button>
                }
                <button nz-button nzType="link" nzSize="small" nzDanger (click)="remove(h)">删除</button>
              </td></tr>
            }
          </tbody>
        </nz-table>
        <ng-template #totalTplList let-total>共 {{ total }} 条</ng-template>
        <ng-template #emptyTplList><nz-empty nzNotFoundContent="暂无 IEP 方案，点击上方新建"></nz-empty></ng-template>
      </nz-tab>
      <nz-tab [nzTitle]="'待审核 (' + pending().length + ')'">
        <nz-table [nzData]="pagedPending()" [nzFrontPagination]="false" [nzTotal]="pending().length"
          [nzPageIndex]="pendingPageIndex()" [nzPageSize]="pendingPageSize()"
          (nzPageIndexChange)="onPendingPageIndexChange($event)" (nzPageSizeChange)="onPendingPageSizeChange($event)"
          [nzShowSizeChanger]="true" [nzShowQuickJumper]="true" [nzShowTotal]="totalTplPending" [nzNoResult]="emptyTplPending"
          nzSize="small">
          <thead><tr><th>学生</th><th>类别</th><th>版本</th><th>指派审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of pagedPending(); track h.id) {
              <tr><td>{{ h.studentName }}</td><td>{{ h.categoryName }}</td><td>v{{ h.versionNumber }}</td>
              <td>{{ h.reviewerName || '未指派' }}</td>
              <td class="row-actions">
                <button nz-button nzType="link" nzSize="small" (click)="view(h)">查看</button>
                <button nz-button nzType="link" nzSize="small" (click)="openEdit(h)">编辑</button>
                <button nz-button nzType="link" nzSize="small" (click)="openReview(h, true)">通过</button>
                <button nz-button nzType="link" nzSize="small" nzDanger (click)="openReview(h, false)">驳回</button>
              </td></tr>
            }
          </tbody>
        </nz-table>
        <ng-template #totalTplPending let-total>共 {{ total }} 条</ng-template>
        <ng-template #emptyTplPending><nz-empty nzNotFoundContent="暂无待审核 IEP"></nz-empty></ng-template>
      </nz-tab>
    </nz-tabs>
  </nz-card>
  </div>

  <!-- 新建：弹出表单 -->
  <nz-modal [(nzVisible)]="createVisible" nzTitle="新建 IEP 方案" [nzWidth]="640" (nzOnCancel)="createVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    <div class="dh-modal">
    <div class="dh-form">
    <div class="field">
      <span class="label">关联课程</span>
      <nz-select [(ngModel)]="input.courseId" (ngModelChange)="onCourseChange()" nzAllowClear nzPlaceHolder="选择课程（自动加载）" style="width:100%">
        @for (c of courses(); track c.id) { <nz-option [nzValue]="c.id" [nzLabel]="c.title"></nz-option> }
      </nz-select>
    </div>
    <div class="field">
      <span class="label">学生</span>
      <nz-select [(ngModel)]="input.studentUserId" (ngModelChange)="onStudentChange()" nzShowSearch nzPlaceHolder="选择学生" style="width:100%">
        @for (s of students(); track s.id) {
          <nz-option [nzValue]="s.id" [nzLabel]="s.name + ' (' + s.userName + ')' + (s.enrolledInSelectedCourse ? ' · 已选课' : '')"></nz-option>
        }
      </nz-select>
    </div>
    <label class="field full">
      <span class="label">障碍类别</span>
      <nz-select [(ngModel)]="input.category" style="width:100%">
        @for (c of categories; track c.value) { <nz-option [nzValue]="c.value" [nzLabel]="c.label"></nz-option> }
      </nz-select>
    </label>
    <label class="field full">
      <span class="label">评估数据</span>
      <textarea nz-input rows="3" [(ngModel)]="input.assessmentData" placeholder="如：听力损失60dB，词汇量30，精细动作弱"></textarea>
    </label>
    <label class="field full">
      <span class="label">当前发展水平</span>
      <textarea nz-input rows="2" [(ngModel)]="input.currentLevel" placeholder="如：能指认20个常见词，等待能力约30秒"></textarea>
    </label>
    <label class="field full">
      <span class="label">家庭需求</span>
      <textarea nz-input rows="2" [(ngModel)]="input.familyNeeds" placeholder="如：家长每晚可配合15分钟"></textarea>
    </label>
    </div>
    <nz-spin [nzSpinning]="generating()">
      @if (result(); as r) {
        <nz-divider></nz-divider>
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
      }
    </nz-spin>
    <div class="modal-foot">
      <button nz-button nzShape="circle" nz-tooltip [nzTooltipTitle]="helpTpl" nzTooltipPlacement="top" aria-label="填写说明">?</button>
      <ng-template #helpTpl>
        <div>选择课程与学生，填写评估数据与发展水平后生成 IEP。</div>
        <div>包含学生现状分析、长期/短期目标、教学策略、评估方式、家校协同。</div>
        <div>生成后自动保存为草稿，支持结构化编辑（自动版本管理）、提交审核、导出 Word。</div>
      </ng-template>
      <button nz-button (click)="createVisible = false">取消</button>
      <button nz-button nzType="primary" (click)="generate()" [nzLoading]="generating()">生成 IEP（自动存为草稿）</button>
    </div>
    </div>

    </ng-container>
  </nz-modal>

  <!-- 查看：按 id 拉详情，直接渲染结构化字段 -->
  <nz-modal [(nzVisible)]="viewVisible" [nzTitle]="'IEP：' + (viewDetail()?.studentName || viewTarget?.studentName || '')" [nzWidth]="720" (nzOnCancel)="viewVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    @if (viewLoading()) {
      <div style="text-align:center;padding:48px;"><nz-spin nzSimple></nz-spin></div>
    } @else if (viewDetail(); as v) {
      <p>{{ v.categoryName || viewTarget?.categoryName }} · v{{ v.versionNumber ?? viewTarget?.versionNumber }} · {{ statusName(v.status ?? viewTarget?.status) }}</p>
      <h3>现状分析</h3><p>{{ v.profileSummary || '—' }}</p>
      <nz-divider nzText="长期目标"></nz-divider>
      @for (g of (v.longTermGoals || []); track $index) { <p>• {{ g }}</p> } @empty { <p style="color:#999">暂无</p> }
      <nz-divider nzText="短期目标"></nz-divider>
      @for (g of (v.shortTermGoals || []); track $index) { <p>• {{ g }}</p> } @empty { <p style="color:#999">暂无</p> }
      <nz-divider nzText="教学策略"></nz-divider>
      @for (g of (v.strategies || []); track $index) { <p>• {{ g }}</p> } @empty { <p style="color:#999">暂无</p> }
      <nz-divider nzText="评估方式"></nz-divider>
      @for (g of (v.evaluation || []); track $index) { <p>• {{ g }}</p> } @empty { <p style="color:#999">暂无</p> }
      <nz-divider nzText="家校协同"></nz-divider>
      @for (g of (v.homeSchool || []); track $index) { <p>• {{ g }}</p> } @empty { <p style="color:#999">暂无</p> }
      @if (v.legalBasis) { <p style="color:#888">法规依据：{{ v.legalBasis }}</p> }
      @if (v.reviewComment || viewTarget?.reviewComment) { <p style="color:#c00">审核意见：{{ v.reviewComment || viewTarget?.reviewComment }}</p> }
      <div style="margin-top:12px">
        <button nz-button nzType="primary" (click)="openEdit(v)">编辑内容（当前 v{{ v.versionNumber ?? 1 }}）</button>
        <button nz-button style="margin-left:8px" (click)="exportViewDocx()">导出 Word</button>
      </div>
    } @else {
      <p style="color:#999">暂无可展示内容。</p>
    }
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
      <span class="label">现状分析</span>
      <textarea nz-input rows="3" [(ngModel)]="edit.profileSummary"></textarea>
      <app-content-version-field [versions]="fieldVersions('profileSummary')" (adopt)="adoptField('profileSummary', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">长期目标（每行一条）</span>
      <textarea nz-input rows="3" [(ngModel)]="edit.longTermGoals"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('longTermGoals')" (adopt)="adoptField('longTermGoals', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">短期目标（每行一条）</span>
      <textarea nz-input rows="3" [(ngModel)]="edit.shortTermGoals"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('shortTermGoals')" (adopt)="adoptField('shortTermGoals', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">教学策略（每行一条）</span>
      <textarea nz-input rows="3" [(ngModel)]="edit.strategies"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('strategies')" (adopt)="adoptField('strategies', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">评估方式（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.evaluation"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('evaluation')" (adopt)="adoptField('evaluation', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">家校协同（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.homeSchool"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('homeSchool')" (adopt)="adoptField('homeSchool', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">法规依据</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.legalBasis"></textarea>
      <app-content-version-field [versions]="fieldVersions('legalBasis')" (adopt)="adoptField('legalBasis', $event)"></app-content-version-field>
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
    <label class="field full"><span class="label">学生</span><span>{{ submitTarget?.studentName }}</span></label>
    <div class="field full">
    <span class="label">审核教师</span>
    <nz-select [(ngModel)]="submitReviewerId" nzAllowClear nzPlaceHolder="选择教师，可不选" style="width:100%">
      @for (t of teachers(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.name + ' (' + t.userName + ' · ' + t.roleName + ')'"></nz-option> }
    </nz-select>
    </div>
    </div>
    </ng-container>
  </nz-modal>

  <nz-modal [(nzVisible)]="reviewVisible" nzTitle="审核 IEP" [nzWidth]="520" (nzOnCancel)="reviewVisible = false" (nzOnOk)="confirmReview()">
    <ng-container *nzModalContent>
    <div class="dh-form">
    <label class="field full"><span class="label">学生</span><span>{{ reviewTarget?.studentName }}</span></label>
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
  // 列表分页（前端分页：数据已全量加载，按页切片展示）
  listPageIndex = signal(1);
  listPageSize = signal(10);
  pagedList = computed(() => {
    const all = this.list();
    const start = (this.listPageIndex() - 1) * this.listPageSize();
    return all.slice(start, start + this.listPageSize());
  });
  pendingPageIndex = signal(1);
  pendingPageSize = signal(10);
  pagedPending = computed(() => {
    const all = this.pending();
    const start = (this.pendingPageIndex() - 1) * this.pendingPageSize();
    return all.slice(start, start + this.pendingPageSize());
  });
  onListPageIndexChange(i: number): void { this.listPageIndex.set(i); }
  onListPageSizeChange(s: number): void { this.listPageSize.set(s); this.listPageIndex.set(1); }
  onPendingPageIndexChange(i: number): void { this.pendingPageIndex.set(i); }
  onPendingPageSizeChange(s: number): void { this.pendingPageSize.set(s); this.pendingPageIndex.set(1); }
  courses = signal<any[]>([]);
  students = signal<any[]>([]);
  teachers = signal<any[]>([]);
  activeTab = 0;
  input: any = { studentName: '', studentUserId: '', courseId: null, category: 0, assessmentData: '', currentLevel: '', familyNeeds: '', customPrompt: '' };
  createVisible = false;
  viewVisible = false;
  viewTarget: any = null;
  viewDetail = signal<any>(null);
  viewLoading = signal(false);
  // 结构化编辑
  editVisible = false;
  editLoading = signal(false);
  editSaving = signal(false);
  versions = signal<any[]>([]);
  parsedVersions = computed(() => this.versions().map(v => ({ ...v, snap: this.svc.tryParse<any>(v.snapshotJson ?? '{}') ?? {} })));
  edit: any = this.emptyEdit();
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
    this.listPageIndex.set(1);
    this.pendingPageIndex.set(1);
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

  openCreate(): void {
    this.result.set(null);
    this.rawJson.set('');
    this.createVisible = true;
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
      next: () => { this.msg.success('已生成并自动保存为草稿'); this.createVisible = false; this.loadAll(); },
      error: () => this.msg.error('自动保存失败，请重试'),
    });
  }

  view(h: any): void {
    this.viewTarget = h;
    this.viewDetail.set(null);
    this.viewVisible = true;
    this.viewLoading.set(true);
    this.http.get<any>(`/api/learning/special-edu/ieps/${h.id}`).subscribe({
      next: r => { this.viewDetail.set(r); this.viewLoading.set(false); },
      error: () => { this.viewDetail.set(h); this.viewLoading.set(false); },
    });
  }

  exportViewDocx(): void {
    const v = this.viewDetail();
    if (!v?.rawJson) { this.msg.warning('该方案暂无可导出内容'); return; }
    this.svc.downloadBlob('/api/learning/special-edu/export-iep-docx', { resultJson: v.rawJson }, `IEP_${v.studentName || Date.now()}.docx`)
      .catch(() => this.msg.error('导出失败'));
  }

  // ── 结构化编辑 + 版本 ──
  emptyEdit(): any {
    return { id: '', versionNumber: 1, profileSummary: '', longTermGoals: '', shortTermGoals: '', strategies: '', evaluation: '', homeSchool: '', legalBasis: '' };
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
    this.http.get<any>(`/api/learning/special-edu/ieps/${h.id}`).subscribe({
      next: d => { this.fillEdit(d); this.editLoading.set(false); },
      error: () => { this.fillEdit(h); this.editLoading.set(false); },
    });
    this.http.get<any[]>(`/api/learning/special-edu/ieps/${h.id}/versions`).subscribe({
      next: v => this.versions.set(v ?? []),
      error: () => this.versions.set([]),
    });
  }

  fillEdit(d: any): void {
    this.edit = {
      id: d.id, versionNumber: d.versionNumber ?? 1,
      profileSummary: d.profileSummary ?? '',
      longTermGoals: this.joinLines(d.longTermGoals), shortTermGoals: this.joinLines(d.shortTermGoals),
      strategies: this.joinLines(d.strategies), evaluation: this.joinLines(d.evaluation),
      homeSchool: this.joinLines(d.homeSchool), legalBasis: d.legalBasis ?? '',
    };
  }

  fieldVersions(key: string): any[] {
    return this.parsedVersions()
      .filter(v => v.versionNumber < (this.edit.versionNumber ?? 999))
      .map(v => ({ versionNumber: v.versionNumber, creationTime: v.creationTime, creatorName: v.creatorName, value: v.snap?.[key] }));
  }

  adoptField(key: string, value: any): void {
    if (Array.isArray(value)) this.edit[key] = value.join('\n');
    else this.edit[key] = value ?? '';
  }

  saveEdit(): void {
    if (!this.edit.id) return;
    const resultJson = JSON.stringify({
      profileSummary: this.edit.profileSummary,
      longTermGoals: this.splitLines(this.edit.longTermGoals), shortTermGoals: this.splitLines(this.edit.shortTermGoals),
      strategies: this.splitLines(this.edit.strategies), evaluation: this.splitLines(this.edit.evaluation),
      homeSchool: this.splitLines(this.edit.homeSchool), legalBasis: this.edit.legalBasis,
    });
    this.editSaving.set(true);
    this.http.post<any>(`/api/learning/special-edu/ieps/${this.edit.id}/content`, {
      profileSummary: this.edit.profileSummary,
      longTermGoals: this.splitLines(this.edit.longTermGoals), shortTermGoals: this.splitLines(this.edit.shortTermGoals),
      strategies: this.splitLines(this.edit.strategies), evaluation: this.splitLines(this.edit.evaluation),
      homeSchool: this.splitLines(this.edit.homeSchool), legalBasis: this.edit.legalBasis, resultJson,
    }).subscribe({
      next: r => {
        this.editSaving.set(false);
        this.editVisible = false;
        this.msg.success(`已保存为 v${r?.versionNumber ?? ''}（自动存为草稿）`);
        this.loadAll();
        if (this.viewTarget?.id === r?.id) this.view(r);
      },
      error: e => { this.editSaving.set(false); this.msg.error(e?.error?.message ?? '保存失败'); },
    });
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
