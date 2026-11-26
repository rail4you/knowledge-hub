import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { IdeologyElementDto, IdeologyService } from './ideology.service';

/**
 * 思政元素体系配置（独立页面 · 列表 + 弹窗表单）。
 *
 * <p>元素是案例要体现的「育人要点」（家国情怀 / 法治意识 / 工匠精神…），
 * 案例通过多对多挂到元素上，图谱与覆盖分析都按这套元素统计。</p>
 *
 * <p>交互：列表占满宽度，左上搜索；「新增元素」和行内「编辑」都打开同一个**弹窗表单**
 * （名称必填、分类 / 说明 / 排序可填、可停用），保存后刷新列表。</p>
 */
@Component({
  selector: 'app-ideology-elements',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzTableModule,
    NzButtonModule,
    NzModalModule,
    NzInputModule,
    NzInputNumberModule,
    NzSwitchModule,
    NzTagModule,
    NzIconModule,
    NzSpaceModule,
    NzPopconfirmModule,
    NzTooltipModule,
    NzEmptyModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="ideology-page">
      <nz-card class="page-header-card" [nzBordered]="false">
        <div class="page-title-block">
          <h1>思政元素体系</h1>
          <p>
            元素是案例要体现的「育人要点」。一条案例可以挂多个元素，图谱与覆盖分析都按这套元素统计。
            建议对照本校《课程思政建设指导纲要》维护；停用的元素不再出现在案例选择器里，历史关联保留。
          </p>
        </div>
      </nz-card>

      <nz-card [nzBordered]="false">
        <div class="ideology-toolbar">
          <div class="ideology-toolbar-left">
            <nz-input-group nzPrefixIcon="search" class="search-input" style="width: 260px">
              <input
                nz-input
                placeholder="搜索元素名 / 分类"
                [ngModel]="keyword()"
                (ngModelChange)="keyword.set($event)"
              />
            </nz-input-group>
            <nz-switch
              [ngModel]="hideDisabled()"
              (ngModelChange)="hideDisabled.set($event)"
              nzCheckedChildren="只看启用"
              nzUnCheckedChildren="全部"
            ></nz-switch>
          </div>
          <div class="ideology-toolbar-right">
            <span>共 <b>{{ rows().length }}</b> 个元素</span>
            <button nz-button nzType="primary" (click)="openCreate()">
              <span nz-icon nzType="plus"></span>
              新增元素
            </button>
          </div>
        </div>

        <nz-table
          [nzData]="rows()"
          nzSize="middle"
          [nzShowPagination]="false"
          [nzLoading]="loading()"
          nzTableLayout="fixed"
        >
          <thead>
            <tr>
              <th nzWidth="70px" nzAlign="center">排序</th>
              <th nzWidth="20%">元素名称</th>
              <th nzWidth="14%">分类</th>
              <th nzWidth="90px" nzAlign="center">案例数</th>
              <th nzWidth="90px" nzAlign="center">启用</th>
              <th>说明</th>
              <th nzWidth="140px" nzAlign="center">操作</th>
            </tr>
          </thead>
          <tbody>
            @for (item of rows(); track item.id) {
              <tr>
                <td nzAlign="center">{{ item.sortOrder }}</td>
                <td><span class="cell-strong">{{ item.name }}</span></td>
                <td>{{ item.category || '-' }}</td>
                <td nzAlign="center">
                  @if ((item.caseCount ?? 0) > 0) {
                    <nz-tag nzColor="blue">{{ item.caseCount }}</nz-tag>
                  } @else {
                    <span class="cell-muted">0</span>
                  }
                </td>
                <td nzAlign="center">
                  <nz-switch
                    [ngModel]="item.isEnabled"
                    (ngModelChange)="toggleEnabled(item, $event)"
                    nzSize="small"
                  ></nz-switch>
                </td>
                <td><span class="cell-ellipsis" [title]="item.description || ''">{{ item.description || '-' }}</span></td>
                <td nzAlign="center">
                  <nz-space>
                    <button nz-button nzType="link" *nzSpaceItem (click)="openEdit(item)">编辑</button>
                    <button
                      nz-button
                      nzType="link"
                      nzDanger
                      *nzSpaceItem
                      nz-popconfirm
                      nzPopconfirmTitle="删除后相关案例的该元素标注会被移除，确定？"
                      nzOkText="删除"
                      nzCancelText="取消"
                      (nzOnConfirm)="remove(item)"
                    >
                      删除
                    </button>
                  </nz-space>
                </td>
              </tr>
            }
          </tbody>
        </nz-table>

        @if (!loading() && !rows().length) {
          <nz-empty
            nzNotFoundImage="simple"
            [nzNotFoundContent]="keyword() ? '没有匹配的元素' : '还没有思政元素，点右上「新增元素」开始维护'"
          ></nz-empty>
        }
      </nz-card>
    </div>

    <!-- ═══════════ 新增 / 编辑（弹窗表单） ═══════════ -->
    <nz-modal
      [nzVisible]="formVisible()"
      [nzTitle]="editingId() ? '编辑思政元素' : '新增思政元素'"
      [nzWidth]="560"
      nzOkText="保存"
      [nzOkLoading]="saving()"
      nzCancelText="取消"
      (nzOnCancel)="closeForm()"
      (nzOnOk)="save()"
    >
      <ng-container *nzModalContent>
        <div class="form-stack">
          <div class="form-item">
            <label>元素名称 <span style="color: #a4262c">*</span></label>
            <input
              nz-input
              [ngModel]="form().name"
              (ngModelChange)="patch({ name: $event })"
              placeholder="如：家国情怀"
              [class.is-invalid]="nameInvalid() && attemptedSave()"
            />
            @if (nameInvalid() && attemptedSave()) {
              <div class="hint" style="color: #a4262c">请填写元素名称（不超过 64 个字）</div>
            } @else {
              <div class="hint">同一租户内不能重名；建议用《纲要》里的标准表述。</div>
            }
          </div>

          <div class="form-grid">
            <div class="form-item">
              <label>分类</label>
              <input
                nz-input
                [ngModel]="form().category"
                (ngModelChange)="patch({ category: $event })"
                placeholder="如：理想信念 / 价值取向 / 职业素养"
              />
            </div>
            <div class="form-item">
              <label>排序（小的在前）</label>
              <nz-input-number
                style="width: 100%"
                [ngModel]="form().sortOrder"
                (ngModelChange)="patch({ sortOrder: $event })"
                [nzMin]="0"
              ></nz-input-number>
            </div>
          </div>

          <div class="form-item">
            <label>说明（给老师看的解释，可空）</label>
            <textarea
              nz-input
              rows="3"
              [ngModel]="form().description"
              (ngModelChange)="patch({ description: $event })"
              placeholder="这个元素关注什么、适合放在哪些章节"
            ></textarea>
          </div>

          <div class="form-item">
            <label>
              <nz-switch [ngModel]="form().isEnabled" (ngModelChange)="patch({ isEnabled: $event })"></nz-switch>
              <span style="margin-left: 8px">启用</span>
            </label>
            <div class="hint">停用后不再出现在案例的「思政元素」选择器里，已有案例的标注保留。</div>
          </div>
        </div>
      </ng-container>
    </nz-modal>
  `,
  styleUrls: ['./ideology-pages.scss'],
})
export class IdeologyElementsComponent implements OnInit {
  private readonly service = inject(IdeologyService);
  private readonly message = inject(NzMessageService);

  readonly elements = signal<IdeologyElementDto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);

  readonly keyword = signal('');
  readonly hideDisabled = signal(false);
  readonly formVisible = signal(false);
  /** 是否点过「保存」：没点之前不报红，避免一打开弹窗就飘错误提示。 */
  readonly attemptedSave = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly form = signal({
    name: '',
    category: '',
    description: '',
    sortOrder: 0,
    isEnabled: true,
  });

  /** 前端只做「非空 + 长度」校验，重名交给后端（唯一索引 + 友好提示）。 */
  readonly nameInvalid = computed(() => {
    const name = this.form().name.trim();
    return name.length === 0 || name.length > 64;
  });

  /** 列表：按关键字 / 启用状态过滤。 */
  readonly rows = computed(() => {
    const keyword = this.keyword().trim().toLowerCase();
    return this.elements().filter(item => {
      if (this.hideDisabled() && !item.isEnabled) return false;
      if (!keyword) return true;
      return (
        item.name.toLowerCase().includes(keyword) ||
        (item.category ?? '').toLowerCase().includes(keyword) ||
        (item.description ?? '').toLowerCase().includes(keyword)
      );
    });
  });

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.service.getElements(false).subscribe({
      next: list => {
        this.elements.set(list || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('读取思政元素失败');
      },
    });
  }

  openCreate(): void {
    this.editingId.set(null);
    this.attemptedSave.set(false);
    this.form.set({
      name: '',
      category: '',
      description: '',
      sortOrder: this.elements().length + 1,
      isEnabled: true,
    });
    this.formVisible.set(true);
  }

  openEdit(item: IdeologyElementDto): void {
    this.editingId.set(item.id);
    this.attemptedSave.set(false);
    this.form.set({
      name: item.name,
      category: item.category ?? '',
      description: item.description ?? '',
      sortOrder: item.sortOrder,
      isEnabled: item.isEnabled,
    });
    this.formVisible.set(true);
  }

  closeForm(): void {
    if (this.saving()) return;
    this.formVisible.set(false);
  }

  patch(patch: Record<string, unknown>): void {
    this.form.set({ ...this.form(), ...patch } as any);
  }

  save(): void {
    this.attemptedSave.set(true);
    if (this.nameInvalid()) {
      this.message.warning('请填写元素名称（不超过 64 个字）');
      return;
    }

    const f = this.form();
    const body = {
      name: f.name.trim(),
      category: f.category?.trim() || null,
      description: f.description?.trim() || null,
      sortOrder: f.sortOrder ?? 0,
      isEnabled: f.isEnabled,
    };

    this.saving.set(true);
    const id = this.editingId();
    const request = id ? this.service.updateElement(id, body as any) : this.service.createElement(body as any);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.formVisible.set(false);
        this.message.success(id ? '已保存' : '已新增');
        this.reload();
      },
      error: (err: any) => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '保存失败');
      },
    });
  }

  /** 行内开关：直接启用 / 停用，不改其他字段。 */
  toggleEnabled(item: IdeologyElementDto, isEnabled: boolean): void {
    this.service
      .updateElement(item.id, {
        name: item.name,
        category: item.category ?? null,
        description: item.description ?? null,
        sortOrder: item.sortOrder,
        isEnabled,
      } as any)
      .subscribe({
        next: () => {
          this.message.success(isEnabled ? '已启用' : '已停用');
          this.reload();
        },
        error: () => this.message.error('操作失败'),
      });
  }

  remove(item: IdeologyElementDto): void {
    this.service.deleteElement(item.id).subscribe({
      next: () => {
        this.message.success('已删除元素');
        this.reload();
      },
      error: () => this.message.error('删除失败'),
    });
  }
}
