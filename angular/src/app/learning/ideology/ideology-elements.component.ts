import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LocalizationService } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { IdeologyElementDto, IdeologyService } from './ideology.service';

/**
 * 思政元素体系配置（独立页面）。
 *
 * <p>元素是"育人要点"（家国情怀 / 法治意识 / 工匠精神…），案例通过多对多挂到元素上，
 * 图谱与覆盖分析都按这套元素统计。不同学校可对照自己的《课程思政建设指导纲要》维护一套。</p>
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
            元素是案例要体现的「育人要点」。案例可以挂多个元素，图谱与覆盖分析都按这套元素统计。
            建议对照本校《课程思政建设指导纲要》维护；停用的元素不再出现在案例选择器里，历史关联保留。
          </p>
        </div>
      </nz-card>

      <div class="element-layout">
        <nz-card [nzBordered]="false" nzTitle="元素列表" class="element-list-card">
          <nz-table [nzData]="elements()" nzSize="middle" [nzShowPagination]="false" [nzLoading]="loading()">
            <thead>
              <tr>
                <th nzWidth="60px" nzAlign="center">排序</th>
                <th nzWidth="22%">元素</th>
                <th nzWidth="16%">分类</th>
                <th nzWidth="90px" nzAlign="center">案例数</th>
                <th nzWidth="90px" nzAlign="center">状态</th>
                <th>说明</th>
                <th nzWidth="140px" nzAlign="center">操作</th>
              </tr>
            </thead>
            <tbody>
              @for (item of elements(); track item.id) {
                <tr [class.is-editing]="form().id === item.id">
                  <td nzAlign="center">{{ item.sortOrder }}</td>
                  <td><span class="cell-strong">{{ item.name }}</span></td>
                  <td>{{ item.category || '-' }}</td>
                  <td nzAlign="center">{{ item.caseCount ?? 0 }}</td>
                  <td nzAlign="center">
                    <nz-tag [nzColor]="item.isEnabled ? 'green' : 'default'">
                      {{ item.isEnabled ? '启用' : '停用' }}
                    </nz-tag>
                  </td>
                  <td><span class="cell-ellipsis" [title]="item.description || ''">{{ item.description }}</span></td>
                  <td nzAlign="center">
                    <nz-space>
                      <button nz-button nzType="link" *nzSpaceItem (click)="edit(item)">编辑</button>
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

          @if (!loading() && !elements().length) {
            <nz-empty nzNotFoundImage="simple" nzNotFoundContent="还没有思政元素，先在右侧新增一个"></nz-empty>
          }
        </nz-card>

        <nz-card [nzBordered]="false" [nzTitle]="form().id ? '编辑元素' : '新增元素'" class="element-form-card">
          <div class="form-stack">
            <div class="form-item">
              <label>元素名称 <span style="color: #a4262c">*</span></label>
              <input
                nz-input
                [ngModel]="form().name"
                (ngModelChange)="patch({ name: $event })"
                placeholder="如：家国情怀"
              />
            </div>
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
              <label>说明（给老师看的解释）</label>
              <textarea
                nz-input
                rows="3"
                [ngModel]="form().description"
                (ngModelChange)="patch({ description: $event })"
                placeholder="这个元素关注什么、适合放在哪些章节"
              ></textarea>
            </div>
            <div class="form-item">
              <label>排序</label>
              <nz-input-number
                style="width: 100%"
                [ngModel]="form().sortOrder"
                (ngModelChange)="patch({ sortOrder: $event })"
                [nzMin]="0"
              ></nz-input-number>
            </div>
            <div class="form-item">
              <label>
                <nz-switch [ngModel]="form().isEnabled" (ngModelChange)="patch({ isEnabled: $event })"></nz-switch>
                <span style="margin-left: 8px">启用（停用后不出现在案例选择器里）</span>
              </label>
            </div>
            <div class="element-form-actions">
              @if (form().id) {
                <button nz-button (click)="resetForm()">取消编辑</button>
              }
              <button nz-button nzType="primary" [nzLoading]="saving()" (click)="save()">
                {{ form().id ? '保存修改' : '新增元素' }}
              </button>
            </div>
          </div>
        </nz-card>
      </div>
    </div>
  `,
  styleUrls: ['./ideology-pages.scss'],
})
export class IdeologyElementsComponent implements OnInit {
  private readonly service = inject(IdeologyService);
  private readonly message = inject(NzMessageService);
  private readonly localization = inject(LocalizationService);

  readonly elements = signal<IdeologyElementDto[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly form = signal({
    id: null as string | null,
    name: '',
    category: '',
    description: '',
    sortOrder: 0,
    isEnabled: true,
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
        if (!this.form().id) {
          this.patch({ sortOrder: (list?.length ?? 0) + 1 });
        }
      },
      error: () => {
        this.loading.set(false);
        this.message.error('读取思政元素失败');
      },
    });
  }

  patch(patch: Record<string, unknown>): void {
    this.form.set({ ...this.form(), ...patch } as any);
  }

  edit(item: IdeologyElementDto): void {
    this.form.set({
      id: item.id,
      name: item.name,
      category: item.category ?? '',
      description: item.description ?? '',
      sortOrder: item.sortOrder,
      isEnabled: item.isEnabled,
    });
  }

  resetForm(): void {
    this.form.set({
      id: null,
      name: '',
      category: '',
      description: '',
      sortOrder: this.elements().length + 1,
      isEnabled: true,
    });
  }

  save(): void {
    const f = this.form();
    if (!f.name.trim()) {
      this.message.warning('请填写元素名称');
      return;
    }

    const body = {
      name: f.name.trim(),
      category: f.category?.trim() || null,
      description: f.description?.trim() || null,
      sortOrder: f.sortOrder ?? 0,
      isEnabled: f.isEnabled,
    };

    this.saving.set(true);
    const request = f.id ? this.service.updateElement(f.id, body as any) : this.service.createElement(body as any);
    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.message.success(f.id ? '已保存' : '已新增');
        this.resetForm();
        this.reload();
      },
      error: (err: any) => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '保存失败');
      },
    });
  }

  remove(item: IdeologyElementDto): void {
    this.service.deleteElement(item.id).subscribe({
      next: () => {
        this.message.success('已删除元素');
        if (this.form().id === item.id) {
          this.resetForm();
        }
        this.reload();
      },
      error: () => this.message.error('删除失败'),
    });
  }

  /** 元素名本地化（后端存中文名，这里兼容以后可能的 ::Key 形式）。 */
  label(name: string): string {
    const text = this.localization.instant(name);
    return text || name;
  }
}
