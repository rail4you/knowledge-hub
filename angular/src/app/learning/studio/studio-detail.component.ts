import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzMessageService } from 'ng-zorro-antd/message';
import { StudioService, StudioDetail, StudioMember, StudioMaterial } from './studio.service';

/**
 * 工作室详情：成员管理 / 资料上传 / 领衔人审核发布。
 *
 * 资料 = 资源中心里的一份文件资源 + 工作室准入状态；预览/下载复用资源文件接口。
 */
@Component({
  selector: 'app-studio-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzModalModule, NzInputModule, NzTagModule, NzSpinModule, NzEmptyModule, NzTableModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sd-page">
      <header class="sd-head">
        <button nz-button nzType="text" (click)="back()"><span nz-icon nzType="arrow-left"></span></button>
        <div class="sd-title">
          <h1>{{ studio()?.name || '加载中…' }}</h1>
          <p>
            领衔人 {{ studio()?.leaderName || '未指定' }} · {{ studio()?.memberCount }} 位成员
            · 已发布 {{ studio()?.publishedCount }} 份
            @if ((studio()?.pendingCount ?? 0) > 0) { · <b class="is-warn">{{ studio()!.pendingCount }} 份待审</b> }
          </p>
        </div>
        <div class="sd-actions">
          @if (studio()?.isLeader) {
            <button nz-button (click)="openMembers()"><span nz-icon nzType="team"></span> 成员管理</button>
          }
          @if (studio()?.isMember) {
            <button nz-button nzType="primary" (click)="openUpload()"><span nz-icon nzType="upload"></span> 上传资料</button>
          }
        </div>
      </header>

      @if (loading()) {
        <div class="sd-loading"><nz-spin nzSimple></nz-spin></div>
      } @else if ((studio()?.materials?.length ?? 0) === 0) {
        <div class="sd-empty">
          <nz-empty nzNotFoundContent="还没有资料，成员上传后由领衔人审核发布"></nz-empty>
        </div>
      } @else {
        <div class="sd-grid">
          @for (m of studio()!.materials; track m.id) {
            <article class="sd-card">
              <div class="sd-card__top">
                <nz-tag [nzColor]="tagColor(m.status)">{{ m.statusName }}</nz-tag>
                <span class="sd-card__ext">{{ (m.fileExtension || '').toUpperCase() || '文件' }}</span>
              </div>
              <h3>{{ m.title }}</h3>
              @if (m.description) { <p class="sd-card__desc">{{ m.description }}</p> }
              <p class="sd-card__by">{{ m.submittedByName }} · {{ m.submittedAt | date: 'MM-dd HH:mm' }}</p>
              @if (m.reviewComment) {
                <p class="sd-card__comment">审核意见：{{ m.reviewComment }}</p>
              }
              <div class="sd-card__ops">
                @if (m.canPreview) {
                  <a nz-button nzSize="small" [href]="previewUrl(m)" target="_blank" rel="noopener">
                    <span nz-icon nzType="eye"></span> 预览
                  </a>
                  <a nz-button nzSize="small" [href]="downloadUrl(m)"><span nz-icon nzType="download"></span> 下载</a>
                }
                @if (m.canSubmit) {
                  <button nz-button nzSize="small" nzType="primary" (click)="submitMaterial(m)">提交审核</button>
                }
                @if (m.canReview) {
                  <button nz-button nzSize="small" nzType="primary" (click)="review(m, true)">通过并发布</button>
                  <button nz-button nzSize="small" nzDanger (click)="review(m, false)">驳回</button>
                }
              </div>
            </article>
          }
        </div>
      }
    </div>

    <!-- 成员管理 -->
    <nz-modal [(nzVisible)]="membersOpen" nzTitle="成员管理" [nzFooter]="null" (nzOnCancel)="membersOpen = false" [nzWidth]="640">
      <ng-container *nzModalContent>
        <div class="mem">
          <h4>已加入（{{ studio()?.members?.length ?? 0 }}）</h4>
          @for (m of studio()?.members ?? []; track m.userId) {
            <div class="mem__row">
              <span class="mem__avatar">{{ initial(m.userName) }}</span>
              <span>{{ m.userName }}</span>
              <nz-tag [nzColor]="m.role === 0 ? 'gold' : 'default'">{{ m.roleName }}</nz-tag>
              @if (m.userId !== studio()?.leaderUserId) {
                <button nz-button nzSize="small" nzType="text" nzDanger (click)="removeMember(m)">移除</button>
              }
            </div>
          }
          <h4 style="margin-top:16px">可邀请的同校教师</h4>
          @for (c of candidates(); track c.userId) {
            @if (c.role === -1) {
              <div class="mem__row">
                <span class="mem__avatar">{{ initial(c.userName) }}</span>
                <span>{{ c.userName }}</span>
                <button nz-button nzSize="small" style="margin-left:auto" (click)="addMember(c)">加入</button>
              </div>
            }
          }
        </div>
      </ng-container>
    </nz-modal>

    <!-- 上传资料：从我的课程资源里选 -->
    <nz-modal
      [(nzVisible)]="uploadOpen"
      nzTitle="上传资料到工作室"
      nzOkText="提交"
      nzCancelText="取消"
      [nzWidth]="760"
      [nzOkDisabled]="!pickedResource"
      [nzOkLoading]="uploading()"
      (nzOnOk)="doUpload()"
      (nzOnCancel)="uploadOpen = false"
    >
      <ng-container *nzModalContent>
        <div class="up">
          <p class="up__tip">
            从你自己的课程资源里选一份文件（文档 / 图片 / 视频）。若需要新文件，请先到「资源中心」上传。
          </p>
          <input nz-input placeholder="搜索资源名称" [(ngModel)]="keyword" (keyup.enter)="loadResources()" style="margin-bottom:10px" />
          @if (resLoading()) {
            <div class="sd-loading"><nz-spin nzSimple></nz-spin></div>
          } @else if (resources().length === 0) {
            <nz-empty nzNotFoundContent="没有可选资源"></nz-empty>
          } @else {
            <div class="up__grid">
              @for (r of resources(); track r.id) {
                <div class="up__item" [class.is-picked]="pickedResource?.id === r.id" (click)="pickedResource = r">
                  <b [title]="r.name">{{ r.name }}</b>
                  <span>{{ (r.fileExtension || '').toUpperCase() }}</span>
                </div>
              }
            </div>
          }
          <label class="up__field">
            <span>资料标题</span>
            <input nz-input [(ngModel)]="materialTitle" [placeholder]="pickedResource?.name || '留空则用资源名'" />
          </label>
          <label class="up__field">
            <span>说明</span>
            <textarea nz-input rows="2" [(ngModel)]="materialDesc" placeholder="这份资料的用途"></textarea>
          </label>
        </div>
      </ng-container>
    </nz-modal>
  `,
  styles: [`
    .sd-page { padding: 20px 30px 40px; max-width: 1400px; margin: 0 auto; }
    .sd-head { display: flex; align-items: center; gap: 10px; margin-bottom: 18px; }
    .sd-title h1 { margin: 0; font-size: 20px; font-weight: 650; }
    .sd-title p { margin: 4px 0 0; font-size: 12px; color: #64757a; }
    .sd-title .is-warn { color: #d6a21e; }
    .sd-actions { margin-left: auto; display: flex; gap: 8px; }
    .sd-loading, .sd-empty { padding: 60px 0; display: grid; place-items: center; }
    .sd-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 14px; }
    .sd-card { padding: 14px 16px; background: #fff; border: 1px solid #e0e8e5; border-radius: 12px; display: flex; flex-direction: column; gap: 6px; }
    .sd-card__top { display: flex; align-items: center; justify-content: space-between; }
    .sd-card__ext { font-size: 11px; color: #96a5aa; }
    .sd-card h3 { margin: 0; font-size: 14px; font-weight: 620; }
    .sd-card__desc { margin: 0; font-size: 12px; color: #64757a; }
    .sd-card__by { margin: 0; font-size: 11px; color: #96a5aa; }
    .sd-card__comment { margin: 0; font-size: 11px; color: #8a6a06; background: rgba(214,162,30,.1); padding: 4px 8px; border-radius: 6px; }
    .sd-card__ops { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 4px; }
    .mem h4 { margin: 0 0 8px; font-size: 13px; color: #203b40; }
    .mem__row { display: flex; align-items: center; gap: 10px; padding: 6px 4px; border-bottom: 1px dashed #f0f4f3; font-size: 13px; }
    .mem__avatar { width: 24px; height: 24px; border-radius: 50%; background: #087f7a; color: #fff; display: grid; place-items: center; font-size: 11px; }
    .up__tip { margin: 0 0 10px; font-size: 12px; color: #64757a; line-height: 1.7; }
    .up__grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 8px; max-height: 240px; overflow: auto; margin-bottom: 12px; }
    .up__item { padding: 8px 10px; border: 1px solid #e0e8e5; border-radius: 8px; cursor: pointer; display: flex; flex-direction: column; gap: 2px; }
    .up__item.is-picked { border-color: #087f7a; box-shadow: 0 0 0 2px rgba(8,127,122,.14); }
    .up__item b { font-size: 12px; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .up__item span { font-size: 10px; color: #96a5aa; }
    .up__field { display: flex; flex-direction: column; gap: 6px; margin-top: 10px; }
    .up__field span { font-size: 12px; color: #64757a; }
  `],
})
export class StudioDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(StudioService);
  private readonly message = inject(NzMessageService);

  readonly studio = signal<StudioDetail | null>(null);
  readonly loading = signal(true);
  readonly candidates = signal<StudioMember[]>([]);
  readonly resources = signal<any[]>([]);
  readonly resLoading = signal(false);
  readonly uploading = signal(false);

  membersOpen = false;
  uploadOpen = false;
  pickedResource: any = null;
  keyword = '';
  materialTitle = '';
  materialDesc = '';

  private studioId = '';

  ngOnInit(): void {
    this.studioId = this.route.snapshot.paramMap.get('id') || '';
    if (!this.studioId) { void this.router.navigate(['/learning/studios']); return; }
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.get(this.studioId).subscribe({
      next: (d) => { this.studio.set(d); this.loading.set(false); },
      error: () => { this.loading.set(false); this.message.error('加载工作室失败'); },
    });
  }

  back(): void { void this.router.navigate(['/learning/studios']); }

  initial(n: string): string { return (n || '师').trim().slice(0, 1); }

  tagColor(status: number): string {
    return status === 2 ? 'green' : status === 1 ? 'gold' : status === 3 ? 'red' : 'default';
  }

  previewUrl(m: StudioMaterial): string { return this.api.resourceUrl(m.resourceId); }
  downloadUrl(m: StudioMaterial): string { return this.api.downloadUrl(m.resourceId); }

  submitMaterial(m: StudioMaterial): void {
    this.api.submitMaterial(this.studioId, m.id).subscribe({
      next: () => { this.message.success('已提交审核'); this.load(); },
      error: (e) => this.message.error(e?.error?.error?.message || '提交失败'),
    });
  }

  review(m: StudioMaterial, approved: boolean): void {
    let comment = '';
    if (!approved) {
      const input = window.prompt('驳回原因（必填）');
      if (!input) return;
      comment = input;
    } else {
      comment = '内容完整，同意发布';
    }
    this.api.reviewMaterial(this.studioId, m.id, approved, comment).subscribe({
      next: (r) => { this.message.success(approved ? '已发布' : '已驳回'); this.load(); },
      error: (e) => this.message.error(e?.error?.error?.message || '审核失败'),
    });
  }

  openMembers(): void {
    this.membersOpen = true;
    this.api.getCandidates(this.studioId).subscribe({
      next: (l) => this.candidates.set(l || []),
      error: () => this.candidates.set([]),
    });
  }

  addMember(c: StudioMember): void {
    this.api.addMember(this.studioId, c.userId, 1).subscribe({
      next: () => { this.message.success('已加入'); this.openMembers(); this.load(); },
      error: (e) => this.message.error(e?.error?.error?.message || '加入失败'),
    });
  }

  removeMember(m: StudioMember): void {
    this.api.removeMember(this.studioId, m.userId).subscribe({
      next: () => { this.message.success('已移除'); this.openMembers(); this.load(); },
      error: (e) => this.message.error(e?.error?.error?.message || '移除失败'),
    });
  }

  openUpload(): void {
    this.uploadOpen = true;
    this.pickedResource = null;
    this.materialTitle = '';
    this.materialDesc = '';
    this.loadResources();
  }

  loadResources(): void {
    this.resLoading.set(true);
    this.api.getMyResources(this.keyword?.trim() || undefined).subscribe({
      next: (r) => { this.resources.set(r.items || []); this.resLoading.set(false); },
      error: () => { this.resources.set([]); this.resLoading.set(false); },
    });
  }

  doUpload(): void {
    if (!this.pickedResource) return;
    this.uploading.set(true);
    this.api.createMaterial(this.studioId, {
      resourceId: this.pickedResource.id,
      title: this.materialTitle?.trim() || this.pickedResource.name,
      description: this.materialDesc?.trim() || undefined,
      submitNow: true,
    }).subscribe({
      next: (m) => {
        this.uploading.set(false);
        this.uploadOpen = false;
        this.message.success(m.status === 2 ? '已发布（领衔人本人上传自动发布）' : '已提交，等待领衔人审核');
        this.load();
      },
      error: (e) => { this.uploading.set(false); this.message.error(e?.error?.error?.message || '上传失败'); },
    });
  }
}
