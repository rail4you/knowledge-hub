import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MenuVisibilityService, TopLevelMenuItem } from '../../shared/menu-visibility/menu-visibility.service';

/**
 * 「老师菜单可见性」配置弹窗。
 *
 * <p>租户管理员按老师单独控制侧边栏**第一级菜单**的显示 / 隐藏。
 * 菜单目录从路由树实时推导（`MenuVisibilityService.getTopLevelMenus()`），不写死清单。</p>
 *
 * <p>只影响导航外观：隐藏菜单不会回收该老师的任何功能权限，直接输网址仍可访问。</p>
 */
@Component({
  selector: 'app-user-menu-visibility',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzModalModule,
    NzButtonModule,
    NzSwitchModule,
    NzTagModule,
    NzIconModule,
    NzAlertModule,
    NzSpinModule,
    NzTooltipModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nz-modal
      [nzVisible]="true"
      [nzTitle]="title()"
      [nzWidth]="640"
      [nzMaskClosable]="!saving()"
      [nzClosable]="!saving()"
      (nzOnCancel)="close()"
    >
      <ng-container *nzModalContent>
        <nz-alert
          nzType="info"
          nzShowIcon
          nzBanner
          [nzMessage]="'只控制侧边栏菜单的显示，不改变该老师的权限；隐藏后直接输入网址仍可访问对应页面。'"
        ></nz-alert>

        @if (loading()) {
          <div class="mv-loading"><nz-spin nzSimple></nz-spin></div>
        } @else if (!menus().length) {
          <div class="mv-empty">没有可配置的一级菜单。</div>
        } @else {
          <div class="mv-toolbar">
            <span class="mv-count">
              共 {{ menus().length }} 个一级菜单，已隐藏 <b>{{ hiddenCount() }}</b> 个
            </span>
            <span class="mv-toolbar-actions">
              <button nz-button nzType="link" nzSize="small" (click)="showAll()">全部显示</button>
              <button nz-button nzType="link" nzSize="small" nzDanger (click)="hideAll()">全部隐藏</button>
            </span>
          </div>

          <ul class="mv-list">
            @for (menu of menus(); track menu.name) {
              <li class="mv-item" [class.mv-item--hidden]="isHidden(menu.name)">
                <span class="mv-icon">
                  @if (menu.iconClass) {
                    <i [class]="menu.iconClass"></i>
                  }
                </span>
                <span class="mv-name" [nz-tooltip]="menu.name">{{ menu.displayName }}</span>
                <span class="mv-meta">
                  @if (menu.childCount > 0) {
                    <nz-tag nzNoAnimation>{{ menu.childCount }} 个子菜单</nz-tag>
                  }
                </span>
                <span class="mv-switch">
                  <nz-switch
                    [ngModel]="!isHidden(menu.name)"
                    (ngModelChange)="toggle(menu.name, $event)"
                    nzCheckedChildren="显示"
                    nzUnCheckedChildren="隐藏"
                    [nzDisabled]="saving()"
                  ></nz-switch>
                </span>
              </li>
            }
          </ul>
        }
      </ng-container>

      <ng-container *nzModalFooter>
        <button nz-button [disabled]="saving()" (click)="reset()" nz-tooltip="清空该老师的全部菜单限制">
          恢复全部可见
        </button>
        <button nz-button [disabled]="saving()" (click)="close()">取消</button>
        <button nz-button nzType="primary" [nzLoading]="saving()" [disabled]="loading()" (click)="save()">
          保存
        </button>
      </ng-container>
    </nz-modal>
  `,
  styles: [
    `
      .mv-loading,
      .mv-empty {
        padding: 48px 0;
        display: flex;
        align-items: center;
        justify-content: center;
        color: #8f9c9a;
        font-size: 13px;
      }
      .mv-toolbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 12px;
        margin: 14px 0 8px;
        font-size: 13px;
        color: #4c5b58;
      }
      .mv-count b {
        color: #087f7a;
      }
      .mv-toolbar-actions {
        display: flex;
        gap: 4px;
      }
      .mv-list {
        list-style: none;
        margin: 0;
        padding: 0;
        max-height: 52vh;
        overflow: auto;
      }
      .mv-item {
        display: flex;
        align-items: center;
        gap: 10px;
        padding: 9px 10px;
        border-radius: 8px;
        border: 1px solid #eef2ef;
        margin-bottom: 6px;
        transition: background 0.16s ease, border-color 0.16s ease;
      }
      .mv-item:hover {
        background: #f7faf8;
        border-color: #dbe6e1;
      }
      .mv-item--hidden {
        background: #fbfbfa;
      }
      .mv-item--hidden .mv-name,
      .mv-item--hidden .mv-icon {
        color: #9aa5a2;
        text-decoration: line-through;
      }
      .mv-icon {
        width: 18px;
        text-align: center;
        color: #087f7a;
      }
      .mv-name {
        flex: 1 1 auto;
        min-width: 0;
        font-size: 14px;
        color: #183444;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }
      .mv-meta {
        flex: 0 0 auto;
      }
      .mv-switch {
        flex: 0 0 auto;
      }
    `,
  ],
})
export class UserMenuVisibilityComponent implements OnInit {
  /** 目标老师。 */
  readonly userId = input.required<string>();
  readonly userName = input<string>('');

  /** 弹窗关闭（保存成功或取消）。 */
  readonly closed = output<{ saved: boolean }>();

  private readonly service = inject(MenuVisibilityService);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly menus = signal<TopLevelMenuItem[]>([]);
  private readonly hidden = signal<Set<string>>(new Set());

  readonly hiddenCount = computed(() => this.hidden().size);

  readonly title = computed(() => {
    const who = this.userName() || '';
    return who ? `菜单可见性 · ${who}` : '菜单可见性';
  });

  ngOnInit(): void {
    this.menus.set(this.service.getTopLevelMenus());
    this.service.getUserMenus(this.userId()).subscribe({
      next: dto => {
        this.hidden.set(new Set((dto.hiddenMenus || []).filter(Boolean)));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('读取菜单可见性失败');
      },
    });
  }

  isHidden(name: string): boolean {
    return this.hidden().has(name);
  }

  toggle(name: string, visible: boolean): void {
    const next = new Set(this.hidden());
    if (visible) {
      next.delete(name);
    } else {
      next.add(name);
    }
    this.hidden.set(next);
  }

  showAll(): void {
    this.hidden.set(new Set());
  }

  hideAll(): void {
    // 至少保留「系统工作台」：全隐藏会让老师登录后没有任何入口。
    const workbench = this.menus().find(m => m.path === '/admin/workbench');
    const all = this.menus()
      .filter(m => m.name !== workbench?.name)
      .map(m => m.name);
    this.hidden.set(new Set(all));
  }

  save(): void {
    this.saving.set(true);
    const hiddenMenus = Array.from(this.hidden());
    this.service.setUserMenus(this.userId(), hiddenMenus).subscribe({
      next: () => {
        this.saving.set(false);
        this.message.success('已保存，该老师下次刷新页面生效');
        this.closed.emit({ saved: true });
      },
      error: () => {
        this.saving.set(false);
        this.message.error('保存失败');
      },
    });
  }

  reset(): void {
    this.saving.set(true);
    this.service.resetUserMenus(this.userId()).subscribe({
      next: () => {
        this.saving.set(false);
        this.hidden.set(new Set());
        this.message.success('已恢复全部可见');
        this.closed.emit({ saved: true });
      },
      error: () => {
        this.saving.set(false);
        this.message.error('操作失败');
      },
    });
  }

  close(): void {
    if (this.saving()) {
      return;
    }
    this.closed.emit({ saved: false });
  }
}
