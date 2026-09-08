import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
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
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { HttpClient } from '@angular/common/http';
import { SPECIAL_EDU_CATEGORIES, SpecialEduService } from '../special-edu.service';
import { ContentVersionFieldComponent } from '../content-version-field.component';

@Component({
  selector: 'app-teaching-design',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NzCardModule, NzFormModule, NzInputModule, NzInputNumberModule, NzSelectModule, NzButtonModule, NzSpinModule, NzTagModule, NzDividerModule, NzTableModule, NzGridModule, NzTabsModule, NzModalModule, NzRadioModule, NzTooltipModule, NzEmptyModule, ContentVersionFieldComponent],
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
    .dh-modal { display: flex; flex-direction: column; gap: 18px; max-height: 85vh; overflow-y: auto; padding: 2px 2px 0; }
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
    <h1>智能教学设计方案（特教）</h1>
    <p>按特殊教育类别、学科与学生特点 AI 生成完整教案，草稿可提交审核发布，支持版本追溯与 Word 导出。</p>
  </div>
  <nz-card class="spedu-card">
    <nz-tabs [(nzSelectedIndex)]="activeTab">
      <nz-tab nzTitle="方案列表">
        <div class="table-toolbar">
          <button nz-button nzType="primary" (click)="openCreate()">新建方案</button>
        </div>
        <nz-table [nzData]="pagedList()" [nzFrontPagination]="false" [nzTotal]="list().length"
          [nzPageIndex]="listPageIndex()" [nzPageSize]="listPageSize()"
          (nzPageIndexChange)="onListPageIndexChange($event)" (nzPageSizeChange)="onListPageSizeChange($event)"
          [nzShowSizeChanger]="true" [nzShowQuickJumper]="true" [nzShowTotal]="totalTplList" [nzNoResult]="emptyTplList"
          nzSize="small">
          <thead><tr><th>标题</th><th>类别</th><th>版本</th><th>状态</th><th>最后修改</th><th>审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of pagedList(); track h.id) {
              <tr><td>{{ h.title }}</td><td>{{ h.categoryName }}</td><td>v{{ h.versionNumber ?? 1 }}</td><td>{{ statusName(h.status) }}</td>
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
        <ng-template #emptyTplList><nz-empty nzNotFoundContent="暂无教学设计方案，点击上方新建"></nz-empty></ng-template>
      </nz-tab>
      <nz-tab [nzTitle]="'待审核 (' + pending().length + ')'">
        <nz-table [nzData]="pagedPending()" [nzFrontPagination]="false" [nzTotal]="pending().length"
          [nzPageIndex]="pendingPageIndex()" [nzPageSize]="pendingPageSize()"
          (nzPageIndexChange)="onPendingPageIndexChange($event)" (nzPageSizeChange)="onPendingPageSizeChange($event)"
          [nzShowSizeChanger]="true" [nzShowQuickJumper]="true" [nzShowTotal]="totalTplPending" [nzNoResult]="emptyTplPending"
          nzSize="small">
          <thead><tr><th>标题</th><th>类别</th><th>版本</th><th>指派审核教师</th><th>操作</th></tr></thead>
          <tbody>
            @for (h of pagedPending(); track h.id) {
              <tr><td>{{ h.title }}</td><td>{{ h.categoryName }}</td>
              <td>v{{ h.versionNumber ?? 1 }}</td>
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
        <ng-template #emptyTplPending><nz-empty nzNotFoundContent="暂无待审核方案"></nz-empty></ng-template>
      </nz-tab>
    </nz-tabs>
  </nz-card>
  </div>

  <!-- 新建：弹出表单，生成后自动存为草稿 -->
  <nz-modal [(nzVisible)]="createVisible" nzTitle="新建教学设计方案" [nzWidth]="640" (nzOnCancel)="createVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    <div class="dh-modal">
    <div class="dh-form">
    <label class="field">
      <span class="label">特殊教育类别</span>
      <nz-select [(ngModel)]="input.category" style="width:100%">
        @for (c of categories; track c.value) { <nz-option [nzValue]="c.value" [nzLabel]="c.label"></nz-option> }
      </nz-select>
    </label>
    <label class="field">
      <span class="label">学科</span>
      <input nz-input [(ngModel)]="input.subject" placeholder="生活语文" />
    </label>
    <label class="field">
      <span class="label">学段</span>
      <input nz-input [(ngModel)]="input.grade" placeholder="培智三年级" />
    </label>
    <div class="field">
      <span class="label">课时（分钟）</span>
      <nz-input-number [(ngModel)]="input.duration" [nzMin]="20" [nzMax]="120" style="width:100%"></nz-input-number>
    </div>
    <label class="field full">
      <span class="label">课程主题 / 课题</span>
      <input nz-input [(ngModel)]="input.topic" placeholder="如：《认识水果》" />
    </label>
    <label class="field">
      <span class="label">教学目标（关键词）</span>
      <textarea nz-input rows="2" [(ngModel)]="input.objectives" placeholder="如：指认三种水果、颜色配对、卫生习惯"></textarea>
    </label>
    <label class="field">
      <span class="label">学生特点</span>
      <textarea nz-input rows="2" [(ngModel)]="input.studentTraits" placeholder="如：培智三年级，注意力15分钟，需视觉提示"></textarea>
    </label>
    <label class="field">
      <span class="label">教学条件</span>
      <textarea nz-input rows="2" [(ngModel)]="input.conditions" placeholder="如：实物水果、图片卡、小组4人"></textarea>
    </label>
    <label class="field">
      <span class="label">附加要求</span>
      <textarea nz-input rows="2" [(ngModel)]="input.customPrompt"></textarea>
    </label>
    </div>
    <nz-spin [nzSpinning]="generating()">
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
    <div class="modal-foot">
      <button nz-button nzShape="circle" nz-tooltip [nzTooltipTitle]="helpTpl" nzTooltipPlacement="top" aria-label="填写说明">?</button>
      <ng-template #helpTpl>
        <div>按教学目标、学生特点、教学条件生成完整特教教案。</div>
        <div>包含教学目标、重难点、教学过程、教学评价、板书设计及配套课件大纲、活动设计、评估工具。</div>
        <div>生成后自动保存为草稿，可在列表中提交审核、导出 Word。</div>
      </ng-template>
      <button nz-button (click)="createVisible = false">取消</button>
      <button nz-button nzType="primary" (click)="generate()" [nzLoading]="generating()">生成方案（自动存为草稿）</button>
    </div>
    </div>

    </ng-container>
  </nz-modal>

  <!-- 查看：按 id 拉详情，直接渲染结构化字段 -->
  <nz-modal [(nzVisible)]="viewVisible" [nzTitle]="viewDetail()?.title || viewTarget?.title || '查看方案'" [nzWidth]="720" (nzOnCancel)="viewVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    @if (viewLoading()) {
      <div style="text-align:center;padding:48px;"><nz-spin nzSimple></nz-spin></div>
    } @else if (viewDetail(); as v) {
      <p><nz-tag>{{ v.categoryName || viewTarget?.categoryName }}</nz-tag> {{ v.subject }} · {{ v.grade }} · {{ v.duration }}分钟 · {{ statusName(v.status ?? viewTarget?.status) }}</p>
      <nz-divider nzText="教学目标"></nz-divider>
      @for (o of (v.objectives || []); track $index) { <p>• {{ o }}</p> } @empty { <p style="color:#999">暂无</p> }
      <nz-divider nzText="教学重难点"></nz-divider>
      <p>重点：{{ (v.keyPoints || []).join('；') || '—' }}</p>
      <p>难点：{{ (v.difficulties || []).join('；') || '—' }}</p>
      <nz-divider nzText="教学过程"></nz-divider>
      @for (s of (v.sections || []); track $index) {
        <p><b>{{ s.name }}（{{ s.duration }}分钟）</b></p>
        <p>{{ s.content }}</p>
      } @empty { <p style="color:#999">暂无</p> }
      <nz-divider nzText="教学评价"></nz-divider>
      @for (a of (v.assessment || []); track $index) { <p>• {{ a }}</p> } @empty { <p style="color:#999">暂无</p> }
      <nz-divider nzText="板书设计"></nz-divider>
      @for (b of (v.boardDesign || []); track $index) { <p>• {{ b }}</p> } @empty { <p style="color:#999">暂无</p> }
      @if ((v.slidesOutline || []).length) {
        <nz-divider nzText="配套资源"></nz-divider>
        <p>课件大纲：{{ (v.slidesOutline || []).join(' / ') }}</p>
        <p>评估工具：{{ (v.assessmentTools || []).join(' / ') }}</p>
      }
      @if (v.standardBasis) { <p style="color:#888">标准依据：{{ v.standardBasis }}</p> }
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
  <nz-modal [(nzVisible)]="editVisible" [nzTitle]="'编辑内容（当前 v' + (edit.versionNumber ?? 1) + '，保存后自动 +1）'" [nzWidth]="720" (nzOnCancel)="editVisible = false" [nzFooter]="null">
    <ng-container *nzModalContent>
    @if (editLoading()) {
      <div style="text-align:center;padding:48px;"><nz-spin nzSimple></nz-spin></div>
    } @else {
    <div class="dh-modal">
    <div class="dh-form">
    <label class="field">
      <span class="label">标题</span>
      <input nz-input [(ngModel)]="edit.title" />
      <app-content-version-field [versions]="fieldVersions('title')" (adopt)="adoptField('title', $event)"></app-content-version-field>
    </label>
    <div class="field">
      <span class="label">学科</span>
      <input nz-input [(ngModel)]="edit.subject" />
      <app-content-version-field [versions]="fieldVersions('subject')" (adopt)="adoptField('subject', $event)"></app-content-version-field>
    </div>
    <div class="field">
      <span class="label">学段</span>
      <input nz-input [(ngModel)]="edit.grade" />
      <app-content-version-field [versions]="fieldVersions('grade')" (adopt)="adoptField('grade', $event)"></app-content-version-field>
    </div>
    <div class="field">
      <span class="label">课时（分钟）</span>
      <nz-input-number [(ngModel)]="edit.duration" [nzMin]="20" [nzMax]="120" style="width:100%"></nz-input-number>
      <app-content-version-field [versions]="fieldVersions('duration')" (adopt)="adoptField('duration', $event)"></app-content-version-field>
    </div>
    <label class="field full">
      <span class="label">教学目标（每行一条）</span>
      <textarea nz-input rows="3" [(ngModel)]="edit.objectives"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('objectives')" (adopt)="adoptField('objectives', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">教学重点（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.keyPoints"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('keyPoints')" (adopt)="adoptField('keyPoints', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">教学难点（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.difficulties"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('difficulties')" (adopt)="adoptField('difficulties', $event)"></app-content-version-field>
    </label>
    <div class="field full">
      <span class="label">教学过程（分环节）</span>
      @for (s of edit.sections; track $index; let i = $index) {
        <div style="border:1px solid #f0f0f0;border-radius:8px;padding:8px;margin-bottom:8px;">
          <div style="display:flex;gap:8px;margin-bottom:6px;">
            <input nz-input [(ngModel)]="s.name" placeholder="环节名称" style="flex:1" />
            <nz-input-number [(ngModel)]="s.duration" [nzMin]="0" nzPlaceHolder="分钟" style="width:110px"></nz-input-number>
            <button nz-button nzType="default" nzDanger nzSize="small" (click)="removeSection(i)">删除</button>
          </div>
          <textarea nz-input rows="2" [(ngModel)]="s.content" placeholder="环节内容"></textarea>
        </div>
      }
      <div><button nz-button nzType="dashed" nzSize="small" (click)="addSection()">+ 添加环节</button></div>
      <app-content-version-field kind="sections" [versions]="fieldVersions('sections')" (adopt)="adoptField('sections', $event)"></app-content-version-field>
    </div>
    <label class="field full">
      <span class="label">教学评价（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.assessment"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('assessment')" (adopt)="adoptField('assessment', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">板书设计（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.boardDesign"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('boardDesign')" (adopt)="adoptField('boardDesign', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">教学方法（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.methods"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('methods')" (adopt)="adoptField('methods', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">配套资源（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.resources"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('resources')" (adopt)="adoptField('resources', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">课件大纲（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.slidesOutline"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('slidesOutline')" (adopt)="adoptField('slidesOutline', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">评估工具（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.assessmentTools"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('assessmentTools')" (adopt)="adoptField('assessmentTools', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">课后作业（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.homework"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('homework')" (adopt)="adoptField('homework', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">课堂活动（每行一条）</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.activities"></textarea>
      <app-content-version-field kind="list" [versions]="fieldVersions('activities')" (adopt)="adoptField('activities', $event)"></app-content-version-field>
    </label>
    <label class="field full">
      <span class="label">标准依据</span>
      <textarea nz-input rows="2" [(ngModel)]="edit.standardBasis"></textarea>
      <app-content-version-field [versions]="fieldVersions('standardBasis')" (adopt)="adoptField('standardBasis', $event)"></app-content-version-field>
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

  <!-- 提交审核：指派教师 -->
  <nz-modal [(nzVisible)]="submitVisible" nzTitle="提交审核 — 指派审核教师" [nzWidth]="520" (nzOnCancel)="submitVisible = false" (nzOnOk)="confirmSubmit()">
    <ng-container *nzModalContent>
    <div class="dh-form">
    <label class="field full"><span class="label">方案</span><span>{{ submitTarget?.title }}</span></label>
    <div class="field full">
    <span class="label">审核教师</span>
    <nz-select [(ngModel)]="submitReviewerId" nzAllowClear nzPlaceHolder="选择教师，可不选" style="width:100%">
      @for (t of teachers(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.name + ' (' + t.userName + ' · ' + t.roleName + ')'"></nz-option> }
    </nz-select>
    </div>
    </div>
    </ng-container>
  </nz-modal>

  <!-- 审核 -->
  <nz-modal [(nzVisible)]="reviewVisible" nzTitle="审核方案" [nzWidth]="520" (nzOnCancel)="reviewVisible = false" (nzOnOk)="confirmReview()">
    <ng-container *nzModalContent>
    <div class="dh-form">
    <label class="field full"><span class="label">方案</span><span>{{ reviewTarget?.title }}</span></label>
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
  activeTab = 0;
  input: any = { category: 0, topic: '', objectives: '', studentTraits: '', conditions: '', subject: '生活语文', grade: '', duration: 40, customPrompt: '' };
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
    this.loadTeachers();
  }

  statusName(s: number): string {
    return ['草稿', '待审核', '已审核', '已发布', '已归档'][s] ?? String(s);
  }

  loadAll(): void {
    this.listPageIndex.set(1);
    this.pendingPageIndex.set(1);
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
    this.viewDetail.set(null);
    this.viewVisible = true;
    this.viewLoading.set(true);
    this.http.get<any>(`/api/learning/special-edu/teaching-designs/${h.id}`).subscribe({
      next: r => { this.viewDetail.set(r); this.viewLoading.set(false); },
      error: () => { this.viewDetail.set(h); this.viewLoading.set(false); },
    });
  }

  exportViewDocx(): void {
    const v = this.viewDetail();
    if (!v?.rawJson) { this.msg.warning('该方案暂无可导出内容'); return; }
    this.svc.downloadBlob('/api/learning/special-edu/export-teaching-design-docx', { resultJson: v.rawJson }, `${v.title || '特教教案'}.docx`)
      .catch(() => this.msg.error('导出失败'));
  }

  // ── 结构化编辑 + 版本 ──
  emptyEdit(): any {
    return {
      id: '', versionNumber: 1, title: '', subject: '', grade: '', duration: 40,
      objectives: '', keyPoints: '', difficulties: '', sections: [],
      methods: '', resources: '', assessment: '', homework: '',
      boardDesign: '', slidesOutline: '', activities: '', assessmentTools: '', standardBasis: '',
    };
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
    this.http.get<any>(`/api/learning/special-edu/teaching-designs/${h.id}`).subscribe({
      next: d => { this.fillEdit(d); this.editLoading.set(false); },
      error: () => { this.fillEdit(h); this.editLoading.set(false); },
    });
    this.http.get<any[]>(`/api/learning/special-edu/teaching-designs/${h.id}/versions`).subscribe({
      next: v => this.versions.set(v ?? []),
      error: () => this.versions.set([]),
    });
  }

  fillEdit(d: any): void {
    this.edit = {
      id: d.id, versionNumber: d.versionNumber ?? 1,
      title: d.title ?? '', subject: d.subject ?? '', grade: d.grade ?? '', duration: d.duration ?? 40,
      objectives: this.joinLines(d.objectives), keyPoints: this.joinLines(d.keyPoints),
      difficulties: this.joinLines(d.difficulties),
      sections: Array.isArray(d.sections) ? JSON.parse(JSON.stringify(d.sections)) : [],
      methods: this.joinLines(d.methods), resources: this.joinLines(d.resources),
      assessment: this.joinLines(d.assessment), homework: this.joinLines(d.homework),
      boardDesign: this.joinLines(d.boardDesign), slidesOutline: this.joinLines(d.slidesOutline),
      activities: this.joinLines(d.activities), assessmentTools: this.joinLines(d.assessmentTools),
      standardBasis: d.standardBasis ?? '',
    };
  }

  fieldVersions(key: string): any[] {
    return this.parsedVersions()
      .filter(v => v.versionNumber < (this.edit.versionNumber ?? 999))
      .map(v => ({ versionNumber: v.versionNumber, creationTime: v.creationTime, creatorName: v.creatorName, value: v.snap?.[key] }));
  }

  adoptField(key: string, value: any): void {
    if (key === 'sections') {
      this.edit.sections = Array.isArray(value) ? JSON.parse(JSON.stringify(value)) : [];
    } else if (key === 'duration') {
      this.edit.duration = Number(value) || 40;
    } else if (Array.isArray(value)) {
      this.edit[key] = value.join('\n');
    } else {
      this.edit[key] = value ?? '';
    }
  }

  addSection(): void {
    this.edit.sections = [...(this.edit.sections || []), { name: '', duration: 10, content: '', activities: [] }];
  }

  removeSection(i: number): void {
    this.edit.sections = (this.edit.sections || []).filter((_: any, idx: number) => idx !== i);
  }

  saveEdit(): void {
    if (!this.edit.id) return;
    const sections = (this.edit.sections || []).map((s: any) => ({
      name: s.name ?? '', duration: Number(s.duration) || 0, content: s.content ?? '', activities: s.activities ?? [],
    }));
    const resultJson = JSON.stringify({
      title: this.edit.title, subject: this.edit.subject, grade: this.edit.grade, duration: Number(this.edit.duration) || 40,
      objectives: this.splitLines(this.edit.objectives), keyPoints: this.splitLines(this.edit.keyPoints),
      difficulties: this.splitLines(this.edit.difficulties), sections,
      methods: this.splitLines(this.edit.methods), resources: this.splitLines(this.edit.resources),
      assessment: this.splitLines(this.edit.assessment), homework: this.splitLines(this.edit.homework),
      boardDesign: this.splitLines(this.edit.boardDesign), slidesOutline: this.splitLines(this.edit.slidesOutline),
      activities: this.splitLines(this.edit.activities), assessmentTools: this.splitLines(this.edit.assessmentTools),
      standardBasis: this.edit.standardBasis,
    });
    this.editSaving.set(true);
    this.http.post<any>(`/api/learning/special-edu/teaching-designs/${this.edit.id}/content`, {
      title: this.edit.title, subject: this.edit.subject, grade: this.edit.grade, duration: Number(this.edit.duration) || 40,
      objectives: this.splitLines(this.edit.objectives), keyPoints: this.splitLines(this.edit.keyPoints),
      difficulties: this.splitLines(this.edit.difficulties), sections,
      methods: this.splitLines(this.edit.methods), resources: this.splitLines(this.edit.resources),
      assessment: this.splitLines(this.edit.assessment), homework: this.splitLines(this.edit.homework),
      boardDesign: this.splitLines(this.edit.boardDesign), slidesOutline: this.splitLines(this.edit.slidesOutline),
      activities: this.splitLines(this.edit.activities), assessmentTools: this.splitLines(this.edit.assessmentTools),
      standardBasis: this.edit.standardBasis, resultJson,
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
