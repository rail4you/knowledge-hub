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
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { WhiteboardService, WhiteboardListItem } from './whiteboard.service';

/**
 * 智能白板列表：我创建 + 我被邀请的白板。
 *
 * 新建只填「主题 / 学科年级 / 说明」，可顺带关联一门课程
 * （关联后右侧 AI 助手能拿到课程名、课程简介与章节目录，产出更贴合这门课）；
 * 创建后直接进入画布房间。
 */
@Component({
  selector: 'app-whiteboard-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule,
    NzButtonModule, NzIconModule, NzModalModule, NzInputModule,
    NzEmptyModule, NzSpinModule, NzTooltipModule, NzPopconfirmModule, NzSelectModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wb-page">
      <header class="wb-head">
        <div>
          <h1>智能白板</h1>
          <p class="wb-sub">
            创建白板并邀请同校其他教师一起备课：多人同时打开即可实时看到彼此的编辑与光标，右侧还能拉起 AI 课程助教，
            素材可直接从自己的课程资源里选。
          </p>
        </div>
        <button nz-button nzType="primary" (click)="openCreate()">
          <span nz-icon nzType="plus"></span>
          新建白板
        </button>
      </header>

      @if (loading()) {
        <div class="wb-loading"><nz-spin nzSimple></nz-spin></div>
      } @else if (boards().length === 0) {
        <div class="wb-empty">
          <nz-empty nzNotFoundContent="还没有白板，点「新建白板」开始一次教研"></nz-empty>
        </div>
      } @else {
        <div class="wb-grid">
          @for (b of boards(); track b.id) {
            <article class="wb-card" (click)="open(b)">
              <div class="wb-card__top">
                <span class="wb-card__badge" [class.is-owner]="b.isOwner">
                  {{ b.isOwner ? '我创建' : '被邀请' }}
                </span>
                <span class="wb-card__time">{{ (b.lastModificationTime || b.creationTime) | date: 'MM-dd HH:mm' }}</span>
              </div>
              <h3 class="wb-card__title">{{ b.title }}</h3>
              @if (b.courseName) {
                <p class="wb-card__course"><span nz-icon nzType="book"></span> {{ b.courseName }}</p>
              }
              @if (b.subject || b.topic) {
                <p class="wb-card__meta">{{ b.subject }}{{ b.subject && b.topic ? ' · ' : '' }}{{ b.topic }}</p>
              }
              <div class="wb-card__foot">
                <span><span nz-icon nzType="team"></span> {{ b.memberCount }} 人</span>
                <span><span nz-icon nzType="appstore"></span> {{ b.elementCount }} 个元素</span>
                <span class="wb-card__creator">{{ b.creatorName }}</span>
                @if (b.isOwner) {
                  <button
                    class="wb-card__del"
                    nz-button
                    nzType="text"
                    nzDanger
                    nzSize="small"
                    nz-popconfirm
                    nzPopconfirmTitle="删除后不可恢复，确定吗？"
                    (nzOnConfirm)="remove(b)"
                    (click)="$event.stopPropagation()"
                  >
                    <span nz-icon nzType="delete"></span>
                  </button>
                }
              </div>
            </article>
          }
        </div>
      }
    </div>

    <!-- 新建白板 -->
    <nz-modal
      [(nzVisible)]="createOpen"
      nzTitle="创建智能白板"
      nzOkText="创建并进入"
      nzCancelText="取消"
      [nzOkLoading]="creating()"
      (nzOnOk)="submitCreate()"
      (nzOnCancel)="createOpen = false"
    >
      <ng-container *nzModalContent>
        <div class="wb-form">
          <label>
            <span>主题 <em>*</em></span>
            <input nz-input [(ngModel)]="form.title" placeholder="如：七年级语文《春》单元教学设计" />
          </label>
          <label>
            <span>关联课程</span>
            <nz-select
              nzShowSearch
              nzAllowClear
              nzPlaceHolder="选择这块智能白板对应的课程（可选）"
              [nzLoading]="coursesLoading()"
              [(ngModel)]="form.courseId"
            >
              @for (c of courses(); track c.id) {
                <nz-option [nzValue]="c.id" [nzLabel]="c.title" [nzCustomContent]="true">
                  <span class="wb-opt__title">{{ c.title }}</span>
                  @if (c.isOtherSchoolCourse) {
                    <span class="wb-opt__tag">{{ c.ownerTenantName || '共建库' }}</span>
                  } @else if (c.libraries?.length) {
                    <span class="wb-opt__tag">{{ c.libraries[0].name || '共享资源库' }}</span>
                  } @else {
                    <span class="wb-opt__tag is-mine">我的课程</span>
                  }
                </nz-option>
              }
            </nz-select>
            <small class="wb-form__hint">关联后，右侧「AI 助手」会知道课程与章节，生成的内容更贴合这门课。</small>
          </label>
          <label>
            <span>学科 / 年级</span>
            <input nz-input [(ngModel)]="form.subject" placeholder="如：语文 · 七年级" />
          </label>
          <label>
            <span>说明</span>
            <textarea nz-input rows="3" [(ngModel)]="form.description" placeholder="本次教研要讨论的内容"></textarea>
          </label>
        </div>
      </ng-container>
    </nz-modal>
  `,
  styles: [`
    .wb-page { padding: 24px 32px 48px; max-width: 1400px; margin: 0 auto; }
    .wb-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 20px; margin-bottom: 20px; }
    .wb-head h1 { margin: 0; font-size: 22px; font-weight: 650; }
    .wb-sub { margin: 8px 0 0; max-width: 720px; font-size: 13px; line-height: 1.7; color: #64757a; }
    .wb-loading { padding: 80px 0; display: flex; justify-content: center; }
    .wb-empty { padding: 60px 0; }
    .wb-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 16px; }
    .wb-card {
      position: relative; display: flex; flex-direction: column; gap: 8px;
      padding: 16px 18px; background: #fff; border: 1px solid #e0e8e5; border-radius: 12px;
      cursor: pointer; transition: border-color .16s ease, box-shadow .16s ease, transform .16s ease;
    }
    .wb-card:hover { border-color: #087f7a; box-shadow: 0 6px 18px rgba(16,32,48,.08); transform: translateY(-1px); }
    .wb-card__top { display: flex; align-items: center; justify-content: space-between; }
    .wb-card__badge { padding: 1px 8px; border-radius: 10px; font-size: 11px; background: #eef2f1; color: #64757a; }
    .wb-card__badge.is-owner { background: rgba(8,127,122,.1); color: #087f7a; }
    .wb-card__time { font-size: 11px; color: #96a5aa; }
    .wb-card__title { margin: 2px 0 0; font-size: 15px; font-weight: 600; line-height: 1.4; }
    .wb-card__meta { margin: 0; font-size: 12px; color: #64757a; }
    .wb-card__foot { display: flex; align-items: center; gap: 14px; margin-top: 4px; font-size: 12px; color: #96a5aa; }
    .wb-card__creator { margin-left: auto; max-width: 120px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    /* 删除按钮跟在页脚行内，不再绝对定位压住「创建者」名字 */
    .wb-card__del {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 26px;
      height: 26px;
      margin-left: -4px;
      padding: 0;
      border-radius: 6px;
      color: #a8b6ba;
      transition: background .16s ease, color .16s ease;
    }
    .wb-card__del:hover { color: #d4380d; background: rgba(212, 56, 13, .08); }
    .wb-form { display: flex; flex-direction: column; gap: 14px; }
    .wb-form label { display: flex; flex-direction: column; gap: 6px; }
    .wb-form span { font-size: 12px; color: #64757a; }
    .wb-form em { color: #e03a3a; font-style: normal; }
    .wb-form__hint { font-size: 11px; line-height: 1.5; color: #96a5aa; }
    .wb-opt__title { margin-right: 8px; }
    .wb-opt__tag {
      padding: 0 6px; border-radius: 8px; font-size: 10.5px;
      background: #f2f6f5; color: #7d8d93; border: 1px solid #e3eae8;
    }
    .wb-opt__tag.is-mine { background: #f1faf8; color: #087f7a; border-color: #cfe9dd; }
    .wb-card__course {
      margin: 0; display: inline-flex; align-items: center; gap: 4px;
      font-size: 12px; color: #087f7a;
    }
  `],
})
export class WhiteboardListComponent implements OnInit {
  private readonly api = inject(WhiteboardService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly boards = signal<WhiteboardListItem[]>([]);
  readonly loading = signal(true);
  readonly creating = signal(false);

  createOpen = false;
  form: { title: string; subject: string; description: string; courseId: string | null } = {
    title: '', subject: '', description: '', courseId: null,
  };
  readonly courses = signal<any[]>([]);
  readonly coursesLoading = signal(false);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getList().subscribe({
      next: (list) => { this.boards.set(list || []); this.loading.set(false); },
      error: () => { this.loading.set(false); this.message.error('加载白板列表失败'); },
    });
  }

  openCreate(): void {
    this.form = { title: '', subject: '', description: '', courseId: null };
    this.createOpen = true;
    this.loadCourses();
  }

  private loadCourses(): void {
    if (this.courses().length) return;
    this.coursesLoading.set(true);
    this.api.getCourseOptions().subscribe({
      next: (r) => { this.courses.set(r.items || []); this.coursesLoading.set(false); },
      error: () => { this.courses.set([]); this.coursesLoading.set(false); },
    });
  }

  submitCreate(): void {
    if (!this.form.title.trim()) { this.message.warning('请填写白板主题'); return; }
    this.creating.set(true);
    this.api.create({
      title: this.form.title.trim(),
      subject: this.form.subject.trim() || undefined,
      description: this.form.description.trim() || undefined,
      courseId: this.form.courseId || undefined,
    }).subscribe({
      next: (d) => {
        this.creating.set(false);
        this.createOpen = false;
        void this.router.navigate(['/learning/whiteboards', d.id]);
      },
      error: (err) => { this.creating.set(false); this.message.error(err?.error?.error?.message || '创建失败'); },
    });
  }

  open(b: WhiteboardListItem): void {
    void this.router.navigate(['/learning/whiteboards', b.id]);
  }

  remove(b: WhiteboardListItem): void {
    this.api.delete(b.id).subscribe({
      next: () => { this.message.success('已删除'); this.load(); },
      error: (err) => this.message.error(err?.error?.error?.message || '删除失败'),
    });
  }
}
