import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { PermissionService } from '@abp/ng.core';
import { StudioService, StudioListItem } from './studio.service';

/** 名师工作室列表（平台/院校管理员可新建；领衔人进去审核资料）。 */
@Component({
  selector: 'app-studio-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzModalModule, NzInputModule, NzEmptyModule, NzSpinModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="st-page">
      <header class="st-head">
        <div>
          <h1>名师工作室</h1>
          <p class="st-sub">
            工作室成员上传文档 / 图片 / 视频资料，<b>领衔人审核通过后在工作室内发布</b>，师生可查看已发布资料。
          </p>
        </div>
        @if (canManage()) {
          <button nz-button nzType="primary" (click)="openCreate()">
            <span nz-icon nzType="plus"></span> 新建工作室
          </button>
        }
      </header>

      @if (loading()) {
        <div class="st-loading"><nz-spin nzSimple></nz-spin></div>
      } @else if (list().length === 0) {
        <div class="st-empty">
          <nz-empty nzNotFoundContent="还没有工作室" [nzNotFoundFooter]="canManage() ? '' : ''"></nz-empty>
        </div>
      } @else {
        <div class="st-grid">
          @for (s of list(); track s.id) {
            <article class="st-card" (click)="open(s)">
              <div class="st-card__top">
                @if (s.isLeader) { <span class="st-badge is-leader">我是领衔人</span> }
                @else if (s.isMember) { <span class="st-badge">成员</span> }
                @else { <span class="st-badge is-plain">访客</span> }
                <span class="st-status" [class.is-off]="s.status === 2">{{ s.statusName }}</span>
              </div>
              <h3>{{ s.name }}</h3>
              @if (s.description) { <p class="st-desc">{{ s.description }}</p> }
              <div class="st-card__meta">
                <span><span nz-icon nzType="crown"></span> {{ s.leaderName || '未指定领衔人' }}</span>
              </div>
              <div class="st-card__foot">
                <span><span nz-icon nzType="team"></span> {{ s.memberCount }} 人</span>
                <span><span nz-icon nzType="check-circle"></span> {{ s.publishedCount }} 已发布</span>
                @if (s.pendingCount > 0) {
                  <span class="is-warn"><span nz-icon nzType="clock-circle"></span> {{ s.pendingCount }} 待审</span>
                }
              </div>
            </article>
          }
        </div>
      }
    </div>

    <nz-modal
      [(nzVisible)]="createOpen"
      nzTitle="新建名师工作室"
      nzOkText="创建"
      nzCancelText="取消"
      [nzOkLoading]="creating()"
      (nzOnOk)="submitCreate()"
      (nzOnCancel)="createOpen = false"
    >
      <ng-container *nzModalContent>
        <div class="st-form">
          <label><span>工作室名称 <em>*</em></span><input nz-input [(ngModel)]="form.name" placeholder="如：语文名师工作室" /></label>
          <label><span>简介</span><textarea nz-input rows="3" [(ngModel)]="form.description" placeholder="工作室定位与教研方向"></textarea></label>
          <p class="st-hint">创建后可在工作室详情里指定领衔人并添加成员。</p>
        </div>
      </ng-container>
    </nz-modal>
  `,
  styles: [`
    .st-page { padding: 22px 30px 40px; max-width: 1400px; margin: 0 auto; }
    .st-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 20px; margin-bottom: 18px; }
    .st-head h1 { margin: 0; font-size: 21px; font-weight: 650; }
    .st-sub { margin: 8px 0 0; max-width: 760px; font-size: 13px; line-height: 1.7; color: #64757a; }
    .st-loading, .st-empty { padding: 60px 0; display: grid; place-items: center; }
    .st-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 16px; }
    .st-card { padding: 16px 18px; background: #fff; border: 1px solid #e0e8e5; border-radius: 12px; cursor: pointer;
               transition: border-color .16s, box-shadow .16s, transform .16s; display: flex; flex-direction: column; gap: 8px; }
    .st-card:hover { border-color: #087f7a; box-shadow: 0 6px 18px rgba(16,32,48,.08); transform: translateY(-1px); }
    .st-card__top { display: flex; align-items: center; justify-content: space-between; }
    .st-card h3 { margin: 0; font-size: 15px; font-weight: 620; }
    .st-desc { margin: 0; font-size: 12px; color: #64757a; line-height: 1.6;
               display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
    .st-badge { padding: 1px 8px; border-radius: 10px; font-size: 11px; background: #eef2f1; color: #64757a; }
    .st-badge.is-leader { background: rgba(214,162,30,.16); color: #8a6a06; }
    .st-badge.is-plain { background: #f5f8f7; color: #96a5aa; }
    .st-status { font-size: 11px; color: #18a058; }
    .st-status.is-off { color: #96a5aa; }
    .st-card__meta { font-size: 12px; color: #64757a; }
    .st-card__foot { display: flex; gap: 14px; font-size: 12px; color: #96a5aa; }
    .st-card__foot .is-warn { color: #d6a21e; }
    .st-form { display: flex; flex-direction: column; gap: 14px; }
    .st-form label { display: flex; flex-direction: column; gap: 6px; }
    .st-form span { font-size: 12px; color: #64757a; }
    .st-form em { color: #e03a3a; font-style: normal; }
    .st-hint { margin: 0; font-size: 12px; color: #96a5aa; }
  `],
})
export class StudioListComponent implements OnInit {
  private readonly api = inject(StudioService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly permission = inject(PermissionService);

  readonly list = signal<StudioListItem[]>([]);
  readonly loading = signal(true);
  readonly creating = signal(false);
  readonly canManage = signal(false);

  createOpen = false;
  form = { name: '', description: '' };

  ngOnInit(): void {
    this.canManage.set(this.permission.getGrantedPolicy('KnowledgeHub.Studios.Manage'));
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getList().subscribe({
      next: (l) => { this.list.set(l || []); this.loading.set(false); },
      error: () => { this.loading.set(false); this.message.error('加载工作室失败'); },
    });
  }

  openCreate(): void {
    this.form = { name: '', description: '' };
    this.createOpen = true;
  }

  submitCreate(): void {
    if (!this.form.name.trim()) { this.message.warning('请填写工作室名称'); return; }
    this.creating.set(true);
    this.api.create({ name: this.form.name.trim(), description: this.form.description.trim() || undefined }).subscribe({
      next: (d) => {
        this.creating.set(false);
        this.createOpen = false;
        void this.router.navigate(['/learning/studios', d.id]);
      },
      error: (e) => { this.creating.set(false); this.message.error(e?.error?.error?.message || '创建失败'); },
    });
  }

  open(s: StudioListItem): void {
    void this.router.navigate(['/learning/studios', s.id]);
  }
}
